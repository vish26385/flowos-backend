namespace FlowOS.Api.DTOs.Plan
{
    public class GeneratePlanDto
    {
        public DateTime Date { get; set; }
        public string? Tone { get; set; } // optional override
    }
}
