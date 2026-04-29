using System.ComponentModel.DataAnnotations;

namespace AirrostiDemo.Shared.Auth
{
    /// <summary>
    /// Request body posted by the client to <c>POST /api/Auth/login</c>. On
    /// success the server replies with an <see cref="AuthResponse"/> carrying
    /// a freshly-minted JWT.
    /// </summary>
    /// <remarks>
    /// Note the deliberate asymmetry with <see cref="RegisterDto"/>: login
    /// does not enforce <c>MinLength</c> on the password, because we want
    /// uniform "Invalid email or password" messaging regardless of length and
    /// don't want to leak that the policy exists at all.
    /// </remarks>
    public class LoginDto
    {
        /// <summary>
        /// The email the user originally registered with. Looked up via
        /// <c>UserManager.FindByEmailAsync</c> on the server side.
        /// </summary>
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Plaintext password supplied by the user. The server compares it
        /// against the stored hash via <c>UserManager.CheckPasswordAsync</c>;
        /// it is never stored or logged.
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
