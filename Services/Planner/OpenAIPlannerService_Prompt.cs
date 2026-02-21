
// ---------------------------------------------
// FlowOS.Api.Services.Planner - Prompt (Partial)
// File: OpenAIPlannerService_Prompt.cs
// Purpose: Holds prompt-building and JSON schema methods
// Notes:
//  - This file is a partial of OpenAIPlannerService
//  - Prompt uses conversational, FlowOS-styled guidance (no chain-of-thought)
//  - Sections (PS3) with descriptive variable names (N2) and emoji headers (E)
//  - Break strategy: micro + one longer break
//  - Tone: auto by default, override respected if provided (TO3)
//  - Output enforced by JSON Schema Mode (returned by BuildSchema())
// ---------------------------------------------

#region Old Code
//using FlowOS.Api.Services.Planner.Models;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Text.Json.Nodes;

//namespace FlowOS.Api.Services.Planner
//{
//    public partial class OpenAIPlannerService
//    {      
//        #region Build Prompt

//        /// <summary>
//        /// Builds the system, user, and rules prompt messages required for generating 
//        /// a personalized daily plan using AI. The prompt includes user context, 
//        /// tone, date, tasks, and work-hour preferences.
//        /// </summary>
//        /// <param name="request">
//        /// The AI plan request containing user details, tone, date, and task data.
//        /// </param>
//        /// <returns>
//        /// A tuple containing:
//        /// systemPrompt - high-level AI behavior and role instructions  
//        /// userPrompt - user-specific data and personal context  
//        /// rulesPrompt - strict formatting & rule enforcement for the model  
//        /// </returns>

//        /// <remarks>
//        /// P3 multi-message format:
//        ///  - systemPrompt: role, goals, style (adaptive by tone)
//        ///  - userPrompt: concrete data (date, work window, tasks)
//        ///  - rulesPrompt: hard constraints (JSON-only, schema compliance)
//        /// </remarks>
//        private (string systemPrompt, string userPrompt, string rulesPrompt) BuildPrompt(AiPlanRequest request)
//        {
//            // --- Normalize tone ----------------------------------------------------
//            var tone = NormalizeTone(request.Tone);
//            var (toneVoice, toneDo, toneDont) = tone switch
//            {
//                "soft" => (
//                    "Warm, encouraging, kind mentor.",
//                    "use short, supportive sentences and encourage balance",
//                    "be harsh, judgmental, or demanding"
//                ),
//                "strict" => (
//                    "Disciplined productivity coach.",
//                    "be direct, prioritize impact, and push for momentum",
//                    "use emojis, filler words, or long inspirational text"
//                ),
//                "playful" => (
//                    "Fun, upbeat, slightly humorous companion.",
//                    "keep it light, add small sparks of motivation",
//                    "overuse emojis or be cheesy"
//                ),
//                _ => (
//                    "Balanced, friendly strategist.",
//                    "blend focus with wellbeing and momentum",
//                    "be negative or overly rigid"
//                )
//            };

//            // --- Extract user context ---------------------------------------------
//            var firstName = string.IsNullOrWhiteSpace(request.User.FirstName)
//                ? "there"
//                : request.User.FirstName.Trim();

//            var ws = request.User.WorkStart; // TimeSpan
//            var we = request.User.WorkEnd;   // TimeSpan

//            // Render work window as local-friendly HH:mm
//            string FormatTS(TimeSpan t) => new DateTime(1, 1, 1, t.Hours, t.Minutes, 0)
//                .ToString("HH:mm", CultureInfo.InvariantCulture);

//            var workStartStr = FormatTS(ws);
//            var workEndStr = FormatTS(we);

//            // --- Build task list (compact, sorted by priority desc then due asc) ---
//            var tasks = request.Tasks
//                .OrderByDescending(t => t.Priority)
//                .ThenBy(t => t.DueDate)
//                .ToList();

//            var sb = new StringBuilder();
//            foreach (var t in tasks)
//            {
//                // Trim description for prompt hygiene (<= 140 chars)
//                var desc = (t.Description ?? "").Trim();
//                if (desc.Length > 140) desc = desc[..140] + "…";

//                sb.AppendLine(
//                    $"- id:{t.Id}; title:{Sanitize(t.Title)}; priority:{t.Priority}; estMin:{t.EstimatedMinutes}; due:{t.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}; energy:{(t.EnergyLevel ?? "unknown")}; desc:{Sanitize(desc)}"
//                );
//            }

//            var taskBlock = sb.Length == 0
//                ? "(no pending tasks provided)"
//                : sb.ToString();

//            // --- SYSTEM PROMPT -----------------------------------------------------
//            var systemPrompt = $@"
//You are FlowOS — an AI daily planning engine and a compassionate productivity partner.
//Speak primarily in a {toneVoice} tone. Remain crisp, helpful, and actionable.

//Your mission:
//1) Create a realistic schedule for the day that respects the user's work window.
//2) Maximize impact early (high-priority + high-energy alignment).
//3) Include short breaks to protect energy; insert them naturally.
//4) Keep the plan feasible; avoid overlapping blocks or unrealistic density.
//5) If tasks are few or none, schedule meaningful recovery, planning, or restoration time.

