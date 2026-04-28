using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AirrostiDemo.Shared.Auth;
using Microsoft.JSInterop;

namespace AirrostiDemo.Services
{
    public class AuthService
    {
        public const string TokenStorageKey = "airrosti_token";
        public const string ExpiresStorageKey = "airrosti_expires";
        public const string EmailStorageKey = "airrosti_email";

        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private bool _initialized;

        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public event Action? AuthStateChanged;

        public string? CurrentEmail { get; private set; }

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
            await _js.InvokeVoidAsync("localStorage.setItem", EmailStorageKey, dto.Email);

            CurrentEmail = dto.Email;
            _initialized = true;
            AuthStateChanged?.Invoke();
            return (true, null);
        }

        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", ExpiresStorageKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", EmailStorageKey);
            CurrentEmail = null;
            _initialized = true;
            AuthStateChanged?.Invoke();
        }

        public async Task<string?> GetTokenAsync()
        {
            await EnsureInitializedAsync();

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

        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            CurrentEmail = await _js.InvokeAsync<string?>("localStorage.getItem", EmailStorageKey);
            _initialized = true;
        }

        private static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
        {
            string body;
            try
            {
                body = await resp.Content.ReadAsStringAsync();
            }
            catch
            {
                return $"HTTP {(int)resp.StatusCode}";
            }

            if (string.IsNullOrWhiteSpace(body)) return $"HTTP {(int)resp.StatusCode}";

            var pretty = TryExtractProblemMessage(body);
            return pretty ?? body;
        }

        private static string? TryExtractProblemMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                if (root.TryGetProperty("errors", out var errors) &&
                    errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var msg in field.Value.EnumerateArray())
                            {
                                var s = msg.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) return s;
                            }
                        }
                    }
                }

                if (root.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.String)
                {
                    var s = detail.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }

                if (root.TryGetProperty("title", out var title) &&
                    title.ValueKind == JsonValueKind.String)
                {
                    var s = title.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            catch (JsonException)
            {
                // not JSON — caller falls back to raw body
            }
            return null;
        }
    }
}
