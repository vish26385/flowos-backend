using FlowOS.Api.Data;
using FlowOS.Api.DTOs;
using FlowOS.Api.Models;
using FlowOS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FlowOS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private static readonly Dictionary<string, string> _refreshTokens = new(); // demo in-memory store
        private readonly TokenService _tokenService;
        private readonly FlowOSContext _context;
        private readonly IEmailService _emailService;

        public AuthController(UserManager<ApplicationUser> userManager, 
                              IConfiguration config, 
                              TokenService tokenService, 
                              FlowOSContext context,
                              IEmailService emailService
                              )
        {
            _userManager = userManager;
            _config = config;
            _tokenService = tokenService;
            _context = context;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 🧠 Create the new Identity user
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FullName = dto.FullName
            };

            // ✅ Save user to the Identity store
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // 🧩 Generate JWT + Refresh Token for the newly registered user
            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Save refresh token to DB
            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // valid for 7 days
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(userRefresh);
            await _context.SaveChangesAsync();

            return Ok(new { token, refreshToken });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
                return Unauthorized(new { message = "Invalid credentials" });

            // ✅ Generate tokens
            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Save refresh token to DB
            var userRefresh = new UserRefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // valid for 7 days
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(userRefresh);
            await _context.SaveChangesAsync();


            // ✅ Return all together
            return Ok(new
            {
                token,
                refreshToken,
                user = new
                {
                    user.Id,
                    user.Email,
                    user.UserName
                }
            });
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenRefreshDto dto)
        {
            if (string.IsNullOrEmpty(dto.RefreshToken))
                return BadRequest(new { message = "Refresh token is required" });

            var storedRefresh = await _context.UserRefreshTokens
                 .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (storedRefresh == null)
                return Unauthorized(new { message = "Invalid refresh token" });

            if (!storedRefresh.IsActive)
                return Unauthorized(new { message = "Token expired or revoked" });

            var user = await _userManager.FindByIdAsync(storedRefresh.UserId);
            if (user == null)
                return Unauthorized(new { message = "User not found" });

            // 🔄 Generate new tokens
            var newJwt = await _tokenService.GenerateJwtTokenAsync(user);
            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Revoke old one and add new record
            storedRefresh.RevokedAt = DateTime.UtcNow;
            var newRecord = new UserRefreshToken
            {
                UserId = user.Id,
                Token = newRefreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserRefreshTokens.Add(newRecord);
            await _context.SaveChangesAsync();

            return Ok(new { token = newJwt, refreshToken = newRefreshToken });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]        
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Ok(new { message = "If the email exists, a reset link has been sent." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Build reset link
            var resetLink = $"{_config["ClientBaseUrl"]}/reset-password?email={dto.Email}&token={Uri.EscapeDataString(token)}";

            var subject = "FlowOS Password Reset";
            var body = $@"
                        <h2>Reset your FlowOS password</h2>
                        <p>Click the link below to reset your password:</p>
                        <a href='{resetLink}'>{resetLink}</a>
                        <br/><br/>
                        <p>If you didn’t request this, please ignore this email.</p>";

            await _emailService.SendEmailAsync(dto.Email, subject, body);

            return Ok(new { message = "Password reset link sent if email exists." });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid email" });

            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Optional: revoke refresh tokens if you want to invalidate old sessions
            var tokens = _context.UserRefreshTokens.Where(t => t.UserId == user.Id && t.RevokedAt == null);
            foreach (var t in tokens) t.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successful." });
        }

        // ✅ GET /auth/me
        [HttpGet("me")]
        public IActionResult Me()
        {
            // Extract user data from JWT claims
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var name = User.Identity?.Name ?? "Unknown";
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var id = idClaim != null ? int.Parse(idClaim) : 0;

            return Ok(new
            {
                id,
                name,
                email
            });
        }

        [HttpGet("env-check")]
        public IActionResult GetEnvCheck()
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var key = _config["Jwt:Key"]?.Substring(0, 10);
            var conn = _config.GetConnectionString("DefaultConnection");

            return Ok(new
            {
                environment = env,
                jwtKeyStart = key,
                connection = conn
            });
        }
    }
}
