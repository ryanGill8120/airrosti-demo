using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AirrostiDemo.Server.Data;
using AirrostiDemo.Shared.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AirrostiDemo.Server.Services
{
    /// <summary>
    /// Mints signed JSON Web Tokens for authenticated users. This is the only
    /// place in the codebase that produces a JWT — every controller that
    /// "logs a user in" delegates here, which keeps the claim set, the
    /// expiry, and the signing key in exactly one spot.
    /// </summary>
    /// <remarks>
    /// Pulls <c>Jwt:Key</c>, <c>Jwt:Issuer</c>, <c>Jwt:Audience</c> and
    /// <c>Jwt:ExpiresMinutes</c> from <see cref="IConfiguration"/>. In dev
    /// these come from <c>appsettings.Development.json</c>; in production
    /// Render injects them as environment variables (with the
    /// <c>Jwt__Key</c> double-underscore convention to map onto the
    /// configuration colon).
    /// </remarks>
    public class JwtTokenService
    {
        private readonly IConfiguration _config;

        /// <summary>
        /// DI-friendly constructor. We deliberately resolve the JWT settings
        /// per-call inside <see cref="Create"/> rather than caching them at
        /// construction time so that hot config reloads (e.g. a Render env
        /// var change without a restart) take effect on the next mint.
        /// </summary>
        public JwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Builds and signs a JWT representing the supplied user, and packs
        /// it together with its absolute expiry into the
        /// <see cref="AuthResponse"/> shape that the client expects.
        /// </summary>
        /// <param name="user">The authenticated user — usually fetched by
        /// <see cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/>
        /// after a successful password check.</param>
        /// <returns>A serialized bearer token plus the absolute UTC instant
        /// at which it stops being valid.</returns>
        public AuthResponse Create(AppUser user)
        {
            // Pull JWT settings from configuration each call so live config
            // updates take effect immediately. Missing Key is fatal — refuse
            // to issue an unsigned (or wrongly-signed) token.
            var jwt = _config.GetSection("Jwt");
            var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
            var minutes = int.TryParse(jwt["ExpiresMinutes"], out var m) ? m : 60;

            // The signing credentials: HMAC-SHA256 over the symmetric key
            // bytes. The same key is configured on the JwtBearer validator
            // in Program.cs so produce + validate stay in sync.
            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var expires = DateTimeOffset.UtcNow.AddMinutes(minutes);

            // The claim set carried inside the token. `sub` is the canonical
            // "subject" (the user id), which JwtBearer maps onto
            // ClaimTypes.NameIdentifier on the controller side. `jti` gives
            // each token a unique id which we could later use for revocation
            // lists; we don't use it yet but minting it costs nothing.
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            // Assemble the JWT: standard issuer / audience / expiry header
            // claims plus the user-specific claims above, all signed with
            // the credentials built earlier.
            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                expires: expires.UtcDateTime,
                signingCredentials: creds);

            // Serialize the token into the compact "header.payload.signature"
            // string the client will send back on every request, and pair it
            // with its expiry for the client's session bookkeeping.
            return new AuthResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpiresAt = expires,
            };
        }
    }
}
