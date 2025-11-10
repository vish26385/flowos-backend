namespace FlowOS.Api.DTOs.Plan
{
    public class PlanItemDto
    {
        public int ItemId { get; set; }
        public int? TaskId { get; set; }
        public string Label { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int Confidence { get; set; }
        public DateTime? NudgeAt { get; set; }
    }
}
