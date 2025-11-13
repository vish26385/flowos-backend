namespace FlowOS.Api.Models.Audit
{
    public class AiPlanAuditDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public DateTime RequestedAt { get; set; }
        public long LatencyMs { get; set; }
        public string ModelUsed { get; set; } = "";
        public bool WasRegenerated { get; set; }
        public double AvgConfidence { get; set; }
        public double CoveragePercent { get; set; }
        public double AlignedTasksPercent { get; set; }
        public int OverlapCount { get; set; }
    }
}
