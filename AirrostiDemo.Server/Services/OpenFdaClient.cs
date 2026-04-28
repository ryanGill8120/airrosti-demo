using System.Net;
using System.Net.Http.Json;
using AirrostiDemo.Shared.OpenFda;
using Microsoft.AspNetCore.WebUtilities;

namespace AirrostiDemo.Server.Services
{
    public class OpenFdaClient
    {
        public const string HttpClientName = "openFDA";

        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<OpenFdaClient> _logger;
        private readonly string? _apiKey;

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

        public async Task<FdaCountResponse> GetReactionCountsAsync(
            string drugName,
            int limit = 10,
            CancellationToken ct = default)
        {
            var http = _httpFactory.CreateClient(HttpClientName);

            var query = new Dictionary<string, string?>
            {
                ["search"] = $"patient.drug.medicinalproduct:\"{drugName}\"",
                ["count"] = "patient.reaction.reactionmeddrapt.exact",
                ["limit"] = limit.ToString(),
            };
            if (_apiKey is not null)
            {
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

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenFDA returned unexpected {Status} for drug={Drug}",
                    (int)response.StatusCode, drugName);
                throw new OpenFdaUnavailableException(response.StatusCode, null);
            }

            var envelope = await response.Content.ReadFromJsonAsync<CountEnvelope>(cancellationToken: ct);
            return new FdaCountResponse
            {
                DrugName = drugName,
                Results = envelope?.Results ?? new(),
            };
        }

        private class CountEnvelope
        {
            public List<FdaReactionCount> Results { get; set; } = new();
        }
    }

    public class OpenFdaUnavailableException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public int? RetryAfterSeconds { get; }

        public OpenFdaUnavailableException(HttpStatusCode statusCode, int? retryAfterSeconds)
            : base($"OpenFDA returned {(int)statusCode}.")
        {
            StatusCode = statusCode;
            RetryAfterSeconds = retryAfterSeconds;
        }
    }

    public class DrugNotFoundException : Exception
    {
        public string DrugName { get; }

        public DrugNotFoundException(string drugName)
            : base($"OpenFDA has no events matching '{drugName}'.")
        {
            DrugName = drugName;
        }
    }
}
