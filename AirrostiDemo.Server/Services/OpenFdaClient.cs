using System.Net;
using System.Net.Http.Json;
using AirrostiDemo.Shared.OpenFda;
using Microsoft.AspNetCore.WebUtilities;

namespace AirrostiDemo.Server.Services
{
    /// <summary>
    /// Typed wrapper around OpenFDA's <c>drug/event.json</c> endpoint. This is
    /// the single chokepoint through which the server talks to FDA — every
    /// controller that needs adverse-event data injects this service rather
    /// than building its own <see cref="HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// We sit on top of <see cref="IHttpClientFactory"/> (named client
    /// <see cref="HttpClientName"/>) instead of holding a long-lived
    /// <see cref="HttpClient"/> field — that's the modern .NET pattern and
    /// it gives us connection-pool reuse plus DNS refresh for free.
    /// <para>
    /// All upstream failure modes are translated into our own exception
    /// types here so the controller layer can map them cleanly to HTTP
    /// statuses without leaking <see cref="HttpStatusCode"/> values from
    /// the FDA into our public API surface.
    /// </para>
    /// </remarks>
    public class OpenFdaClient
    {
        /// <summary>
        /// The DI key under which the underlying named <see cref="HttpClient"/>
        /// is registered in <c>Program.cs</c>. Exposed as a constant so the
        /// registration site and the consumer can't drift out of sync.
        /// </summary>
        public const string HttpClientName = "openFDA";

        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<OpenFdaClient> _logger;
        private readonly string? _apiKey;

        /// <summary>
        /// Resolves the optional <c>OpenFda:ApiKey</c> setting once at
        /// construction time. Unauthenticated traffic is rate-limited to
        /// roughly 1,000 requests per IP per day; if a key is supplied the
        /// quota lifts to ~120,000/day. We coerce empty/whitespace strings
        /// to <c>null</c> so the "no key configured" code path is the same
        /// whether the key is missing or just blank.
        /// </summary>
        public OpenFdaClient(
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            ILogger<OpenFdaClient> logger)
        {
            _httpFactory = httpFactory;
            _logger = logger;
            var key = configuration["OpenFda:ApiKey"];
            _apiKey = string.IsNullOrWhiteSpace(key) ? null : key;
        }

        /// <summary>
        /// Calls <c>drug/event.json</c> with a <c>count</c> facet on
        /// <c>patient.reaction.reactionmeddrapt.exact</c> and returns the
        /// top <paramref name="limit"/> reaction terms for the given drug.
        /// </summary>
        /// <param name="drugName">Free-text drug name from the user. Wrapped
        /// in quotes inside the search expression so multi-word names work,
        /// and bad input is caught by the 400 → DrugNotFound translation
        /// below.</param>
        /// <param name="limit">How many ranked terms to return. Caller is
        /// expected to clamp to a sensible range; we do not re-clamp here.</param>
        /// <param name="ct">Propagates HTTP-cancellation when the originating
        /// request is aborted.</param>
        /// <exception cref="DrugNotFoundException">Thrown when FDA returns
        /// 404 (no matching events) or 400 (unparseable query).</exception>
        /// <exception cref="OpenFdaUnavailableException">Thrown for 429,
        /// 503, and any other non-success response.</exception>
        public async Task<FdaCountResponse> GetReactionCountsAsync(
            string drugName,
            int limit = 10,
            CancellationToken ct = default)
        {
            var http = _httpFactory.CreateClient(HttpClientName);

            // Build the OpenFDA query parameters. The "search" expression
            // restricts to reports that mention the drug's medicinal product
            // name, and "count" asks FDA to bucket-and-count by the reaction
            // term so we don't have to do the aggregation ourselves.
            var query = new Dictionary<string, string?>
            {
                ["search"] = $"patient.drug.medicinalproduct:\"{drugName}\"",
                ["count"] = "patient.reaction.reactionmeddrapt.exact",
                ["limit"] = limit.ToString(),
            };
            if (_apiKey is not null)
            {
                // Append the API key only when present; otherwise OpenFDA
                // rejects an empty api_key parameter outright.
                query["api_key"] = _apiKey;
            }
            var url = QueryHelpers.AddQueryString("drug/event.json", query);

            using var response = await http.GetAsync(url, ct);

            // OpenFDA returns 404 when no events match. 400 means the drug name
            // produced an unparseable query (e.g. random punctuation). For the
            // user, both mean "we couldn't find that drug."
            if (response.StatusCode == HttpStatusCode.NotFound
                || response.StatusCode == HttpStatusCode.BadRequest)
            {
                throw new DrugNotFoundException(drugName);
            }

            // Upstream is throttling us (429) or briefly down (503). Capture
            // any Retry-After hint so we can pass it through to the client
            // and they can back off intelligently rather than hammering us.
            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds is double s
                    ? (int)s
                    : (int?)null;
                _logger.LogWarning(
                    "OpenFDA returned {Status} for drug={Drug}. Retry-After={RetryAfter}s",
                    (int)response.StatusCode, drugName, retryAfter);
                throw new OpenFdaUnavailableException(response.StatusCode, retryAfter);
            }

