
// ---------------------------------------------
// FlowOS.Api.Services.Planner - OpenAI Call (Partial)
// File: OpenAIPlannerService_OpenAI.cs
// Purpose: Executes the OpenAI Chat Completions call in JSON Schema Mode
// Notes:
//  - Partial class, pairs with OpenAIPlannerService_Prompt.cs
//  - Uses EH3 (Developer Debug Mode): returns raw output on failure instead of throwing
//  - Verbose multi-line logging (LG1) for development
//  - Reads model from configuration (_settings.Model)
// ---------------------------------------------

#region Old Code
//using FlowOS.Api.DTOs.Plan;
//using FlowOS.Api.Models;
//using FlowOS.Api.Services.Planner.Models;
//using Microsoft.EntityFrameworkCore;
//using OpenAI;
//using OpenAI.Chat;
//using System;
//using System.ClientModel; // For ClientResultException in OpenAI .NET 2.x
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http;               // (kept if you use HttpClient elsewhere)
//using System.Text.Json;
//using System.Text.Json.Nodes;
//using System.Text.Json.Serialization;
//using Newtonsoft.Json.Linq;          // Fallback parser
//// ReSharper disable InconsistentNaming

//namespace FlowOS.Api.Services.Planner
//{
//    public partial class OpenAIClientOptionsPlaceholder { } // (no-op; ensure partial compiles cleanly if needed)

//    public partial class OpenAIPlannerService
//    {
//        // NOTE: This partial holds ONLY:
//        //  - The low-level OpenAI call (single attempt) → returns raw JSON (string)
//        //  - Robust parse method (raw JSON → DailyPlanAiResult) with STJ primary + Newtonsoft fallback
//        //  - Helper DTOs and util methods for parsing/normalization
//        //
//        // R-Structured retry/fallback orchestration + ModelUsed assignment happens in the other partial
//        // (OpenAIPlannerService.cs) inside GenerateAiPlanAsync(AiPlanRequest request).

//        #region OpenAI Call (single attempt; model passed in)

//        /// <summary>
//        /// Sends one chat-completions request to the specified model and returns raw JSON text.
//        /// This method performs a single attempt and does not decide retries/fallbacks.
//        /// The caller (GenerateAiPlanAsync) implements the R-Structured retry strategy.
//        /// </summary>
//        /// <param name="model">Model name to call (e.g., ""gpt-4.1-mini"" or ""gpt-3.5-turbo"").</param>
//        /// <param name="systemPrompt">System role prompt.</param>
//        /// <param name="userPrompt">User context prompt.</param>
//        /// <param name="rulesPrompt">Hidden rules prompt (assistant message).</param>
//        /// <param name="schema">JSON schema string for strict response shaping.</param>
//        /// <returns>Raw JSON string produced by the model (JSON only), or ""{}"" on failure.</returns>
//        private async Task<string> CallOpenAiForPlanAsync(
//            string model,
//            string systemPrompt,
//            string userPrompt,
//            string rulesPrompt,
//            string schema)
//        {
//            // Build chat client for the chosen model (CC2-A)
//            var chat = _openAIClient.GetChatClient(model);

//            // P3 order: system → assistant(rules) → user
//            ChatMessage[] messages =
//            [
//                new SystemChatMessage(systemPrompt),
//                new AssistantChatMessage(rulesPrompt), // hidden rules to enforce schema + JSON-only
//                new UserChatMessage(userPrompt)
//            ];

//            // Strict JSON Schema response format (v2.6.0 signature: name, jsonSchema, description?, strict?)
//            var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
//                "flowos_daily_plan",
//                BinaryData.FromString(schema),
//                null,
//                true
//            );

//            var options = new ChatCompletionOptions
//            {
//                Temperature = 0.45f, // T-Med
//                ResponseFormat = responseFormat
//            };

//            try
//            {
//                // Non-streaming call (ST2-ready: can be swapped for streaming later)
//                var completion = await chat.CompleteChatAsync(messages, options).ConfigureAwait(false);