//Tone instructions (requested tone = {tone}):
//- You must follow this tone as the primary guide.
//- You may adjust tone slightly **only if it meaningfully improves the user's emotional experience for today** (±1 tone shift maximum).
//- If you adjust tone, the output JSON 'tone' field must reflect the final tone you used.

//Style guide:
//- Do: {toneDo}
//- Don't: {toneDont}
//- Keep language concise; UI will show details.
//";

//            // --- USER PROMPT -------------------------------------------------------
//            // (Concrete facts the model should plan around)
//            var userPrompt = $@"
//Plan request for {firstName}
//Date: {request.Date:yyyy-MM-dd}
//Work window: {workStartStr}–{workEndStr} (local time)
//Tone preference: {tone}

//Tasks (sorted by priority desc, due asc):
//{taskBlock}

//Planning requirements:
//- Allocate time blocks within the work window. No overlaps.
//- Use estimated minutes per task as a baseline; you may slightly adjust.
//- Align high-energy tasks earlier if possible.
//- Insert short recovery breaks based on plan length and task density.
//- If some tasks do not fit today, add them to carryOverTaskIds.

//Important behaviors:
//- If there are zero tasks, propose a restorative, mindful plan (e.g., deep thinking, learning, reflection).
//- If tasks are many, cluster similar ones and timebox them.
//- Add a brief focus line (single sentence) summarizing the day’s intent.
//";

//            // --- RULES PROMPT (strict JSON + schema alignment) ---------------------
//            var rulesPrompt = @"
//OUTPUT RULES (CRITICAL):
//1) Output JSON ONLY. Do not include explanations or prose.
//2) The JSON MUST strictly match the provided JSON Schema.
//3) All timestamps MUST be ISO-8601 with 'Z' (UTC). Example: 2025-10-26T09:00:00Z
//4) Ensure blocks do NOT overlap and fall within the work window.
//5) For each scheduled item, include: label, start, end, confidence (1-5).
//6) Where helpful, include nudgeAt (usually 5–10 minutes before start).
//7) carryOverTaskIds must reference task ids that were not scheduled today.";

//            return (systemPrompt.Trim(), userPrompt.Trim(), rulesPrompt.Trim());
//        }

//        // ----------------- helpers -----------------

//        private static string NormalizeTone(string? tone)
//        {
//            if (string.IsNullOrWhiteSpace(tone)) return "balanced";
//            tone = tone.Trim().ToLowerInvariant();
//            return tone is "soft" or "strict" or "playful" or "balanced" ? tone : "balanced";
//        }

//        private static string Sanitize(string s)
//        {
//            // Remove newlines and collapse excessive spaces to keep the prompt compact.
//            var x = s.Replace("\r", " ").Replace("\n", " ").Trim();
//            while (x.Contains("  ")) x = x.Replace("  ", " ");
//            return x;
//        }

//        #endregion

//        #region JSON Schema

//        /// <summary>
//        /// Returns the JSON Schema that the AI must follow while generating the daily plan. 
//        /// This schema ensures the model outputs valid structured JSON matching the 
//        /// <see cref="DailyPlanAiResult"/> format (timeline, carryover tasks, tone, focus, etc.).
//        /// </summary>
//        /// <returns>
//        /// A string containing the JSON Schema definition for the AI response.
//        /// </returns>
//        private string GetPlanResponseJsonSchema()
//        {
//            // JSON Schema for validating the AI-generated daily plan
//            return @"
//            {
//              ""type"": ""object"",
//              ""properties"": {
//                ""tone"": {
//                  ""type"": ""string"",
//                  ""description"": ""The tone applied when generating the plan (soft, strict, playful, balanced)""
//                },
//                ""focus"": {
//                  ""type"": ""string"",
//                  ""description"": ""A one-sentence summary of the day's key intention or theme""
//                },
//                ""items"": {
//                  ""type"": ""array"",
//                  ""description"": ""Timeline items scheduled for the day"",
//                  ""items"": {
//                    ""type"": ""object"",
//                    ""properties"": {
//                      ""label"": {
//                        ""type"": ""string"",
//                        ""description"": ""Short title of the scheduled block""
//                      },
//                      ""start"": {
//                        ""type"": ""string"",
//                        ""format"": ""date-time"",
//                        ""description"": ""Start time in ISO-8601 format with Z timezone""
//                      },
//                      ""end"": {
//                        ""type"": ""string"",
//                        ""format"": ""date-time"",
//                        ""description"": ""End time in ISO-8601 format with Z timezone""
//                      },
//                      ""confidence"": {
//                        ""type"": ""integer"",
//                        ""minimum"": 1,
//                        ""maximum"": 5,
//                        ""description"": ""AI confidence score for this scheduling choice (1–5)""
//                      },
//                      ""nudgeAt"": {
//                        ""type"": [""string"", ""null""],
//                        ""format"": ""date-time"",
//                        ""description"": ""When a notification/nudge should be sent (ISO-8601, optional)""
//                      }
//                    },
//                    ""required"": [""label"", ""start"", ""end"", ""confidence""]
//                  }
//                },
//                ""carryOverTaskIds"": {
//                  ""type"": ""array"",
//                  ""description"": ""Tasks not scheduled today that should be moved to the next planning cycle"",
//                  ""items"": {
//                    ""type"": ""integer""
//                  }
//                }
//              },
//              ""required"": [""tone"", ""focus"", ""items"", ""carryOverTaskIds""]
//            }
//            ".Trim();
//        }

