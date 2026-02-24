//using FlowOS.Api.Configurations;
//using FlowOS.Api.Data;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Options;

//namespace FlowOS.Api.Services.Notifications
//{
//    public class NudgeWorker : BackgroundService
//    {
//        private readonly IServiceScopeFactory _scopeFactory;
//        private readonly ILogger<NudgeWorker> _logger;
//        private readonly IOptions<ExpoPushOptions> _opt;

//        public NudgeWorker(
//            IServiceScopeFactory scopeFactory,
//            ILogger<NudgeWorker> logger,
//            IOptions<ExpoPushOptions> opt)
//        {
//            _scopeFactory = scopeFactory;
//            _logger = logger;
//            _opt = opt;
//        }

//        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//        {
//            _logger.LogInformation("✅ NudgeWorker started.");

//            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

//            while (!stoppingToken.IsCancellationRequested)
//            {
//                try
//                {
//                    await RunOnce(stoppingToken);
//                }
//                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
//                {
//                    break;
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "❌ NudgeWorker loop error");
//                }

//                try
//                {
//                    await timer.WaitForNextTickAsync(stoppingToken);
//                }
//                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
//                {
//                    break;
//                }
//            }
//        }

//        private async Task RunOnce(CancellationToken ct)
//        {
//            using var scope = _scopeFactory.CreateScope();
//            var db = scope.ServiceProvider.GetRequiredService<FlowOSContext>();
//            var push = scope.ServiceProvider.GetRequiredService<ExpoPushClient>();

//            var nowUtc = DateTime.UtcNow;

//            // Small tolerance to avoid early sends if clocks jitter.
//            // We'll only send if WhenUtc <= nowUtc (as you already do).
//            // (Keep it simple; avoid extra complexity.)
//            var limit = 200;

//            // ✅ Pull all due nudges (start + end) for REAL tasks only
//            // We query due items (read-only projection), then load tracked entities to update flags.
//            var due = await db.DailyPlanItems
//                .AsNoTracking()
//                .Include(i => i.Plan)
//                .Where(i =>
//                    i.Plan != null &&
//                    i.TaskId != null && // ✅ ONLY real tasks
//                    i.End > nowUtc && // ✅ don't notify for already-ended blocks
//                    (
//                        (i.NudgeAt != null && i.NudgeAt <= nowUtc && i.NudgeSentAtUtc == null)
//                        ||
//                        (i.EndNudgeAtUtc != null && i.EndNudgeAtUtc <= nowUtc && i.EndNudgeSentAtUtc == null)
//                    )
//                )
//                .OrderBy(i => i.NudgeAt ?? i.EndNudgeAtUtc)
//                .Take(limit)
//                .Select(i => new
//                {
//                    ItemId = i.Id,
//                    UserId = i.Plan!.UserId,
//                    TaskId = i.TaskId!.Value,
//                    Label = i.Label,

//                    StartWhenUtc = (DateTime?)i.NudgeAt,
//                    EndWhenUtc = (DateTime?)i.EndNudgeAtUtc,

//                    StartPending = i.NudgeAt != null && i.NudgeAt <= nowUtc && i.NudgeSentAtUtc == null,
//                    EndPending = i.EndNudgeAtUtc != null && i.EndNudgeAtUtc <= nowUtc && i.EndNudgeSentAtUtc == null
//                })
//                .ToListAsync(ct);

//            if (due.Count == 0) return;

//            _logger.LogInformation("🔔 NudgeWorker found {Count} due plan nudges.", due.Count);

//            // Load device tokens for all users involved
//            var userIds = due.Select(x => x.UserId).Distinct().ToList();

//            var tokens = await db.UserDeviceTokens
//                .Where(x => x.IsActive && userIds.Contains(x.UserId))
//                .ToListAsync(ct);

//            var options = _opt.Value ?? new ExpoPushOptions();
//            var batchSize = Math.Max(1, options.BatchSize);

//            // Load tracked entities once so we can set sent flags
//            var itemIds = due.Select(x => x.ItemId).Distinct().ToList();

//            var itemsToUpdate = await db.DailyPlanItems
//                .Include(i => i.Plan)
//                .Where(i => itemIds.Contains(i.Id) && i.TaskId != null) // ✅ ONLY real tasks
//                .ToListAsync(ct);

//            foreach (var d in due)
//            {
//                var item = itemsToUpdate.FirstOrDefault(i => i.Id == d.ItemId);
//                if (item == null) continue;

//                var userTokens = tokens
//                    .Where(x => x.UserId == d.UserId)
//                    .Select(x => x.ExpoPushToken)
//                    .Distinct()
//                    .ToList();

//                if (userTokens.Count == 0)
//                {
//                    item.LastNudgeError = "No active device tokens for user.";
//                    continue;
//                }

//                // ✅ Send START nudge if pending
//                if (d.StartPending && item.NudgeSentAtUtc == null && item.NudgeAt.HasValue && item.NudgeAt.Value <= nowUtc)
//                {
//                    var title = "⏰ Task starting soon";
//                    var body = $"{d.Label} starts in 5 minutes.";

