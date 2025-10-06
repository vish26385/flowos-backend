namespace FlowOS.Api.DTOs
{
    public class TaskCreateDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime DueDate { get; set; }
        public int Priority { get; set; }
    }
}