//        #endregion
//    }
//}
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes; // (not required by this file but harmless if present)
using FlowOS.Api.Services.Planner.Models;
using Microsoft.Extensions.Logging;

namespace FlowOS.Api.Services.Planner
{
    public partial class OpenAIPlannerService
    {
        #region Prompt Builder (FlowOS AI Standard v2: RP-E + TL-C + C3 + E4)

        /// <summary>
        /// Builds the system, user, and rules prompt messages for the AI daily plan.
        /// Implements FlowOS AI Standard v2:
        ///  - RP-E: Extended rules w/ explicit constraints + example
        ///  - TL-C: Tone is guided by planner hint; AI may adjust ±1 level
        ///  - C3: Smart Carry semantics (prioritize high-priority/urgent, cap carry overs, avoid guilt clutter)
        ///  - E4: Adaptive emotional intelligence (model may soften/harden tone slightly based on context)
        ///  - L2: Action-verb labels (2–5 words), concise, human-friendly
        ///  - Strict JSON-only output with provided JSON Schema (name: flowos_daily_plan)
        /// </summary>
        /// <param name="request">AI planning request (user context, tasks, date, tone hint).</param>
        /// <returns>(systemPrompt, userPrompt, rulesPrompt)</returns>
        private (string systemPrompt, string userPrompt, string rulesPrompt) BuildPrompt(AiPlanRequest request)
        {
            // ---- Normalize tone hint from PlannerService (TL-C: planner picks hint, AI may ±1) ----
            var toneHint = NormalizeTone(request.Tone);

            // Derive tone voice & micro-guidelines for the *hinted* tone
            var (toneVoice, toneDo, toneDont) = GetToneGuidelines(toneHint);

            // ---- Compose SYSTEM PROMPT (role, mission, global rules) ----
            var systemPrompt = $@"
You are **FlowOS** — an AI daily planning engine and a compassionate productivity partner.
Your job: produce a **single-day plan** that people love to follow.

**Core mission**
1. Create a realistic, motivating schedule for the requested day.
2. Respect the user's **work window** strictly — do not schedule outside it unless explicitly told to.
3. Front-load **high-priority** items when energy is highest; place deep work in strong-focus windows.
4. Insert **short, purposeful breaks** to sustain energy.
5. Avoid **over-scheduling** and **overlapping** blocks. Plans must feel achievable and supportive.
6. Use **one clear daily focus** (a single sentence).

**Tone (hint = `{toneHint}`) — TL-C Hybrid**
- Primary tone = `{toneHint}`. You may adjust tone **by at most one level** (±1) if it clearly improves today’s experience given workload & likely stress.
- If you adjust tone, set `" + "tone" + @"` in JSON to the **final** tone you used.
- Tone levels (ordered): `strict` ↔ `balanced` ↔ `soft`  (and `playful` is a lateral style; use only if appropriate).
- Tone must be consistent within the plan: one value for the whole day.

**Emotional Intelligence (E4 – Adaptive)**
- Infer the user's likely state from the task set (e.g., many high-priority items → potential stress; few tasks → potential idle energy).
- Incorporate **one or two short supportive phrases** in labels (e.g., “Take a mindful pause”, “Nice progress!”) that match the chosen tone.
- Avoid therapy talk or deep personal assumptions. Keep it professional, empathetic, and succinct.

**Label Style (L2 – Action Verb)**
- Each item.label must be a short *actionable* phrase (2–5 words), starting with a **verb**:
  - Good: ""Write summary"", ""Review PRs"", ""Plan tomorrow""
  - Avoid: long paragraphs, emojis, or trailing punctuation (no ""..."", no markdown)

**Smart Carry (C3)**
- If some tasks remain unscheduled for the day, add their IDs to `carryOverTaskIds` using smart logic:
  - **Carry**: items with priority ≥ 3, or tasks due tomorrow or overdue.
  - **Do NOT carry**: low-impact / low-priority tasks that create clutter or guilt.
  - Prefer carrying **≤ 3** items total (unless tone = `strict`, where up to 5 is acceptable).
  - If today was overloaded, carry only the **most meaningful** unscheduled items.

**Breaks (autonomous & reasonable)**
- Insert 5–10 minute breaks after ~60–90 minutes of focused work.
- For very long tasks, split into 60–90 minute work blocks with short breaks.

**Output discipline (STRICT)**
- **Return JSON ONLY** (no prose, no markdown).
- **Must validate** against the provided JSON Schema `flowos_daily_plan`.
- All timestamps must be **ISO-8601** with a trailing **Z** (UTC), e.g. `" + "2025-10-26T09:00:00Z" + @"`.
- Do not include any extra fields beyond the schema.
- If uncertain about details (e.g., missing estimates), choose safe, realistic defaults rather than inventing facts.
";

            // ---- Compose USER PROMPT (concrete facts) ----
            // We present the request data in a tightly controlled, compact list for the model.
            // Note: WorkStart/WorkEnd are TimeSpan (local). For simplicity and schema safety (DT1),
            // we treat them as the effective UTC window for this day's plan. The model must output
            // ISO-8601 timestamps with 'Z' within this window.
            string FormatHHmm(TimeSpan t) =>
                new DateTime(1, 1, 1, t.Hours, t.Minutes, 0).ToString("HH:mm", CultureInfo.InvariantCulture);

            var workStartStr = $"{FormatHHmm(request.User.WorkStart)}Z";
            var workEndStr = $"{FormatHHmm(request.User.WorkEnd)}Z";

            var taskLines = BuildTaskLines(request.Tasks);

            var userPrompt = $@"
User: {Sanitize(request.User.FirstName)}
Date (UTC day): {request.Date:yyyy-MM-dd}
Work window (UTC): {workStartStr}–{workEndStr}
Tone hint from planner (TL-C): {toneHint}

Tasks (sorted by priority desc, then due asc):
{taskLines}

**Plan requirements**
- Schedule only within the Work window above (no items before {workStartStr} or after {workEndStr}).
- Use Task.estMinutes as baseline; you may adjust to keep plan realistic.
- Place high-priority + high-energy tasks earlier when possible.
- Insert short recovery breaks (5–10 min) after 60–90 min focused blocks.
- If 0–1 tasks exist, create a **Productive Light Day**: 1–2 small but meaningful wins + restorative blocks (walk, planning, reflection). 
- For any tasks not scheduled today, choose smart `carryOverTaskIds` (C3 rules).
";

            // ---- Compose RULES PROMPT (hard constraints + example) ----
            // NOTE: This is *assistant* content in P3; it enforces JSON-only, schema-conformant output.

            var rulesPrompt = @"
# OUTPUT RULES (STRICT — CRITICAL)
- Output JSON ONLY. No extra text, no markdown, no commentary outside JSON.
- The JSON MUST validate against the provided JSON Schema: `flowos_daily_plan`.

# REQUIRED FIELDS
- Every `items[i]` MUST contain: `taskId`, `label`, `start`, `end`, `confidence` (1–5).
- `nudgeAt` is optional and may be null.

# TASK ID RULES (VERY IMPORTANT)
- If an item represents a real task from TASK LIST, you MUST set `taskId` to that exact numeric id.
- If an item is NOT a real task (break / walk / planning / reflection / buffer), you MUST set `taskId` = null.
- NEVER invent task ids.
- NEVER omit `taskId`.

# LABEL RULES (VERY IMPORTANT)
- If `taskId` != null:
  - `label` MUST be EXACTLY the task TITLE from TASK LIST (the text inside quotes).
  - `label` MUST NOT include the id number.
  - `label` MUST NOT include priority/est/due/brackets or any metadata.
  - Example good: ""Pay school fees""
  - Example bad: ""- 12: Pay school fees [p3, est 30m]""
- If `taskId` == null:
  - `label` should be 2–5 words and start with a verb (e.g., ""Stretch break"", ""Take a walk"").

# TIME RULES
- `start`, `end`, and (optional) `nudgeAt` MUST be ISO 8601 with trailing `Z` (UTC), e.g. `2025-10-26T09:00:00Z`.
- Do NOT schedule outside the provided work window.
- Do NOT overlap time blocks.

# QUALITY RULES
- Keep plan achievable (avoid cramming; allow breathing room).
- Tone must be a single value from: `soft`, `strict`, `playful`, `balanced`.
- You may adjust at most ±1 tone level from the requested hint if it truly benefits the user today.
- If you adjust, set `tone` to the FINAL tone used.
- `carryOverTaskIds` must contain ONLY task ids that are not scheduled in `items`.
- Prefer ≤ 3 carries, unless tone=`strict` (then up to 5).

# EXAMPLE OUTPUT (illustrative; your actual items depend on input)
{
  ""tone"": ""balanced"",
  ""focus"": ""Make clear progress on key work and keep energy steady"",
  ""items"": [
    {
      ""taskId"": 101,
      ""label"": ""Draft quarterly report"",
      ""start"": ""2025-10-26T09:00:00Z"",
      ""end"": ""2025-10-26T10:15:00Z"",
      ""confidence"": 4,
      ""nudgeAt"": ""2025-10-26T08:55:00Z""
    },
    {
      ""taskId"": null,
      ""label"": ""Stretch break"",
      ""start"": ""2025-10-26T10:15:00Z"",
      ""end"": ""2025-10-26T10:25:00Z"",
      ""confidence"": 5,
      ""nudgeAt"": null
    },
    {
      ""taskId"": 203,
      ""label"": ""Review client email"",
      ""start"": ""2025-10-26T10:25:00Z"",
      ""end"": ""2025-10-26T10:55:00Z"",
      ""confidence"": 4,
      ""nudgeAt"": ""2025-10-26T10:20:00Z""
    }
  ],
  ""carryOverTaskIds"": []
}
";

            //            var rulesPrompt = @"
            //# OUTPUT RULES (STRICT — CRITICAL)
            //- Output JSON ONLY. No extra text, no markdown, no commentary outside JSON.
            //- The JSON MUST validate against the provided JSON Schema: `flowos_daily_plan`.
            //- Every `items[i]` must have: `label` (2–5 words, starts with a verb), `start`, `end`, `confidence` (1–5).
            //- `start`, `end`, and (optional) `nudgeAt` MUST be ISO 8601 with trailing `Z` (UTC), e.g. `2025-10-26T09:00:00Z`.
            //- Do NOT schedule outside the provided work window.
            //- Do NOT overlap time blocks.
            //- Keep plan achievable (avoid cramming; allow breathing room).
            //- Tone must be a single value from: `soft`, `strict`, `playful`, `balanced`. You may adjust at most ±1 tone level from the requested hint if it truly benefits the user today. If you adjust, set `tone` to the FINAL tone used.
            //- `carryOverTaskIds` must contain ONLY task ids that are not scheduled in `items`. Prefer ≤ 3 carries, unless tone=`strict` (then up to 5).

            //# EXAMPLE OUTPUT (illustrative; your actual items depend on input)
            //{
            //  ""tone"": ""balanced"",
            //  ""focus"": ""Make clear progress on key work and keep energy steady"",
            //  ""items"": [
            //    {{
            //      ""label"": ""Draft report intro"",
            //      ""start"": ""2025-10-26T09:00:00Z"",
            //      ""end"": ""2025-10-26T10:15:00Z"",
            //      ""confidence"": 4,
            //      ""nudgeAt"": ""2025-10-26T08:55:00Z""
            //    }},
            //    {{
            //      ""label"": ""Stretch break"",
            //      ""start"": ""2025-10-26T10:15:00Z"",
            //      ""end"": ""2025-10-26T10:25:00Z"",
            //      ""confidence"": 5
            //    }},
            //    {{
            //      ""label"": ""Review email triage"",
            //      ""start"": ""2025-10-26T10:25:00Z"",
            //      ""end"": ""2025-10-26T10:55:00Z"",
            //      ""confidence"": 4
            //    }},
            //    {{
            //      ""label"": ""Refine slides"",
            //      ""start"": ""2025-10-26T11:00:00Z"",
            //      ""end"": ""2025-10-26T12:00:00Z"",
            //      ""confidence"": 3,
            //      ""nudgeAt"": ""2025-10-26T10:55:00Z""
            //    }}
            //  ],
            //  ""carryOverTaskIds"": [101, 203]
            //}
            //";          

            return (systemPrompt.Trim(), userPrompt.Trim(), rulesPrompt.Trim());
        }

