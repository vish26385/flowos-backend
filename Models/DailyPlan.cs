namespace FlowOS.Api.Models
{
    public class DailyPlan
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string PlanJson { get; set; } = ""; // AI-generated plan in JSON
        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
