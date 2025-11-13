#region Old Code
//using FlowOS.Api.Configurations;
//using FlowOS.Api.Data;
//using FlowOS.Api.DTOs.Plan;
//using FlowOS.Api.Models;
//using FlowOS.Api.Services.Planner.Models;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Options;
//using OpenAI;
//using System.Globalization;
//using System.Text.Json;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

//namespace FlowOS.Api.Services.Planner
//{
//    public partial class OpenAIPlannerService //: IPlannerService
//    {
//        private readonly FlowOSContext _context;
//        private readonly OpenAISettings _settings;
//        private readonly ILogger<OpenAIPlannerService> _logger;
//        private readonly OpenAIClient _openAIClient;

//        #region Constructor
//        public OpenAIPlannerService(
//            FlowOSContext context,
//            IOptions<OpenAISettings> settings,
//            OpenAIClient openAIClient,
//            ILogger<OpenAIPlannerService> logger)
//            {
//                _context = context;
//                _settings = settings.Value;
//                _openAIClient = openAIClient;
//                _logger = logger;
//            }
//        #endregion

//        #region Generate AI Plan

//        /// <summary>
//        /// Generates an AI-powered daily plan using the provided request data 
//        /// (user context, tasks, date, tone). 
//        /// This method orchestrates prompt creation, schema usage, OpenAI call, 
//        /// and parsing of the plan into a <see cref="DailyPlanAiResult"/>.
//        /// </summary>
//        /// <param name="request">
//        /// The full AI plan request containing user context, tasks, target date, and tone.
//        /// </param>
//        /// <returns>
//        /// A <see cref="DailyPlanAiResult"/> containing raw and clean JSON, timeline items, 
//        /// carryover tasks, tone, and focus.
//        /// </returns>

//        public async Task<DailyPlanAiResult> GenerateAiPlanAsync(AiPlanRequest request)
//        {
//            // ----- 0) Validate -----------------------------------------------------
//            if (request == null) throw new ArgumentNullException(nameof(request));
//            if (request.User == null) throw new ArgumentException("request.User is required.");
//            if (request.Tasks == null) request.Tasks = new List<TaskAiContext>();

//            var userId = request.UserId; // ← adjust if your model uses a different field
//            if (string.IsNullOrWhiteSpace(userId))
//                throw new ArgumentException("UserId is required in request.User.");

//            var date = request.Date.Date; // normalize
//            var tone = NormalizeTone(request.Tone);
//            var force = request.ForceRegenerate; // if your model differs, set to false or adapt

//            // ----- 1) Short-circuit: return existing if not force -----------------
//            var existing = await _context.DailyPlans
//                .AsNoTracking()
//                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date.Date == date);

//            if (existing != null && !force)
//            {
//                // Return the existing plan (build from DB + clean JSON for carryOver)
//                var existingItems = await _context.DailyPlanItems
//                    .Where(i => i.PlanId == existing.Id)
//                    .OrderBy(i => i.Start)
//                    .ToListAsync();

//                var carryOver = ExtractCarryOverIds(existing.PlanJsonClean);
//                var mapped = existingItems.Select(i => new AiPlanTimelineItem
//                {
//                    TaskId = i.TaskId,
//                    Label = i.Label,
//                    Start = DateTime.SpecifyKind(i.Start, DateTimeKind.Utc),
//                    End = DateTime.SpecifyKind(i.End, DateTimeKind.Utc),
//                    Confidence = i.Confidence,
//                    NudgeAt = i.NudgeAt
//                }).ToList();

//                return new DailyPlanAiResult
//                {
//                    Tone = existing.Tone ?? "balanced",
//                    Focus = existing.Focus ?? "",
//                    RawJson = existing.PlanJsonRaw ?? "{}",
//                    CleanJson = existing.PlanJsonClean ?? "{\"tone\":\"balanced\",\"focus\":\"\",\"items\":[],\"carryOverTaskIds\":[]}",
//                    Timeline = mapped,
//                    CarryOverTaskIds = carryOver
//                };
//            }

