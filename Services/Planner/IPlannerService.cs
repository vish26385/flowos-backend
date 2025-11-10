using FlowOS.Api.DTOs.Plan;

namespace FlowOS.Api.Services.Planner
{
    public interface IPlannerService
    {
        Task<PlanResponseDto> GeneratePlanAsync(string userId, DateTime date, string? toneOverride = null, bool forceRegenerate = false);
    }
}
