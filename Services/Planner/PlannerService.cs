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

        //public async Task<PlanResponseDto> GeneratePlanAsync(
        //    string userId,
        //    DateTime date,
        //    string? toneOverride = null,
        //    bool forceRegenerate = false,
        //    DateTime? planStartUtc = null
        //)
        //{
        //    var userOffset = TimeSpan.FromMinutes(330); // IST

        //    // ✅ IST calendar date key (date-only)
        //    var istDate = DateOnly.FromDateTime(date.Date);

        //    // ✅ IST day window -> UTC window (for task filtering)
        //    var istStartLocal = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        //    var istEndLocal = istStartLocal.AddDays(1);

        //    var startUtc = new DateTimeOffset(istStartLocal, userOffset).UtcDateTime;
        //    var endUtc = new DateTimeOffset(istEndLocal, userOffset).UtcDateTime;

        //    // --- Step 0: Load user + reuse existing plan (unless forceRegenerate) ---
        //    var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        //    if (user == null) throw new Exception("User not found");

        //    var existing = await _context.DailyPlans
        //        .AsNoTracking()
        //        .Include(p => p.Items)
        //        .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == istDate);

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
        //        // ✅ keep AI request date as UTC anchor if your AI expects DateTime:
        //        // use startUtc (UTC instant for IST midnight) only for AI context, not DB key
        //        Date = startUtc,
        //        Tone = toneForThisPlanStr,
        //        ForceRegenerate = forceRegenerate
        //    };

        //    // --- Step 2: Call AI engine ---
        //    DailyPlanAiResult aiResult = await _aiPlanner.GenerateAiPlanAsync(aiRequest);

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
        //            Label = "Manual Planning Required",
        //            Start = DateTime.UtcNow,
        //            End = DateTime.UtcNow.AddMinutes(30),
        //            Confidence = 1
        //        }
        //    }
        //        };
        //    }

        //    // --- Step 3: Save to DB atomically ---
        //    var exec = _context.Database.CreateExecutionStrategy();
        //    DailyPlan savedPlan = null!;

        //    await exec.ExecuteAsync(async () =>
        //    {
        //        await using var tx = await _context.Database.BeginTransactionAsync();

        //        var plan = await _context.DailyPlans
        //            .Include(p => p.Items)
        //            .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == istDate);

        //        if (plan == null)
        //        {
        //            plan = new DailyPlan
        //            {
        //                UserId = userId,
        //                Date = istDate
        //            };
        //            _context.DailyPlans.Add(plan);
        //            await _context.SaveChangesAsync();
        //        }
        //        else
        //        {
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

        //            var earliest = planStartUtc.HasValue
        //                ? EnsureUtc(planStartUtc.Value)
        //                : EarliestStartUtc(nowUtc, uWorkStart, uWorkEnd, bufferMinutes: 10, roundToMinutes: 5);

        //            var normalized = aiResult.Timeline
        //                .Select(i => new
        //                {
        //                    i.TaskId,
        //                    i.Label,
        //                    StartUtc = EnsureUtc(i.Start),
        //                    EndUtc = EnsureUtc(i.End),
        //                    i.Confidence,
        //                    NudgeAtUtc = i.NudgeAt.HasValue ? EnsureUtc(i.NudgeAt.Value) : (DateTime?)null
        //                })
        //                .ToList();

        //            var minStartUtc = normalized.Min(x => x.StartUtc);
        //            var shift = earliest - minStartUtc;

        //            var items = normalized
        //                .OrderBy(x => x.StartUtc)
        //                .Select(x =>
        //                {
        //                    var start = x.StartUtc + shift;
        //                    var end = x.EndUtc + shift;
        //                    if (end <= start) end = start.AddMinutes(30);

        //                    return new DailyPlanItem
        //                    {
        //                        PlanId = plan.Id,
        //                        TaskId = x.TaskId,
        //                        Label = x.Label,
        //                        Start = start,
        //                        End = end,
        //                        Confidence = Math.Clamp(x.Confidence, 1, 5),
        //                        NudgeAt = x.NudgeAtUtc.HasValue ? x.NudgeAtUtc.Value + shift : null
        //                    };
        //                })
        //                .ToList();

        //            await _context.DailyPlanItems.AddRangeAsync(items);
        //            await _context.SaveChangesAsync();
        //        }

        //        await tx.CommitAsync();

        //        savedPlan = await _context.DailyPlans
        //            .AsNoTracking()
        //            .Include(p => p.Items)
        //            .FirstAsync(p => p.Id == plan.Id);
        //    });

        //    // Tone learning can still use IST day as key if your method accepts DateTime:
        //    // convert DateOnly -> DateTime
        //    await ApplyToneLearningAsync(user, istDate.ToDateTime(TimeOnly.MinValue), aiResult, toneForThisPlanStr);

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

            var startUtc = new DateTimeOffset(istStartLocal, userOffset).UtcDateTime;
            var endUtc = startUtc.AddDays(1);

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
            var dbTasks = await _context.Tasks
                .Where(t => t.UserId == userId
                    && !t.Completed
                    && t.DueDate >= startUtc
                    && t.DueDate < endUtc)
                .OrderByDescending(t => t.Priority)
                .ToListAsync();

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

                // ✅ If your AI expects DateTime, give it the UTC anchor for IST midnight
                Date = startUtc,

                Tone = toneForThisPlanStr,
                ForceRegenerate = forceRegenerate
            };

            // --- Step 2: Call AI engine ---
            DailyPlanAiResult aiResult = await _aiPlanner.GenerateAiPlanAsync(aiRequest);

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
                    Label = "Manual Planning Required",
                    Start = DateTime.UtcNow,
                    End = DateTime.UtcNow.AddMinutes(30),
                    Confidence = 1
                }
            }
                };
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
                        Date = dateKey // ✅ DateOnly stored
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
                plan.Focus = string.IsNullOrWhiteSpace(aiResult.Focus) ? (plan.Focus ?? "Your plan") : aiResult.Focus;
                plan.GeneratedAt = DateTime.UtcNow;
                plan.PlanJsonRaw = aiResult.RawJson ?? "";
                plan.PlanJsonClean = MinifyJson(aiResult.CleanJson ?? aiResult.RawJson ?? "{}");
                plan.ModelUsed = aiResult.ModelUsed;

                await _context.SaveChangesAsync();

                if (aiResult.Timeline.Any())
                {
                    var nowUtc = DateTime.UtcNow;

                    var uWorkStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
                    var uWorkEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

                    var earliest = planStartUtc.HasValue
                        ? EnsureUtc(planStartUtc.Value)
                        : EarliestStartUtc(nowUtc, uWorkStart, uWorkEnd, bufferMinutes: 10, roundToMinutes: 5);

                    var normalized = aiResult.Timeline
                        .Select(i => new
                        {
                            i.TaskId,
                            i.Label,
                            StartUtc = EnsureUtc(i.Start),
                            EndUtc = EnsureUtc(i.End),
                            i.Confidence,
                            NudgeAtUtc = i.NudgeAt.HasValue ? EnsureUtc(i.NudgeAt.Value) : (DateTime?)null
                        })
                        .ToList();

                    var minStartUtc = normalized.Min(x => x.StartUtc);
                    var shift = earliest - minStartUtc;

                    var items = normalized
                        .OrderBy(x => x.StartUtc)
                        .Select(x =>
                        {
                            var start = x.StartUtc + shift;
                            var end = x.EndUtc + shift;
                            if (end <= start) end = start.AddMinutes(30);

                            return new DailyPlanItem
                            {
                                PlanId = plan.Id,
                                TaskId = x.TaskId,
                                Label = x.Label,

                                Start = start,
                                End = end,

                                Confidence = Math.Clamp(x.Confidence, 1, 5),

                                // ✅ Start reminder = 5 min before start
                                NudgeAt = (start.AddMinutes(-5) <= nowUtc)
                                ? nowUtc.AddSeconds(10)           // if already late, fire ASAP
                                : start.AddMinutes(-5),

                                NudgeSentAtUtc = null,

                                // ✅ End reminder = 5 min before end
                                EndNudgeAtUtc = (end.AddMinutes(-5) <= nowUtc)
                                ? nowUtc.AddSeconds(15)           // if already late, fire ASAP
                                : end.AddMinutes(-5),

                                EndNudgeSentAtUtc = null,

                                LastNudgeError = null
                            };

                            //return new DailyPlanItem
                            //{
                            //    PlanId = plan.Id,
                            //    TaskId = x.TaskId,
                            //    Label = x.Label,
                            //    Start = start,
                            //    End = end,
                            //    Confidence = Math.Clamp(x.Confidence, 1, 5),
                            //    NudgeAt = x.NudgeAtUtc.HasValue ? x.NudgeAtUtc.Value + shift : null
                            //};
                        })
                        .ToList();

                    await _context.DailyPlanItems.AddRangeAsync(items);
                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();

                savedPlan = await _context.DailyPlans
                    .AsNoTracking()
                    .Include(p => p.Items)
                    .FirstAsync(p => p.Id == plan.Id);
            });

            // ✅ Tone learning: convert DateOnly -> DateTime at IST midnight (Kind Unspecified is fine)
            await ApplyToneLearningAsync(
                user,
                dateKey.ToDateTime(TimeOnly.MinValue),
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
