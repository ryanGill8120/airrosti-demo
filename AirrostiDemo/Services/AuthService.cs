using System.Net;
using System.Net.Http.Json;
using AirrostiDemo.Shared.Auth;
using Microsoft.JSInterop;

namespace AirrostiDemo.Services
{
    public class AuthService
    {
        public const string TokenStorageKey = "airrosti_token";
        public const string ExpiresStorageKey = "airrosti_expires";

        private readonly HttpClient _http;
        private readonly IJSRuntime _js;

        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public event Action? AuthStateChanged;

        public async Task<(bool Ok, string? Error)> RegisterAsync(RegisterDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/Auth/register", dto);
            if (resp.IsSuccessStatusCode) return (true, null);
            var msg = await ReadErrorAsync(resp);
            return (false, msg);
        }

        public async Task<(bool Ok, string? Error)> LoginAsync(LoginDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/Auth/login", dto);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return (false, "Invalid email or password.");
                }
                return (false, await ReadErrorAsync(resp));
            }

            var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null || string.IsNullOrEmpty(auth.Token))
            {
                return (false, "Empty response from server.");
            }

            await _js.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, auth.Token);
            await _js.InvokeVoidAsync(
                "localStorage.setItem",
                ExpiresStorageKey,
                auth.ExpiresAt.ToString("o"));

            AuthStateChanged?.Invoke();
            return (true, null);
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", ExpiresStorageKey);
            AuthStateChanged?.Invoke();
        }

        public async Task<string?> GetTokenAsync()
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
            if (string.IsNullOrEmpty(token)) return null;

            var expRaw = await _js.InvokeAsync<string?>("localStorage.getItem", ExpiresStorageKey);
            if (DateTimeOffset.TryParse(expRaw, out var exp) && exp <= DateTimeOffset.UtcNow)
            {
                await LogoutAsync();
                return null;
            }
            return token;
        }

        public async Task<bool> IsAuthenticatedAsync() => await GetTokenAsync() is not null;

        private static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
        {
            try
            {
                var body = await resp.Content.ReadAsStringAsync();
                return string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)resp.StatusCode}" : body;
            }
            catch
            {
                return $"HTTP {(int)resp.StatusCode}";
            }
        }
    }
}
