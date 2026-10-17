using LiteErp.Core.Enums;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ErpDbContext _db;

    public ReportsController(ErpDbContext db) => _db = db;

    // ---------- DTOs ----------
    public record GlLineDto(
        DateOnly Date, string VoucherNo, string VoucherType, string Narration,
        decimal Debit, decimal Credit, decimal RunningBalance);

    public record GlAccountDto(
        int AccountHeadId, string Code, string Name,
        decimal OpeningBalance, List<GlLineDto> Lines, decimal ClosingBalance);

    public record TrialBalanceRowDto(
        int AccountHeadId, string Code, string Name, int Level, bool IsTransactional,
        decimal OpeningDr, decimal OpeningCr,
        decimal PeriodDr, decimal PeriodCr,
        decimal ClosingDr, decimal ClosingCr);

    // ---------- Helpers ----------
    private async Task<HashSet<int>> DescendantIdsAsync(int headId)
    {
        var heads = await _db.AccountHeads.AsNoTracking()
            .Select(h => new { h.Id, h.ParentId }).ToListAsync();
        var byParent = heads.ToLookup(h => h.ParentId);
        var result = new HashSet<int> { headId };
        var stack = new Stack<int>();
        stack.Push(headId);
        while (stack.Count > 0)
            foreach (var c in byParent[stack.Pop()])
                if (result.Add(c.Id)) stack.Push(c.Id);
        return result;
    }

    private IQueryable<Core.Entities.VoucherLine> PostedLines(int companyId, int? projectId) =>
        _db.VoucherLines.AsNoTracking()
            .Where(l => l.Voucher.CompanyId == companyId
                && l.Voucher.Status == VoucherStatus.Posted
                && (!projectId.HasValue || l.Voucher.ProjectId == projectId));

    // ---------- General Ledger ----------
    /// <summary>
    /// General ledger for one head (optionally including sub-heads) over a date range,
    /// with opening balance, running balance per line and closing balance.
    /// </summary>
    [HttpGet("general-ledger")]
    public async Task<ActionResult<GlAccountDto>> GeneralLedger(
        [FromQuery] int companyId,
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int accountHeadId,
        [FromQuery] bool includeSubHeads = true,
        [FromQuery] int? projectId = null,
        [FromQuery] int? costCenterId = null)
    {
        var head = await _db.AccountHeads.AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == accountHeadId);
        if (head is null) return NotFound(new { message = "Account head not found." });

        var headIds = includeSubHeads
            ? await DescendantIdsAsync(accountHeadId)
            : new HashSet<int> { accountHeadId };

        var q = PostedLines(companyId, projectId)
            .Where(l => headIds.Contains(l.AccountHeadId)
                && (!costCenterId.HasValue || l.CostCenterId == costCenterId));

        var opening = await q.Where(l => l.Voucher.VoucherDate < from)
            .SumAsync(l => l.Debit - l.Credit);

        var lines = await q.Where(l => l.Voucher.VoucherDate >= from && l.Voucher.VoucherDate <= to)
            .OrderBy(l => l.Voucher.VoucherDate).ThenBy(l => l.VoucherId)
            .Select(l => new
            {
                l.Voucher.VoucherDate, l.Voucher.VoucherNo, l.Voucher.VoucherType,
                l.Narration, l.Debit, l.Credit
            }).ToListAsync();

        var running = opening;
        var dtos = lines.Select(l =>
        {
            running += l.Debit - l.Credit;
            return new GlLineDto(l.VoucherDate, l.VoucherNo, l.VoucherType.ToString(),
                l.Narration, l.Debit, l.Credit, running);
        }).ToList();

        return Ok(new GlAccountDto(head.Id, head.Code, head.Name, opening, dtos, running));
    }

    // ---------- Trial Balance ----------
    /// <summary>
    /// Trial balance. columns=2 → Account | Dr | Cr (net closing).
    /// columns=6 → Account | Opening Dr/Cr | Period Dr/Cr | Closing Dr/Cr.
    /// Hierarchical order (by Code) with Level for indentation; group heads show subtotals.
    /// </summary>
    [HttpGet("trial-balance")]
    public async Task<ActionResult<List<TrialBalanceRowDto>>> TrialBalance(
        [FromQuery] int companyId,
        [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int columns = 2,
        [FromQuery] int? projectId = null)
    {
        if (columns is not (2 or 6)) return BadRequest(new { message = "columns must be 2 or 6." });

        var heads = await _db.AccountHeads.AsNoTracking().OrderBy(h => h.Code).ToListAsync();
        var lines = PostedLines(companyId, projectId);

        // aggregate per head: opening (before from), period (from..to)
        var agg = await lines
            .GroupBy(l => l.AccountHeadId)
            .Select(g => new
            {
                HeadId = g.Key,
                Opening = g.Where(l => l.Voucher.VoucherDate < from).Sum(l => l.Debit - l.Credit),
                PeriodDr = g.Where(l => l.Voucher.VoucherDate >= from && l.Voucher.VoucherDate <= to).Sum(l => l.Debit),
                PeriodCr = g.Where(l => l.Voucher.VoucherDate >= from && l.Voucher.VoucherDate <= to).Sum(l => l.Credit)
            }).ToListAsync();
        var byHead = agg.ToDictionary(a => a.HeadId);

        // roll up to parents: process deepest-first so group heads get subtotals
        var childrenOf = heads.ToLookup(h => h.ParentId);
        var totals = new Dictionary<int, (decimal open, decimal pDr, decimal pCr)>();
        foreach (var h in heads) totals[h.Id] = (0, 0, 0);
        foreach (var h in heads.Where(h => h.IsTransactional))
            if (byHead.TryGetValue(h.Id, out var a)) totals[h.Id] = (a.Opening, a.PeriodDr, a.PeriodCr);

        int LevelOf(int id)
        {
            var lvl = 0; var cur = heads.First(h => h.Id == id);
            while (cur.ParentId.HasValue) { lvl++; cur = heads.First(h => h.Id == cur.ParentId.Value); }
            return lvl;
        }

        // propagate children totals into parents (deepest first)
        foreach (var h in heads.OrderByDescending(h => LevelOf(h.Id)))
        {
            if (!h.ParentId.HasValue) continue;
            var (o, d, c) = totals[h.Id];
            var p = totals[h.ParentId.Value];
            totals[h.ParentId.Value] = (p.open + o, p.pDr + d, p.pCr + c);
        }

        static (decimal dr, decimal cr) Split(decimal net) =>
            net >= 0 ? (net, 0m) : (0m, -net);

        var rows = new List<TrialBalanceRowDto>();
        foreach (var h in heads)
        {
            var (open, pDr, pCr) = totals[h.Id];
            if (open == 0 && pDr == 0 && pCr == 0) continue; // hide zero rows
            var (oDr, oCr) = Split(open);
            var closing = open + pDr - pCr;
            var (cDr, cCr) = Split(closing);
            rows.Add(new TrialBalanceRowDto(h.Id, h.Code, h.Name, LevelOf(h.Id), h.IsTransactional,
                oDr, oCr, pDr, pCr, cDr, cCr));
        }

        return Ok(rows);
    }
}
