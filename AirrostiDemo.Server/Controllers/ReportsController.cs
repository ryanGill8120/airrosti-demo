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
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<ActionResult<SavedReportDto>> Save(
            [FromBody] FdaCountResponse body,
            CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(body.DrugName))
            {
                return BadRequest("DrugName is required");
            }

            var entity = new SavedReport
            {
                UserId = userId,
                DrugName = body.DrugName,
                ReportJson = JsonSerializer.Serialize(body),
                SavedAt = DateTime.UtcNow,
            };

            _db.SavedReports.Add(entity);
            await _db.SaveChangesAsync(ct);

            var dto = new SavedReportDto
            {
                Id = entity.Id,
                DrugName = entity.DrugName,
                SavedAt = entity.SavedAt,
                Report = body,
            };

            return CreatedAtAction(nameof(GetAll), null, dto);
        }

        [HttpGet]
        public async Task<ActionResult<List<SavedReportDto>>> GetAll(CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var rows = await _db.SavedReports
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.SavedAt)
                .ToListAsync(ct);

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