//                    var messages = userTokens.Select(tok => new ExpoPushMessage
//                    {
//                        To = tok,
//                        Title = title,
//                        Body = body,
//                        Data = new Dictionary<string, object>
//                        {
//                            ["planItemId"] = d.ItemId,
//                            ["taskId"] = d.TaskId,
//                            ["type"] = "plan_start_nudge"
//                        }
//                    });

//                    var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

//                    if (anyOk)
//                    {
//                        item.NudgeSentAtUtc = nowUtc;
//                        item.LastNudgeError = null;
//                    }
//                    else
//                    {
//                        item.LastNudgeError = lastError ?? "Unknown Expo error";
//                        // keep sent null so it retries
//                    }
//                }

//                // ✅ Send END nudge if pending
//                if (d.EndPending && item.EndNudgeSentAtUtc == null && item.EndNudgeAtUtc.HasValue && item.EndNudgeAtUtc.Value <= nowUtc)
//                {
//                    var title = "✅ Task ending soon";
//                    var body = $"{d.Label} ends in 5 minutes.";

//                    var messages = userTokens.Select(tok => new ExpoPushMessage
//                    {
//                        To = tok,
//                        Title = title,
//                        Body = body,
//                        Data = new Dictionary<string, object>
//                        {
//                            ["planItemId"] = d.ItemId,
//                            ["taskId"] = d.TaskId,
//                            ["type"] = "plan_end_nudge"
//                        }
//                    });

//                    var (anyOk, lastError) = await SendBatches(push, messages, batchSize, ct);

//                    if (anyOk)
//                    {
//                        item.EndNudgeSentAtUtc = nowUtc;
//                        item.LastNudgeError = null;
//                    }
//                    else
//                    {
//                        item.LastNudgeError = lastError ?? "Unknown Expo error";
//                    }
//                }
//            }

//            await db.SaveChangesAsync(ct);
//        }

//        private static async Task<(bool anyOk, string? lastError)> SendBatches(
//            ExpoPushClient push,
//            IEnumerable<ExpoPushMessage> messages,
//            int batchSize,
//            CancellationToken ct)
//        {
//            bool anyOk = false;
//            string? lastError = null;

//            foreach (var batch in messages.Chunk(batchSize))
//            {
//                var (ok, error) = await push.SendAsync(batch, ct);
//                if (ok) anyOk = true;
//                if (!ok) lastError = error;
//            }

//            return (anyOk, lastError);
//        }
//    }
//}

