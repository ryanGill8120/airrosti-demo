namespace AirrostiDemo.Shared.Auth
{
    /// <summary>
    /// Body returned by <c>POST /api/Auth/login</c> on a successful sign-in.
    /// Contains the bearer token that the client must attach to every
    /// subsequent authenticated request, plus a hint as to when the token
    /// will stop being honoured.
    /// </summary>
    /// <remarks>
    /// The expiry is sent as a <see cref="DateTimeOffset"/> (i.e. it carries
    /// its own UTC offset) so the client can compare it directly against
    /// <c>DateTimeOffset.UtcNow</c> without having to know the server's local
    /// time zone. The client persists both fields to <c>localStorage</c>
    /// (keys <c>airrosti_token</c> / <c>airrosti_expires</c>) so the session
    /// survives a page refresh.
    /// </remarks>
    public class AuthResponse
    {
        /// <summary>
        /// The encoded JWT — three base64url segments separated by dots
        /// (header.payload.signature). The client sends this verbatim in the
        /// <c>Authorization: Bearer</c> header.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Absolute UTC instant at which the token's <c>exp</c> claim will
        /// have passed. Stored on the client so it can pre-emptively log the
        /// user out instead of waiting for the next 401.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