        #endregion

        #region Helpers (tone + text)

        /// <summary>
        /// Normalize any incoming tone text to one of: ""soft"", ""strict"", ""playful"", ""balanced"".
        /// Fallback to ""balanced"".
        /// </summary>
        private static string NormalizeTone(string? tone)
        {
            if (string.IsNullOrWhiteSpace(tone)) return "balanced";
            var t = tone.Trim().ToLowerInvariant();
            return t switch
            {
                "soft" => "soft",
                "strict" => "strict",
                "playful" => "playful",
                "balanced" => "balanced",
                _ => "balanced"
            };
        }

        /// <summary>
        /// Returns the voice descriptor and do/don't guidance for a given tone keyword.
        /// </summary>
        private static (string Voice, string Do, string Dont) GetToneGuidelines(string tone)
        {
            var t = (tone ?? ""); // already normalized by caller
            return t switch
            {
                "soft" => (
                    "kind, calm, encouraging mentor",
                    "use warm supportive language; validate feelings; keep suggestions gentle and realistic",
                    "avoid strict commands or guilt-inducing phrasing"
                ),
                "strict" => (
                    "clear, disciplined, no-nonsense coach",
                    "be direct, time-box decisively, emphasize accountability and follow-through",
                    "avoid rambling, overly soft or vague language"
                ),
                "playful" => (
                    "light-hearted, upbeat, friendly motivator",
                    "use a touch of positivity; keep it fun yet focused",
                    "avoid sarcasm or flippancy; do not trivialize important tasks"
                ),
                _ => (
                    "balanced, calm, professional guide",
                    "blend clarity with empathy; maintain steady motivation and feasibility",
                    "avoid extremes (too harsh or too soft) and avoid excessive emojis/markup"
                )
            };
        }

