using System;
using System.Collections.Generic;
using System.Linq;
using FlowOS.Api.Services.Planner.Models;

namespace FlowOS.Api.Services.Planner.Helpers
{
    public static class AiPlanQualityAnalyzer
    {
        public static PlanQualityMetrics Analyze(DailyPlanAiResult aiResult, int totalUserTasks)
        {
            if (aiResult == null || aiResult.Timeline.Count == 0)
                return new PlanQualityMetrics { Status = "Empty", Notes = "No timeline items generated." };

            var items = aiResult.Timeline;
            var avgConfidence = Math.Round(items.Average(i => i.Confidence), 2);

            // Calculate total time covered
            var start = items.Min(i => i.Start);
            var end = items.Max(i => i.End);
            var totalMinutes = (end - start).TotalMinutes;
            var coveredMinutes = items.Sum(i => (i.End - i.Start).TotalMinutes);
            var coveragePercent = Math.Round((coveredMinutes / totalMinutes) * 100, 2);

            // Check duplicates or overlaps
            var overlapCount = CountOverlaps(items);

            // Compute alignment ratio
            var alignedCount = items.Count(i => i.TaskId.HasValue);
            var alignmentPercent = totalUserTasks == 0 ? 0 : Math.Round((alignedCount * 100.0 / totalUserTasks), 2);

            return new PlanQualityMetrics
            {
                Status = "OK",
                AvgConfidence = avgConfidence,
                CoveragePercent = coveragePercent,
                AlignedTasksPercent = alignmentPercent,
                OverlapCount = overlapCount,
                Notes = $"{items.Count} items analyzed."
            };
        }

        private static int CountOverlaps(List<AiPlanTimelineItem> items)
        {
            int overlaps = 0;
            var sorted = items.OrderBy(i => i.Start).ToList();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].End > sorted[i + 1].Start)
                    overlaps++;
            }

            return overlaps;
        }
    }

    public class PlanQualityMetrics
    {
        public string Status { get; set; } = "Unknown";
        public double AvgConfidence { get; set; }
        public double CoveragePercent { get; set; }
        public double AlignedTasksPercent { get; set; }
        public int OverlapCount { get; set; }
        public string Notes { get; set; } = "";
    }
}
