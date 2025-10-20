using FlowOS.Api.Data;
using FlowOS.Api.Models;
using Microsoft.EntityFrameworkCore;
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

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Generates a JWT token for the specified user.
        /// Includes user ID, username, and email as claims.
        /// </summary>
        public async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("id", user.Id),                                   // your custom claim
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
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
