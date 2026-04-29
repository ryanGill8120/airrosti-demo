using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace AirrostiDemo.Services
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> that transparently attaches the
    /// stored JWT to every outbound request made by the named "AirrostiApi"
    /// HttpClient. Slotted into the client pipeline in <c>Program.cs</c> so
    /// that pages and services can simply <c>@inject HttpClient</c> and not
    /// worry about authentication plumbing.
    /// </summary>
    /// <remarks>
    /// Because Blazor WebAssembly runs in the browser sandbox we can't read
    /// localStorage from C# directly — we have to bounce through
    /// <see cref="IJSRuntime"/>. That's an async call, which is why this
    /// handler overrides the async <c>SendAsync</c> rather than the
    /// synchronous one.
    /// </remarks>
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly IJSRuntime _js;

        /// <summary>
        /// Constructor: <see cref="IJSRuntime"/> is the only dependency; we
        /// use it to reach <c>localStorage</c> from inside the WASM runtime.
        /// </summary>
        public AuthHeaderHandler(IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>
        /// Reads the JWT (if any) from <c>localStorage</c>, attaches it as a
        /// <c>Bearer</c> Authorization header, and forwards the request down
        /// the handler chain. Anonymous endpoints simply ignore the header
        /// when no token is present.
        /// </summary>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Reach across the JS interop boundary to read the persisted
            // token. The storage key matches the constant defined in
            // AuthService so the two stay in lockstep.
            var token = await _js.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                new object[] { AuthService.TokenStorageKey });

            // Only attach the header when we actually have something to
            // attach — sending "Authorization: Bearer " with an empty token
            // would invite gratuitous 401s on otherwise anonymous endpoints.
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Forward to the inner handler (which is the real HttpClient
            // pipeline that actually sends the request).
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
