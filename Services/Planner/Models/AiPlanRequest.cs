namespace FlowOS.Api.Services.Planner.Models
{
    /// <summary>
    /// Wrapper for all inputs the AI planner needs to generate a day plan.
    /// Using a single request object keeps the AI API stable as we add more signals (mood, weather, meetings).
    /// </summary>
    public record AiPlanRequest
    {
        #region Properties

        /// <summary>
        /// The user ID (Identity) used for DB storage — NOT passed to AI.
        /// We keep this outside UserAiContext to maintain clean separation between "AI needs" and "DB needs".
        /// </summary>
        public required string UserId { get; init; }

        /// <summary>
        /// Minimal user profile context for personalization and schedule bounds.
        /// </summary>
        public required UserAiContext User { get; init; }

        /// <summary>
        /// Pending tasks to consider when creating the plan (already filtered by caller).
        /// </summary>
        public required List<TaskAiContext> Tasks { get; set; } = new();

        /// <summary>
        /// The target date for which the plan is being generated (local date, time part ignored).
        /// </summary>
        public DateTime Date { get; init; }

        /// <summary>
        /// Tone preference for this particular plan: "soft" | "strict" | "playful" | "balanced".
        /// </summary>
        public required string Tone { get; init; }

        /// <summary>
        /// If true, the AI will re-generate the plan even if one already exists for this date.
        /// Default = false.
        /// </summary>
        public bool ForceRegenerate { get; init; } = false;

        public DateTime StartedAt { get; init; } = DateTime.UtcNow;

        #endregion
    }
}
