using AirrostiDemo.Server.Data;
using AirrostiDemo.Server.Services;
using AirrostiDemo.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AirrostiDemo.Server.Controllers
{
    /// <summary>
    /// Endpoints for creating an account and exchanging credentials for a
    /// JWT. Both routes are explicitly anonymous so the user can call them
    /// before they have a token.
    /// </summary>
    /// <remarks>
    /// We use <see cref="UserManager{TUser}"/> directly (no
    /// <c>SignInManager</c>) because this is a JWT-only API surface — there
    /// is no cookie auth flow to drive, and SignInManager would only add a
    /// dependency on the cookie scheme we deliberately don't register in
    /// <c>Program.cs</c>.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly JwtTokenService _jwt;

        /// <summary>
        /// Constructor: pulls in Identity's <see cref="UserManager{TUser}"/>
        /// for credential operations, and our own
        /// <see cref="JwtTokenService"/> for minting the bearer token on
        /// successful login.
        /// </summary>
        public AuthController(UserManager<AppUser> users, JwtTokenService jwt)
        {
            _users = users;
            _jwt = jwt;
        }

        /// <summary>
        /// Creates a new local account. Returns 200 on success, or a
        /// validation problem with field-level errors when the email is
        /// already taken or the password is rejected by Identity's policy.
        /// </summary>
        /// <param name="dto">Registration credentials. Validated by the
        /// data-annotation attributes on <see cref="RegisterDto"/>.</param>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // Bail on annotation-level errors (email format, min length, etc.)
            // before touching the database.
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            // Pre-check for an existing email so we can return a clean
            // "Email already registered" instead of one of Identity's
            // generic duplicate-username errors.
            var existing = await _users.FindByEmailAsync(dto.Email);
            if (existing is not null)
            {
                ModelState.AddModelError(nameof(dto.Email), "Email already registered.");
                return ValidationProblem(ModelState);
            }

            // Use the email as both the username and the email — we don't
            // currently expose a separate display name. Identity will hash
            // the password (PBKDF2) before persisting it.
            var user = new AppUser { UserName = dto.Email, Email = dto.Email };
            var result = await _users.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                // Translate any Identity-policy violations into per-field
                // model errors so the client can show them inline next to
                // the offending input.
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError(err.Code, err.Description);
                }
                return ValidationProblem(ModelState);
            }

            return Ok();
        }

        /// <summary>
        /// Verifies email + password and, on success, returns a freshly-minted
        /// JWT plus its expiry. On any failure we return a flat 401 with no
        /// body — distinguishing "no such user" from "wrong password" would
        /// help an attacker enumerate valid emails.
        /// </summary>
        /// <param name="dto">Login credentials.</param>
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            // Look up the user by email. We deliberately return the same
            // 401 for missing user and bad password so the response is
            // useless for account enumeration.
            var user = await _users.FindByEmailAsync(dto.Email);
            if (user is null) return Unauthorized();

            // CheckPasswordAsync runs the supplied plaintext through the
            // same hash function Identity used at registration and compares
            // — never a clear-text comparison.
            if (!await _users.CheckPasswordAsync(user, dto.Password))
            {
                return Unauthorized();
            }

            // Mint a JWT for this user via the dedicated service. This is
            // the ONLY place tokens are produced — keeping the claim shape
            // and signing config in one spot.
            return Ok(_jwt.Create(user));
        }
    }
}
