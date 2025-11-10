using System.ComponentModel.DataAnnotations;

namespace FlowOS.Api.Models
{
    public class NudgeFeedback
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        public int PlanItemId { get; set; }
        public DailyPlanItem PlanItem { get; set; } = default!;

        // done | skip | delay
        public string Action { get; set; } = string.Empty;

        public int? Minutes { get; set; } // used only when action = delay

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
