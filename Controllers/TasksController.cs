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

            // IMPORTANT: dto.DueDate should already come in as UTC from mobile
            // but we still force UTC Kind to avoid Npgsql issues
            var dueUtc = dto.DueDate.UtcDateTime;

            var task = new Task
            {
                Title = dto.Title,
                Description = dto.Description,
                //DueDate = dto.DueDate,
                DueDate = dueUtc,
                Priority = dto.Priority,
                UserId = userId,
                Completed = false,

                // ✅ STEP 11.2
                NudgeAtUtc = CalcNudgeAtUtc(dueUtc),
                NudgeSentAtUtc = null,
                LastNudgeError = null
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            return Ok(task);
        }

        //// GET: api/tasks
        //[HttpGet("Get")]
        //public async Task<IActionResult> GetTasks()
        //{
        //    var userId = User.FindFirst("id")?.Value; // from JWT (string)

        //    if (userId == null)
        //        return Unauthorized();

        //    var tasks = await _context.Tasks
        //        .Where(t => t.UserId == userId)
        //        .ToListAsync();
        //    return Ok(tasks);
        //}        

        // ✅ Get all /api/tasks or by due date /api/tasks?due=2025-10-13
        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] DateTime? due)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirst("id")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var query = _context.Tasks
                .Where(t => t.UserId == userId)
                .AsQueryable();

            if (due.HasValue)
                query = query.Where(t => t.DueDate.Date == due.Value.Date);

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

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
            if (task == null) return NotFound();

            var oldDueUtc = task.DueDate;
            var oldCompleted = task.Completed;

            // ✅ Convert DTO -> UTC DateTime for timestamptz
            var newDueUtc = dto.DueDate.UtcDateTime;

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.DueDate = newDueUtc;
            task.Priority = dto.Priority;
            task.Completed = dto.Completed;

            var dueChanged = oldDueUtc != newDueUtc;
            var reopened = oldCompleted == true && task.Completed == false;

            var nowUtc = DateTime.UtcNow;

            // ✅ If completed => stop nudges completely
            if (task.Completed)
            {
                task.NudgeAtUtc = null;         // ✅ stop future nudges
                task.LastNudgeError = null;
                // keep NudgeSentAtUtc as-is (don’t fake "sent now")
            }
            else
            {
                // ✅ If due changed OR task reopened => reschedule and allow send again
                if (dueChanged || reopened)
                {
                    var nudgeAt = CalcNudgeAtUtc(newDueUtc);

                    // Optional safety: avoid "past" nudges causing instant spam
                    if (nudgeAt < nowUtc) nudgeAt = nowUtc;

                    task.NudgeAtUtc = nudgeAt;
                    task.NudgeSentAtUtc = null;   // ✅ allow sending again
                    task.LastNudgeError = null;
                }
                else
                {
                    // If unchanged and no nudge set yet, set it once
                    if (task.NudgeAtUtc == null)
                    {
                        var nudgeAt = CalcNudgeAtUtc(task.DueDate);
                        if (nudgeAt < nowUtc) nudgeAt = nowUtc;
                        task.NudgeAtUtc = nudgeAt;
                    }
                    // don't touch NudgeSentAtUtc here
                }
            }

            await _context.SaveChangesAsync();
            return Ok(task);
        }

        // DELETE: api/tasks/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var userId = User.FindFirst("id")?.Value; // from JWT (string)

            if (userId == null)
                return Unauthorized();

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null) return NotFound();

            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private const int DefaultNudgeMinutesBefore = 10;

        private static DateTime CalcNudgeAtUtc(DateTime dueUtc)
        {
            var nudge = dueUtc.AddMinutes(-DefaultNudgeMinutesBefore);

            var nowUtc = DateTime.UtcNow;

            // If calculated nudge time is already in past → send ASAP
            if (nudge < nowUtc)
                return nowUtc;

            return nudge;
        }
    }
}