//            // ----- 2) Create/prepare plan row BEFORE AI call (A) ------------------
//            DailyPlan plan;
//            if (existing != null && force)
//            {
//                // wipe items (HD)
//                var oldItems = _context.DailyPlanItems.Where(i => i.PlanId == existing.Id);
//                _context.DailyPlanItems.RemoveRange(oldItems);
//                await _context.SaveChangesAsync();

//                // reuse plan row
//                plan = await _context.DailyPlans.FirstAsync(p => p.Id == existing.Id);
//                plan.Tone = tone;
//                plan.Focus = ""; // will set after AI parse
//                plan.GeneratedAt = DateTime.UtcNow;
//                plan.PlanJsonRaw = null;
//                plan.PlanJsonClean = null;
//            }
//            else
//            {
//                plan = new DailyPlan
//                {
//                    UserId = userId,
//                    Date = date,
//                    Tone = tone,
//                    Focus = "",
//                    GeneratedAt = DateTime.UtcNow,
//                    PlanJsonRaw = null,
//                    PlanJsonClean = null
//                };
//                _context.DailyPlans.Add(plan);
//            }

//            await _context.SaveChangesAsync(); // ensure plan.Id exists

//            // ----- 3) Build prompts + schema and call OpenAI ----------------------
//            var (systemPrompt, userPrompt, rulesPrompt) = BuildPrompt(request);
//            var schema = GetPlanResponseJsonSchema();

//            var model = "gpt-4.1-mini";
//            // 1st attempt
//            var raw1 = await CallOpenAiForPlanAsync(systemPrompt, userPrompt, rulesPrompt, schema);
//            var parsed = ParseAiPlanResponse(raw1);

//            // If parse clearly failed (no items), retry once (E3 step 2)
//            if (parsed.Timeline.Count == 0)
//            {
//                model = "gpt-3.5-turbo";
//                var raw2 = await CallOpenAiForPlanAsync(systemPrompt, userPrompt, rulesPrompt, schema);
//                var parsed2 = ParseAiPlanResponse(raw2);

//                if (parsed2.Timeline.Count > 0)
//                {
//                    parsed = parsed2;
//                    raw1 = raw2; // use the good raw
//                }
//            }

//            // ----- 4) If still empty → Smart+ Fallback (F3) -----------------------
//            if (parsed.Timeline.Count == 0)
//            {
//                // Try reuse yesterday
//                var reused = await TryReuseYesterdayPlanAsync(userId, date, request.User.WorkStart, request.User.WorkEnd);
//                if (reused != null && reused.Timeline.Count > 0)
//                {
//                    parsed = reused;
//                    parsed.Focus = string.IsNullOrWhiteSpace(parsed.Focus) ? "Stay consistent and keep momentum" : parsed.Focus;
//                    // Note: We keep raw/clean placeholders below
//                }
//                else
//                {
//                    // Balanced from today's tasks
//                    parsed = BuildBalancedFallbackPlan(request);
//                }

//                // mark that we’re not using true AI output — we still save clean JSON we build
//                // (If you add a bool IsAiGenerated column later, set it false here)
//                // plan.IsAiGenerated = false; // <- optional future flag

//                // Ensure Raw/Clean at least minimal
//                if (string.IsNullOrWhiteSpace(parsed.RawJson))
//                    parsed.RawJson = "{\"note\":\"fallback used\"}";
//                if (string.IsNullOrWhiteSpace(parsed.CleanJson))
//                    parsed.CleanJson = BuildCleanJson(parsed);
//            }
//            else
//            {
//                // parsed came from AI OK — ensure clean JSON present
//                if (string.IsNullOrWhiteSpace(parsed.CleanJson))
//                    parsed.CleanJson = BuildCleanJson(parsed);
//                if (string.IsNullOrWhiteSpace(parsed.RawJson))
//                    parsed.RawJson = raw1 ?? "{}";
//            }

