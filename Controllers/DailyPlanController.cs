using FlowOS.Api.Data;
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
        [HttpGet("{date}")]
        public async Task<IActionResult> GetPlan(string date)
        {
            //var userId = User.FindFirst("id")?.Value;
            var userId = User.FindFirstValue("id")
                         ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            if (!DateTime.TryParseExact(
                date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
                return BadRequest("Invalid date. Use yyyy-MM-dd");
            //if (!DateTime.TryParse(date, out var parsed)) 
            //    return BadRequest("Invalid date");
            //var plan = await _context.DailyPlans.FirstOrDefaultAsync(d => d.UserId
            //== userId && d.Date.Date == parsed.Date);
            var plan = await _context.DailyPlans
                      .AsNoTracking()
                      .Include(p => p.Items)
                      .FirstOrDefaultAsync(d => d.UserId == userId && d.Date.Date == parsed.Date);
            if (plan == null) return NotFound();
            var dto = PlanMapper.ToPlanResponseDto(plan);
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
