using FlowOS.Api.Models.Enums;

namespace FlowOS.Api.Models
{
    public class ToneHistory
    {
        public int Id { get; set; }

        // who + when
        public string UserId { get; set; } = string.Empty;
        public DateTime Date { get; set; }     // local day (Date component)

        // TD2 signals
        public int EmotionalScore { get; set; }    // -2..+2 (0 if unknown)
        public int PerformanceScore { get; set; }  // -2..+2

        // What AI suggested vs what we actually used
        public PlanTone? SuggestedTone { get; set; }
        public PlanTone? AppliedTone { get; set; }

        // Confidence delta applied to CurrentTone after today (e.g., +4, -6)
        public int ConfidenceDelta { get; set; }

        // Optional notes (short)
        public string? Notes { get; set; }
    }
}