            // Any other non-2xx is unexpected — log it, then surface the
            // generic "FDA unavailable" exception so the controller can
            // return a 503 to the caller.
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenFDA returned unexpected {Status} for drug={Drug}",
                    (int)response.StatusCode, drugName);
                throw new OpenFdaUnavailableException(response.StatusCode, null);
            }

            // Happy path: deserialize FDA's "results" array off their envelope
            // into our own DTO shape, attaching the original drug name so the
            // caller doesn't have to thread it through separately.
            var envelope = await response.Content.ReadFromJsonAsync<CountEnvelope>(cancellationToken: ct);
            return new FdaCountResponse
            {
                DrugName = drugName,
                Results = envelope?.Results ?? new(),
            };
        }

        /// <summary>
        /// Internal-only mirror of OpenFDA's response envelope. We never
        /// return this type from the service; it exists solely to give
        /// <see cref="System.Net.Http.Json.HttpContentJsonExtensions"/>
        /// something to deserialize into.
        /// </summary>
        private class CountEnvelope
        {
            public List<FdaReactionCount> Results { get; set; } = new();
        }
    }

    /// <summary>
    /// Thrown when OpenFDA itself is unhealthy (rate-limited, down, or
    /// returning a status we don't have specific handling for). The
    /// controller catches this and converts it into an HTTP 503 with a
    /// retry-after hint so the WASM client can prompt the user to try again.
    /// </summary>
    public class OpenFdaUnavailableException : Exception
    {
        /// <summary>The actual HTTP status FDA returned, for logging.</summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Seconds to wait before retrying, parsed from the upstream
        /// <c>Retry-After</c> header when present. <c>null</c> means we
        /// have no specific guidance and the client should pick its own
        /// back-off.
        /// </summary>
        public int? RetryAfterSeconds { get; }

        /// <summary>
        /// Builds a transient-failure exception carrying both the upstream
        /// status and any retry guidance FDA gave us.
        /// </summary>
        public OpenFdaUnavailableException(HttpStatusCode statusCode, int? retryAfterSeconds)
            : base($"OpenFDA returned {(int)statusCode}.")
        {
            StatusCode = statusCode;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }

    /// <summary>
    /// Thrown when FDA tells us the drug doesn't exist in their dataset
    /// (404) or when the drug name was so malformed FDA couldn't parse it
    /// at all (400). The controller maps this to a friendly 404 response
    /// rather than letting the user see a 5xx for what is really a "not
    /// found" condition.
    /// </summary>
    public class DrugNotFoundException : Exception
    {
        /// <summary>The drug name the caller searched for, preserved for
        /// logging and for inclusion in the message.</summary>
        public string DrugName { get; }

        /// <summary>
        /// Builds a "no such drug" exception. Distinct from
        /// <see cref="OpenFdaUnavailableException"/> because the user can
        /// fix the input themselves — no point telling them to retry.
        /// </summary>
        public DrugNotFoundException(string drugName)
            : base($"OpenFDA has no events matching '{drugName}'.")
        {
            DrugName = drugName;
        }
    }
}
