#region old commented code
using FlowOS.Api.Data;
using FlowOS.Api.DTOs.Plan;
using FlowOS.Api.Models;
using FlowOS.Api.Models.Audit;
using FlowOS.Api.Models.Enums;            // ← PlanTone enum
using FlowOS.Api.Services.Planner;
using FlowOS.Api.Services.Planner.Helpers;
using FlowOS.Api.Services.Planner.Models; // AiPlanRequest, UserAiContext, TaskAiContext, DailyPlanAiResult
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;       // ← ensure logging namespace
using System.Diagnostics;
using System.Text.Json;
using FlowOS.Api.Helpers;
using Task = FlowOS.Api.Models.Task;

namespace FlowOS.Api.Services.Planner
{
    /// <summary>
    /// Orchestrator for Daily Plan:
    /// - Reuse existing plan when allowed
    /// - Build AiPlanRequest and call OpenAIPlannerService
    /// - Save plan + items atomically
    /// - Tone strategy: TAP2 + TL-C hybrid + EV3-M learning
    /// - Return PlanResponseDto for the app
    /// </summary>
    public class PlannerService : IPlannerService
    {
        private readonly FlowOSContext _context;
        private readonly OpenAIPlannerService _aiPlanner; // Hybrid DI (concrete for OpenAI features)
        private readonly ILogger<PlannerService> _logger;

        // Thresholds for auto re-generation
        private const double MinConfidenceThreshold = 3.0;   // average AI confidence
        private const double MinCoverageThreshold = 60.0;    // % of workday covered
        private const double MinAlignedThreshold = 50.0;    // % of tasks planned

        public PlannerService(
            FlowOSContext context,
            OpenAIPlannerService aiPlanner,
            ILogger<PlannerService> logger)
        {
            _context = context;
            _aiPlanner = aiPlanner;
            _logger = logger;
        }
       
        static int? TryMapTaskIdByTitle(string label, List<Task> dbTasks)
        {
            label = (label ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(label) || dbTasks.Count == 0) return null;

            var best = dbTasks
                .Select(t =>
                {
                    var title = (t.Title ?? "").Trim().ToLowerInvariant();
                    return new
                    {
                        t.Id,
                        Title = title,
                        Score = SimilarityScore(label, title)
                    };
                })
                .Where(x => x.Title.Length > 0)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault();

            // Token-overlap threshold (tune if needed)
            return best != null && best.Score >= 0.45 ? best.Id : null;
        }

        static double SimilarityScore(string a, string b)
        {
            static HashSet<string> Tokens(string s)
            {
                return s.Split(new[] { ' ', ',', '.', ';', ':', '-', '_', '/', '\\', '|', '(', ')', '[', ']', '{', '}', '"', '\'' },
                               StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length >= 3) // ignore very short words
                        .ToHashSet();
            }

            var ta = Tokens(a);
            var tb = Tokens(b);

            if (ta.Count == 0 || tb.Count == 0) return 0;

            int intersect = ta.Intersect(tb).Count();
            int denom = Math.Max(ta.Count, tb.Count);

            return denom == 0 ? 0 : (double)intersect / denom;
        }

        //public async Task<PlanResponseDto> GeneratePlanAsync(
        //    string userId,
        //    DateOnly dateKey,                 // ✅ IST calendar date key
        //    string? toneOverride = null,
        //    bool forceRegenerate = false,
        //    DateTime? planStartUtc = null
        //)
        //{
        //    var userOffset = TimeSpan.FromMinutes(330); // IST (+05:30)

        //    // ✅ IST day window -> UTC window (for task filtering)
        //    var istStartLocal = DateTime.SpecifyKind(
        //        dateKey.ToDateTime(TimeOnly.MinValue),
        //        DateTimeKind.Unspecified
        //    );

        //    var startUtc = new DateTimeOffset(istStartLocal, userOffset).UtcDateTime;
        //    var endUtc = startUtc.AddDays(1);

        //    // --- Step 0: Load user + reuse existing plan (unless forceRegenerate) ---
        //    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        //    if (user == null) throw new Exception("User not found");

        //    var existing = await _context.DailyPlans
        //        .AsNoTracking()
        //        .Include(p => p.Items)
        //        .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == dateKey);

        //    if (existing != null && !forceRegenerate)
        //        return MapToDto(existing);

        //    // --- Step 1: Build AI request ---
        //    var workStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
        //    var workEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

        //    var firstName = (user.FullName ?? "")
        //        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        //        .FirstOrDefault() ?? "Friend";

        //    var toneForThisPlanEnum = user.PreferredTone ?? user.CurrentTone;
        //    var toneForThisPlanStr = (toneOverride ?? ToneToAiString(toneForThisPlanEnum))
        //        .Trim()
        //        .ToLowerInvariant();

