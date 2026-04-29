using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AirrostiDemo.Shared.Auth;
using Microsoft.JSInterop;

namespace AirrostiDemo.Services
{
    /// <summary>
    /// Client-side façade for everything authentication-related. Wraps the
    /// register / login / logout HTTP calls, persists the resulting token to
    /// <c>localStorage</c>, and broadcasts auth-state changes to interested
    /// components via <see cref="AuthStateChanged"/>.
    /// </summary>
    /// <remarks>
    /// This is the only place that reads or writes the auth-related
    /// localStorage keys; <see cref="AuthHeaderHandler"/> reads
    /// <see cref="TokenStorageKey"/> at request time but doesn't mutate it.
    /// Keeping that split means future changes to how the token is persisted
    /// (e.g. moving to <c>sessionStorage</c> or an in-memory cache) only
    /// touch this class.
    /// </remarks>
    public class AuthService
    {
        /// <summary>localStorage key holding the raw JWT.</summary>
        public const string TokenStorageKey = "airrosti_token";

        /// <summary>localStorage key holding the absolute expiry as ISO-8601.</summary>
        public const string ExpiresStorageKey = "airrosti_expires";

        /// <summary>localStorage key holding the user's email for UI display.</summary>
        public const string EmailStorageKey = "airrosti_email";

        private readonly HttpClient _http;
        private readonly IJSRuntime _js;
        private bool _initialized;

        /// <summary>
        /// Standard DI constructor. The injected <see cref="HttpClient"/> is
        /// the named "AirrostiApi" client that already has
        /// <see cref="AuthHeaderHandler"/> in its pipeline — useful for
        /// future endpoints, though register/login themselves don't require
        /// auth.
        /// </summary>
        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        /// <summary>
        /// Fired whenever the user signs in or signs out. Components like
        /// the nav bar subscribe to refresh themselves without polling.
        /// </summary>
        public event Action? AuthStateChanged;

        /// <summary>
        /// The currently-signed-in user's email, or <c>null</c> when nobody
        /// is signed in. Cached locally for cheap, sync access from the UI.
        /// </summary>
        public string? CurrentEmail { get; private set; }

        /// <summary>
        /// POSTs to <c>/api/Auth/register</c> and returns a tuple describing
        /// the outcome. We don't auto-login here — the caller decides whether
        /// to immediately follow up with <see cref="LoginAsync"/>.
        /// </summary>
        public async Task<(bool Ok, string? Error)> RegisterAsync(RegisterDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/Auth/register", dto);
            if (resp.IsSuccessStatusCode) return (true, null);
            var msg = await ReadErrorAsync(resp);
            return (false, msg);
        }

        /// <summary>
        /// POSTs to <c>/api/Auth/login</c>, persists the returned token and
        /// expiry to <c>localStorage</c>, and fires
        /// <see cref="AuthStateChanged"/>. Returns a friendly error string on
        /// failure (we explicitly translate 401 into "Invalid email or
        /// password" rather than dumping the raw response body).
        /// </summary>
        public async Task<(bool Ok, string? Error)> LoginAsync(LoginDto dto)
        {
            var resp = await _http.PostAsJsonAsync("api/Auth/login", dto);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // The server intentionally returns a flat 401 with no
                    // body to avoid leaking which credential was wrong.
                    return (false, "Invalid email or password.");
                }
                return (false, await ReadErrorAsync(resp));
            }

