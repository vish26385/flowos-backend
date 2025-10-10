using FlowOS.Api.Data;
using FlowOS.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DailyPlanController : Controller
    {

        private readonly FlowOSContext _context;
        public DailyPlanController(FlowOSContext context)
        {
            _context = context;
        }
        [HttpGet("{date}")]
        public async Task<IActionResult> GetPlan(string date)
        {
            var userId = User.FindFirst("id")?.Value;
            if (userId == null) return Unauthorized();
            if (!DateTime.TryParse(date, out var parsed)) return
            BadRequest("Invalid date");
            var plan = await _context.DailyPlans.FirstOrDefaultAsync(d => d.UserId
            == userId && d.Date.Date == parsed.Date);
            if (plan == null) return NotFound();
            return Ok(plan);
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
