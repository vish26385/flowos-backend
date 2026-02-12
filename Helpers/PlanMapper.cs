using FlowOS.Api.DTOs.Plan;
using FlowOS.Api.Models;

namespace FlowOS.Api.Helpers
{
    public static class PlanMapper
    {
        public static PlanResponseDto ToPlanResponseDto(DailyPlan plan)
        {
            return new PlanResponseDto
            {
                PlanId = plan.Id,
                Date = plan.Date.ToString("yyyy-MM-dd"),
                Focus = plan.Focus ?? "",
                Timeline = plan.Items
                    .OrderBy(i => i.Start)
                    .Select(i => new PlanItemDto
                    {
                        ItemId = i.Id,
                        TaskId = i.TaskId,
                        Label = i.Label,
                        Start = i.Start,
                        End = i.End,
                        Confidence = i.Confidence,
                        NudgeAt = i.NudgeAt
                    })
                    .ToList(),

                // Keep default if you’re not using it here
                CarryOver = new List<int>(),

                // IMPORTANT: do NOT return raw json here for production
                RawJson = null,
                PrettyJson = plan.PlanJsonClean
            };
        }
    }
}