        /// <summary>
        /// Render tasks as compact single-line bullet items for the user prompt.
        /// Ensures stable formatting and short fields (L2).
        /// </summary>

        private static string BuildTaskLines(IEnumerable<TaskAiContext> tasks)
        {
            if (tasks == null) return "TASK LIST: (empty)";

            var ordered = tasks
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.DueDate)
                .ToList();

            if (ordered.Count == 0) return "TASK LIST: (empty)";

            var sb = new StringBuilder();
            sb.AppendLine("TASK LIST (use IDs exactly; do NOT invent IDs):");

            foreach (var t in ordered)
            {
                // Keep it simple so model won't copy metadata into label
                // ID and Title are the ONLY things the model should "copy exactly"
                var title = Sanitize(t.Title ?? "");
                if (string.IsNullOrWhiteSpace(title)) title = "(untitled task)";

                // Optional: add small non-copy hint in brackets (NOT part of title)
                var due = t.DueDate.ToString("yyyy-MM-dd");
                var est = t.EstimatedMinutes;
                var pr = t.Priority;

                sb.AppendLine($"- {t.Id}: \"{title}\"  [p{pr}, est {est}m, due {due}]");
            }

            return sb.ToString().TrimEnd();
        }

        //private static string BuildTaskLines(IEnumerable<TaskAiContext> tasks)
        //{
        //    if (tasks == null) return "(no pending tasks provided)";

