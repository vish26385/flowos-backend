using FlowOS.Api.DTOs;
using FlowOS.Api.Models;
using FlowOS.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
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

        public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config, TokenService tokenService)
        {
            _userManager = userManager;
            _config = config;
            _tokenService = tokenService;
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

            // Store refresh token (temporary memory for demo — use DB in production)
            _refreshTokens[refreshToken] = user.Email;

            return Ok(new { token, refreshToken });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !(await _userManager.CheckPasswordAsync(user, dto.Password)))
                return Unauthorized(new { message = "Invalid credentials" });

            var token = await _tokenService.GenerateJwtTokenAsync(user);
            var refreshToken = await _tokenService.GenerateRefreshTokenAsync();
            _refreshTokens[refreshToken] = dto.Email;

            return Ok(new { token, refreshToken });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
        {
            if (!_refreshTokens.TryGetValue(dto.RefreshToken, out var email))
                return Unauthorized(new { message = "Invalid or expired refresh token" });

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return Unauthorized(new { message = "User not found" });

            // 🔄 Generate new tokens
            var newJwt = await _tokenService.GenerateJwtTokenAsync(user);
            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync();

            // Replace old token
            _refreshTokens.Remove(dto.RefreshToken);
            _refreshTokens[newRefreshToken] = email;

            return Ok(new { token = newJwt, refreshToken = newRefreshToken });
        }
    }
}
