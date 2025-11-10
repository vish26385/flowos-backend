namespace FlowOS.Api.DTOs.Plan
{
    public class PlanResponseDto
    {
        public int PlanId { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Focus { get; set; } = string.Empty;
        public List<PlanItemDto> Timeline { get; set; } = new();
        public List<int> CarryOver { get; set; } = new(); // NEW

        // 🔥 Temporary fields for Dev Testing – REMOVE IN PART C
        public string? RawJson { get; set; }
        public string? PrettyJson { get; set; }
    }
}
