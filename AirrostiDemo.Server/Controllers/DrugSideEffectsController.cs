using AirrostiDemo.Server.Services;
using AirrostiDemo.Shared.OpenFda;
using Microsoft.AspNetCore.Mvc;

namespace AirrostiDemo.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DrugSideEffectsController : ControllerBase
    {
        private readonly OpenFdaClient _openFda;
        private readonly ILogger<DrugSideEffectsController> _logger;

        public DrugSideEffectsController(
            OpenFdaClient openFda,
            ILogger<DrugSideEffectsController> logger)
        {
            _openFda = openFda;
            _logger = logger;
        }

        [HttpGet("{drugName}")]
        public async Task<ActionResult<FdaCountResponse>> Get(
            string drugName,
            [FromQuery] int limit = 10,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(drugName))
            {
                return BadRequest("drugName is required");
            }

            limit = Math.Clamp(limit, 1, 50);

            try
            {
                var result = await _openFda.GetReactionCountsAsync(drugName, limit, ct);
                return Ok(result);
            }
            catch (OpenFdaUnavailableException ex)
            {
                _logger.LogWarning(
                    "OpenFDA unavailable ({Status}) for drug={Drug}",
                    (int)ex.StatusCode, drugName);
                return StatusCode(503, new
                {
                    message = "OpenFDA is temporarily unavailable, please try again shortly.",
                    retryAfterSeconds = ex.RetryAfterSeconds,
                });
            }
        }
    }
}