        //    // ✅ Pull candidate tasks for IST day (via UTC window)
        //    var dbTasks = await _context.Tasks
        //        .Where(t => t.UserId == userId
        //            && !t.Completed
        //            && t.DueDate >= startUtc
        //            && t.DueDate < endUtc)
        //        .OrderByDescending(t => t.Priority)
        //        .ToListAsync();

        //    // ✅ Fast lookup for user-owned times
        //    var taskById = dbTasks.ToDictionary(t => t.Id);

        //    var taskCtx = dbTasks.Select(t => new TaskAiContext
        //    {
        //        Id = t.Id,
        //        Title = t.Title,
        //        Description = t.Description,
        //        Priority = t.Priority,
        //        DueDate = t.DueDate,
        //        EstimatedMinutes = t.EstimatedMinutes ?? 30,
        //        EnergyLevel = t.EnergyLevel
        //    }).ToList();

        //    var aiRequest = new AiPlanRequest
        //    {
        //        UserId = userId,
        //        User = new UserAiContext
        //        {
        //            Id = userId,
        //            FirstName = firstName,
        //            FullName = user.FullName,
        //            WorkStart = workStart,
        //            WorkEnd = workEnd,
        //            PreferredTone = user.PreferredTone?.ToString()
        //        },
        //        Tasks = taskCtx,

        //        // ✅ For AI context (not DB key): UTC anchor corresponding to IST midnight
        //        Date = startUtc,

        //        Tone = toneForThisPlanStr,
        //        ForceRegenerate = forceRegenerate
        //    };

        //    // --- Step 2: Call AI engine ---
        //    DailyPlanAiResult aiResult = await _aiPlanner.GenerateAiPlanAsync(aiRequest);
        //    _logger.LogInformation("AI Clean JSON: {Json}", aiResult.CleanJson ?? aiResult.RawJson);

        //    // ✅ Hard fallback if AI returns empty
        //    if (aiResult == null || aiResult.Timeline == null || aiResult.Timeline.Count == 0)
        //    {
        //        aiResult = new DailyPlanAiResult
        //        {
        //            Tone = "balanced",
        //            Focus = "Fallback plan for today.",
        //            Timeline = new List<AiPlanTimelineItem>
        //    {
        //        new AiPlanTimelineItem
        //        {
        //            TaskId = null,
        //            Label = "Manual Planning Required",
        //            Start = DateTime.UtcNow,
        //            End = DateTime.UtcNow.AddMinutes(30),
        //            Confidence = 1,
        //            NudgeAt = null
        //        }
        //    }
        //        };
        //    }

        //    // ✅ Fix taskId mapping (AI may omit or invent ids)
        //    var validTaskIds = taskById.Keys.ToHashSet();

        //    foreach (var item in aiResult.Timeline)
        //    {
        //        if (item.TaskId.HasValue && !validTaskIds.Contains(item.TaskId.Value))
        //        {
        //            item.TaskId = TryMapTaskIdByTitle(item.Label, dbTasks);
        //        }

        //        if (item.TaskId == null)
        //        {
        //            item.TaskId = TryMapTaskIdByTitle(item.Label, dbTasks);
        //        }

        //        _logger.LogInformation("AI item mapped: label='{Label}' taskId={TaskId}",
        //            item.Label, item.TaskId?.ToString() ?? "null");
        //    }

        //    // --- Step 3: Save to DB atomically ---
        //    var exec = _context.Database.CreateExecutionStrategy();
        //    DailyPlan savedPlan = null!;

        //    await exec.ExecuteAsync(async () =>
        //    {
        //        await using var tx = await _context.Database.BeginTransactionAsync();

        //        var plan = await _context.DailyPlans
        //            .Include(p => p.Items)
        //            .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == dateKey);

        //        if (plan == null)
        //        {
        //            plan = new DailyPlan
        //            {
        //                UserId = userId,
        //                Date = dateKey
        //            };
        //            _context.DailyPlans.Add(plan);
        //            await _context.SaveChangesAsync();
        //        }
        //        else
        //        {
        //            // hard delete old items so nudges reset cleanly on regenerate
        //            if (plan.Items.Any())
        //            {
        //                _context.DailyPlanItems.RemoveRange(plan.Items);
        //                await _context.SaveChangesAsync();
        //            }
        //        }

        //        var appliedToneString = string.IsNullOrWhiteSpace(aiResult.Tone)
        //            ? toneForThisPlanStr
        //            : aiResult.Tone.Trim().ToLowerInvariant();

        //        plan.Tone = appliedToneString;
        //        plan.Focus = string.IsNullOrWhiteSpace(aiResult.Focus) ? (plan.Focus ?? "Your plan") : aiResult.Focus;
        //        plan.GeneratedAt = DateTime.UtcNow;
        //        plan.PlanJsonRaw = aiResult.RawJson ?? "";
        //        plan.PlanJsonClean = MinifyJson(aiResult.CleanJson ?? aiResult.RawJson ?? "{}");
        //        plan.ModelUsed = aiResult.ModelUsed;

