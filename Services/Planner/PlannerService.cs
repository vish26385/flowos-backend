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
        //                                    string userId,
        //                                    DateTime date,
        //                                    string? toneOverride = null,
        //                                    bool forceRegenerate = false,
        //                                    DateTime? planStartUtc = null // ✅ add this
        //                                )
        //{
        //    // ✅ ALWAYS normalize incoming date to UTC day start (no Local DateTime ever)
        //    var startUtc = DateTimeUtc.UtcDayStart(date);
        //            var endUtc = startUtc.AddDays(1);

        //            // If your DailyPlans.Date is a "date-only" semantic stored as DateTime,
        //            // keep it as a UTC midnight DateTime.
        //            var day = startUtc;

        //            // --- Step 0: Load user + (optionally) reuse existing plan ---
        //            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        //            if (user == null) throw new Exception("User not found");

        //            var existing = await _context.DailyPlans
        //                .AsNoTracking()
        //                .Include(p => p.Items)
        //                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == day);

        //            if (existing != null && !forceRegenerate)
        //            {
        //                _logger.LogInformation(
        //                    "AI_PLAN_REUSE: User={UserId} Date={Date} Items={Items} Focus='{Focus}'",
        //                    userId, day.ToString("yyyy-MM-dd"), existing.Items.Count, existing.Focus ?? "");
        //                return MapToDto(existing);
        //            }

        //            // --- Step 1: Build AiPlanRequest (TAP2 + TL-C) ---
        //            var workStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
        //            var workEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

        //            var firstName = (user.FullName ?? "")
        //                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        //                .FirstOrDefault() ?? "Friend";

        //            // Tone hint for AI (string), respecting TAP2 and toneOverride
        //            // Order of precedence: override -> PreferredTone -> CurrentTone -> Balanced
        //            var toneForThisPlanEnum = user.PreferredTone ?? user.CurrentTone; // Preferred wins; else current
        //            var toneForThisPlanStr = (toneOverride ?? ToneToAiString(toneForThisPlanEnum))
        //                .Trim()
        //                .ToLowerInvariant();

        //            // ✅ Pull candidate tasks for this UTC day window (pending only)
        //            var dbTasks = await _context.Tasks
        //                .Where(t => t.UserId == userId
        //                    && !t.Completed
        //                    && t.DueDate >= startUtc
        //                    && t.DueDate < endUtc)
        //                .OrderByDescending(t => t.Priority)
        //                .ToListAsync();

        //            var taskCtx = dbTasks.Select(t => new TaskAiContext
        //            {
        //                Id = t.Id,
        //                Title = t.Title,
        //                Description = t.Description,
        //                Priority = t.Priority,
        //                DueDate = t.DueDate,               // already UTC if your DB is timestamptz
        //                EstimatedMinutes = t.EstimatedMinutes ?? 30,
        //                EnergyLevel = t.EnergyLevel
        //            }).ToList();

        //            var aiRequest = new AiPlanRequest
        //            {
        //                UserId = userId,
        //                User = new UserAiContext
        //                {
        //                    Id = userId,
        //                    FirstName = firstName,
        //                    FullName = user.FullName,
        //                    WorkStart = workStart,
        //                    WorkEnd = workEnd,
        //                    PreferredTone = user.PreferredTone?.ToString()
        //                },
        //                Tasks = taskCtx,
        //                Date = day, // ✅ UTC midnight
        //                Tone = toneForThisPlanStr,
        //                ForceRegenerate = forceRegenerate
        //            };

        //            // --- Step 2: Call AI engine (OpenAIPlannerService handles retries/fallbacks) ---
        //            var swAi = Stopwatch.StartNew();
        //            DailyPlanAiResult aiResult = await _aiPlanner.GenerateAiPlanAsync(aiRequest);

        //            var metrics = AiPlanQualityAnalyzer.Analyze(aiResult, aiRequest.Tasks.Count);

        //            bool shouldRegenerate =
        //                metrics.AvgConfidence < MinConfidenceThreshold ||
        //                metrics.CoveragePercent < MinCoverageThreshold ||
        //                metrics.AlignedTasksPercent < MinAlignedThreshold;

        //            if (shouldRegenerate)
        //            {
        //                _logger.LogWarning(
        //                    "⚠️ AI plan quality below threshold | Confidence={0} | Coverage={1}% | Aligned={2}% — retrying once.",
        //                    metrics.AvgConfidence, metrics.CoveragePercent, metrics.AlignedTasksPercent
        //                );

        //                try
        //                {
        //                    var retryRequest = aiRequest with { ForceRegenerate = true };
        //                    var retryResult = await _aiPlanner.GenerateAiPlanAsync(retryRequest);
        //                    var retryMetrics = AiPlanQualityAnalyzer.Analyze(retryResult, aiRequest.Tasks.Count);

        //                    if (retryMetrics.AvgConfidence >= MinConfidenceThreshold &&
        //                        retryMetrics.CoveragePercent >= MinCoverageThreshold)
        //                    {
        //                        _logger.LogInformation("✅ Regeneration successful: plan quality improved to acceptable levels.");
        //                        aiResult = retryResult;
        //                    }
        //                    else
        //                    {
        //                        _logger.LogWarning("⚠️ Regeneration did not sufficiently improve quality. Keeping original plan.");
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    _logger.LogError(ex, "AI plan regeneration attempt failed.");
        //                }
        //            }

        //            // ✅ Audit timing should be UTC
        //            var audit = new AiPlanAudit
        //            {
        //                UserId = aiRequest.User.Id,
        //                RequestedAt = DateTimeUtc.ToUtc(aiRequest.StartedAt),
        //                CompletedAt = DateTime.UtcNow,
        //                LatencyMs = (long)(DateTime.UtcNow - DateTimeUtc.ToUtc(aiRequest.StartedAt)).TotalMilliseconds,

        //                ModelUsed = aiResult.ModelUsed ?? "unknown",
        //                WasRegenerated = shouldRegenerate,

        //                AvgConfidence = metrics.AvgConfidence,
        //                CoveragePercent = metrics.CoveragePercent,
        //                AlignedTasksPercent = metrics.AlignedTasksPercent,
        //                OverlapCount = metrics.OverlapCount,

        //                RawJson = aiResult.RawJson ?? "",
        //                CleanJson = aiResult.CleanJson ?? "",

        //                Notes = metrics.Notes
        //            };

        //            _context.AiPlanAudits.Add(audit);
        //            await _context.SaveChangesAsync();

        //            _logger.LogInformation(
        //                "📊 AI PLAN QUALITY | Confidence={AvgConfidence} | Coverage={Coverage}% | Aligned={Aligned}% | Overlaps={Overlaps} | Items={Items} | Status={Status}",
        //                metrics.AvgConfidence,
        //                metrics.CoveragePercent,
        //                metrics.AlignedTasksPercent,
        //                metrics.OverlapCount,
        //                aiResult.Timeline.Count,
        //                metrics.Status
        //            );

        //            if (metrics.AvgConfidence < 2.5 || metrics.CoveragePercent < 50)
        //            {
        //                _logger.LogWarning("⚠️ Low AI plan quality detected. Consider re-generating.");
        //            }

        //            if (aiResult == null || aiResult.Timeline.Count == 0)
        //            {
        //                _logger.LogWarning("⚠️ AI returned empty or invalid plan — using fallback.");
        //                aiResult = new DailyPlanAiResult
        //                {
        //                    Tone = "balanced",
        //                    Focus = "Fallback plan for today.",
        //                    Timeline = new List<AiPlanTimelineItem>
        //            {
        //                new AiPlanTimelineItem
        //                {
        //                    Label = "Manual Planning Required",
        //                    Start = DateTime.UtcNow,
        //                    End = DateTime.UtcNow.AddMinutes(30),
        //                    Confidence = 1
        //                }
        //            }
        //                };
        //            }

        //            _logger.LogInformation(
        //                "✅ AI Plan Generated | Model={ModelUsed} | Tasks={ItemCount}\nTone={Tone}\nFocus={Focus}\nRawJsonLength={Len}",
        //                aiResult?.ModelUsed ?? "(unknown)",
        //                aiResult?.Timeline?.Count ?? 0,
        //                aiResult?.Tone ?? "(none)",
        //                aiResult?.Focus ?? "(none)",
        //                aiResult?.RawJson?.Length ?? 0
        //            );

        //            swAi.Stop();

        //            var rawSizeKb = aiResult.RawJson is null ? 0 : (aiResult.RawJson.Length / 1024.0);
        //            var cleanSizeKb = aiResult.CleanJson is null ? 0 : (aiResult.CleanJson.Length / 1024.0);

        //            _logger.LogInformation(
        //                "AI_PLAN_GEN: User={UserId} Date={Date} ToneHint={ToneHint} ToneReturned={ToneReturned} Items={Items} ModelUsed={Model} RawKB={RawKb:N1} CleanKB={CleanKb:N1} Ms={Ms}ms",
        //                userId, day.ToString("yyyy-MM-dd"), toneForThisPlanStr, aiResult.Tone, aiResult.Timeline.Count,
        //                aiResult.ModelUsed ?? "(unknown)", rawSizeKb, cleanSizeKb, swAi.ElapsedMilliseconds);

        //            // --- Step 3: Save to DB atomically (hard delete items on regenerate) ---
        //            var swSave = Stopwatch.StartNew();
        //            var exec = _context.Database.CreateExecutionStrategy();
        //            DailyPlan savedPlan = null!;

        //            await exec.ExecuteAsync(async () =>
        //            {
        //                await using var tx = await _context.Database.BeginTransactionAsync();

        //                var plan = await _context.DailyPlans
        //                    .Include(p => p.Items)
        //                    .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == day);

        //                if (plan == null)
        //                {
        //                    plan = new DailyPlan
        //                    {
        //                        UserId = userId,
        //                        Date = day
        //                    };
        //                    _context.DailyPlans.Add(plan);
        //                    await _context.SaveChangesAsync();
        //                }
        //                else
        //                {
        //                    if (plan.Items.Any())
        //                    {
        //                        _context.DailyPlanItems.RemoveRange(plan.Items);
        //                        await _context.SaveChangesAsync();
        //                    }
        //                }

        //                var appliedToneString = string.IsNullOrWhiteSpace(aiResult.Tone)
        //                    ? toneForThisPlanStr
        //                    : aiResult.Tone.Trim().ToLowerInvariant();

        //                plan.Tone = appliedToneString;
        //                plan.Focus = string.IsNullOrWhiteSpace(aiResult.Focus) ? (plan.Focus ?? "Your plan") : aiResult.Focus;
        //                plan.GeneratedAt = DateTime.UtcNow;
        //                plan.PlanJsonRaw = aiResult.RawJson ?? "";
        //                plan.PlanJsonClean = MinifyJson(aiResult.CleanJson ?? aiResult.RawJson ?? "{}");
        //                plan.ModelUsed = aiResult.ModelUsed;

        //                await _context.SaveChangesAsync();

        //                //if (aiResult.Timeline.Any())
        //                //{
        //                //    var items = aiResult.Timeline
        //                //        .OrderBy(i => i.Start)
        //                //        .Select(i => new DailyPlanItem
        //                //        {
        //                //            PlanId = plan.Id,
        //                //            TaskId = i.TaskId,
        //                //            Label = i.Label,

        //                //            // ✅ Ensure these are UTC before writing to timestamptz
        //                //            Start = EnsureUtc(i.Start),
        //                //            End = EnsureUtc(i.End),
        //                //            Confidence = Math.Clamp(i.Confidence, 1, 5),
        //                //            NudgeAt = i.NudgeAt.HasValue ? EnsureUtc(i.NudgeAt.Value) : null
        //                //        });

        //                //    await _context.DailyPlanItems.AddRangeAsync(items);
        //                //    await _context.SaveChangesAsync();
        //                //}

        //                if (aiResult.Timeline.Any())
        //                {
        //                    var nowUtc = DateTime.UtcNow;

        //                    // user working hours (UTC)
        //                    var workStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
        //                    var workEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

        //                    //// ✅ determine realistic first start time
        //                    //var earliest = EarliestStartUtc(
        //                    //    nowUtc,
        //                    //    workStart,
        //                    //    workEnd,
        //                    //    bufferMinutes: 5,
        //                    //    roundToMinutes: 10
        //                    //);

        //                    // if caller passed planStartUtc, use it.
        //                    // else fallback to your EarliestStartUtc(...) rule
        //                    var earliest = planStartUtc ?? EarliestStartUtc(
        //                        nowUtc,
        //                        workStart,
        //                        workEnd,
        //                        bufferMinutes: 10,
        //                        roundToMinutes: 5
        //                    );

        //                    // 1) Normalize AI times to UTC once
        //                    var normalized = aiResult.Timeline
        //                        .Select(i => new
        //                        {
        //                            i.TaskId,
        //                            i.Label,
        //                            StartUtc = EnsureUtc(i.Start),
        //                            EndUtc = EnsureUtc(i.End),
        //                            i.Confidence,
        //                            NudgeAtUtc = i.NudgeAt.HasValue ? EnsureUtc(i.NudgeAt.Value) : (DateTime?)null
        //                        })
        //                        .ToList();

        //                    // 2) Compute ONE global shift (preserve spacing)
        //                    var minStartUtc = normalized.Min(x => x.StartUtc);

        //                    var shift = minStartUtc < earliest
        //                        ? (earliest - minStartUtc)
        //                        : TimeSpan.Zero;

        //                    // 3) Create DB items with shifted times
        //                    var items = normalized
        //                        .OrderBy(x => x.StartUtc)
        //                        .Select(x =>
        //                        {
        //                            var start = x.StartUtc + shift;
        //                            var end = x.EndUtc + shift;

        //                            // safety: invalid duration fallback
        //                            if (end <= start)
        //                                end = start.AddMinutes(30);

        //                            return new DailyPlanItem
        //                            {
        //                                PlanId = plan.Id,
        //                                TaskId = x.TaskId,
        //                                Label = x.Label,

        //                                Start = start,
        //                                End = end,

        //                                Confidence = Math.Clamp(x.Confidence, 1, 5),

        //                                // keep nudge aligned with shifted timeline
        //                                NudgeAt = x.NudgeAtUtc.HasValue
        //                                    ? x.NudgeAtUtc.Value + shift
        //                                    : null
        //                            };
        //                        })
        //                        .ToList();

        //                    await _context.DailyPlanItems.AddRangeAsync(items);
        //                    await _context.SaveChangesAsync();
        //                }

        //                await tx.CommitAsync();

        //                savedPlan = await _context.DailyPlans
        //                    .AsNoTracking()
        //                    .Include(p => p.Items)
        //                    .FirstAsync(p => p.Id == plan.Id);
        //            });

        //            swSave.Stop();

        //            _logger.LogInformation(
        //                "AI_PLAN_SAVE: User={UserId} Date={Date} Items={Items} ModelUsed={Model} RawKB={RawKb:N1} CleanKB={CleanKb:N1} SaveMs={SaveMs}ms",
        //                userId,
        //                day.ToString("yyyy-MM-dd"),
        //                savedPlan.Items.Count,
        //                savedPlan.ModelUsed ?? "(unknown)",
        //                rawSizeKb,
        //                cleanSizeKb,
        //                swSave.ElapsedMilliseconds
        //            );

        //            // --- Step 3.1: Tone Learning (EV3-M + TD2 + TE2) -------------------
        //            await ApplyToneLearningAsync(user, day, aiResult, toneForThisPlanStr);

        //            // --- Step 4: Return DTO ---
        //            return MapToDto(savedPlan);
        //        }

        // ----------------- Tone Learning (EV3-M) -----------------

        public async Task<PlanResponseDto> GeneratePlanAsync(
            string userId,
            DateTime date,
            string? toneOverride = null,
            bool forceRegenerate = false,
            DateTime? planStartUtc = null
        )
        {
            // ✅ ALWAYS normalize incoming date to UTC day start (no Local DateTime ever)
            var startUtc = DateTimeUtc.UtcDayStart(date);
            var endUtc = startUtc.AddDays(1);

            // If your DailyPlans.Date is a "date-only" semantic stored as DateTime,
            // keep it as a UTC midnight DateTime.
            var day = startUtc;

            // --- Step 0: Load user + (optionally) reuse existing plan ---
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) throw new Exception("User not found");

            var existing = await _context.DailyPlans
                .AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == day);

            if (existing != null && !forceRegenerate)
            {
                _logger.LogInformation(
                    "AI_PLAN_REUSE: User={UserId} Date={Date} Items={Items} Focus='{Focus}'",
                    userId, day.ToString("yyyy-MM-dd"), existing.Items.Count, existing.Focus ?? "");
                return MapToDto(existing);
            }

            // --- Step 1: Build AiPlanRequest (TAP2 + TL-C) ---
            var workStart = user.WorkStart ?? new TimeSpan(9, 0, 0);
            var workEnd = user.WorkEnd ?? new TimeSpan(18, 0, 0);

            var firstName = (user.FullName ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "Friend";

            var toneForThisPlanEnum = user.PreferredTone ?? user.CurrentTone;
            var toneForThisPlanStr = (toneOverride ?? ToneToAiString(toneForThisPlanEnum))
                .Trim()
                .ToLowerInvariant();

            // ✅ Pull candidate tasks for this UTC day window (pending only)
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
                Date = day, // ✅ UTC midnight
                Tone = toneForThisPlanStr,
                ForceRegenerate = forceRegenerate
            };

            // --- Step 2: Call AI engine ---
            var swAi = Stopwatch.StartNew();
            DailyPlanAiResult aiResult = await _aiPlanner.GenerateAiPlanAsync(aiRequest);

            var metrics = AiPlanQualityAnalyzer.Analyze(aiResult, aiRequest.Tasks.Count);

            bool shouldRegenerate =
                metrics.AvgConfidence < MinConfidenceThreshold ||
                metrics.CoveragePercent < MinCoverageThreshold ||
                metrics.AlignedTasksPercent < MinAlignedThreshold;

            if (shouldRegenerate)
            {
                _logger.LogWarning(
                    "⚠️ AI plan quality below threshold | Confidence={0} | Coverage={1}% | Aligned={2}% — retrying once.",
                    metrics.AvgConfidence, metrics.CoveragePercent, metrics.AlignedTasksPercent
                );

                try
                {
                    var retryRequest = aiRequest with { ForceRegenerate = true };
                    var retryResult = await _aiPlanner.GenerateAiPlanAsync(retryRequest);
                    var retryMetrics = AiPlanQualityAnalyzer.Analyze(retryResult, aiRequest.Tasks.Count);

                    if (retryMetrics.AvgConfidence >= MinConfidenceThreshold &&
                        retryMetrics.CoveragePercent >= MinCoverageThreshold)
                    {
                        _logger.LogInformation("✅ Regeneration successful: plan quality improved to acceptable levels.");
                        aiResult = retryResult;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Regeneration did not sufficiently improve quality. Keeping original plan.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI plan regeneration attempt failed.");
                }
            }

            // ✅ Audit timing should be UTC
            var audit = new AiPlanAudit
            {
                UserId = aiRequest.User.Id,
                RequestedAt = DateTimeUtc.ToUtc(aiRequest.StartedAt),
                CompletedAt = DateTime.UtcNow,
                LatencyMs = (long)(DateTime.UtcNow - DateTimeUtc.ToUtc(aiRequest.StartedAt)).TotalMilliseconds,

                ModelUsed = aiResult.ModelUsed ?? "unknown",
                WasRegenerated = shouldRegenerate,

                AvgConfidence = metrics.AvgConfidence,
                CoveragePercent = metrics.CoveragePercent,
                AlignedTasksPercent = metrics.AlignedTasksPercent,
                OverlapCount = metrics.OverlapCount,

                RawJson = aiResult.RawJson ?? "",
                CleanJson = aiResult.CleanJson ?? "",

                Notes = metrics.Notes
            };

            _context.AiPlanAudits.Add(audit);
            await _context.SaveChangesAsync();

            if (aiResult == null || aiResult.Timeline.Count == 0)
            {
                _logger.LogWarning("⚠️ AI returned empty or invalid plan — using fallback.");
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

            swAi.Stop();

            var rawSizeKb = aiResult.RawJson is null ? 0 : (aiResult.RawJson.Length / 1024.0);
            var cleanSizeKb = aiResult.CleanJson is null ? 0 : (aiResult.CleanJson.Length / 1024.0);

            // --- Step 3: Save to DB atomically (hard delete items on regenerate) ---
            var exec = _context.Database.CreateExecutionStrategy();
            DailyPlan savedPlan = null!;

            await exec.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var plan = await _context.DailyPlans
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == day);

                if (plan == null)
                {
                    plan = new DailyPlan
                    {
                        UserId = userId,
                        Date = day
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

                    // ✅ If caller passed planStartUtc, use it EXACTLY.
                    // otherwise fallback to your EarliestStartUtc rule.
                    var earliest = planStartUtc.HasValue
                        ? EnsureUtc(planStartUtc.Value)
                        : EarliestStartUtc(
                            nowUtc,
                            uWorkStart,
                            uWorkEnd,
                            bufferMinutes: 10,
                            roundToMinutes: 5
                        );

                    // 1) Normalize AI times to UTC once
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

                    // ✅ FIX: SHIFT CAN BE NEGATIVE TOO
                    // If AI starts at 09:00 UTC but user chose 06:30 UTC, shift becomes -02:30
                    var shift = earliest - minStartUtc;

                    var items = normalized
                        .OrderBy(x => x.StartUtc)
                        .Select(x =>
                        {
                            var start = x.StartUtc + shift;
                            var end = x.EndUtc + shift;

                            if (end <= start)
                                end = start.AddMinutes(30);

                            return new DailyPlanItem
                            {
                                PlanId = plan.Id,
                                TaskId = x.TaskId,
                                Label = x.Label,

                                Start = start,
                                End = end,

                                Confidence = Math.Clamp(x.Confidence, 1, 5),

                                NudgeAt = x.NudgeAtUtc.HasValue
                                    ? x.NudgeAtUtc.Value + shift
                                    : null
                            };
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

            // --- Step 3.1: Tone Learning ---
            await ApplyToneLearningAsync(user, day, aiResult, toneForThisPlanStr);

            // --- Step 4: Return DTO ---
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
