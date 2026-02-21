using FlowOS.Api.Models;
using System.Text.Json.Serialization;

namespace FlowOS.Api.Services.Planner.Models
{   
    /// <summary>
    /// Rich AI output returned by the OpenAI planner engine.
    /// This is NOT the API DTO and NOT the EF entity — it is an internal result model.
    /// PlannerService maps this to DB (DailyPlan + DailyPlanItem) and to API DTO (PlanResponseDto).
    /// </summary>
    public class DailyPlanAiResult
    {
        #region Properties

        /// <summary>
        /// Tone actually used by the AI ("soft" | "strict" | "playful" | "balanced").
        /// </summary>
        [JsonPropertyName("tone")]
        public required string Tone { get; set; } = "balanced";

        /// <summary>
        /// The day's main theme or focus line (e.g., "Deep Work: FlowOS Planner refactor").
        /// </summary>
        [JsonPropertyName("focus")]
        public string Focus { get; set; } = "";

        /// <summary>
        /// Raw JSON returned by the AI (before cleanup). Useful for debugging and audits.
        /// </summary>
        public string RawJson { get; set; } = "";

        /// <summary>
        /// Clean (and typically minified) JSON used by the app and stored in DB for quick retrieval.
        /// </summary>
        public string CleanJson { get; set; } = "";

        /// <summary>
        /// The scheduled timeline items for the day, in chronological order.
        /// </summary>
        [JsonPropertyName("items")]
        public List<AiPlanTimelineItem> Timeline { get; set; } = new();

        /// <summary>
        /// Tasks intentionally excluded from today's schedule (for carry-forward / tomorrow).
        /// </summary>
        public List<int> CarryOverTaskIds { get; set; } = new();

        public string? ModelUsed { get; set; }   // e.g., "gpt-4.1-mini" or "gpt-3.5"

        #endregion
    }

    /// <summary>
    /// Internal AI timeline item model (kept separate from API DTO and EF entity).
    /// </summary>
    public class AiPlanTimelineItem
    {
        #region Properties

        /// <summary>
        /// Optional mapping to an existing Task. Null when the block is a break or meta-activity.
        /// </summary>
        [JsonPropertyName("taskId")]
        public int? TaskId { get; set; }

        /// <summary>
        /// Human-readable label for the block (task title or activity name).
        /// </summary>
        [JsonPropertyName("label")]
        public required string Label { get; init; }

        /// <summary>
        /// Start time (UTC preferred upstream; the PlannerService will ensure UTC as a final step).
        /// </summary>
        [JsonPropertyName("start")]
        public DateTimeOffset Start { get; init; }

        /// <summary>
        /// End time (UTC preferred upstream).
        /// </summary>
        [JsonPropertyName("end")]
        public DateTimeOffset End { get; init; }

        /// <summary>
        /// Confidence score (1–5). Higher = AI is more certain this is a good allocation.
        /// </summary>
        [JsonPropertyName("confidence")]
        public int Confidence { get; init; } = 3;

        /// <summary>
        /// Optional nudge notification time for this block (UTC preferred).
        /// </summary>
        [JsonPropertyName("nudgeAt")]
        public DateTime? NudgeAt { get; init; }

        #endregion
    }
}