            // Deserialize the auth payload (token + expiry). A null body
            // here would mean the server returned 2xx with no content,
            // which would be a server-side bug worth surfacing.
            var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null || string.IsNullOrEmpty(auth.Token))
            {
                return (false, "Empty response from server.");
            }

            // Persist the token and expiry to localStorage so the session
            // survives a page refresh. ISO-8601 ("o" format) round-trips
            // cleanly through DateTimeOffset.TryParse on the way back out.
            await _js.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, auth.Token);
            await _js.InvokeVoidAsync(
                "localStorage.setItem",
                ExpiresStorageKey,
                auth.ExpiresAt.ToString("o"));
            await _js.InvokeVoidAsync("localStorage.setItem", EmailStorageKey, dto.Email);

            // Update in-memory state and fan out the change to subscribers
            // (nav bar, page components) so they re-render immediately.
            CurrentEmail = dto.Email;
            _initialized = true;
            AuthStateChanged?.Invoke();
            return (true, null);
        }

        /// <summary>
        /// Clears all auth-related state — both the persisted localStorage
        /// keys and the in-memory <see cref="CurrentEmail"/> — and notifies
        /// subscribers. Safe to call when nobody is signed in.
        /// </summary>
        public async Task LogoutAsync()
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", ExpiresStorageKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", EmailStorageKey);
            CurrentEmail = null;
            _initialized = true;
            AuthStateChanged?.Invoke();
        }

        /// <summary>
        /// Returns the persisted JWT, or <c>null</c> if none is present or
        /// the stored expiry has already passed. As a side-effect, an
        /// expired token triggers a logout so the UI updates.
        /// </summary>
        public async Task<string?> GetTokenAsync()
        {
            await EnsureInitializedAsync();

            var token = await _js.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey);
            if (string.IsNullOrEmpty(token)) return null;

            // Pre-emptive expiry check: rather than wait for the next API
            // call to come back with a 401, we look at the persisted expiry
            // and self-destruct the session if we're already past it.
            var expRaw = await _js.InvokeAsync<string?>("localStorage.getItem", ExpiresStorageKey);
            if (DateTimeOffset.TryParse(expRaw, out var exp) && exp <= DateTimeOffset.UtcNow)
            {
                await LogoutAsync();
                return null;
            }
            return token;
        }

        /// <summary>
        /// Convenience boolean that simply asks "is there a non-expired
        /// token?". Built on top of <see cref="GetTokenAsync"/> so the two
        /// can never disagree.
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync() => await GetTokenAsync() is not null;

        /// <summary>
        /// Lazy first-access initialization that pulls
        /// <see cref="CurrentEmail"/> back out of localStorage on a fresh
        /// page load. We intentionally don't do this in the constructor
        /// because Blazor WebAssembly doesn't allow JS interop during the
        /// pre-rendered constructor phase.
        /// </summary>
        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            CurrentEmail = await _js.InvokeAsync<string?>("localStorage.getItem", EmailStorageKey);
            _initialized = true;
        }

        /// <summary>
        /// Best-effort error-message extraction for a non-success
        /// <see cref="HttpResponseMessage"/>. Falls back through several
        /// layers — ProblemDetails fields, raw body, status code — so the
        /// UI can always show *something* useful.
        /// </summary>
        private static async Task<string> ReadErrorAsync(HttpResponseMessage resp)
        {
            string body;
            try
            {
                body = await resp.Content.ReadAsStringAsync();
            }
            catch
            {
                // If even reading the body fails (network glitch, disposed
                // content) we surface the status code so the user sees
                // something more specific than a blank error.
                return $"HTTP {(int)resp.StatusCode}";
            }

            if (string.IsNullOrWhiteSpace(body)) return $"HTTP {(int)resp.StatusCode}";

            var pretty = TryExtractProblemMessage(body);
            return pretty ?? body;
        }

        /// <summary>
        /// Pulls the most useful human-readable message out of an ASP.NET
        /// Core ProblemDetails / ValidationProblemDetails JSON body:
        /// preferring the first field-level error, then <c>detail</c>, then
        /// <c>title</c>. Returns <c>null</c> if the body isn't a JSON object
        /// or none of those fields are present, in which case the caller
        /// falls back to the raw body.
        /// </summary>
        private static string? TryExtractProblemMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                // ValidationProblemDetails: errors is an object whose values
                // are arrays of strings. Surface the first non-empty entry —
                // it's almost always the most actionable message.
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

                // ProblemDetails.detail — usually a longer human message.
                if (root.TryGetProperty("detail", out var detail) &&
                    detail.ValueKind == JsonValueKind.String)
                {
                    var s = detail.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }

                // ProblemDetails.title — the short label (e.g. "Bad Request").
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
