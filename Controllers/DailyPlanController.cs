using FlowOS.Api.Data;
using FlowOS.Api.DTOs.Plan;
using FlowOS.Api.Helpers;
using FlowOS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace FlowOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DailyPlanController : ControllerBase
    {

        private readonly FlowOSContext _context;
        public DailyPlanController(FlowOSContext context)
        {
            _context = context;
        }
        //[HttpGet("{date}")]
        //public async Task<IActionResult> GetPlan(string date)
        //{
        //    try
        //    {
        //        //var userId = User.FindFirst("id")?.Value;
        //        var userId = User.FindFirstValue("id")
        //                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        //                     ?? User.FindFirstValue("sub");
        //        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        //        if (!DateTime.TryParseExact(
        //            date,
        //            "yyyy-MM-dd",
        //            CultureInfo.InvariantCulture,
        //            DateTimeStyles.None,
        //            out var parsed))
        //            return BadRequest("Invalid date. Use yyyy-MM-dd");
        //        //if (!DateTime.TryParse(date, out var parsed)) 
        //        //    return BadRequest("Invalid date");
        //        //var plan = await _context.DailyPlans.FirstOrDefaultAsync(d => d.UserId
        //        //== userId && d.Date.Date == parsed.Date);
        //        var plan = await _context.DailyPlans
        //                  .AsNoTracking()
        //                  .Include(p => p.Items)
        //                  .FirstOrDefaultAsync(d => d.UserId == userId && d.Date.Date == parsed.Date);
        //        if (plan == null) return NotFound();
        //        var dto = PlanMapper.ToPlanResponseDto(plan);
        //        return Ok(dto);
        //    }
        //    catch (Exception ex)
        //    {
        //        // ✅ This will appear in Render logs
        //        Console.WriteLine("❌ GetPlan crashed: " + ex);
        //        return StatusCode(500, "GetPlan crashed: " + ex.Message);
        //    }
        //}

        [HttpGet("{date}")]
        public async Task<IActionResult> GetPlan(string date)
        {
            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            // ✅ Parse exact format
            if (!DateTime.TryParseExact(
                    date,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedLocal))
            {
                return BadRequest("Invalid date. Use yyyy-MM-dd");
            }

            // ✅ CRITICAL FIX: force UTC kind
            // Keep same date value, just mark it as UTC midnight
            var dayUtc = DateTime.SpecifyKind(parsedLocal.Date, DateTimeKind.Utc);

            // ✅ Compare by range (recommended for timestamptz)
            var nextDayUtc = dayUtc.AddDays(1);

            var plan = await _context.DailyPlans
                .AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId &&
                    p.Date >= dayUtc &&
                    p.Date < nextDayUtc);

            if (plan == null) return NotFound();

            // ✅ Use your mapping (also protect Items from null)
            var dto = new PlanResponseDto
            {
                PlanId = plan.Id,
                Date = plan.Date.ToString("yyyy-MM-dd"),
                Focus = plan.Focus ?? "",
                Timeline = (plan.Items ?? new List<DailyPlanItem>())
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
                CarryOver = new List<int>(),
                RawJson = null,
                PrettyJson = plan.PlanJsonClean
            };

            return Ok(dto);
        }

        [HttpPost("save")]
        public async Task<IActionResult> SavePlan([FromBody] DailyPlan model)
        {
            var userId = User.FindFirst("id")?.Value;
            if (userId == null) return Unauthorized();
            model.UserId = userId;
            _context.DailyPlans.Add(model);
            await _context.SaveChangesAsync();
            return Ok(model);
        }
    }
}