//                var chatResult = completion.Value;
//                string raw = "{}";

//                // Extract JSON text
//                if (chatResult is not null && chatResult.Content is not null)
//                {
//                    raw = chatResult.Content
//                                    .Select(c => c.Text)
//                                    .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
//                          ?? "{}";
//                }

//                _logger.LogInformation("AI_CALL_OK: model={Model} parts={Count}", model, chatResult?.Content?.Count ?? 0);
//                return string.IsNullOrWhiteSpace(raw) ? "{}" : raw;
//            }
//            catch (ClientResultException cre)
//            {
//                // SDK-level (transport/protocol) failures
//                //_lineage.SetLastCompletionTokenUsage(null); // placeholder hook if you track usage
//                _logger.LogError(cre, "AI_CALL_FAIL_CLIENT: model={Model} status={Status}", model, cre.Status);
//                return "{}";
//            }
//            catch (Exception ex)
//            {
//                //_lineage.SetLastCompletionTokenUsage(null); // placeholder hook if you track usage
//                _logger.LogError(ex, "AI_CALL_FAIL_UNEXPECTED: model={Model}", model);
//                return "{}";
//            }
//        }

//        #endregion

//        #region Parse AI Response (STJ primary + Newtonsoft fallback + C3-Smart normalization)

//        /// <summary>
//        /// Parses the raw JSON returned by the AI into a <see cref="DailyPlanAiResult"/>.
//        /// Strategy:
//        ///  1) Try System.Text.Json (fast, strict).
//        ///  2) If that fails, try Newtonsoft.Json (tolerant).
//        ///  3) Normalize: tone (soft|strict|playful|balanced), ISO-8601 Z times, clamp confidence 1–5, sort by start.
//        ///  4) Carry-over IDs are preserved as provided (C3 hooks are applied later in the orchestrator).
//        /// </summary>
//        private DailyPlanAiResult ParseAiPlanResponse(string rawJson)
//        {
//            if (string.IsNullOrWhiteSpace(rawJson))
//            {
//                _logger.LogDebug("AI_PARSE_EMPTY_RAW_JSON");
//                return EmptyResult(rawJson);
//            }

//            // --- Primary: STJ ---
//            try
//            {
//                var opts = new JsonSerializerOptions
//                {
//                    PropertyNameCaseInsensitive = true,
//                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
//                };

//                var stj = JsonSerializer.Deserialize<StjRoot>(rawJson, opts);
//                if (stj != null)
//                {
//                    return BuildResultFromStj(stj, rawJson);
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.LogDebug(ex, "AI_PARSE_STJ_FAIL → trying Newtonsoft fallback");
//            }

//            // --- Fallback: Newtonsoft ---
//            try
//            {
//                var j = JToken.Parse(rawJson);

//                var tone = NormalizeTone(j.Value<string>("tone"));
//                var focus = j.Value<string>("focus") ?? string.Empty;

//                var items = new List<AiPlanTimelineItem>();
//                var itemsToken = j["items"] as JArray;
//                if (itemsToken != null)
//                {
//                    foreach (var it in itemsToken)
//                    {
//                        var label = it.Value<string>("label");
//                        var startStr = it.Value<string>("start");
//                        var endStr = it.Value<string>("end");
//                        var confVal = it["confidence"]?.ToObject<int?>() ?? 3;
//                        var nudgeStr = it.Value<string>("nudgeAt");

//                        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(startStr) || string.IsNullOrWhiteSpace(endStr))
//                            continue;

//                        if (!TryParseUtc(startStr, out var startUtc) || !TryParseUtc(endStr, out var endUtc))
//                            continue;
//                        if (endUtc <= startUtc) continue;

//                        items.Add(new AiPlanTimelineItem
//                        {
//                            TaskId = it.Value<int?>("taskId"),
//                            Label = label.Trim(),
//                            Start = startUtc,
//                            End = endUtc,
//                            Confidence = ClampConfidence(confVal),
//                            NudgeAt = TryParseUtc(nudgeStr, out var nAt) ? nAt : null
//                        });
//                    }
//                }

