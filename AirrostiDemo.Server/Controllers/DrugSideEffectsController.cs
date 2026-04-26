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

        public DrugSideEffectsController(OpenFdaClient openFda)
        {
            _openFda = openFda;
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

            var result = await _openFda.GetReactionCountsAsync(drugName, limit, ct);
            return Ok(result);
        }
    }
}
