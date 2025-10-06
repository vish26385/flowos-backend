namespace FlowOS.Api.Models
{
    public class Task
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime DueDate { get; set; }
        public bool Completed { get; set; }

        // New field: Priority (1 = Low, 2 = Medium, 3 = High)
        public int Priority { get; set; }

        // Foreign key to Identity User (string Id)
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
    }
}
