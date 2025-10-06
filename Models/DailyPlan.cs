namespace FlowOS.Api.Models
{
    public class DailyPlan
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }

        // Storing AI-generated plan in JSON
        public string PlanJson { get; set; } = "";

        // Foreign key to Identity User (string Id)
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
    }
}