        //    var ordered = tasks
        //        .OrderByDescending(t => t.Priority)
        //        .ThenBy(t => t.DueDate)
        //        .ToList();

        //    if (ordered.Count == 0) return "(no pending tasks provided)";

        //    var sb = new StringBuilder();
        //    foreach (var t in ordered)
        //    {
        //        var desc = (t.Description ?? string.Empty).Trim();
        //        if (desc.Length > 140) desc = desc[..140] + "…";

        //        sb.AppendLine(
        //            $"- id:{t.Id}; title:{Sanitize(t.Title)}; priority:{t.Priority}; estMin:{t.EstimatedMinutes}; due:{t.DueDate:yyyy-MM-dd}; energy:{(t.EnergyLevel ?? "unknown")}; desc:{Sanitize(desc)}"
        //        );
        //    }
        //    return sb.ToString().TrimEnd();
        //}

        /// <summary>
        /// Collapse whitespace/newlines and strip dangerous quotes for prompt safety.
        /// </summary>
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var x = s.Replace("\r", " ").Replace("\n", " ").Trim();
            while (x.Contains("  ")) x = x.Replace("  ", " ");
            // Guard JSON keys/values from accidental quote storms
            return x.Replace("\"", "'");
        }

        #endregion

        #region JSON Schema (already used by OpenAI call)
        /// <summary>
        /// Returns the JSON Schema that the AI must follow while generating the daily plan. 
        /// This schema ensures the model outputs valid structured JSON matching the 
        /// <see cref=""DailyPlanAiResult""/> format (timeline, carryover tasks, tone, focus, etc.).
        /// </summary>
        /// <returns>A string containing the JSON Schema definition for the AI response.</returns>

        #region GetPlanResponseJsonSchema old
        //private string GetPlanResponseJsonSchema()
        //{
        //    return @"
        //    {
        //      ""type"": ""object"",
        //      ""$id"": ""https://schemas.flowos.app/plan/flowos_daily_plan.json"",
        //      ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
        //      ""title"": ""flowos_daily_plan"",
        //      ""description"": ""Structured response for FlowOS daily planning"",
        //      ""additionalProperties"": false,
        //      ""properties"": {
        //        ""tone"": {
        //          ""type"": ""string"",
        //          ""enum"": [""soft"", ""strict"", ""playful"", ""balanced""],
        //          ""description"": ""Final tone applied to the plan.""
        //        },
        //        ""focus"": {
        //          ""type"": ""string"",
        //          ""minLength"": 3,
        //          ""maxLength"": 140,
        //          ""description"": ""Single concise sentence describing the day’s main intent.""
        //        },
        //        //""items"": {
        //        //  ""type"": ""array"",
        //        //  ""items"": {
        //        //    ""type"": ""object"",
        //        //    ""additionalProperties"": false,
        //        //    ""properties"": {
        //        //      ""label"": {
        //        //        ""type"": ""string"",
        //        //        ""minLength"": 2,
        //        //        ""maxLength"": 60,
        //        //        ""description"": ""Short (2–5 words) starting with a verb. No emojis/markdown/punctuation.""
        //        //      },
        //        //      ""start"": {
        //        //        ""type"": ""string"",
        //        //        ""format"": ""date-time"",
        //        //        ""pattern"": ""^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:(?:\\d{2}|\\d{2}\\.\\d+)Z$"",
        //        //        ""description"": ""Start time in ISO-8601 UTC (Z). Example: 2025-10-26T09:00:00Z""
        //        //      },
        //        //      ""end"": {
        //        //        ""type"": ""string"",
        //        //        ""format"": ""date-time"",
        //        //        ""pattern"": ""^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:(?:\\d{2}|\\d{2}\\.\\d+)Z$"",
        //        //        ""description"": ""End time in ISO-8601 UTC (Z). Must be > start.""
        //        //      },
        //        //      ""confidence"": {
        //        //        ""type"": ""integer"",
        //        //        ""minimum"": 1,
        //        //        ""maximum"": 5,
        //        //        ""description"": ""AI confidence for this block's placement (1–5)""
        //        //      },
        //        //      ""nudgeAt"": {
        //        //        //""oneOf"": [
        //        //        //  { ""type"": ""null"" },
        //        //        //  { 
        //        //        //    ""type"": ""string"",
        //        //        //    ""format"": ""date-time"",
        //        //        //    ""pattern"": ""^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:(?:\\d{2}|\\d{2}\\.\\d+)Z$"",
        //        //        //    ""description"": ""UTC timestamp to send a reminder (usually 5–10 min before start)""
        //        //        //  }
        //        //        //]
        //        //        ""type"": [""string"", ""null""],
        //        //        ""format"": ""date-time"",
        //        //        ""pattern"": ""^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:(?:\\d{2}|\\d{2}\\.\\d+)Z$"",
        //        //        ""description"": ""UTC timestamp to send a reminder (usually 5–10 min before start)""
        //        //      }
        //        //    },
        //        //    ""required"": [""label"", ""start"", ""end"", ""confidence""]
        //        //  }
        //        //},
        //        ""items"": {
        //          ""type"": ""array"",
        //          ""items"": {
        //            ""type"": ""object"",
        //            ""additionalProperties"": false,
        //            ""properties"": {
        //              ""label"": {
        //                ""type"": ""string"",
        //                ""minLength"": 2,
        //                ""maxLength"": 60,
        //                ""description"": ""Short (2–5 words) starting with a verb. No emojis/markdown/punctuation.""
        //              },
        //              ""start"": {
        //                ""type"": ""string"",
        //                ""format"": ""date-time"",
        //                ""pattern"": ""^\\\\d{4}-\\\\d{2}-\\\\d{2}T\\\\d{2}:\\\\d{2}:(?:\\\\d{2}|\\\\d{2}\\\\.\\\\d+)Z$"",
        //                ""description"": ""Start time in ISO-8601 UTC (Z). Example: 2025-10-26T09:00:00Z""
        //              },
        //              ""end"": {
        //                ""type"": ""string"",
        //                ""format"": ""date-time"",
        //                ""pattern"": ""^\\\\d{4}-\\\\d{2}-\\\\d{2}T\\\\d{2}:\\\\d{2}:(?:\\\\d{2}|\\\\d{2}\\\\.\\\\d+)Z$"",
        //                ""description"": ""End time in ISO-8601 UTC (Z). Must be > start.""
        //              },
        //              ""confidence"": {
        //                ""type"": ""integer"",
        //                ""minimum"": 1,
        //                ""maximum"": 5,
        //                ""description"": ""AI confidence for this block's placement (1–5)""
        //              },
        //              ""nudgeAt"": {
        //                ""type"": [""string"", ""null""],
        //                ""format"": ""date-time"",
        //                ""pattern"": ""^\\\\d{4}-\\\\d{2}-\\\\d{2}T\\\\d{2}:\\\\d{2}:(?:\\\\d{2}|\\\\d{2}\\\\.\\\\d+)Z$"",
        //                ""description"": ""UTC timestamp to send a reminder (usually 5–10 min before start)""
        //              }
        //            },
        //            ""required"": [""label"", ""start"", ""end"", ""confidence"", ""nudgeAt""]
        //          }
        //        },
        //        ""carryOverTaskIds"": {
        //          ""type"": ""array"",
        //          ""items"": { ""type"": ""integer"" },
        //          ""description"": ""IDs of tasks not scheduled today that should roll to the next plan (Smart-Carry).""
        //        }
        //      },
        //      ""required"": [""tone"", ""focus"", ""items"", ""carryOverTaskIds""]
        //    }".Trim();
        //}
        #endregion

        // 2.1) Cached copy (built once)
        private static readonly string _cachedPlanSchema = BuildPlanSchema();

        // 2.2) Public (or private) accessor that callers use
        private static string GetPlanResponseJsonSchema() => _cachedPlanSchema;

        // 2.3) Static builder that returns a JSON string (safe, no manual escaping)

        //private static string BuildPlanSchema()
        //{
        //    var schema = new
        //    {
        //        type = "object",
        //        @id = "https://schemas.flowos.app/plan/flowos_daily_plan.json",
        //        @schema = "https://json-schema.org/draft/2020-12/schema",
        //        title = "flowos_daily_plan",
        //        description = "Structured response for FlowOS daily planning",
        //        additionalProperties = false,
        //        properties = new
        //        {
        //            tone = new
        //            {
        //                type = "string",
        //                // enum is a reserved word, prefix with @ to allow it
        //                @enum = new[] { "soft", "strict", "playful", "balanced" },
        //                description = "Final tone applied to the plan."
        //            },
        //            focus = new
        //            {
        //                type = "string",
        //                minLength = 3,
        //                maxLength = 140,
        //                description = "Single concise sentence describing the day’s main intent."
        //            },
        //            items = new
        //            {
        //                type = "array",
        //                items = new
        //                {
        //                    type = "object",
        //                    additionalProperties = false,
        //                    properties = new
        //                    {
        //                        label = new
        //                        {
        //                            type = "string",
        //                            minLength = 2,
        //                            maxLength = 60,
        //                            description = "Short (2–5 words) starting with a verb. No emojis/markdown/punctuation."
        //                        },
        //                        start = new
        //                        {
        //                            type = "string",
        //                            format = "date-time",
        //                            pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:(?:\d{2}|\d{2}\.\d+)Z$",
        //                            description = "Start time in ISO-8601 UTC (Z). Example: 2025-10-26T09:00:00Z"
        //                        },
        //                        end = new
        //                        {
        //                            type = "string",
        //                            format = "date-time",
        //                            pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:(?:\d{2}|\d{2}\.\d+)Z$",
        //                            description = "End time in ISO-8601 UTC (Z). Must be > start."
        //                        },
        //                        confidence = new
        //                        {
        //                            type = "integer",
        //                            minimum = 1,
        //                            maximum = 5,
        //                            description = "AI confidence for this block's placement (1–5)"
        //                        },
        //                        nudgeAt = new
        //                        {
        //                            type = new[] { "string", "null" },
        //                            format = "date-time",
        //                            pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:(?:\d{2}|\d{2}\.\d+)Z$",
        //                            description = "UTC timestamp to send a reminder (usually 5–10 min before start)"
        //                        }
        //                    },
        //                    required = new[] { "label", "start", "end", "confidence", "nudgeAt" }
        //                }
        //            },
        //            carryOverTaskIds = new
        //            {
        //                type = "array",
        //                items = new { type = "integer" },
        //                description = "IDs of tasks not scheduled today that should roll to the next plan (Smart-Carry)."
        //            }
        //        },
        //        required = new[] { "tone", "focus", "items", "carryOverTaskIds" }
        //    };

        //    // Serialize once into compact JSON
        //    var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = false });

        //    // Optional: dev-only validation to fail fast if we break schema later
        //    #if DEBUG
        //        JsonDocument.Parse(json);
        //    #endif

        //    return json;
        //}

        private static string BuildPlanSchema()
        {
            var schema = new
            {
                type = "object",
                @id = "https://schemas.flowos.app/plan/flowos_daily_plan.json",
                @schema = "https://json-schema.org/draft/2020-12/schema",
                title = "flowos_daily_plan",
                description = "Structured response for FlowOS daily planning",
                additionalProperties = false,
                properties = new
                {
                    tone = new
                    {
                        type = "string",
                        @enum = new[] { "soft", "strict", "playful", "balanced" },
                        description = "Final tone applied to the plan."
                    },
                    focus = new
                    {
                        type = "string",
                        minLength = 3,
                        maxLength = 140,
                        description = "Single concise sentence describing the day’s main intent."
                    },
                    items = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            additionalProperties = false,
                            properties = new
                            {
                                // ✅ REQUIRED: taskId (real task id OR null for breaks)
                                taskId = new
                                {
                                    type = new[] { "integer", "null" },
                                    description = "Task ID from input list, or null for breaks / meta blocks."
                                },

                                label = new
                                {
                                    type = "string",
                                    minLength = 2,
                                    maxLength = 120, // allow real task titles
                                    description = "If taskId != null, must equal task title. If null, short verb phrase."
                                },
                                start = new
                                {
                                    type = "string",
                                    format = "date-time",
                                    pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:(?:\d{2}|\d{2}\.\d+)Z$",
                                    description = "Start time in ISO-8601 UTC (Z). Example: 2025-10-26T09:00:00Z"
                                },
                                end = new
                                {
                                    type = "string",
                                    format = "date-time",
                                    pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:(?:\d{2}|\d{2}\.\d+)Z$",
                                    description = "End time in ISO-8601 UTC (Z). Must be > start."
                                },
                                confidence = new
                                {
                                    type = "integer",
                                    minimum = 1,
                                    maximum = 5,
                                    description = "AI confidence for this block's placement (1–5)"
                                },
                                nudgeAt = new
                                {
                                    type = new[] { "string", "null" },
                                    format = "date-time",
                                    pattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:(?:\d{2}|\d{2}\.\d+)Z$",
                                    description = "UTC timestamp to send a reminder (usually 5 min before start)"
                                }
                            },

                            // ✅ taskId is now REQUIRED
                            required = new[] { "taskId", "label", "start", "end", "confidence", "nudgeAt" }
                        }
                    },
                    carryOverTaskIds = new
                    {
                        type = "array",
                        items = new { type = "integer" },
                        description = "IDs of tasks not scheduled today that should roll to the next plan (Smart-Carry)."
                    }
                },
                required = new[] { "tone", "focus", "items", "carryOverTaskIds" }
            };

            var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = false });

            #if DEBUG
            JsonDocument.Parse(json);
            #endif

            return json;
        }

        #endregion
    }
}