//            // ----- 5) Save plan fields -------------------------------------------
//            plan.Tone = parsed.Tone ?? tone;
//            plan.Focus = parsed.Focus ?? "";
//            plan.PlanJsonRaw = parsed.RawJson;
//            plan.PlanJsonClean = parsed.CleanJson;
//            plan.GeneratedAt = DateTime.UtcNow;

//            await _context.SaveChangesAsync();

//            // ----- 6) Save items (HD replace) ------------------------------------
//            // Items already wiped above if force; ensure no leftovers
//            var leftovers = _context.DailyPlanItems.Where(i => i.PlanId == plan.Id);
//            _context.DailyPlanItems.RemoveRange(leftovers);
//            await _context.SaveChangesAsync();

//            foreach (var it in parsed.Timeline.OrderBy(x => x.Start))
//            {
//                var item = new DailyPlanItem
//                {
//                    PlanId = plan.Id,
//                    TaskId = it.TaskId, // may be null if AI created a free block like “Break”
//                    Label = it.Label ?? "",
//                    Start = DateTime.SpecifyKind(it.Start, DateTimeKind.Utc),
//                    End = DateTime.SpecifyKind(it.End, DateTimeKind.Utc),
//                    Confidence = Math.Max(1, Math.Min(5, it.Confidence)),
//                    NudgeAt = it.NudgeAt
//                };
//                _context.DailyPlanItems.Add(item);
//            }
//            await _context.SaveChangesAsync();

//            parsed.ModelUsed = parsed.Timeline.Count == 0 ? "none" : model;
//            parsed.Tone = parsed.Tone ?? request.Tone;  // ensure non-null

//            // ----- 7) Return final result ----------------------------------------
//            return parsed;
//        }

//        #region Fallback helpers (F3)

//        private async Task<DailyPlanAiResult?> TryReuseYesterdayPlanAsync(
//            string userId,
//            DateTime today,
//            TimeSpan workStart,
//            TimeSpan workEnd)
//        {
//            var yesterday = today.AddDays(-1).Date;

//            var yPlan = await _context.DailyPlans
//                .AsNoTracking()
//                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date.Date == yesterday);

//            if (yPlan == null) return null;

//            var yItems = await _context.DailyPlanItems
//                .AsNoTracking()
//                .Where(i => i.PlanId == yPlan.Id)
//                .OrderBy(i => i.Start)
//                .ToListAsync();

//            if (yItems.Count == 0) return null;

//            // Shift yesterday’s blocks to today's date, clamped to work window
//            var dayStart = today.Date + workStart;
//            var dayEnd = today.Date + workEnd;

//            var shifted = new List<AiPlanTimelineItem>();
//            foreach (var it in yItems)
//            {
//                var duration = it.End - it.Start;
//                if (duration <= TimeSpan.Zero) continue;

//                var slotStart = dayStart;
//                // try to preserve relative order and spacing
//                if (shifted.Count > 0)
//                    slotStart = shifted.Last().End.AddMinutes(10); // 10-min breathing space

//                var slotEnd = slotStart + duration;
//                if (slotEnd > dayEnd) break;

//                shifted.Add(new AiPlanTimelineItem
//                {
//                    TaskId = it.TaskId,
//                    Label = it.Label,
//                    Start = DateTime.SpecifyKind(slotStart, DateTimeKind.Utc),
//                    End = DateTime.SpecifyKind(slotEnd, DateTimeKind.Utc),
//                    Confidence = Math.Max(1, Math.Min(5, it.Confidence)),
//                    NudgeAt = null
//                });
//            }

//            if (shifted.Count == 0) return null;

//            return new DailyPlanAiResult
//            {
//                Tone = NormalizeTone(yPlan.Tone),
//                Focus = string.IsNullOrWhiteSpace(yPlan.Focus) ? "Continue momentum" : yPlan.Focus!,
//                RawJson = "{\"note\":\"reused yesterday plan\"}",
//                CleanJson = BuildCleanJson(new DailyPlanAiResult
//                {
//                    Tone = NormalizeTone(yPlan.Tone),
//                    Focus = string.IsNullOrWhiteSpace(yPlan.Focus) ? "Continue momentum" : yPlan.Focus!,
//                    Timeline = shifted,
//                    CarryOverTaskIds = new List<int>()
//                }),
//                Timeline = shifted,
//                CarryOverTaskIds = new List<int>()
//            };
//        }

