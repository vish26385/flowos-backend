namespace FlowOS.Api.DTOs
{
    public class TaskCreateDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTimeOffset DueDate { get; set; } // ✅ global-safe
        // ✅ USER owns time (optional)
        public DateTimeOffset? PlannedStartUtc { get; set; }
        public DateTimeOffset? PlannedEndUtc { get; set; }
        public int Priority { get; set; }
        public int? EstimatedMinutes { get; set; }   //  ✅ add default to 30 if null
    }
}
