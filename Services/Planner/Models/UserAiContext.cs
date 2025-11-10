namespace FlowOS.Api.Services.Planner.Models
{
    /// <summary>
    /// Minimal user context passed to the AI planning engine.
    /// Keeps the AI layer free from EF entities and API DTOs.
    /// </summary>
    public class UserAiContext
    {
        #region Properties

        /// <summary>
        /// User's first name used for friendly, personalized tone ("Good morning, Vishnu!").
        /// </summary>
        public required string FirstName { get; init; }

        /// <summary>
        /// Full display name if available (optional). May be used for more formal messages.
        /// </summary>
        public string? FullName { get; init; }

        /// <summary>
        /// Preferred work day start (local time). The AI schedules the earliest slot at or after this.
        /// </summary>
        public TimeSpan WorkStart { get; init; }

        /// <summary>
        /// Preferred work day end (local time). The AI avoids scheduling beyond this unless instructed.
        /// </summary>
        public TimeSpan WorkEnd { get; init; }

        /// <summary>
        /// User's preferred tone for the plan: "soft" | "strict" | "playful" | "balanced" (optional).
        /// </summary>
        public string? PreferredTone { get; init; }

        #endregion
    }
}
