using FlowOS.Api.DTOs.Plan;
using FlowOS.Api.Services.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FlowOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlanController : Controller
    {
        private readonly IPlannerService _plannerService;

        public PlanController(IPlannerService plannerService)
        {
            _plannerService = plannerService;
        }

        // POST: api/plan/generate
        [HttpPost("generate")]
        public async Task<IActionResult> GeneratePlan([FromBody] GeneratePlanDto dto)
        {
            //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //if (string.IsNullOrEmpty(userId))
            //    return Unauthorized("User not found in token");

            var userId = User.FindFirst("id")?.Value; // from JWT (string)

            if (userId == null)
                return Unauthorized("User not found in token");

            if (dto.Date == default)
                dto.Date = DateTime.Today;

            var result = await _plannerService.GeneratePlanAsync(userId, dto.Date, dto.Tone);

            return Ok(result);
        }
    }
}
