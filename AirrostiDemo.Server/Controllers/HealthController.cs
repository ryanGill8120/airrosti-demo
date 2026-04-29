using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AirrostiDemo.Server.Controllers
{
    /// <summary>
    /// Lightweight liveness endpoint used by Render's health-check probe.
    /// </summary>
    /// <remarks>
    /// We intentionally keep this on its own controller (rather than tacking
    /// it onto <c>DrugSideEffectsController</c>) so that health pings don't
    /// burn through the OpenFDA quota or touch the database — the route here
    /// returns an in-memory constant and nothing else.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// Returns a 200 OK with a tiny JSON status body. This is the URL
        /// Render configures as the service health check; if it stops
        /// returning 200 the platform restarts the container.
        /// </summary>
        [HttpGet]
        public IActionResult Get() => Ok(new { status = "ok" });
    }
}
