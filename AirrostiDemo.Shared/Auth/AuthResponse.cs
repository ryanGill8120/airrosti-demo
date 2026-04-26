namespace AirrostiDemo.Shared.Auth
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }
    }
}
