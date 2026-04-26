using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace AirrostiDemo.Services
{
    public class AuthHeaderHandler : DelegatingHandler
    {
        private readonly IJSRuntime _js;

        public AuthHeaderHandler(IJSRuntime js)
        {
            _js = js;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var token = await _js.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                new object[] { AuthService.TokenStorageKey });

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
