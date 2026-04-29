using System.ComponentModel.DataAnnotations;

namespace AirrostiDemo.Shared.Auth
{
    /// <summary>
    /// Request body posted by the client to <c>POST /api/Auth/register</c> to
    /// create a new local user account. The same DTO is used by the modal
    /// dialog in the Blazor client and by the ASP.NET Core controller, so the
    /// validation attributes here are the single source of truth for both
    /// sides.
    /// </summary>
    /// <remarks>
    /// The data-annotation attributes ([Required], [EmailAddress], [MinLength])
    /// are picked up automatically by the API controller via
    /// <c>ModelState.IsValid</c>; on the client they show up as the same
    /// validation errors the server returns, keeping the UX consistent.
    /// </remarks>
    public class RegisterDto
    {
        /// <summary>
        /// The email address that will become the user's login identifier and
        /// (because Identity is configured with <c>RequireUniqueEmail = true</c>)
        /// also their unique key in the database.
        /// </summary>
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The plaintext password the user is choosing. It is sent over HTTPS,
        /// hashed on the server by ASP.NET Core Identity (PBKDF2 by default),
        /// and never persisted in clear form. The 6-character minimum mirrors
        /// the relaxed-for-demo Identity policy in <c>Program.cs</c>.
        /// </summary>
        [Required, MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }
}
