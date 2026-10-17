using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

/// <summary>Small dropdown lookups for the UI (companies, projects, cost centers, heads).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ErpDbContext _db;
    public LookupsController(ErpDbContext db) => _db = db;

    public record ItemDto(int Id, string Name, string? Extra = null);

    [HttpGet("companies")]
    public async Task<ActionResult<List<ItemDto>>> Companies() =>
        Ok(await _db.Companies.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .Select(c => new ItemDto(c.Id, c.CompanyName, c.Code)).ToListAsync());

    [HttpGet("projects")]
    public async Task<ActionResult<List<ItemDto>>> Projects([FromQuery] int companyId) =>
        Ok(await _db.Projects.AsNoTracking().Where(p => p.CompanyId == companyId && p.IsActive)
            .OrderBy(p => p.ProjectName)
            .Select(p => new ItemDto(p.Id, p.ProjectName, p.Code)).ToListAsync());

    [HttpGet("cost-centers")]
    public async Task<ActionResult<List<ItemDto>>> CostCenters([FromQuery] int companyId) =>
        Ok(await _db.CostCenters.AsNoTracking().Where(c => c.CompanyId == companyId && c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new ItemDto(c.Id, $"{c.Code} - {c.Name}", null)).ToListAsync());

    [HttpGet("heads")]
    public async Task<ActionResult<List<ItemDto>>> Heads([FromQuery] int? companyId) =>
        Ok(await _db.AccountHeads.AsNoTracking()
            .Where(h => h.IsActive && h.IsTransactional)
            .OrderBy(h => h.Code)
            .Select(h => new ItemDto(h.Id, $"{h.Code} - {h.Name}", null)).ToListAsync());
}