//                var carry = new List<int>();
//                var carryToken = j["carryOverTaskIds"] as JArray;
//                if (carryToken != null)
//                {
//                    foreach (var c in carryToken)
//                    {
//                        if (int.TryParse(c.ToString(), out var id)) carry.Add(id);
//                    }
//                }

//                // Sort & normalize
//                items = items
//                    .Where(x => x.End > x.Start)
//                    .OrderBy(x => x.Start)
//                    .ToList();

//                var cleanObj = new CleanRoot
//                {
//                    tone = tone,
//                    focus = focus,
//                    items = items.Select(ToCleanItem).ToList(), // see note below if casing warns
//                    carryOverTaskIds = carry
//                };

//                var cleanJson = JsonSerializer.Serialize(
//                    cleanObj,
//                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false }
//                );

//                return new DailyPlanAiResult
//                {
//                    Tone = tone,
//                    Focus = focus,
//                    RawJson = rawJson,
//                    CleanJson = cleanJson,
//                    Timeline = items,
//                    CarryOverTaskIds = carry
//                };
//            }
//            catch (Exception ex2)
//            {
//                _logger.LogError(ex2, "AI_PARSE_NEWTONSOFT_FAIL");
//            }

//            // Both failed → empty object (caller will decide retry/fallback)
//            return EmptyResult(rawJson);
//        }

//        // ---------- Helpers & Shapes ----------

//        private static DailyPlanAiResult EmptyResult(string raw) => new DailyPlanAiResult
//        {
//            Tone = "balanced",
//            Focus = string.Empty,
//            RawJson = raw ?? string.Empty,
//            CleanJson = "{\"tone\":\"balanced\",\"focus\":\"\",\"items\":[],\"carryOverTaskIds\":[]}",
//            Timeline = new(),
//            CarryOverTaskIds = new()
//        };

//        private DailyPlanAiResult BuildResultFromStj(StjRoot stj, string rawJson)
//        {
//            var tone = NormalizeTone(stj.Tone);
//            var focus = stj.Focus ?? string.Empty;

//            var items = new List<AiPlanTimelineItem>();
//            if (stj.Items != null)
//            {
//                foreach (var it in stj.Items)
//                {
//                    if (string.IsNullOrWhiteSpace(it.Label) || it.Start == null || it.End == null)
//                        continue;

//                    var startOk = TryParseUtc(it.Start!, out var startUtc);
//                    var endOk = TryParseUtc(it.End!, out var endUtc);
//                    if (!startOk || !endOk || endUtc <= startUtc)
//                        continue;

//                    items.Add(new AiPlanTimelineItem
//                    {
//                        TaskId = it.TaskId,
//                        Label = it.Label.Trim(),
//                        Start = startUtc,
//                        End = endUtc,
//                        Confidence = ClampConfidence(it.Confidence ?? 3),
//                        NudgeAt = TryParseUtc(it.NudgeAt, out var nAt) ? nAt : null
//                    });
//                }
//            }

//            var carry = stj.CarryOverTaskIds?.Where(x => x != null).Select(x => x!.Value).ToList() ?? new();

//            // Sort & normalize
//            items = items
//                .Where(x => x.End > x.Start)
//                .OrderBy(x => x.Start)
//                .ToList();

//            // Build normalized clean JSON
//            var cleanObj = new CleanRoot
//            {
//                tone = tone,
//                focus = focus,
//                items = items.Select(ToCleanItem).ToList(),
//                carryOverTaskIds = carry
//            };

//            var cleanJson = JsonSerializer.Serialize(
//                cleanObj,
//                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false }
//            );

//            return new DailyPlanAiResult
//            {
//                Tone = tone,
//                Focus = focus,
//                RawJson = rawJson,
//                CleanJson = cleanJson,
//                Timeline = items,
//                CarryOverTaskIds = carry
//            };
//        }