        //        await _context.SaveChangesAsync();

        //        if (aiResult.Timeline.Any())
        //        {
        //            var nowUtc = DateTime.UtcNow;

        //            var uWorkStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
        //            var uWorkEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

        //            // ✅ Convert AI items to UTC + compute duration
        //            var normalized = aiResult.Timeline
        //                .Select(i =>
        //                {
        //                    var s = i.Start.UtcDateTime;
        //                    var e = i.End.UtcDateTime;
        //                    if (e <= s) e = s.AddMinutes(30);

        //                    var dur = e - s;
        //                    if (dur.TotalMinutes < 5) dur = TimeSpan.FromMinutes(5);

        //                    return new
        //                    {
        //                        TaskId = i.TaskId,
        //                        Label = i.Label,
        //                        Duration = dur,
        //                        Confidence = i.Confidence
        //                    };
        //                })
        //                .ToList();

        //            // ✅ FIXED slots (user-owned times)
        //            // If user set planned times => fixed block
        //            var fixedSlots = new List<(DateTime Start, DateTime End, int? TaskId)>();

        //            foreach (var t in dbTasks)
        //            {
        //                // handle partials too (safer)
        //                if (t.PlannedStartUtc.HasValue || t.PlannedEndUtc.HasValue)
        //                {
        //                    DateTime start;
        //                    DateTime end;

        //                    if (t.PlannedStartUtc.HasValue)
        //                    {
        //                        start = EnsureUtc(t.PlannedStartUtc.Value);
        //                        end = t.PlannedEndUtc.HasValue
        //                            ? EnsureUtc(t.PlannedEndUtc.Value)
        //                            : start.AddMinutes(t.EstimatedMinutes ?? 30);
        //                    }
        //                    else
        //                    {
        //                        // only end provided (rare) => back-calc start
        //                        end = EnsureUtc(t.PlannedEndUtc!.Value);
        //                        start = end.AddMinutes(-(t.EstimatedMinutes ?? 30));
        //                    }

        //                    if (end <= start) end = start.AddMinutes(t.EstimatedMinutes ?? 30);

        //                    fixedSlots.Add((start, end, t.Id));
        //                }
        //            }

        //            // Normalize fixed slots (sorted, no invalid)
        //            fixedSlots = fixedSlots
        //                .Where(x => x.End > x.Start)
        //                .OrderBy(x => x.Start)
        //                .ToList();

        //            // ✅ Decide the "cursor start" (where flexible scheduling begins)
        //            // Rule: if there is any fixed slot earlier than modal start, plan must start earlier.
        //            DateTime cursorStart;

        //            var candidates = new List<DateTime>();

        //            if (planStartUtc.HasValue)
        //                candidates.Add(EnsureUtc(planStartUtc.Value));

        //            if (fixedSlots.Count > 0)
        //                candidates.Add(fixedSlots.Min(x => x.Start));

        //            if (candidates.Count == 0)
        //            {
        //                cursorStart = EarliestStartUtc(
        //                    nowUtc,
        //                    uWorkStart,
        //                    uWorkEnd,
        //                    bufferMinutes: 10,
        //                    roundToMinutes: 5
        //                );
        //            }
        //            else
        //            {
        //                cursorStart = candidates.Min();
        //            }

        //            // ✅ Helper: place a block into next free time that doesn't overlap fixed slots
        //            static bool Overlaps(DateTime s1, DateTime e1, DateTime s2, DateTime e2)
        //                => s1 < e2 && e1 > s2;

        //            DateTime FindNextFreeStart(DateTime start, TimeSpan duration, List<(DateTime Start, DateTime End, int? TaskId)> reserved)
        //            {
        //                var s = start;
        //                var e = s + duration;

        //                // keep pushing forward until no overlap with any reserved slot
        //                while (true)
        //                {
        //                    var hit = reserved.FirstOrDefault(r => Overlaps(s, e, r.Start, r.End));

        //                    if (hit == default) break;

        //                    // move start to end of conflicting block
        //                    s = hit.End;
        //                    e = s + duration;
        //                }

        //                return s;
        //            }

        //            // ✅ Reserved list starts with fixed slots
        //            var reserved = fixedSlots
        //                .OrderBy(x => x.Start)
        //                .ToList();

        //            var items = new List<DailyPlanItem>();

        //            // ✅ Schedule in the AI order (keeps AI preference), but avoid overlaps
        //            // If a task is fixed, use fixed time; else place around fixed tasks.
        //            var cursor = cursorStart;

        //            foreach (var x in normalized)
        //            {
        //                DateTime start;
        //                DateTime end;

