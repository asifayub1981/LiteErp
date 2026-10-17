using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoaController : ControllerBase
{
    private readonly ErpDbContext _db;

    public CoaController(ErpDbContext db) => _db = db;

    // ---------- DTOs ----------
    public record CoaNodeDto(
        int Id, string Code, string Name, AccountType AccountType,
        bool IsTransactional, bool IsActive, bool ActiveForCompany,
        List<CoaNodeDto> Children);

    public record UpsertHeadRequest(
        int? ParentId, string Code, string Name, AccountType AccountType,
        bool IsTransactional, bool IsActive);

    public record CompanyActivationRequest(int CompanyId, bool IsActive);

    // ---------- Tree ----------
    /// <summary>
    /// Full group COA tree. If companyId given, each node also carries its
    /// per-company activation flag (inactive nodes shown greyed out in UI).
    /// </summary>
    [HttpGet("tree")]
    public async Task<ActionResult<List<CoaNodeDto>>> GetTree([FromQuery] int? companyId)
    {
        var heads = await _db.AccountHeads.AsNoTracking().OrderBy(h => h.Code).ToListAsync();

        HashSet<int> activeIds = new();
        if (companyId.HasValue)
        {
            activeIds = (await _db.CompanyAccountHeads.AsNoTracking()
                .Where(c => c.CompanyId == companyId && c.IsActive)
                .Select(c => c.AccountHeadId).ToListAsync()).ToHashSet();
        }

        var lookup = heads.ToLookup(h => h.ParentId);
        List<CoaNodeDto> Build(int? parentId) =>
            lookup[parentId].Select(h => new CoaNodeDto(
                h.Id, h.Code, h.Name, h.AccountType, h.IsTransactional, h.IsActive,
                !companyId.HasValue || activeIds.Contains(h.Id),
                Build(h.Id))).ToList();

        return Ok(Build(null));
    }

    // ---------- Create ----------
    [HttpPost]
    public async Task<ActionResult<CoaNodeDto>> Create([FromBody] UpsertHeadRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Code and Name are required." });

        if (await _db.AccountHeads.AnyAsync(h => h.Code == req.Code.Trim()))
            return Conflict(new { message = $"Code '{req.Code.Trim()}' already exists." });

        AccountHead? parent = null;
        if (req.ParentId.HasValue)
        {
            parent = await _db.AccountHeads.FindAsync(req.ParentId.Value);
            if (parent is null) return BadRequest(new { message = "Parent head not found." });
            if (parent.IsTransactional)
                return BadRequest(new { message = "Cannot add a child under a transactional (leaf) head." });
        }

        var head = new AccountHead
        {
            ParentId = req.ParentId,
            Code = req.Code.Trim(),
            Name = req.Name.Trim(),
            AccountType = req.AccountType,
            IsTransactional = req.IsTransactional,
            IsActive = req.IsActive
        };
        _db.AccountHeads.Add(head);
        await _db.SaveChangesAsync();

        // auto-activate for all companies so the head is usable immediately
        var companyIds = await _db.Companies.Select(c => c.Id).ToListAsync();
        _db.CompanyAccountHeads.AddRange(companyIds.Select(cid =>
            new CompanyAccountHead { CompanyId = cid, AccountHeadId = head.Id, IsActive = true }));
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTree), new CoaNodeDto(
            head.Id, head.Code, head.Name, head.AccountType,
            head.IsTransactional, head.IsActive, true, new()));
    }

    // ---------- Update ----------
    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpsertHeadRequest req)
    {
        var head = await _db.AccountHeads
            .Include(h => h.Children)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (head is null) return NotFound();

        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Code and Name are required." });

        if (await _db.AccountHeads.AnyAsync(h => h.Id != id && h.Code == req.Code.Trim()))
            return Conflict(new { message = $"Code '{req.Code.Trim()}' already exists." });

        if (req.IsTransactional && head.Children.Any())
            return BadRequest(new { message = "A head with sub-heads cannot be transactional." });

        if (!req.IsTransactional && await _db.VoucherLines.AnyAsync(l => l.AccountHeadId == id))
            return BadRequest(new { message = "A head with posted transactions cannot become a group head." });

        head.Code = req.Code.Trim();
        head.Name = req.Name.Trim();
        head.AccountType = req.AccountType;
        head.IsTransactional = req.IsTransactional;
        head.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Delete ----------
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var head = await _db.AccountHeads
            .Include(h => h.Children)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (head is null) return NotFound();

        if (head.Children.Any())
            return BadRequest(new { message = "Cannot delete a head that has sub-heads." });
        if (await _db.VoucherLines.AnyAsync(l => l.AccountHeadId == id))
            return BadRequest(new { message = "Cannot delete a head that has transactions." });

        _db.CompanyAccountHeads.RemoveRange(
            _db.CompanyAccountHeads.Where(c => c.AccountHeadId == id));
        _db.AccountHeads.Remove(head);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Per-company activation ----------
    [HttpPut("{id:int}/company-activation")]
    public async Task<ActionResult> SetCompanyActivation(int id, [FromBody] CompanyActivationRequest req)
    {
        if (!await _db.AccountHeads.AnyAsync(h => h.Id == id)) return NotFound();
        if (!await _db.Companies.AnyAsync(c => c.Id == req.CompanyId))
            return BadRequest(new { message = "Company not found." });

        var row = await _db.CompanyAccountHeads
            .FirstOrDefaultAsync(c => c.CompanyId == req.CompanyId && c.AccountHeadId == id);
        if (row is null)
        {
            row = new CompanyAccountHead { CompanyId = req.CompanyId, AccountHeadId = id };
            _db.CompanyAccountHeads.Add(row);
        }
        row.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
