namespace FlowOS.Api.DTOs
{
    public class TaskUpdateDto
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime DueDate { get; set; }
        public int Priority { get; set; }
        public bool Completed { get; set; }
    }
}