//        private DailyPlanAiResult BuildBalancedFallbackPlan(AiPlanRequest request)
//        {
//            var start = request.Date.Date + request.User.WorkStart;
//            var end = request.Date.Date + request.User.WorkEnd;
//            var cursor = start;

//            // Sort: priority desc, energy “high” earlier, then due asc
//            var tasks = request.Tasks
//                .OrderByDescending(t => t.Priority)
//                .ThenByDescending(t => RankEnergy(t.EnergyLevel))
//                .ThenBy(t => t.DueDate)
//                .ToList();

//            var items = new List<AiPlanTimelineItem>();
//            var totalSinceBreak = TimeSpan.Zero;

//            foreach (var t in tasks)
//            {
//                var est = TimeSpan.FromMinutes((t.EstimatedMinutes ?? 30));
//                if (cursor + est > end) break;

//                // Insert a short break every ~90 minutes
//                if (totalSinceBreak >= TimeSpan.FromMinutes(90))
//                {
//                    var bStart = cursor;
//                    var bEnd = bStart.AddMinutes(10);
//                    if (bEnd <= end)
//                    {
//                        items.Add(new AiPlanTimelineItem
//                        {
//                            TaskId = null,
//                            Label = "Short break",
//                            Start = DateTime.SpecifyKind(bStart, DateTimeKind.Utc),
//                            End = DateTime.SpecifyKind(bEnd, DateTimeKind.Utc),
//                            Confidence = 4
//                        });
//                        cursor = bEnd;
//                        totalSinceBreak = TimeSpan.Zero;
//                    }
//                }

//                var s = cursor;
//                var e = s + est;
//                if (e > end) break;

//                items.Add(new AiPlanTimelineItem
//                {
//                    TaskId = t.Id,
//                    Label = t.Title,
//                    Start = DateTime.SpecifyKind(s, DateTimeKind.Utc),
//                    End = DateTime.SpecifyKind(e, DateTimeKind.Utc),
//                    Confidence = 3
//                });

//                cursor = e.Add(TimeSpan.FromMinutes(5)); // small buffer
//                totalSinceBreak += est + TimeSpan.FromMinutes(5);
//            }

//            if (items.Count == 0)
//            {
//                // graceful “restorative” plan
//                var pStart = request.Date.Date.AddHours(10);
//                var pEnd = pStart.AddHours(1);
//                items.Add(new AiPlanTimelineItem
//                {
//                    Label = "Reflect, plan, and reset",
//                    Start = DateTime.SpecifyKind(pStart, DateTimeKind.Utc),
//                    End = DateTime.SpecifyKind(pEnd, DateTimeKind.Utc),
//                    Confidence = 5
//                });
//            }

//            var result = new DailyPlanAiResult
//            {
//                Tone = NormalizeTone(request.Tone),
//                Focus = items.Any(i => i.TaskId != null) ? "Make progress on key tasks" : "Recharge & Reset",
//                Timeline = items,
//                CarryOverTaskIds = new List<int>()
//            };
//            result.CleanJson = BuildCleanJson(result);
//            result.RawJson = "{\"note\":\"balanced fallback\"}";
//            return result;
//        }

//        private static int RankEnergy(string? e)
//        {
//            if (string.IsNullOrWhiteSpace(e)) return 0;
//            e = e.Trim().ToLowerInvariant();
//            return e switch
//            {
//                "high" => 3,
//                "medium" or "mid" => 2,
//                "low" => 1,
//                _ => 0
//            };
//        }

//        private static List<int> ExtractCarryOverIds(string? cleanJson)
//        {
//            if (string.IsNullOrWhiteSpace(cleanJson)) return new();
//            try
//            {
//                using var doc = JsonDocument.Parse(cleanJson);
//                if (doc.RootElement.TryGetProperty("carryOverTaskIds", out var arr) && arr.ValueKind == JsonValueKind.Array)
//                {
//                    var list = new List<int>();
//                    foreach (var el in arr.EnumerateArray())
//                    {
//                        if (el.TryGetInt32(out var id)) list.Add(id);
//                    }
//                    return list;
//                }
//            }
//            catch { /* ignore */ }
//            return new();
//        }

