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

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config, TokenService tokenService, FlowOSContext context)
        {
            _userManager = userManager;
            _config = config;
            _tokenService = tokenService;
            _context = context;
        }

        //[HttpPost("register")]
        //public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        //{
        //    var user = new ApplicationUser { UserName = dto.Email, Email = dto.Email, FullName = dto.FullName };
        //    var result = await _userManager.CreateAsync(user, dto.Password);

        //    if (!result.Succeeded)
        //        return BadRequest(result.Errors);

        //    return Ok(new { message = "User registered successfully" });
        //}

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginDto dto)
        //{
        //    var user = await _userManager.FindByEmailAsync(dto.Email);
        //    if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
        //        return Unauthorized();

        //    var claims = new[]
        //    {
        //        new Claim("id", user.Id),                      // 👈 add this line
        //        new Claim(ClaimTypes.Name, user.UserName ?? ""),
        //        new Claim(ClaimTypes.Email, user.Email ?? "")
        //    };

        //    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
        //    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        //    var token = new JwtSecurityToken(
        //        claims: claims,
        //        expires: DateTime.Now.AddDays(7),
        //        signingCredentials: creds);

        //    return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        //}

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

        //[HttpPost("login")]
        //public async Task<IActionResult> Login([FromBody] LoginDto dto)
        //{
        //    var user = await _userManager.FindByEmailAsync(dto.Email);
        //    if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
        //        return Unauthorized(new { message = "Invalid credentials" });

        //    var token = await _tokenService.GenerateJwtTokenAsync(user);
        //    var refreshToken = await _tokenService.GenerateRefreshTokenAsync();
        //    _refreshTokens[refreshToken] = dto.Email;

        //    return Ok(new { token, refreshToken });
        //}

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


        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] TokenRefreshDto dto)
        {
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
    }
}
