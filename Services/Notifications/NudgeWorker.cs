using FlowOS.Api.Configurations;
using FlowOS.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlowOS.Api.Services.Notifications
{
    public class NudgeWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NudgeWorker> _logger;
        private readonly IOptions<ExpoPushOptions> _opt;

        public NudgeWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<NudgeWorker> logger,
            IOptions<ExpoPushOptions> opt)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _opt = opt;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ NudgeWorker started.");

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnce(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ NudgeWorker loop error");
                }

                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task RunOnce(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FlowOSContext>();
            var push = scope.ServiceProvider.GetRequiredService<ExpoPushClient>();

            var nowUtc = DateTime.UtcNow;

            // Small tolerance to avoid early sends if clocks jitter.
            // We'll only send if WhenUtc <= nowUtc (as you already do).
            // (Keep it simple; avoid extra complexity.)
            var limit = 200;

            // ✅ Pull all due nudges (start + end) for REAL tasks only
            // We query due items (read-only projection), then load tracked entities to update flags.
            var due = await db.DailyPlanItems
                .AsNoTracking()
                .Include(i => i.Plan)
                .Where(i =>
                    i.Plan != null &&
                    i.TaskId != null && // ✅ ONLY real tasks
                    i.End > nowUtc && // ✅ don't notify for already-ended blocks
                    (
                        (i.NudgeAt != null && i.NudgeAt <= nowUtc && i.NudgeSentAtUtc == null)
                        ||
                        (i.EndNudgeAtUtc != null && i.EndNudgeAtUtc <= nowUtc && i.EndNudgeSentAtUtc == null)
                    )
                )
                .OrderBy(i => i.NudgeAt ?? i.EndNudgeAtUtc)
                .Take(limit)
                .Select(i => new
                {
                    ItemId = i.Id,
                    UserId = i.Plan!.UserId,
                    TaskId = i.TaskId!.Value,
                    Label = i.Label,

                    StartWhenUtc = (DateTime?)i.NudgeAt,
                    EndWhenUtc = (DateTime?)i.EndNudgeAtUtc,

                    StartPending = i.NudgeAt != null && i.NudgeAt <= nowUtc && i.NudgeSentAtUtc == null,
                    EndPending = i.EndNudgeAtUtc != null && i.EndNudgeAtUtc <= nowUtc && i.EndNudgeSentAtUtc == null
                })
                .ToListAsync(ct);

            if (due.Count == 0) return;

            _logger.LogInformation("🔔 NudgeWorker found {Count} due plan nudges.", due.Count);

            // Load device tokens for all users involved
            var userIds = due.Select(x => x.UserId).Distinct().ToList();

            var tokens = await db.UserDeviceTokens
                .Where(x => x.IsActive && userIds.Contains(x.UserId))
                .ToListAsync(ct);

            var options = _opt.Value ?? new ExpoPushOptions();
            var batchSize = Math.Max(1, options.BatchSize);

            // Load tracked entities once so we can set sent flags
            var itemIds = due.Select(x => x.ItemId).Distinct().ToList();

            var itemsToUpdate = await db.DailyPlanItems
                .Include(i => i.Plan)
                .Where(i => itemIds.Contains(i.Id) && i.TaskId != null) // ✅ ONLY real tasks
                .ToListAsync(ct);

            foreach (var d in due)
            {
                var item = itemsToUpdate.FirstOrDefault(i => i.Id == d.ItemId);
                if (item == null) continue;

                var userTokens = tokens
                    .Where(x => x.UserId == d.UserId)
                    .Select(x => x.ExpoPushToken)
                    .Distinct()
                    .ToList();

                if (userTokens.Count == 0)
                {
                    item.LastNudgeError = "No active device tokens for user.";
                    continue;
                }

                // ✅ Send START nudge if pending
                if (d.StartPending && item.NudgeSentAtUtc == null && item.NudgeAt.HasValue && item.NudgeAt.Value <= nowUtc)
                {
                    var title = "⏰ Task starting soon";
                    var body = $"{d.Label} starts in 5 minutes.";

                    var messages = userTokens.Select(tok => new ExpoPushMessage
                    {
                        To = tok,
                        Title = title,
                        Body = body,
                        Data = new Dictionary<string, object>
                        {
                            ["planItemId"] = d.ItemId,
                            ["taskId"] = d.TaskId,
                            ["type"] = "plan_start_nudge"
                        }
                    });

                    var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

                    if (anyOk)
                    {
                        item.NudgeSentAtUtc = nowUtc;
                        item.LastNudgeError = null;
                    }
                    else
                    {
                        item.LastNudgeError = lastError ?? "Unknown Expo error";
                        // keep sent null so it retries
                    }
                }

                // ✅ Send END nudge if pending
                if (d.EndPending && item.EndNudgeSentAtUtc == null && item.EndNudgeAtUtc.HasValue && item.EndNudgeAtUtc.Value <= nowUtc)
                {
                    var title = "✅ Task ending soon";
                    var body = $"{d.Label} ends in 5 minutes.";

                    var messages = userTokens.Select(tok => new ExpoPushMessage
                    {
                        To = tok,
                        Title = title,
                        Body = body,
                        Data = new Dictionary<string, object>
                        {
                            ["planItemId"] = d.ItemId,
                            ["taskId"] = d.TaskId,
                            ["type"] = "plan_end_nudge"
                        }
                    });

                    var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

                    if (anyOk)
                    {
                        item.EndNudgeSentAtUtc = nowUtc;
                        item.LastNudgeError = null;
                    }
                    else
                    {
                        item.LastNudgeError = lastError ?? "Unknown Expo error";
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }

        private static async Task<(bool anyOk, string? lastError)> SendBatches(
            ExpoPushClient push,
            IEnumerable<ExpoPushMessage> messages,
            int batchSize,
            CancellationToken ct)
        {
            bool anyOk = false;
            string? lastError = null;

            foreach (var batch in messages.Chunk(batchSize))
            {
                var (ok, error) = await push.SendAsync(batch, ct);
                if (ok) anyOk = true;
                if (!ok) lastError = error;
            }

            return (anyOk, lastError);
        }
    }
}