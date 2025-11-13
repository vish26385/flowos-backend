namespace FlowOS.Api.Models.Audit
{
    public class AuditStatsDto
    {
        public int TotalPlans { get; set; }
        public double RegenerationRate { get; set; }
        public long AvgLatencyMs { get; set; }
        public double AvgConfidence { get; set; }
        public double AvgCoverage { get; set; }
    }
}