//        private static string BuildCleanJson(DailyPlanAiResult r)
//        {
//            var obj = new
//            {
//                tone = NormalizeTone(r.Tone),
//                focus = r.Focus ?? "",
//                items = r.Timeline.Select(i => new
//                {
//                    taskId = i.TaskId,
//                    label = i.Label,
//                    start = i.Start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
//                    end = i.End.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
//                    confidence = Math.Max(1, Math.Min(5, i.Confidence)),
//                    nudgeAt = i.NudgeAt?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
//                }),
//                carryOverTaskIds = r.CarryOverTaskIds ?? new List<int>()
//            };

//            return JsonSerializer.Serialize(obj, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });
//        }
//        #endregion

//        #endregion
//    }
//}
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using FlowOS.Api.Configurations;
using FlowOS.Api.Services.Planner.Models;

namespace FlowOS.Api.Services.Planner
{
    /// <summary>
    /// AI planning engine (OpenAI-backed) – orchestrates prompt build, model selection (MS2-Balanced / U2),
    /// calls OpenAI, parses JSON (RP-E), and returns a <see cref="DailyPlanAiResult"/>.
    /// This class is partial; other parts provide Prompt/Schema builders and the low-level OpenAI call & parser.
    /// </summary>
    public partial class OpenAIPlannerService
    {
        private readonly OpenAIClient _openAIClient;
        private readonly OpenAISettings _settings;
        private readonly System.Net.Http.HttpClient _httpClient; // reserved for future use (tools, external signals)
        private readonly ILogger<OpenAIPlannerService> _logger;

        #region ctor
        public OpenAIPlannerService(
            OpenAIClient openAIClient,
            IOptions<OpenAISettings> settings,
            System.Net.Http.HttpClient httpClient,
            ILogger<OpenAIPlannerService> logger)
        {
            _openAIClient = openAIClient;
            _settings = settings.Value;
            _logger = logger;
            _httpClient = httpClient;
        }
        #endregion

