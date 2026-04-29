using System.Security.Claims;
using System.Text.Json;
using AirrostiDemo.Server.Data;
using AirrostiDemo.Shared.OpenFda;
using AirrostiDemo.Shared.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace AirrostiDemo.Server.Controllers
{
    /// <summary>
    /// User-scoped CRUD for saved drug-search snapshots. Every action here
    /// requires a valid bearer token and operates exclusively on rows that
    /// belong to the calling user.
    /// </summary>
    /// <remarks>
    /// The owning user id is always read from
    /// <see cref="ClaimTypes.NameIdentifier"/> on the validated JWT — never
    /// from the request body or query string. This is the single most
    /// important rule in this controller: trusting a client-supplied user
    /// id would be the textbook IDOR vulnerability.
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]  // *** True Auth lives here,  ***
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _db;

        /// <summary>
        /// Standard constructor: the EF Core context is injected per-request
        /// (scoped lifetime) so we don't accidentally share a tracker
        /// between concurrent requests.
        /// </summary>
        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Persists a drug-search snapshot for the current user. The body is
        /// the same <see cref="FdaCountResponse"/> shape the client just
        /// received from <c>GET /api/DrugSideEffects</c>; we serialize it
        /// to JSON in one column rather than normalising into a child table.
        /// </summary>
        /// <param name="body">The full FDA payload to snapshot.</param>
        /// <param name="ct">Request abort token, threaded through the
        /// <c>SaveChangesAsync</c> call.</param>
        [HttpPost]
        public async Task<ActionResult<SavedReportDto>> Save(
            [FromBody] FdaCountResponse body,
            CancellationToken ct)
        {
            // Pull the owning user id from the validated JWT. The JwtBearer
            // middleware mapped the token's `sub` claim onto NameIdentifier
            // during the auth pipeline, so this is the canonical "who is
            // calling" value — never accept this from the client body.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body.DrugName))
            {
                return BadRequest("DrugName is required");
            }

            // Build the row. ReportJson holds the verbatim payload so the
            // user always sees what FDA returned at save time, even if FDA
            // later updates or removes a report.
            var entity = new SavedReport
            {
                UserId = userId,
                DrugName = body.DrugName,
                ReportJson = JsonSerializer.Serialize(body),
                SavedAt = DateTime.UtcNow,
            };

            _db.SavedReports.Add(entity);
            await _db.SaveChangesAsync(ct);

            // Project the freshly-saved entity back into the DTO shape the
            // client expects, including the auto-generated Id from EF.
            var dto = new SavedReportDto
            {
                Id = entity.Id,
                DrugName = entity.DrugName,
                SavedAt = entity.SavedAt,
                Report = body,
            };

            // 201 Created with a Location header pointing at the GET-all
            // endpoint. We don't have a per-id GET, so we link to the list.
            return CreatedAtAction(nameof(GetAll), null, dto);
        }

        /// <summary>
        /// Returns every saved report belonging to the current user, newest
        /// first. The JSON blob is re-hydrated server-side so the client
        /// gets a strongly-typed DTO rather than an opaque string.
        /// </summary>
        /// <param name="ct">Request abort token threaded through the
        /// <c>ToListAsync</c> call.</param>
        [HttpGet]
        public async Task<ActionResult<List<SavedReportDto>>> GetAll(CancellationToken ct)
        {
            // Same JWT → user-id flow as the Save action above. Critical
            // that this be the FILTER on the EF query, not just an
            // afterthought, otherwise a logic bug could expose another
            // user's reports.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Per-user query. The UserId index defined in
            // AppDbContext.OnModelCreating keeps this efficient at scale.
            var rows = await _db.SavedReports
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.SavedAt)
                .ToListAsync(ct);

            // Project rows into DTOs. Two things worth noting here:
            //   1) DateTime.SpecifyKind tags the timestamp as UTC because
            //      SQLite stores it kind-less; without this the client
            //      would treat it as local time and shift the display.
            //   2) Deserialization could in principle return null for a
            //      malformed blob, so we coalesce to an empty response
            //      rather than crash the whole list.
            var list = rows.Select(r => new SavedReportDto
            {
                Id = r.Id,
                DrugName = r.DrugName,
                SavedAt = DateTime.SpecifyKind(r.SavedAt, DateTimeKind.Utc),
                Report = JsonSerializer.Deserialize<FdaCountResponse>(r.ReportJson) ?? new FdaCountResponse(),
            }).ToList();

            return Ok(list);
        }
    }
}
