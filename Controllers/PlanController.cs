using FlowOS.Api.DTOs.Plan;
using FlowOS.Api.Services.Planner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
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

        //// POST /api/plan/generate?date=2026-02-15&planStartLocal=2026-02-15T23:30:00+05:30        
        //[HttpPost("generate")]
        //public async Task<IActionResult> Generate(
        //[FromQuery] string? date = null,
        //[FromQuery] DateTimeOffset? planStartLocal = null)
        //{
        //    var userId =
        //        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        //        User.FindFirst("id")?.Value;

        //    if (string.IsNullOrEmpty(userId))
        //        return Unauthorized();

        //    // IST offset (later: per-user timezone)
        //    var userOffset = TimeSpan.FromMinutes(330);

        //    // ✅ If date not provided, default to TODAY in IST
        //    if (string.IsNullOrWhiteSpace(date))
        //    {
        //        var istNow = DateTimeOffset.UtcNow.ToOffset(userOffset);
        //        date = istNow.ToString("yyyy-MM-dd");
        //    }

        //    // ✅ Parse strict IST calendar date (yyyy-MM-dd)
        //    if (!DateTime.TryParseExact(
        //            date,
        //            "yyyy-MM-dd",
        //            CultureInfo.InvariantCulture,
        //            DateTimeStyles.None,
        //            out var istDate))
        //    {
        //        return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });
        //    }

        //    // ✅ Date-only (IST calendar date) passed into service
        //    var istDay = istDate.Date;

        //    // ✅ Convert chosen planStartLocal -> UTC (client sends +05:30)
        //    DateTime? planStartUtc = null;
        //    if (planStartLocal.HasValue)
        //    {
        //        planStartUtc = planStartLocal.Value.UtcDateTime;
        //    }

        //    // ✅ IMPORTANT:
        //    // Your service expects `date` as IST date-only and will build IST->UTC window itself
        //    var dtoResult = await _plannerService.GeneratePlanAsync(
        //        userId,
        //        istDay,
        //        toneOverride: null,
        //        forceRegenerate: true,
        //        planStartUtc: planStartUtc
        //    );

        //    return Ok(dtoResult);
        //}

        [HttpPost("generate")]
        public async Task<IActionResult> Generate(
        [FromQuery] string? date = null,
        [FromQuery] DateTimeOffset? planStartLocal = null)
        {
            var userId =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // IST offset (later: per-user timezone)
            var userOffset = TimeSpan.FromMinutes(330);

            // ✅ If date not provided, default to TODAY in IST (yyyy-MM-dd)
            if (string.IsNullOrWhiteSpace(date))
            {
                var istNow = DateTimeOffset.UtcNow.ToOffset(userOffset);
                date = istNow.ToString("yyyy-MM-dd");
            }

            // ✅ Parse strict IST calendar date (yyyy-MM-dd) -> DateOnly
            if (!DateOnly.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var istDay))
            {
                return BadRequest(new { message = "Invalid date. Use yyyy-MM-dd." });
            }

            // ✅ Convert chosen planStartLocal -> UTC (client sends +05:30)
            DateTime? planStartUtc = null;
            if (planStartLocal.HasValue)
            {
                planStartUtc = planStartLocal.Value.UtcDateTime;
            }

            // ✅ Service expects IST calendar DateOnly and will build IST->UTC window internally
            var dtoResult = await _plannerService.GeneratePlanAsync(
                userId,
                istDay,
                toneOverride: null,
                forceRegenerate: true,
                planStartUtc: planStartUtc
            );

            return Ok(dtoResult);
        }
    }
}
