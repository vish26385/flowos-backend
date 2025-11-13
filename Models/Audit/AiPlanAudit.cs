namespace FlowOS.Api.Models.Audit
{
    public class AiPlanAudit
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public long LatencyMs { get; set; }

        public string ModelUsed { get; set; } = string.Empty;
        public bool WasRegenerated { get; set; }

        public double AvgConfidence { get; set; }
        public double CoveragePercent { get; set; }
        public double AlignedTasksPercent { get; set; }
        public int OverlapCount { get; set; }

        public string RawJson { get; set; } = string.Empty;
        public string CleanJson { get; set; } = string.Empty;

        public string Notes { get; set; } = "";
    }
}
