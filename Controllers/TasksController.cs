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
            var userId = User.FindFirst("id")?.Value; // from JWT (string)

            if (userId == null)
                return Unauthorized();

            var task = new Task
            {
                Title = dto.Title,
                Description = dto.Description,
                DueDate = dto.DueDate,
                Priority = dto.Priority,
                UserId = userId,
                Completed = false
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
        // PUT: api/tasks/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] TaskUpdateDto dto)
        {
            var userId = User.FindFirst("id")?.Value; // from JWT (string)

            if (userId == null)
                return Unauthorized();

            var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null) return NotFound();

            task.Title = dto.Title;
            task.Description = dto.Description;
            task.DueDate = dto.DueDate;
            task.Priority = dto.Priority;
            task.Completed = dto.Completed;

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
    }
}
