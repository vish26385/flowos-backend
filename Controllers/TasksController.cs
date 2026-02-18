using FlowOS.Api.Data;
using FlowOS.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Task = FlowOS.Api.Models.Task;

namespace FlowOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // Require login
    public class TasksController : ControllerBase
    {
        private readonly FlowOSContext _context;

        public TasksController(FlowOSContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] TaskCreateDto dto)
        {
            //var userId = User.FindFirst("id")?.Value; // from JWT (string)

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!HasOffsetInJson(dto.DueDate))
                return BadRequest(new { message = "DueDate must include timezone offset (Z or +05:30)." });

            // IMPORTANT: dto.DueDate should already come in as UTC from mobile
            // but we still force UTC Kind to avoid Npgsql issues
            var dueUtc = dto.DueDate.UtcDateTime;

            // ✅ HARD GUARD: reject truly past tasks
            if (dueUtc < DateTime.UtcNow.AddMinutes(-2))
                return BadRequest(new { message = "Due time must be in the future." });

            var task = new Task
            {
                Title = dto.Title,
                Description = dto.Description,
                //DueDate = dto.DueDate,
                DueDate = dueUtc,
                Priority = dto.Priority,
                UserId = userId,
                Completed = false,

                //// ✅ Default duration saved in DB
                //EstimatedMinutes = dto.EstimatedMinutes.HasValue && dto.EstimatedMinutes.Value > 0
                //? dto.EstimatedMinutes.Value
                //: 30,

                // ✅ STEP 11.2
                NudgeAtUtc = CalcNudgeAtUtc(dueUtc),
                NudgeSentAtUtc = null,
                LastNudgeError = null
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(task);
        }

        //// ✅ Get all /api/tasks or by due date /api/tasks?due=2025-10-13
        //[HttpGet]
        //public async Task<IActionResult> GetTasks([FromQuery] DateTime? due)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        //                 ?? User.FindFirst("id")?.Value;

        //    if (string.IsNullOrEmpty(userId))
        //        return Unauthorized();

        //    var query = _context.Tasks
        //        .Where(t => t.UserId == userId)
        //        .AsQueryable();

        //    if (due.HasValue)
        //        query = query.Where(t => t.DueDate.Date == due.Value.Date);

        //    var tasks = await query
        //        .OrderBy(t => t.DueDate)
        //        .ThenByDescending(t => t.Priority)
        //        .ToListAsync();

        //    return Ok(tasks);
        //}

        // ✅ Get all /api/tasks or by due date /api/tasks?due=2025-10-13
        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] string? due)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var query = _context.Tasks
                .Where(t => t.UserId == userId)
                .AsQueryable();

            // ✅ IST offset for now (later: per-user timezone)
            var istOffset = TimeSpan.FromMinutes(330);

            if (!string.IsNullOrWhiteSpace(due))
            {
                // due is expected as "yyyy-MM-dd"
                if (!DateTime.TryParse(due, out var dueDate))
                    return BadRequest(new { message = "Invalid due. Use yyyy-MM-dd." });

                // treat as DATE only
                var istDay = dueDate.Date;

                // IST day window -> UTC window
                var istStartLocal = DateTime.SpecifyKind(istDay, DateTimeKind.Unspecified);
                var istEndLocal = DateTime.SpecifyKind(istDay.AddDays(1), DateTimeKind.Unspecified);

                var startUtc = new DateTimeOffset(istStartLocal, istOffset).UtcDateTime;
                var endUtc = new DateTimeOffset(istEndLocal, istOffset).UtcDateTime;

                query = query.Where(t => t.DueDate >= startUtc && t.DueDate < endUtc);
            }

            var tasks = await query
                .OrderBy(t => t.DueDate)
                .ThenByDescending(t => t.Priority)
                .ToListAsync();

            return Ok(tasks);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTask(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Find the task that belongs to the current user
            var task = await _context.Tasks
                .Where(t => t.UserId == userId && t.Id == id)
                .FirstOrDefaultAsync();

            if (task == null)
                return NotFound(new { message = "Task not found or access denied." });

            return Ok(task);
        }
        //// PUT: api/tasks/{id}
        //[HttpPut("{id}")]
        //public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDto dto)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        //         ?? User.FindFirst("id")?.Value;

        //    if (string.IsNullOrEmpty(userId))
        //        return Unauthorized();

        //    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        //    if (task == null) return NotFound();

        //    var oldDue = task.DueDate;

        //    task.Title = dto.Title;
        //    task.Description = dto.Description;
        //    //task.DueDate = dto.DueDate;
        //    var newDueUtc = dto.DueDate.UtcDateTime;
        //    task.DueDate = newDueUtc;
        //    task.Priority = dto.Priority;
        //    task.Completed = dto.Completed;

        //    // ✅ If due date changed OR user re-opened task, recompute nudge & allow sending again
        //    var dueChanged = oldDue != newDueUtc;

        //    if (dueChanged)
        //    {
        //        task.NudgeAtUtc = CalcNudgeAtUtc(newDueUtc);
        //        task.NudgeSentAtUtc = null;      // allow sending again for the new schedule
        //        task.LastNudgeError = null;
        //    }

        //    // ✅ If user marks completed, stop future nudges
        //    if (task.Completed)
        //    {
        //        task.NudgeSentAtUtc = DateTime.UtcNow; // mark as "done" to prevent sending
        //        task.LastNudgeError = null;
        //    }
        //    else
        //    {
        //        // if user re-opens a task (Completed=false), ensure nudge exists
        //        task.NudgeAtUtc ??= CalcNudgeAtUtc(task.DueDate);
        //        // do NOT force NudgeSentAtUtc null here unless due changed
        //    }

        //    await _context.SaveChangesAsync();
        //    return Ok(task);
        //}

        // PUT: api/tasks/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (!HasOffsetInJson(dto.DueDate))
                return BadRequest(new { message = "DueDate must include timezone offset (Z or +05:30)." });

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (task == null) return NotFound();

            var oldDue = task.DueDate;
            var newDueUtc = dto.DueDate.UtcDateTime;

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.DueDate = newDueUtc;
            task.Priority = dto.Priority;
            task.Completed = dto.Completed;

            //task.EstimatedMinutes = dto.EstimatedMinutes.HasValue && dto.EstimatedMinutes.Value > 0
            //                        ? dto.EstimatedMinutes.Value
            //                        : (task.EstimatedMinutes ?? 30);

            var dueChanged = oldDue != newDueUtc;

            if (dueChanged)
            {
                task.NudgeAtUtc = CalcNudgeAtUtc(newDueUtc);
                task.NudgeSentAtUtc = null;
                task.LastNudgeError = null;
            }

            if (task.Completed)
            {
                // stop future nudges
                task.NudgeAtUtc = null;
                task.NudgeSentAtUtc = DateTime.UtcNow;
                task.LastNudgeError = null;
            }
            else
            {
                // ensure nudge exists
                task.NudgeAtUtc ??= CalcNudgeAtUtc(task.DueDate);
            }

            await _context.SaveChangesAsync();
            return Ok(task);
        }

        //// DELETE: api/tasks/{id}
        //[HttpDelete("{id}")]
        //public async Task<IActionResult> DeleteTask(int id)
        //{
        //    var userId = User.FindFirst("id")?.Value; // from JWT (string)

        //    if (userId == null)
        //        return Unauthorized();

        //    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        //    if (task == null) return NotFound();

        //    _context.Tasks.Remove(task);
        //    await _context.SaveChangesAsync();

        //    return NoContent();
        //}

        // DELETE: api/tasks/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Load the task first (we need its due date day)
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null)
                return NotFound();

            // ✅ Task due date is stored as UTC in your DB (timestamptz)
            // We will delete the plan for the SAME UTC day as the task due date.
            var dayUtc = DateTime.SpecifyKind(task.DueDate.Date, DateTimeKind.Utc);

            await using var tx = await _context.Database.BeginTransactionAsync();

            // 1) Delete the task
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            // 2) Delete the plan for that day (if exists)
            // Because DailyPlan -> DailyPlanItems is Cascade delete, items will delete automatically.
            var plan = await _context.DailyPlans
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Date == dayUtc);

            if (plan != null)
            {
                _context.DailyPlans.Remove(plan);
                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();

            return NoContent();
        }

        private static DateTime? CalcNudgeAtUtc(DateTime dueUtc, int leadMinutes = 10)
        {
            var nowUtc = DateTime.UtcNow;

            var grace = TimeSpan.FromMinutes(2);

            if (dueUtc <= nowUtc)
            {
                if (nowUtc - dueUtc <= grace)
                    return nowUtc.AddSeconds(30);   // soft nudge

                return null; // truly old
            }

            var target = dueUtc.AddMinutes(-leadMinutes);

            var minAllowed = nowUtc.AddSeconds(30);
            if (target <= minAllowed)
                return minAllowed;

            return target;
        }

        private static bool HasOffsetInJson(DateTimeOffset dtoDueDate)
        {
            // DateTimeOffset always has an Offset, but if client sent "no offset",
            // model binding may assume server local offset, which we don’t want.
            // So we guard using a stricter approach: require client to send UTC (Offset=0)
            // OR IST offset (+05:30). Otherwise reject.
            return dtoDueDate.Offset == TimeSpan.Zero || dtoDueDate.Offset == TimeSpan.FromMinutes(330);
        }
    }
}