        //                // if this is a real task and has planned times => FIXED
        //                if (x.TaskId.HasValue && taskById.TryGetValue(x.TaskId.Value, out var t)
        //                    && (t.PlannedStartUtc.HasValue || t.PlannedEndUtc.HasValue))
        //                {
        //                    if (t.PlannedStartUtc.HasValue)
        //                    {
        //                        start = EnsureUtc(t.PlannedStartUtc.Value);
        //                        end = t.PlannedEndUtc.HasValue
        //                            ? EnsureUtc(t.PlannedEndUtc.Value)
        //                            : start.AddMinutes(t.EstimatedMinutes ?? 30);
        //                    }
        //                    else
        //                    {
        //                        end = EnsureUtc(t.PlannedEndUtc!.Value);
        //                        start = end.AddMinutes(-(t.EstimatedMinutes ?? 30));
        //                    }

        //                    if (end <= start)
        //                        end = start.AddMinutes(t.EstimatedMinutes ?? 30);
        //                }
        //                else
        //                {
        //                    // FLEX: schedule after cursor but skip over fixed slots
        //                    var dur = x.Duration;

        //                    start = FindNextFreeStart(cursor, dur, reserved);
        //                    end = start + dur;

        //                    // reserve this flex block too (so later flex blocks won't overlap it)
        //                    reserved.Add((start, end, x.TaskId));
        //                    reserved = reserved.OrderBy(r => r.Start).ToList();

        //                    cursor = end;
        //                }

        //                // label: if real task, always use DB title
        //                var label = x.Label;
        //                if (x.TaskId.HasValue && taskById.TryGetValue(x.TaskId.Value, out var t2))
        //                {
        //                    if (!string.IsNullOrWhiteSpace(t2.Title))
        //                        label = t2.Title;
        //                }

        //                // nudges
        //                DateTime? startNudge = start.AddMinutes(-5);
        //                if (startNudge <= nowUtc)
        //                    startNudge = (nowUtc < start) ? nowUtc.AddSeconds(10) : null;

        //                DateTime? endNudge = end.AddMinutes(-5);
        //                if (endNudge <= nowUtc)
        //                    endNudge = (nowUtc < end) ? nowUtc.AddSeconds(15) : null;

        //                items.Add(new DailyPlanItem
        //                {
        //                    PlanId = plan.Id,
        //                    TaskId = x.TaskId,
        //                    Label = label,
        //                    Start = start,
        //                    End = end,
        //                    Confidence = Math.Clamp(x.Confidence, 1, 5),
        //                    NudgeAt = startNudge,
        //                    NudgeSentAtUtc = null,
        //                    EndNudgeAtUtc = endNudge,
        //                    EndNudgeSentAtUtc = null,
        //                    LastNudgeError = null
        //                });
        //            }

        //            await _context.DailyPlanItems.AddRangeAsync(items);
        //            await _context.SaveChangesAsync();
        //        }

        //        await tx.CommitAsync();

        //        savedPlan = await _context.DailyPlans
        //            .AsNoTracking()
        //            .Include(p => p.Items)
        //            .FirstAsync(p => p.Id == plan.Id);
        //    });

        //    // ✅ Tone learning dayUtc: DateOnly (IST midnight) -> UTC instant
        //    var dayUtc = new DateTimeOffset(
        //        dateKey.ToDateTime(TimeOnly.MinValue),
        //        TimeSpan.FromMinutes(330)
        //    ).UtcDateTime;

        //    await ApplyToneLearningAsync(
        //        user,
        //        dayUtc,
        //        aiResult,
        //        toneForThisPlanStr
        //    );

        //    return MapToDto(savedPlan);
        //}

        public async Task<PlanResponseDto> GeneratePlanAsync(
            string userId,
            DateOnly dateKey,                 // ✅ IST calendar date key
            string? toneOverride = null,
            bool forceRegenerate = false,
            DateTime? planStartUtc = null
        )
        {
            var userOffset = TimeSpan.FromMinutes(330); // IST (+05:30)

            // ✅ IST day window -> UTC window (for task filtering)
            var istStartLocal = DateTime.SpecifyKind(
                dateKey.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified
            );

            var dayStartUtc = new DateTimeOffset(istStartLocal, userOffset).UtcDateTime;
            var dayEndUtc = dayStartUtc.AddDays(1);

            // --- Step 0: Load user + reuse existing plan (unless forceRegenerate) ---
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) throw new Exception("User not found");

            var existing = await _context.DailyPlans
                .AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == dateKey);

            if (existing != null && !forceRegenerate)
                return MapToDto(existing);

            // --- Step 1: Build AI request ---
            var workStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
            var workEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

