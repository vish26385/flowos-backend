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

        //// POST: api/plan/generate
        //[HttpPost("generate")]
        //public async Task<IActionResult> GeneratePlan([FromBody] GeneratePlanDto dto)
        //{
        //    //var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    //if (string.IsNullOrEmpty(userId))
        //    //    return Unauthorized("User not found in token");

        //    var userId = User.FindFirst("id")?.Value; // from JWT (string)

        //    if (userId == null)
        //        return Unauthorized("User not found in token");

        //    if (dto.Date == default)
        //        dto.Date = DateTime.Today;

        //    var result = await _plannerService.GeneratePlanAsync(userId, dto.Date, dto.Tone);

        //    return Ok(result);
        //}

        // POST /api/dailyplan/generate?date=2026-02-15&planStartLocal=2026-02-15T23:30:00
        //[HttpPost("generate")]
        //public async Task<IActionResult> Generate([FromQuery] string date, [FromQuery] DateTime? planStartLocal = null)
        //{
        //    var userId =
        //        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        //        User.FindFirst("id")?.Value;

        //    if (string.IsNullOrEmpty(userId))
        //        return Unauthorized();

        //    if (!DateTime.TryParse(date, out var day))
        //        return BadRequest("Invalid date");

        //    // IST offset for now (later: per-user timezone)
        //    var userOffset = TimeSpan.FromMinutes(330);

        //    DateTime? planStartUtc = null;

        //    if (planStartLocal.HasValue)
        //    {
        //        // Treat input as user-local (IST) even if Kind is Unspecified
        //        var localUnspec = DateTime.SpecifyKind(planStartLocal.Value, DateTimeKind.Unspecified);
        //        planStartUtc = new DateTimeOffset(localUnspec, userOffset).UtcDateTime;
        //    }

        //    var dto = await _plannerService.GeneratePlanAsync(
        //        userId,
        //        day,
        //        toneOverride: null,
        //        forceRegenerate: true,
        //        planStartUtc: planStartUtc // ✅ new parameter
        //    );

        //    return Ok(dto);
        //}

        // POST /api/plan/generate?date=2026-02-15&planStartLocal=2026-02-15T23:30:00
        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
            [FromQuery] string? date = null,
            [FromQuery] DateTime? planStartLocal = null)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // IST offset for now (later: per-user timezone)
            var userOffset = TimeSpan.FromMinutes(330);

            // ✅ If date not provided, default to TODAY in IST
            if (string.IsNullOrWhiteSpace(date))
            {
                var istNow = DateTimeOffset.UtcNow.ToOffset(userOffset);
                date = istNow.ToString("yyyy-MM-dd");
            }

            if (!DateTime.TryParse(date, out var day))
                return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });

            // ✅ day should be treated as a DATE (ignore time)
            day = day.Date;

            DateTime? planStartUtc = null;

            if (planStartLocal.HasValue)
            {
                // Treat input as user-local (IST) even if Kind is Unspecified
                var localUnspec = DateTime.SpecifyKind(planStartLocal.Value, DateTimeKind.Unspecified);

                // ✅ Convert IST local -> UTC
                planStartUtc = new DateTimeOffset(localUnspec, userOffset).UtcDateTime;
            }

            var dto = await _plannerService.GeneratePlanAsync(
                userId,
                day,
                toneOverride: null,
                forceRegenerate: true,
                planStartUtc: planStartUtc
            );

            return Ok(dto);
        }
    }
}
