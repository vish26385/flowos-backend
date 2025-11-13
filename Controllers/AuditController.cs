using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditQueryService _auditService;

    public AuditController(IAuditQueryService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent()
    {
        var audits = await _auditService.GetRecentAsync();
        return Ok(audits);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetForUser(string userId)
    {
        var audits = await _auditService.GetByUserAsync(userId);
        return Ok(audits);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await _auditService.GetStatsAsync();
        return Ok(stats);
    }
}
