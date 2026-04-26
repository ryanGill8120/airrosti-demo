using AirrostiDemo.Server.Data;
using AirrostiDemo.Server.Services;
using AirrostiDemo.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AirrostiDemo.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _users;
        private readonly JwtTokenService _jwt;

        public AuthController(UserManager<AppUser> users, JwtTokenService jwt)
        {
            _users = users;
            _jwt = jwt;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var existing = await _users.FindByEmailAsync(dto.Email);
            if (existing is not null)
            {
                ModelState.AddModelError(nameof(dto.Email), "Email already registered.");
                return ValidationProblem(ModelState);
            }

            var user = new AppUser { UserName = dto.Email, Email = dto.Email };
            var result = await _users.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError(err.Code, err.Description);
                }
                return ValidationProblem(ModelState);
            }

            return Ok();
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var user = await _users.FindByEmailAsync(dto.Email);
            if (user is null) return Unauthorized();

            if (!await _users.CheckPasswordAsync(user, dto.Password))
            {
                return Unauthorized();
            }

            return Ok(_jwt.Create(user));
        }
    }
}