//        private static CleanItem ToCleanItem(AiPlanTimelineItem i) => new CleanItem
//        {
//            label = i.Label,
//            // Ensure ISO-8601 Z
//            start = i.Start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
//            end = i.End.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
//            confidence = ClampConfidence(i.Confidence),
//            nudgeAt = i.NudgeAt?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
//        };

//        private static bool TryParseUtc(string? s, out DateTime utc)
//        {
//            utc = default;
//            if (string.IsNullOrWhiteSpace(s)) return false;

//            if (DateTime.TryParse(
//                    s,
//                    null,
//                    System.Globalization.DateTimeStyles.AdjustToUniversal |
//                    System.Globalization.DateTimeStyles.AssumeUniversal,
//                    out var dt))
//            {
//                utc = DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
//                return true;
//            }

//            if (DateTimeOffset.TryParse(
//                    s,
//                    null,
//                    System.Globalization.DateTimeStyles.AssumeUniversal,
//                    out var dto))
//            {
//                utc = dto.UtcDateTime;
//            }

//            return utc != default;
//        }

//        private static int ClampConfidence(int value) => Math.Min(5, Math.Max(1, value));

//        // ---------- Shapes for STJ ----------

//        private sealed class StjRoot
//        {
//            [JsonPropertyName("tone")] public string? Tone { get; set; }
//            [JsonPropertyName("focus")] public string? Focus { get; set; }
//            [JsonPropertyName("items")] public List<StjItem>? Items { get; set; }
//            [JsonPropertyName("carryOverTaskIds")] public List<int?>? CarryOverTaskIds { get; set; }
//        }

//        private sealed class StjItem
//        {
//            [JsonPropertyName("taskId")] public int? TaskId { get; set; }
//            [JsonPropertyName("label")] public string? Label { get; set; }
//            [JsonPropertyName("start")] public string? Start { get; set; }
//            [JsonPropertyName("end")] public string? End { get; set; }
//            [JsonPropertyName("confidence")] public int? Confidence { get; set; }
//            [JsonPropertyName("nudgeAt")] public string? NudgeAt { get; set; }
//        }

//        // ---------- Clean shape for re-serialize ----------

//        private sealed class CleanRoot
//        {
//            public string tone { get; set; } = "balanced";
//            public string focus { get; set; } = string.Empty;
//            public List<CleanItem> items { get; set; } = new();
//            public List<int> carryOverTaskIds { get; set; } = new();
//        }

//        private sealed class CleanItem
//        {
//            public string label { get; set; } = "";
//            public string start { get; set; } = "";
//            public string end { get; set; } = "";
//            public int confidence { get; set; }
//            public string? nudgeAt { get; set; }
//        }

//        #endregion
//    }
//}
#endregion

