using System.ComponentModel.DataAnnotations;

namespace FlowOS.Api.Models
{
    public class DailyPlanItem
    {
        [Key]
        public int Id { get; set; }

        public int PlanId { get; set; }
        public DailyPlan Plan { get; set; } = default!;

        public int? TaskId { get; set; }
        public Task? Task { get; set; }

        public string Label { get; set; } = string.Empty;

        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        // Confidence score (1–5) to show AI certainty
        public int Confidence { get; set; } = 3;

        // ✅ Start reminder: 5 minutes before Start (UTC)
        public DateTime? NudgeAt { get; set; }
        public DateTime? NudgeSentAtUtc { get; set; }

        // ✅ End reminder: 5 minutes before End (UTC)
        public DateTime? EndNudgeAtUtc { get; set; }
        public DateTime? EndNudgeSentAtUtc { get; set; }

        public string? LastNudgeError { get; set; }
    }
}
