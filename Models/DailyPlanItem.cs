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

        // When to send a nudge notification
        public DateTime? NudgeAt { get; set; }
    }
}