using FlowOS.Api.DTOs.Plan;
using FlowOS.Api.Models;
using FlowOS.Api.Services.Planner.Models;       // AiPlanTimelineItem, DailyPlanAiResult
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;          // Fallback parser
using OpenAI;
using OpenAI.Chat;
using System;
using System;
using System.ClientModel;                       // ClientResult<T>, ClientResultException, ApiKeyCredential
using System.Collections.Generic;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq;
using System.Net.Http; 
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FlowOS.Api.Services.Planner
{
    public partial class OpenAIPlannerService
    {
        #region OpenAI Call (CC2-A, SDK 2.6.0)

        /// <summary>
        /// Calls the OpenAI chat model with P3 messages and a JSON-Schema response format.
        /// Returns the raw JSON text produced by the model.
        /// </summary>
        private async Task<string> CallOpenAiForPlanAsync(
            string model,
            string systemPrompt,
            string userPrompt,
            string rulesPrompt,
            string schema)
        {
            var chat = _openAIClient.GetChatClient(model);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(systemPrompt),
                // Hidden “rules” as assistant to enforce schema & JSON-only output (RP-E).
                new AssistantChatMessage(rulesPrompt),
                new UserChatMessage(userPrompt)
            };
           
            // SDK 2.6.0 signature: (name, jsonSchema, description?, strict?)
            var responseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "flowos_daily_plan",
                BinaryData.FromString(schema),
                null,
                true
            );

            var options = new ChatCompletionOptions
            {
                Temperature = 0.45f,
                ResponseFormat = responseFormat
            };

            try
            {
                var sw = Stopwatch.StartNew();
                var result = await chat.CompleteChatAsync(messages, options).ConfigureAwait(false);
                sw.Stop();
                _logger?.LogInformation(
                    "⏱️ AI call completed in {Elapsed} ms using model {Model}",
                    sw.ElapsedMilliseconds,
                    model
                );
                var completion = result.Value;

                string raw = "{}";
                if (completion?.Content is { Count: > 0 })
                {
                    // Concatenate any text parts (v2.6.0: Content is a list of parts).
                    var sb = new StringBuilder();
                    foreach (var part in completion.Content)
                    {
                        if (part.Text is { Length: > 0 })
                            sb.Append(part.Text);
                    }
                    if (sb.Length > 0)
                        raw = sb.ToString();
                }

                _logger?.LogInformation("AI_CALL_OK: model={Model} chars={Len}", model, raw.Length);
                return raw;
            }
            catch (ClientResultException cre)
            {
                _logger?.LogError(cre, "AI_CALL_FAIL: transport or API error (model={Model})", model);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "AI_CALL_FAIL_UNEXPECTED (model={Model})", model);
                throw;
            }
        }

        /// <summary>
        /// Back-compat overload for older callsites that don’t pass an explicit model.
        /// Uses OpenAI:Model or falls back to gpt-4.1-mini.
        /// </summary>
        private Task<string> CallOpenAiForPlanAsync(
            string systemPrompt,
            string userPrompt,
            string rulesPrompt,
            string schema)
        {
            var model = string.IsNullOrWhiteSpace(_settings.Model) ? "gpt-4.1-mini" : _settings.Model!.Trim();
            return CallOpenAiForPlanAsync(model, systemPrompt, userPrompt, rulesPrompt, schema);
        }

        #endregion

        #region Parse AI Response (R-Structured + C3-aware)

        /// <summary>
        /// Parses raw JSON from the model into a <see cref="DailyPlanAiResult"/>.
        /// Primary parser: System.Text.Json (fast). Fallback: Newtonsoft (tolerant).
        /// Enforces:
        ///  • tone normalized to { soft | strict | playful | balanced }
        ///  • ISO-8601 'Z' timestamps parsed to UTC DateTime
        ///  • confidence clamped to [1..5]
        ///  • items sorted by start and pruned for invalid ranges
        ///  • carryOverTaskIds parsed if present (C3-B: if absent, caller may compute)
        /// </summary>
        private DailyPlanAiResult ParseAiPlanResponse(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                _logger?.LogWarning("AI_PARSE_EMPTY: raw payload empty.");
                return EmptyResult(rawJson);
            }

            // ---------- Primary: System.Text.Json ----------
            try
            {
                var opts = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                };

                var stj = JsonSerializer.Deserialize<StjRoot>(rawJson, opts);
                if (stj is not null)
                {
                    return BuildResultFromStj(stj, rawJson);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "AI_PARSE_STJ_FAILED → trying Newtonsoft fallback");
            }

            // ---------- Fallback: Newtonsoft.Json ----------
            try
            {
                var j = Newtonsoft.Json.Linq.JToken.Parse(rawJson);

                var tone = NormalizeTone(j.Value<string>("tone"));
                var focus = j.Value<string>("focus") ?? string.Empty;

                focus = string.IsNullOrWhiteSpace(focus)
                        ? "Focus your energy today."
                        : focus.Length > 140 ? focus[..140] : focus;

                var items = new List<AiPlanTimelineItem>();
                var itemsToken = j["items"] as Newtonsoft.Json.Linq.JArray;
                if (itemsToken != null)
                {
                    foreach (var it in itemsToken)
                    {
                        var label = it.Value<string>("label");
                        var startS = it.Value<string>("start");
                        var endS = it.Value<string>("end");
                        var confVal = it["confidence"]?.ToObject<int?>() ?? 3;
                        var nudgeS = it.Value<string>("nudgeAt");

                        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(startS) || string.IsNullOrWhiteSpace(endS))
                            continue;

                        if (!TryParseUtc(startS, out var startUtc) || !TryParseUtc(endS, out var endUtc))
                            continue;
                        if (endUtc <= startUtc) continue;

                        items.Add(new AiPlanTimelineItem
                        {
                            TaskId = it.Value<int?>("taskId"),
                            Label = label.Trim(),
                            Start = startUtc,
                            End = endUtc,
                            Confidence = ClampConfidence(confVal),
                            NudgeAt = TryParseUtc(nudgeS, out var nAt) ? nAt : null
                        });
                    }
                }

                if (items == null || items.Count == 0)
                {
                    _logger?.LogWarning("AI_PLAN_NO_TIMELINE: substituting fallback plan.");
                    return EmptyResult(rawJson);
                }

                var carry = new List<int>();
                if (string.IsNullOrWhiteSpace(tone))
                    tone = "balanced";
                var carryToken = j["carryOverTaskIds"] as Newtonsoft.Json.Linq.JArray;
                if (carryToken != null)
                {
                    foreach (var c in carryToken)
                    {
                        if (int.TryParse(c.ToString(), out var id)) carry.Add(id);
                    }
                }

                items = items.Where(x => x.End > x.Start).OrderBy(x => x.Start).ToList();

                var cleanObj = new CleanRoot
                {
                    tone = tone,
                    focus = focus,
                    items = items.Select(ToCleanItem).ToList(),
                    carryOverTaskIds = carry
                };

                var cleanJson = JsonSerializer.Serialize(
                    cleanObj,
                    new JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = false }
                );

                if (items.Count == 0)
                    _logger?.LogWarning("AI_PARSE_EMPTY_ITEMS: No valid timeline items found.");

                if (items == null || items.Count == 0)
                {
                    _logger?.LogWarning("AI_PLAN_EMPTY_ITEMS: Falling back to default placeholder block.");

                    items = new List<AiPlanTimelineItem>
                    {
                        new AiPlanTimelineItem
                        {
                            Label = "Plan your day manually",
                            Start = DateTime.UtcNow,
                            End = DateTime.UtcNow.AddMinutes(30),
                            Confidence = 1,
                            NudgeAt = DateTime.UtcNow.AddMinutes(5)
                        }
                    };
                }

                return new DailyPlanAiResult
                {
                    Tone = tone,
                    Focus = focus,
                    RawJson = rawJson,
                    CleanJson = cleanJson,
                    Timeline = items,
                    CarryOverTaskIds = carry
                };
            }
            catch (Exception ex2)
            {
                _logger?.LogError(ex2, "AI_PARSE_NEWTONSOFT_FAILED");
            }
         
            return EmptyResult(rawJson);
        }

        // ---------- Helpers & Shapes ----------

        private static DailyPlanAiResult EmptyResult(string raw) => new DailyPlanAiResult
        {
            Tone = "balanced",
            Focus = "Your day plan could not be generated.",
            RawJson = raw ?? string.Empty,
            CleanJson = "{\"tone\":\"balanced\",\"focus\":\"\",\"items\":[],\"carryOverTaskIds\":[]}",
            Timeline = new List<AiPlanTimelineItem>
            {
                new AiPlanTimelineItem
                {
                    Label = "Manual Planning Required",
                    Start = DateTime.UtcNow,
                    End = DateTime.UtcNow.AddMinutes(30),
                    Confidence = 1
                }
            },
            CarryOverTaskIds = new List<int>()
        };

        private sealed class StjRoot
        {
            [JsonPropertyName("tone")] public string? Tone { get; set; }
            [JsonPropertyName("focus")] public string? Focus { get; set; }
            [JsonPropertyName("items")] public List<StjItem>? Items { get; set; }
            [JsonPropertyName("carryOverTaskIds")] public List<int?>? CarryOverTaskIds { get; set; }
        }

        private sealed class StjItem
        {
            [JsonPropertyName("taskId")] public int? TaskId { get; set; }
            [JsonPropertyName("label")] public string? Label { get; set; }
            [JsonPropertyName("start")] public string? Start { get; set; }
            [JsonPropertyName("end")] public string? End { get; set; }
            [JsonPropertyName("confidence")] public int? Confidence { get; set; }
            [JsonPropertyName("nudgeAt")] public string? NudgeAt { get; set; }
        }

        private sealed class CleanRoot
        {
            public string? tone { get; set; } = "balanced";
            public string? focus { get; set; } = string.Empty;
            public List<CleanItem> items { get; set; } = new ();
            public List<int> carryOverTaskIds { get; set; } = new();
        }

        private sealed class CleanItem
        {
            public string? label { get; set; } = "";
            public string? start { get; set; } = "";
            public string? end { get; set; } = "";
            public int confidence { get; set; }
            public string? nudgeAt { get; set; }
        }

        private static int ClampConfidence(int value) => Math.Min(5, Math.Max(1, value));

        private static bool TryParseUtc(string? s, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(s)) return false;

            if (DateTime.TryParse(
                    s,
                    null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var dt))
            {
                utc = DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
                return true;
            }

            if (DateTimeOffset.TryParse(
                    s,
                    null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out var dto))
            {
                utc = dto.UtcDateTime;
                return true;
            }

            return false;
        }

        private DailyPlanAiResult BuildResultFromStj(StjRoot stj, string rawJson)
        {
            var tone = NormalizeTone(stj.Tone);
            var focus = stj.Focus ?? string.Empty;

            var items = new List<AiPlanTimelineItem>();
            if (stj.Items != null)
            {
                foreach (var it in stj.Items)
                {
                    if (string.IsNullOrWhiteSpace(it.Label) || it.Start == null || it.End == null)
                        continue;

                    var startOk = TryParseUtc(it.Start!, out var startUtc);
                    var endOk = TryParseUtc(it.End!, out var endUtc);
                    if (!startOk || !endOk || endUtc <= startUtc)
                        continue;

                    items.Add(new AiPlanTimelineItem
                    {
                        TaskId = it.TaskId,
                        Label = it.Label.Trim(),
                        Start = startUtc,
                        End = endUtc,
                        Confidence = ClampConfidence(it.Confidence ?? 3),
                        NudgeAt = TryParseUtc(it.NudgeAt, out var nAt) ? nAt : null
                    });
                }
            }

            var carry = stj.CarryOverTaskIds?.Where(x => x != null).Select(x => x!.Value).ToList() ?? new();

            // Sort & normalize
            items = items
                .Where(x => x.End > x.Start)
                .OrderBy(x => x.Start)
                .ToList();

            // Build normalized clean JSON
            var cleanObj = new CleanRoot
            {
                tone = tone,
                focus = focus,
                items = items.Select(ToCleanItem).ToList(),
                carryOverTaskIds = carry
            };

            var cleanJson = JsonSerializer.Serialize(
                cleanObj,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false }
            );

            return new DailyPlanAiResult
            {
                Tone = tone,
                Focus = focus,
                RawJson = rawJson,
                CleanJson = cleanJson,
                Timeline = items,
                CarryOverTaskIds = carry
            };
        }

        private static CleanItem ToCleanItem(AiPlanTimelineItem i) => new CleanItem
        {
            label = i.Label,
            start = i.Start.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            end = i.End.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            confidence = Math.Min(5, Math.Max(1, i.Confidence)),
            nudgeAt = i.NudgeAt?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
        };

        #endregion
    }
}