using FlowOS.Api.Configurations;
using FlowOS.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FlowOS.Api.Services.Notifications
{
    public class NudgeWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NudgeWorker> _logger;
        private readonly IOptions<ExpoPushOptions> _opt;

        // Strong types (NO dynamic)
        private sealed record DueNudge(
            int ItemId,
            string UserId,
            int TaskId,
            string Label,
            bool StartPending,
            bool EndPending
        );

        private sealed record TokenRow(string UserId, string ExpoPushToken);

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
            var limit = 200;

            // ✅ Pull due nudges (projection only). No tracked entities. No Include().
            var due = await SafeDbQuery(async () =>
            {
                return await (
                    from i in db.DailyPlanItems.AsNoTracking()
                    join p in db.DailyPlans.AsNoTracking() on i.PlanId equals p.Id
                    where i.TaskId != null
                          && i.End > nowUtc
                          && (
                                (i.NudgeAt != null && i.NudgeAt <= nowUtc && i.NudgeSentAtUtc == null)
                             || (i.EndNudgeAtUtc != null && i.EndNudgeAtUtc <= nowUtc && i.EndNudgeSentAtUtc == null)
                             )
                    orderby (i.NudgeAt ?? i.EndNudgeAtUtc)
                    select new DueNudge(
                        i.Id,
                        p.UserId,
                        i.TaskId!.Value,
                        i.Label,
                        i.NudgeAt != null && i.NudgeAt <= nowUtc && i.NudgeSentAtUtc == null,
                        i.EndNudgeAtUtc != null && i.EndNudgeAtUtc <= nowUtc && i.EndNudgeSentAtUtc == null
                    )
                )
                .Take(limit)
                .ToListAsync(ct);
            }, fallback: new List<DueNudge>());

            if (due.Count == 0) return;

            _logger.LogInformation("🔔 NudgeWorker found {Count} due plan nudges.", due.Count);

            // ✅ Load device tokens for all users involved (read-only)
            var userIds = due.Select(x => x.UserId).Distinct().ToList();

            var tokens = await SafeDbQuery(async () =>
            {
                return await db.UserDeviceTokens
                    .AsNoTracking()
                    .Where(x => x.IsActive && userIds.Contains(x.UserId))
                    .Select(x => new TokenRow(x.UserId, x.ExpoPushToken))
                    .ToListAsync(ct);
            }, fallback: new List<TokenRow>());

            var tokensByUser = tokens
                .GroupBy(t => t.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ExpoPushToken).Distinct().ToList());

            var options = _opt.Value ?? new ExpoPushOptions();
            var batchSize = Math.Max(1, options.BatchSize);

            foreach (var d in due)
            {
                ct.ThrowIfCancellationRequested();

                if (!tokensByUser.TryGetValue(d.UserId, out var userTokens) || userTokens.Count == 0)
                {
                    // best-effort write; if item is deleted, ignore
                    await SafeExecuteUpdate(
                        () => SafeSetLastError(db, d.ItemId, "No active device tokens for user.", ct));
                    continue;
                }

                // ✅ START nudge (claim first, then send)
                if (d.StartPending)
                {
                    var claimed = await SafeExecuteUpdateBool(
                        () => ClaimStartNudge(db, d.ItemId, nowUtc, ct));

                    if (claimed)
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
                            await SafeExecuteUpdate(() => ClearLastError(db, d.ItemId, ct));
                        }
                        else
                        {
                            await SafeExecuteUpdate(() => RevertStartClaim(db, d.ItemId, lastError ?? "Unknown Expo error", ct));
                        }
                    }
                }

                // ✅ END nudge (claim first, then send)
                if (d.EndPending)
                {
                    var claimed = await SafeExecuteUpdateBool(
                        () => ClaimEndNudge(db, d.ItemId, nowUtc, ct));

                    if (claimed)
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
                            await SafeExecuteUpdate(() => ClearLastError(db, d.ItemId, ct));
                        }
                        else
                        {
                            await SafeExecuteUpdate(() => RevertEndClaim(db, d.ItemId, lastError ?? "Unknown Expo error", ct));
                        }
                    }
                }
            }
        }

        // ----------------- DB SAFETY WRAPPERS -----------------

        private async Task<T> SafeDbQuery<T>(Func<Task<T>> action, T fallback)
        {
            try
            {
                return await action();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
            {
                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during query. Skipping this tick.");
                return fallback;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ DB query failed. Skipping this tick.");
                return fallback;
            }
        }

        private async Task SafeExecuteUpdate(Func<Task> updateAction)
        {
            try
            {
                await updateAction();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
            {
                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during update. Will retry next tick.");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "⚠️ Concurrency during update (row may be deleted). Ignoring.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Update failed. Ignoring for this tick.");
            }
        }

        private async Task<bool> SafeExecuteUpdateBool(Func<Task<bool>> updateAction)
        {
            try
            {
                return await updateAction();
            }
            catch (PostgresException ex) when (ex.SqlState == "40P01" || ex.SqlState == "40001")
            {
                _logger.LogWarning(ex, "⚠️ DB transient error (deadlock/serialization) during update. Will retry next tick.");
                return false;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "⚠️ Concurrency during update (row may be deleted). Ignoring.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Update failed. Ignoring for this tick.");
                return false;
            }
        }

        // ----------------- CLAIM HELPERS (atomic, short locks) -----------------

        private static async Task<bool> ClaimStartNudge(FlowOSContext db, int itemId, DateTime nowUtc, CancellationToken ct)
        {
            var rows = await db.DailyPlanItems
                .Where(i =>
                    i.Id == itemId
                    && i.TaskId != null
                    && i.NudgeAt != null
                    && i.NudgeAt <= nowUtc
                    && i.NudgeSentAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NudgeSentAtUtc, nowUtc)
                    .SetProperty(i => i.LastNudgeError, (string?)null),
                    ct);

            return rows == 1;
        }

        private static async Task<bool> ClaimEndNudge(FlowOSContext db, int itemId, DateTime nowUtc, CancellationToken ct)
        {
            var rows = await db.DailyPlanItems
                .Where(i =>
                    i.Id == itemId
                    && i.TaskId != null
                    && i.EndNudgeAtUtc != null
                    && i.EndNudgeAtUtc <= nowUtc
                    && i.EndNudgeSentAtUtc == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EndNudgeSentAtUtc, nowUtc)
                    .SetProperty(i => i.LastNudgeError, (string?)null),
                    ct);

            return rows == 1;
        }

        private static async Task RevertStartClaim(FlowOSContext db, int itemId, string error, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId && i.NudgeSentAtUtc != null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.NudgeSentAtUtc, (DateTime?)null)
                    .SetProperty(i => i.LastNudgeError, error),
                    ct);
        }

        private static async Task RevertEndClaim(FlowOSContext db, int itemId, string error, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId && i.EndNudgeSentAtUtc != null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.EndNudgeSentAtUtc, (DateTime?)null)
                    .SetProperty(i => i.LastNudgeError, error),
                    ct);
        }

        private static async Task SafeSetLastError(FlowOSContext db, int itemId, string error, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.LastNudgeError, error),
                    ct);
        }

        private static async Task ClearLastError(FlowOSContext db, int itemId, CancellationToken ct)
        {
            await db.DailyPlanItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(i => i.LastNudgeError, (string?)null),
                    ct);
        }

        // ----------------- EXPO SEND -----------------

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
                // ✅ Explicit tuple types to avoid CS8130 inference errors
                (bool ok, string? error) = await push.SendAsync(batch, ct);

                if (ok) anyOk = true;
                if (!ok) lastError = error;
            }

            return (anyOk, lastError);
        }
    }
}