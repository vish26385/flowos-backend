using FlowOS.Api.Data;
using FlowOS.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace FlowOS.Api.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;

        public TokenService(IConfiguration config, UserManager<ApplicationUser> userManager)
        {
            _config = config;
            _userManager = userManager;
        }

        /// <summary>
        /// Generates a JWT token for the specified user.
        /// Includes user ID, username, and email as claims.
        /// </summary>
        public async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var jwtSection = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim("id", user.Id),                                   // your custom claim
                new Claim("name", user.UserName ?? string.Empty),
                new Claim("email", user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // ADD ROLES INSIDE JWT
            var roles = await _userManager.GetRolesAsync(user);

            claims.AddRange(
                roles.Select(r => new Claim("role", r))
            );

            var token = new JwtSecurityToken(
                issuer: jwtSection["Issuer"],
                audience: jwtSection["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return await Task.FromResult(tokenString);
        }

        /// <summary>
        /// Generates a refresh token for the user.
        /// </summary>
        public async Task<string> GenerateRefreshTokenAsync()
        {
            var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            return await Task.FromResult(refreshToken);
        }
    }
}
