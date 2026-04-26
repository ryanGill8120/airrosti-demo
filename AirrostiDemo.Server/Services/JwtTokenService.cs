using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AirrostiDemo.Server.Data;
using AirrostiDemo.Shared.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AirrostiDemo.Server.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _config;

        public JwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        public AuthResponse Create(AppUser user)
        {
            var jwt = _config.GetSection("Jwt");
            var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var minutes = int.TryParse(jwt["ExpiresMinutes"], out var m) ? m : 60;

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var expires = DateTimeOffset.UtcNow.AddMinutes(minutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expires.UtcDateTime,
                signingCredentials: creds);

            return new AuthResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expires,
            };
        }
    }
}
