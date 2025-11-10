namespace FlowOS.Api.Services.Planner.Models
{
    /// <summary>
    /// AI-focused projection of a task used for plan generation.
    /// Free of EF dependencies and safe for LLM prompts.
    /// </summary>
    public class TaskAiContext
    {
        #region Properties

        /// <summary>
        /// Original task id (DB id). Helps map AI timeline items back to tasks.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Short user-friendly title. Keep it crisp to avoid long prompt payloads.
        /// </summary>
        public required string Title { get; init; }

        /// <summary>
        /// Due date for the task. The AI uses this when prioritizing today's timeline.
        /// </summary>
        public DateTime DueDate { get; init; }

        /// <summary>
        /// Priority as an integer (e.g., 1–3 or 1–5). Higher = more important.
        /// </summary>
        public int Priority { get; init; }

        /// <summary>
        /// Estimated effort in minutes (used to size time blocks).
        /// </summary>
        public int? EstimatedMinutes { get; init; }

        /// <summary>
        /// Optional energy requirement: "low" | "medium" | "high" (free-form string for now).
        /// </summary>
        public string? EnergyLevel { get; init; }

        /// <summary>
        /// Optional short description (already trimmed if needed for token limits).
        /// </summary>
        public string? Description { get; init; }

        #endregion
    }
}
