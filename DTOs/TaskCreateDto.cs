namespace FlowOS.Api.DTOs
{
    public class TaskCreateDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTimeOffset DueDate { get; set; } // ✅ global-safe
        public int Priority { get; set; }
    }
}