        /// <summary>
        /// Generates a plan using OpenAI (no DB writes here). PlannerService orchestrates persistence.
        /// Implements MS2-Balanced: start with base model (usually gpt-4.1-mini), upgrade once to premium
        /// (usually gpt-4.1) if the day looks complex or the tone suggests it’s worthwhile.
        /// </summary>
        public async Task<DailyPlanAiResult> GenerateAiPlanAsync(AiPlanRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (request.User is null) throw new ArgumentException("AiPlanRequest.User is required.", nameof(request));
            if (request.Tasks is null) request.Tasks = new List<TaskAiContext>();

            // --- Build prompts & schema (RP-E) ---
            var (systemPrompt, userPrompt, rulesPrompt) = BuildPrompt(request);
            var planSchema = GetPlanResponseJsonSchema();
            #if DEBUG
                JsonDocument.Parse(planSchema);
            #endif

            // --- Model choice (MS2-Balanced / U2) ---
            var (primaryModel, premiumModel, shouldTryUpgrade) = SelectModelsU2(request);

            // First attempt with the base model
            var usedModel = primaryModel;
            string raw = await CallOpenAiForPlanAsync(usedModel, systemPrompt, userPrompt, rulesPrompt, planSchema);
            var parsed = ParseAiPlanResponse(raw);

            // Heuristic to judge “underfilled/weak” output
            bool underfilled =
                parsed.Timeline.Count == 0 ||
                (request.Tasks.Count > 0 && parsed.Timeline.Count < Math.Min(3, request.Tasks.Count / 2));

            // If underfilled and U2 says the day is complex → try one premium upgrade
            if (underfilled && shouldTryUpgrade && !string.IsNullOrWhiteSpace(premiumModel))
            {
                _logger.LogInformation("AI_PLAN_UPGRADE_DECISION: upgrading model from {Base} to {Pro} (tasks={Tasks}, tone={Tone})",
                    primaryModel, premiumModel, request.Tasks.Count, request.Tone);

                #pragma warning disable CS0219
                    // (kept for clarity if you later log token usage per attempt)
                    int attempt = 1;
                #pragma warning restore CS0219

                usedModel = premiumModel;
                var raw2 = await CallOpenAiForPlanAsync(usedModel, systemPrompt, userPrompt, rulesPrompt, planSchema);
                var parsed2 = ParseAiPlanResponse(raw2);

                // Prefer result that actually produced a non-empty / larger timeline
                if (parsed2.Timeline.Count >= parsed.Timeline.Count)
                {
                    parsed = parsed2;
                    raw = raw2;
                }
                else
                {
                    // premium didn’t improve → keep first
                    usedModel = primaryModel;
                }
            }

            // Ensure we always return clean+raw JSON and a normalized tone
            if (string.IsNullOrWhiteSpace(parsed.CleanJson))
                parsed.CleanJson = BuildCleanJson(parsed);
            if (string.IsNullOrWhiteSpace(parsed.RawJson))
                parsed.RawJson = raw ?? "{}";
            parsed.Tone = NormalizeTone(string.IsNullOrWhiteSpace(parsed.Tone) ? request.Tone : parsed.Tone);
            parsed.ModelUsed = parsed.Timeline.Count == 0 ? "none" : usedModel;

            _logger?.LogInformation(
                "AI_PLAN_RESULT: user={User} date={Date} tone={Tone} model={Model} items={Items}",
                request.User?.FirstName ?? "(user)", request.Date.ToString("yyyy-MM-dd"),
                parsed.Tone, parsed.ModelUsed, parsed.Timeline.Count);

            return parsed;
        }

        #region MS2 (U2) — Balanced upgrade heuristic
        /// <summary>
        /// Decide base & premium models and whether we should attempt a single upgrade
        /// for a “wow but cost-aware” experience.
        /// </summary>
        private (string primaryModel, string premiumModel, bool shouldTryUpgrade) SelectModelsU2(AiPlanRequest req)
        {
            var baseModel = string.IsNullOrWhiteSpace(_settings.Model) ? "gpt-4.1-mini" : _settings.Model!.Trim();
            var proModel = (typeof(OpenAISettings).GetProperty("ModelPremium")?.GetValue(_settings) as string)
                            ?? "gpt-4.1";

            // Heuristics (U2):
            // - 7+ tasks in scope → likely complex → try upgrade
            // - or tone is "strict" (user expects firm, precise guidance) → consider upgrade when needed
            // - or mixed energy (both high & low) with at least 5 tasks → upgrade when needed
            var taskCount = req?.Tasks?.Count ?? 0;
            var tone = NormalizeTone(req?.Tone);
            var hasHigh = req?.Tasks?.Any(t => string.Equals(t.EnergyLevel, "high", StringComparison.OrdinalIgnoreCase)) == true;
            var hasLow = req?.Tasks?.Any(t => string.Equals(t.EnergyLevel, "low", StringComparison.OrdinalIgnoreCase)) == true;

            bool complexityTrigger =
                taskCount >= 7 ||
                (tone == "strict" && taskCount >= 5) ||
                (hasHigh && hasLow && taskCount >= 5);

            return (baseModel, proModel, complexityTrigger);
        }
        #endregion

        private static string BuildCleanJson(DailyPlanAiResult r)
        {
            var obj = new
            {
                tone = NormalizeTone(r.Tone),
                focus = r.Focus ?? "",
                items = r.Timeline.Select(i => new
                {
                    taskId = i.TaskId,
                    label = i.Label,
                    start = i.Start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                    end = i.End.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                    confidence = Math.Max(1, Math.Min(5, i.Confidence)),
                    nudgeAt = i.NudgeAt?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
                }),
                carryOverTaskIds = r.CarryOverTaskIds ?? new List<int>()
            };

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}
