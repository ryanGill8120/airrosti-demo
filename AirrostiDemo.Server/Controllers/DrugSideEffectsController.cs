using AirrostiDemo.Server.Services;
using AirrostiDemo.Shared.OpenFda;
using Microsoft.AspNetCore.Mvc;

namespace AirrostiDemo.Server.Controllers
{
    /// <summary>
    /// Public-facing proxy in front of OpenFDA's adverse-event API. The
    /// Blazor client never calls FDA directly — it goes through this
    /// controller, which keeps any future API key, rate-limit handling, or
    /// upstream switch confined to the server.
    /// </summary>
    /// <remarks>
    /// The route is <c>GET /api/DrugSideEffects/{drugName}</c>; it's
    /// intentionally anonymous (no <c>[Authorize]</c>) so a visitor can
    /// browse the demo without signing up. Only saving a result requires
    /// authentication, which is handled by <see cref="ReportsController"/>.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    public class DrugSideEffectsController : ControllerBase
    {
        private readonly OpenFdaClient _openFda;
        private readonly ILogger<DrugSideEffectsController> _logger;

        /// <summary>
        /// Standard constructor injection: <see cref="OpenFdaClient"/> is the
        /// only place we go to reach FDA; the logger gets warning/error
        /// signal whenever upstream misbehaves.
        /// </summary>
        public DrugSideEffectsController(
            OpenFdaClient openFda,
            ILogger<DrugSideEffectsController> logger)
        {
            _openFda = openFda;
            _logger = logger;
        }

        /// <summary>
        /// Look up the top reactions for a drug. Maps every plausible
        /// upstream outcome to a clean HTTP status so the WASM client only
        /// ever sees 200 / 400 / 404 / 503 — never a raw 5xx leak.
        /// </summary>
        /// <param name="drugName">Drug name from the URL segment, already
        /// URL-decoded by ASP.NET Core.</param>
        /// <param name="limit">How many reaction terms to return; clamped
        /// 1..50 to protect both us and FDA from runaway requests.</param>
        /// <param name="ct">Request abort token; flows through to the FDA
        /// HTTP call so a cancelled browser tab cancels the upstream call
        /// too.</param>
        [HttpGet("{drugName}")]
        public async Task<ActionResult<FdaCountResponse>> Get(
            string drugName,
            [FromQuery] int limit = 10,
            CancellationToken ct = default)
        {
            // Defensive validation: the route template won't match an empty
            // segment, but it can match whitespace / weird-encoded inputs.
            if (string.IsNullOrWhiteSpace(drugName))
            {
                return BadRequest("drugName is required");
            }

            // Clamp on the way in — easier to reason about than letting a
            // huge limit propagate to FDA and possibly be rejected upstream.
            limit = Math.Clamp(limit, 1, 50);

            try
            {
                // Delegate the actual HTTP work to the typed FDA client. It
                // raises domain-specific exceptions for the failure modes we
                // care about, which lets the catch arms below stay focused.
                var result = await _openFda.GetReactionCountsAsync(drugName, limit, ct);
                return Ok(result);
            }
            catch (DrugNotFoundException)
            {
                // Upstream said "no such drug." Surface that as a clean 404
                // rather than the generic 503 below — the user can fix it.
                return NotFound(new { message = "Drug not found." });
            }
            catch (OpenFdaUnavailableException ex)
            {
                // FDA is throttling us or briefly down. Pass the retry hint
                // through to the client unmodified; the UI shows a "try
                // again shortly" message.
                _logger.LogWarning(
                    "OpenFDA unavailable ({Status}) for drug={Drug}",
                    (int)ex.StatusCode, drugName);
                return StatusCode(503, new
                {
                    message = "OpenFDA is temporarily unavailable, please try again shortly.",
                    retryAfterSeconds = ex.RetryAfterSeconds,
                });
            }
            catch (OperationCanceledException)
            {
                // Caller went away — propagate so ASP.NET Core can short-circuit
                // the response pipeline. We don't log: cancellation is normal.
                throw;
            }
            catch (Exception ex)
            {
                // Last-resort net: log the full exception with the drug name
                // and serve the same generic 503 the client already knows how
                // to render. We never want a stack trace reaching the browser.
                _logger.LogError(ex, "Unexpected error fetching reactions for drug={Drug}", drugName);
                return StatusCode(503, new
                {
                    message = "Something went wrong fetching that drug, please try again.",
                });
            }
        }
    }
}
