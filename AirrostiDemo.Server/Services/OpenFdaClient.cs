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

        public OpenFdaClient(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
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
            var url = QueryHelpers.AddQueryString("drug/event.json", query);

            using var response = await http.GetAsync(url, ct);

            // OpenFDA returns 404 when a drug has no matching events — treat as empty.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new FdaCountResponse { DrugName = drugName };
            }

            response.EnsureSuccessStatusCode();

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
}
