using FlowOS.Api.Configurations;
using FlowOS.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlowOS.Api.Services.Notifications
{
    public class NudgeWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NudgeWorker> _logger;
        private readonly ExpoPushOptions _opt;

        public NudgeWorker(IServiceScopeFactory scopeFactory, ILogger<NudgeWorker> logger, ExpoPushOptions opt)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _opt = opt;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ NudgeWorker started.");

            // every 1 minute
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnce(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ NudgeWorker loop error");
                }

                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlowOSContext>();
            var push = scope.ServiceProvider.GetRequiredService<ExpoPushClient>();

            var nowUtc = DateTime.UtcNow;

            // Find tasks to nudge
            var dueTasks = await db.Tasks
                .Where(t =>
                    !t.Completed &&
                    t.NudgeAtUtc != null &&
                    t.NudgeAtUtc <= nowUtc &&
                    t.NudgeSentAtUtc == null
                )
                .OrderBy(t => t.NudgeAtUtc)
                .Take(200)
                .ToListAsync(ct);

            if (dueTasks.Count == 0) return;

            _logger.LogInformation("🔔 NudgeWorker found {Count} tasks to nudge.", dueTasks.Count);

            // Group by user
            var userIds = dueTasks.Select(t => t.UserId).Distinct().ToList();

            var tokens = await db.UserDeviceTokens
                .Where(x => x.IsActive && userIds.Contains(x.UserId))
                .ToListAsync(ct);

            foreach (var task in dueTasks)
            {
                var userTokens = tokens
                    .Where(x => x.UserId == task.UserId)
                    .Select(x => x.ExpoPushToken)
                    .Distinct()
                    .ToList();

                if (userTokens.Count == 0)
                {
                    task.LastNudgeError = "No active device tokens for user.";
                    // keep NudgeSentAtUtc null so it retries later after user logs in again
                    continue;
                }

                var title = "⏰ Task Reminder";
                var body = $"{task.Title} is coming up.";

                // Create expo messages (one per device token)
                var messages = userTokens.Select(tok => new ExpoPushMessage
                {
                    To = tok,
                    Title = title,
                    Body = body,
                    Data = new Dictionary<string, object>
                    {
                        ["taskId"] = task.Id,
                        ["type"] = "task_nudge"
                    }
                });

                // Batch handling
                var batchSize = Math.Max(1, _opt.BatchSize);
                var batches = messages.Chunk(batchSize);

                bool anyOk = false;
                string? lastError = null;

                foreach (var batch in batches)
                {
                    var (ok, error) = await push.SendAsync(batch, ct);
                    if (ok) anyOk = true;
                    if (!ok) lastError = error;
                }

                if (anyOk)
                {
                    task.NudgeSentAtUtc = nowUtc;
                    task.LastNudgeError = null;
                }
                else
                {
                    task.LastNudgeError = lastError ?? "Unknown Expo error";
                    // keep NudgeSentAtUtc null so it retries next minute
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