            var firstName = (user.FullName ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "Friend";

            var toneForThisPlanEnum = user.PreferredTone ?? user.CurrentTone;
            var toneForThisPlanStr = (toneOverride ?? ToneToAiString(toneForThisPlanEnum))
                .Trim()
                .ToLowerInvariant();

            // ✅ Pull candidate tasks for IST day (via UTC window)
            // NOTE: still calls AI even if 0 tasks (routine plan)
            var dbTasks = await _context.Tasks
                .Where(t => t.UserId == userId
                    && !t.Completed
                    && t.DueDate >= dayStartUtc
                    && t.DueDate < dayEndUtc)
                .OrderByDescending(t => t.Priority)
                .ToListAsync();

            var taskById = dbTasks.ToDictionary(t => t.Id);
            var validTaskIds = taskById.Keys.ToHashSet();

            var taskCtx = dbTasks.Select(t => new TaskAiContext
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                DueDate = t.DueDate,
                EstimatedMinutes = t.EstimatedMinutes ?? 30,
                EnergyLevel = t.EnergyLevel
            }).ToList();

            var aiRequest = new AiPlanRequest
            {
                UserId = userId,
                User = new UserAiContext
                {
                    Id = userId,
                    FirstName = firstName,
                    FullName = user.FullName,
                    WorkStart = workStart,
                    WorkEnd = workEnd,
                    PreferredTone = user.PreferredTone?.ToString()
                },
                Tasks = taskCtx,

                // ✅ UTC instant corresponding to IST midnight (original behavior)
                Date = dayStartUtc,

                Tone = toneForThisPlanStr,
                ForceRegenerate = forceRegenerate
            };

            // --- Step 2: Call AI engine (ALWAYS) ---
            DailyPlanAiResult aiResult = await _aiPlanner.GenerateAiPlanAsync(aiRequest);
            _logger.LogInformation("AI Clean JSON: {Json}", aiResult?.CleanJson ?? aiResult?.RawJson);

            // ✅ Fallback ONLY if AI totally fails
            if (aiResult == null || aiResult.Timeline == null || aiResult.Timeline.Count == 0)
            {
                aiResult = new DailyPlanAiResult
                {
                    Tone = "balanced",
                    Focus = "Fallback plan for today.",
                    Timeline = new List<AiPlanTimelineItem>
            {
                new AiPlanTimelineItem
                {
                    TaskId = null,
                    Label = "Manual Planning Required",
                    Start = DateTimeOffset.UtcNow,
                    End = DateTimeOffset.UtcNow.AddMinutes(30),
                    Confidence = 1,
                    NudgeAt = null
                }
            }
                };
            }

            // ✅ Fix taskId mapping ONLY when tasks exist.
            if (dbTasks.Count > 0)
            {
                foreach (var item in aiResult.Timeline)
                {
                    if (item.TaskId.HasValue && !validTaskIds.Contains(item.TaskId.Value))
                        item.TaskId = TryMapTaskIdByTitle(item.Label, dbTasks);

                    if (item.TaskId == null)
                        item.TaskId = TryMapTaskIdByTitle(item.Label, dbTasks);

                    _logger.LogInformation("AI item mapped: label='{Label}' taskId={TaskId}",
                        item.Label, item.TaskId?.ToString() ?? "null");
                }
            }
            else
            {
                // routine-only mode
                foreach (var item in aiResult.Timeline)
                    item.TaskId = null;
            }

            // --- Step 3: Save to DB atomically ---
            var exec = _context.Database.CreateExecutionStrategy();
            DailyPlan savedPlan = null!;

