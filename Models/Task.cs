namespace FlowOS.Api.Models
{
    public class Task
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        // Stored as UTC timestamptz
        public DateTime DueDate { get; set; }
        public bool Completed { get; set; }

        // New field: Priority (1 = Low, 2 = Medium, 3 = High)
        public int Priority { get; set; }

        // Foreign key to Identity User (string Id)
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public int? EstimatedMinutes { get; set; }   // default to 30 if null

        public string? EnergyLevel { get; set; } // "low" | "medium" | "high"

        // ✅ STEP 11.2 (nudges)
        public DateTime? NudgeAtUtc { get; set; }
        public DateTime? NudgeSentAtUtc { get; set; }
        public string? LastNudgeError { get; set; }
    }
}
