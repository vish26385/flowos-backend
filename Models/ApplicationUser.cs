using FlowOS.Api.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace FlowOS.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public TimeSpan? WorkStart { get; set; }  // e.g., 09:00
        public TimeSpan? WorkEnd { get; set; }    // e.g., 18:00
                                                  // User’s explicit preference (nullable; TAP2)
        public PlanTone? PreferredTone { get; set; }   // stored as string

        // AI’s current best guess (used when PreferredTone is null)
        public PlanTone CurrentTone { get; set; } = PlanTone.Balanced;

        // Confidence (0–100) in CurrentTone
        public int ToneConfidence { get; set; } = 50;

        // Prevents rapid switching (EV3-M)
        public DateTime? LastToneChangeDate { get; set; }
    }
}
