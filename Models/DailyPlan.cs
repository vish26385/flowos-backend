using System.ComponentModel.DataAnnotations;

namespace FlowOS.Api.Models
{
    //public class DailyPlan
    //{
    //    public int Id { get; set; }
    //    public DateTime Date { get; set; }

    //    // Storing AI-generated plan in JSON
    //    public string PlanJson { get; set; } = "";

    //    // Foreign key to Identity User (string Id)
    //    public string UserId { get; set; } = string.Empty;
    //    public ApplicationUser? User { get; set; }
    //}
    public class DailyPlan
    {
        [Key]
        public int Id { get; set; }

        public DateTime Date { get; set; }

        // Clean (final) JSON used by FlowOS to display the plan
        public string? PlanJsonClean { get; set; } = string.Empty;
        public string? PlanJsonRaw { get; set; } // AI full response

        // AI tone: soft | strict | playful
        public string Tone { get; set; } = "soft";

        // Today's theme or primary focus
        public string? Focus { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        // Navigation for related plan items
        public List<DailyPlanItem> Items { get; set; } = new();

        public string? ModelUsed { get; set; }   // ← NEW (nullable)
    }
}