            await exec.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var plan = await _context.DailyPlans
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == dateKey);

                if (plan == null)
                {
                    plan = new DailyPlan
                    {
                        UserId = userId,
                        Date = dateKey
                    };
                    _context.DailyPlans.Add(plan);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    if (plan.Items.Any())
                    {
                        _context.DailyPlanItems.RemoveRange(plan.Items);
                        await _context.SaveChangesAsync();
                    }
                }

                var appliedToneString = string.IsNullOrWhiteSpace(aiResult.Tone)
                    ? toneForThisPlanStr
                    : aiResult.Tone.Trim().ToLowerInvariant();

                plan.Tone = appliedToneString;
                plan.Focus = string.IsNullOrWhiteSpace(aiResult.Focus)
                    ? (dbTasks.Count == 0 ? "Light routine plan for today." : (plan.Focus ?? "Your plan"))
                    : aiResult.Focus;

                plan.GeneratedAt = DateTime.UtcNow;
                plan.PlanJsonRaw = aiResult.RawJson ?? "";
                plan.PlanJsonClean = MinifyJson(aiResult.CleanJson ?? aiResult.RawJson ?? "{}");
                plan.ModelUsed = aiResult.ModelUsed;

                await _context.SaveChangesAsync();

                // ✅ Build final timeline WITHOUT OVERLAPS:
                // - Fixed tasks block time
                // - Flex tasks + routine items are packed into gaps around fixed
                if (aiResult.Timeline.Any())
                {
                    var nowUtc = DateTime.UtcNow;

                    // Work window (IST) -> UTC (optional; we mainly prevent overlaps with fixed tasks)
                    var workStartLocalIst = DateTime.SpecifyKind(dateKey.ToDateTime(TimeOnly.MinValue).Add(workStart), DateTimeKind.Unspecified);
                    var workEndLocalIst = DateTime.SpecifyKind(dateKey.ToDateTime(TimeOnly.MinValue).Add(workEnd), DateTimeKind.Unspecified);
                    var workStartUtc = new DateTimeOffset(workStartLocalIst, userOffset).UtcDateTime;
                    var workEndUtc = new DateTimeOffset(workEndLocalIst, userOffset).UtcDateTime;
                    if (workEndUtc <= workStartUtc) workEndUtc = workEndUtc.AddDays(1);

                    // 1) Fixed blocks
                    var fixedBlocks = dbTasks
                        .Where(t => t.PlannedStartUtc.HasValue && t.PlannedEndUtc.HasValue)
                        .Select(t =>
                        {
                            var s = EnsureUtc(t.PlannedStartUtc!.Value);
                            var e = EnsureUtc(t.PlannedEndUtc!.Value);
                            if (e <= s) e = s.AddMinutes(t.EstimatedMinutes ?? 30);
                            return new
                            {
                                TaskId = t.Id,
                                Label = string.IsNullOrWhiteSpace(t.Title) ? "Task" : t.Title!,
                                Start = s,
                                End = e,
                                Confidence = 5
                            };
                        })
                        .OrderBy(x => x.Start)
                        .ToList();

                    var fixedTaskIds = fixedBlocks.Select(x => x.TaskId).ToHashSet();

                    // 2) Normalize AI timeline, shift to anchor (for flex/routine drafts)
                    var earliestAnchor = planStartUtc.HasValue
                        ? EnsureUtc(planStartUtc.Value)
                        : EarliestStartUtc(
                            nowUtc,
                            workStart,
                            workEnd,
                            bufferMinutes: 10,
                            roundToMinutes: 5
                        );

                    if (earliestAnchor < nowUtc.AddMinutes(1))
                        earliestAnchor = nowUtc.AddMinutes(1);

                    // Use AI minStart to compute shift
                    var normalizedAi = aiResult.Timeline
                        .Select(i => new
                        {
                            TaskId = i.TaskId,
                            Label = i.Label,
                            StartUtc = i.Start.UtcDateTime,
                            EndUtc = i.End.UtcDateTime,
                            Confidence = Math.Clamp(i.Confidence, 1, 5)
                        })
                        .ToList();

                    var minAiStart = normalizedAi.Min(x => x.StartUtc);
                    var shift = earliestAnchor - minAiStart;

                    // 3) Build FLEX items list (tasks without planned times + routine items)
                    //    (we pack these into gaps; no overlaps)
                    var flexItems = new List<(int? TaskId, string Label, int Confidence, int DurationMin)>();

                    foreach (var x in normalizedAi.OrderBy(x => x.StartUtc))
                    {
                        // If it's a fixed task → do not treat as flex (we already have exact block)
                        if (x.TaskId.HasValue && fixedTaskIds.Contains(x.TaskId.Value))
                            continue;

                        // Determine label
                        string label = x.Label;
                        int durationMin;

                        if (x.TaskId.HasValue && taskById.TryGetValue(x.TaskId.Value, out var t))
                        {
                            label = string.IsNullOrWhiteSpace(t.Title) ? label : t.Title!;
                            durationMin = t.EstimatedMinutes ?? 30;
                        }
                        else
                        {
                            // routine item duration from AI
                            var dur = (x.EndUtc - x.StartUtc).TotalMinutes;
                            durationMin = (int)Math.Round(dur <= 0 ? 30 : dur);
                        }

                        if (durationMin < 5) durationMin = 5;

                        flexItems.Add((x.TaskId, label, x.Confidence, durationMin));
                    }

                    // If user has tasks but AI returned none mapped (edge), ensure tasks exist in flex/fixed:
                    // (Optional) not required if AI behaves well.

                    // 4) Pack FLEX items into gaps around FIXED blocks
                    var scheduled = new List<(int? TaskId, string Label, DateTime Start, DateTime End, int Confidence)>();

                    // add fixed first into schedule (we'll merge while packing)
                    // packing cursor:
                    var cursor = earliestAnchor;

                    // If you want: clamp cursor to workStartUtc when there are NO fixed blocks
                    // (keeps plans from starting at 3am unless user picked it)
                    if (fixedBlocks.Count == 0 && cursor < workStartUtc)
                        cursor = workStartUtc;

                    // helper: fill a gap [gapStart, gapEnd)
                    void FillGap(DateTime gapStart, DateTime gapEnd)
                    {
                        if (gapEnd <= gapStart) return;
                        if (flexItems.Count == 0) return;

                        var localCursor = gapStart;

                        while (flexItems.Count > 0)
                        {
                            var next = flexItems[0];
                            var start = localCursor;
                            var end = start.AddMinutes(next.DurationMin);

                            if (end > gapEnd)
                                break; // doesn't fit, stop this gap

                            scheduled.Add((next.TaskId, next.Label, start, end, next.Confidence));
                            flexItems.RemoveAt(0);
                            localCursor = end;
                        }
                    }

                    foreach (var fx in fixedBlocks)
                    {
                        // pack before fixed
                        var gapStart = cursor;
                        var gapEnd = fx.Start;

                        // Don't allow negative/past gaps
                        if (gapStart < nowUtc.AddMinutes(1)) gapStart = nowUtc.AddMinutes(1);

                        FillGap(gapStart, gapEnd);

                        // add fixed
                        scheduled.Add((fx.TaskId, fx.Label, fx.Start, fx.End, fx.Confidence));

                        // advance cursor beyond fixed
                        cursor = fx.End > cursor ? fx.End : cursor;
                    }

                    // After last fixed: pack remaining flex (first inside work window, then beyond)
                    // Try inside work window first (optional)
                    if (cursor < workEndUtc)
                    {
                        FillGap(cursor, workEndUtc);
                        cursor = cursor > workEndUtc ? cursor : workEndUtc;
                    }

                    // Remaining flex goes after cursor sequentially
                    while (flexItems.Count > 0)
                    {
                        var next = flexItems[0];
                        var start = cursor;
                        var end = start.AddMinutes(next.DurationMin);

                        scheduled.Add((next.TaskId, next.Label, start, end, next.Confidence));
                        flexItems.RemoveAt(0);
                        cursor = end;
                    }

                    // If absolutely nothing scheduled, keep fallback
                    if (scheduled.Count == 0)
                    {
                        scheduled.Add((null, "Manual Planning Required", nowUtc, nowUtc.AddMinutes(30), 1));
                    }

                    // Final sort by start (guaranteed no overlaps from packing)
                    scheduled = scheduled.OrderBy(x => x.Start).ToList();

                    // 5) Create DailyPlanItems with safe nudge schedule logic
                    var items = scheduled.Select(x =>
                    {
                        var start = EnsureUtc(x.Start);
                        var end = EnsureUtc(x.End);
                        if (end <= start) end = start.AddMinutes(30);

                        DateTime? startNudge = start.AddMinutes(-5);
                        if (startNudge <= nowUtc)
                            startNudge = (nowUtc < start) ? nowUtc.AddSeconds(10) : null;

                        DateTime? endNudge = end.AddMinutes(-5);
                        if (endNudge <= nowUtc)
                            endNudge = (nowUtc < end) ? nowUtc.AddSeconds(15) : null;

                        return new DailyPlanItem
                        {
                            PlanId = plan.Id,
                            TaskId = x.TaskId,
                            Label = x.Label,

                            Start = start,
                            End = end,

                            Confidence = Math.Clamp(x.Confidence, 1, 5),

                            NudgeAt = startNudge,
                            NudgeSentAtUtc = null,

                            EndNudgeAtUtc = endNudge,
                            EndNudgeSentAtUtc = null,

                            LastNudgeError = null
                        };
                    }).ToList();

                    await _context.DailyPlanItems.AddRangeAsync(items);
                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();

                savedPlan = await _context.DailyPlans
                    .AsNoTracking()
                    .Include(p => p.Items)
                    .FirstAsync(p => p.Id == plan.Id);
            });

            // ✅ Keep your tone learning (unchanged)
            var dayUtc = new DateTimeOffset(
                dateKey.ToDateTime(TimeOnly.MinValue),
                TimeSpan.FromMinutes(330)
            ).UtcDateTime;

            await ApplyToneLearningAsync(
                user,
                dayUtc,
                aiResult,
                toneForThisPlanStr
            );

            return MapToDto(savedPlan);
        }

        private async System.Threading.Tasks.Task ApplyToneLearningAsync(ApplicationUser user, DateTime day, DailyPlanAiResult aiResult, string toneHint)
        {
            try
            {
                // Performance heuristic from carry-overs (TD2 baseline)
                int performanceScore = 0;
                if (aiResult.CarryOverTaskIds != null)
                {
                    var carry = aiResult.CarryOverTaskIds.Count;
                    if (carry == 0) performanceScore = +1;
                    else if (carry <= 3) performanceScore = -1;
                    else performanceScore = -2;
                }

                // Emotional signal (placeholder until you add reflection/mood)
                int emotionalScore = 0;

                // Suggested tone from AI (string → enum)
                var suggestedToneEnum = MapToneFromString(aiResult.Tone);
                // Applied tone under TAP2: if user has PreferredTone → that wins
                var appliedToneEnum = user.PreferredTone ?? suggestedToneEnum;

                // Confidence delta (EV3-M moderate): small nudges, clamp -6..+6
                int delta = Math.Clamp(emotionalScore + performanceScore, -2, 2) * 3;

                // Write daily history (one per user/day)
                var history = new ToneHistory
                {
                    UserId = user.Id,
                    Date = day,
                    EmotionalScore = emotionalScore,
                    PerformanceScore = performanceScore,
                    SuggestedTone = suggestedToneEnum,
                    AppliedTone = appliedToneEnum,
                    ConfidenceDelta = delta,
                    Notes = null
                };
                _context.ToneHistories.Add(history);

                // Update user-level state only if no explicit PreferredTone (TAP2)
                if (user.PreferredTone is null)
                {
                    var cooldownDays = 3;
                    var canSwitch = user.LastToneChangeDate is null
                        || (DateTime.UtcNow - user.LastToneChangeDate.Value).TotalDays >= cooldownDays;

                    // Adjust confidence
                    user.ToneConfidence = Math.Clamp(user.ToneConfidence + delta, 0, 100);

                    // Gentle switch rule: pivot only when confidence crosses thresholds & cooldown passed
                    if (canSwitch && (user.ToneConfidence <= 30 || user.ToneConfidence >= 70))
                    {
                        if (appliedToneEnum != user.CurrentTone)
                        {
                            user.CurrentTone = appliedToneEnum;
                            user.LastToneChangeDate = DateTime.UtcNow;

                            // TE2: gentle awareness (log hook; surface via /nudges later)
                            _logger.LogInformation(
                                "TONE_ADAPT: User={UserId} Date={Date} Hint={Hint} Returned={Returned} NewCurrent={Current} Confidence={Conf}",
                                user.Id, day.ToString("yyyy-MM-dd"), toneHint, aiResult.Tone, user.CurrentTone, user.ToneConfidence);
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    // User has an explicit preference — we still log for analytics
                    _logger.LogInformation(
                        "TONE_LEARN_SKIPPED_PREFERRED: User={UserId} Pref={Pref} Returned={Returned} ΔConf={Delta}",
                        user.Id, user.PreferredTone, aiResult.Tone, delta);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tone learning step skipped due to error.");
            }
        }

        // ----------------- Helpers -----------------

        private static string MinifyJson(string json)
        {
            try
            {
                using var node = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return json; // defensive
            }
        }

        private static DateTime EnsureUtc(DateTime dt)
            => dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);

        public static DateTime EarliestStartUtc(
                      DateTime nowUtc,
                      TimeSpan workStartUtcTime,
                      TimeSpan workEndUtcTime,
                      int bufferMinutes = 5,
                      int roundToMinutes = 10)
                          {
                              if (nowUtc.Kind != DateTimeKind.Utc)
                                  nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

                              var t = nowUtc.AddMinutes(bufferMinutes);

                              var remainder = t.Minute % roundToMinutes;
                              if (remainder != 0)
                                  t = t.AddMinutes(roundToMinutes - remainder);

                              t = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Utc);

                              var dayStart = new DateTime(t.Year, t.Month, t.Day, 0, 0, 0, DateTimeKind.Utc);
                              var workStart = dayStart.Add(workStartUtcTime);
                              var workEnd = dayStart.Add(workEndUtcTime);

                              if (t < workStart)
                                  t = workStart;

                              if (t >= workEnd)
                                  t = dayStart.AddDays(1).Add(workStartUtcTime);

                              return t;
                          }

        private static PlanTone MapToneFromString(string? s)
        {
            var v = (s ?? "").Trim().ToLowerInvariant();
            return v switch
            {
                "soft" => PlanTone.Soft,
                "strict" => PlanTone.Strict,
                "playful" => PlanTone.Playful,
                _ => PlanTone.Balanced
            };
        }

        private static string ToneToAiString(PlanTone tone) => tone switch
        {
            PlanTone.Soft => "soft",
            PlanTone.Strict => "strict",
            PlanTone.Playful => "playful",
            PlanTone.Balanced => "balanced",
            _ => "balanced"
        };

        private static PlanResponseDto MapToDto(DailyPlan plan)
        {
            return new PlanResponseDto
            {
                PlanId = plan.Id,
                Date = plan.Date.ToString("yyyy-MM-dd"),
                Focus = plan.Focus ?? "",
                Timeline = plan.Items
                    .OrderBy(i => i.Start)
                    .Select(i => new PlanItemDto
                    {
                        ItemId = i.Id,
                        TaskId = i.TaskId,
                        Label = i.Label,
                        Start = i.Start,
                        End = i.End,
                        Confidence = i.Confidence, // ← if you renamed to Confidence, switch back to i.Confidence
                        NudgeAt = i.NudgeAt
                    })
                    .ToList()
            };
        }
    }
}
#endregion
