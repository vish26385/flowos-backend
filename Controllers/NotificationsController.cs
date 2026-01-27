using FlowOS.Api.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto)
    {
        var userId = User.FindFirstValue("id");
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.PushToken))
            return BadRequest("PushToken is required.");

        await _notificationService.RegisterDeviceAsync(userId, dto.PushToken!, dto.Platform ?? "unknown");
        return Ok(new { message = "Device token registered." });
    }
}

public class RegisterDeviceDto
{
    public string? PushToken { get; set; }
    public string? Platform { get; set; }
}
