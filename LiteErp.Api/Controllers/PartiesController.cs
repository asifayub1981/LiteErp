using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

/// <summary>Customer/Supplier master. Firm rule: parties are never deleted —
/// set inactive instead. Codes (CUST-0001) are assigned at save.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PartiesController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IDocumentNumberService _numbers;

    public PartiesController(ErpDbContext db, IDocumentNumberService numbers)
    {
        _db = db; _numbers = numbers;
    }

    public record PartyListDto(int Id, string Code, string Name, string PartyType,
        string HeadName, string? Phone, bool IsActive, decimal Balance);

    public record PartyDetailDto(int Id, int CompanyId, string PartyType, string Code,
        string Name, int AccountHeadId, string HeadName, string? Phone, string? Address,
        string? TaxNo, decimal CreditLimit, bool IsActive, decimal Balance);

    public record SavePartyDto(int CompanyId, PartyType PartyType, string Name,
        int? AccountHeadId, string? Phone, string? Address, string? TaxNo,
        decimal CreditLimit, bool IsActive);

    // ---------- List ----------
    [HttpGet]
    public async Task<ActionResult<List<PartyListDto>>> List(
        [FromQuery] int companyId, [FromQuery] PartyType? type, [FromQuery] string? search)
    {
        var q = _db.Parties.AsNoTracking()
            .Where(p => p.CompanyId == companyId);
        if (type.HasValue) q = q.Where(p => p.PartyType == type.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(p => p.Code.Contains(search) || p.Name.Contains(search));

        var parties = await q.OrderBy(p => p.Name).ToListAsync();
        var headNames = await _db.AccountHeads.AsNoTracking()
            .Where(h => parties.Select(p => p.AccountHeadId).Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => $"{h.Code} - {h.Name}");

        var result = new List<PartyListDto>();
        foreach (var p in parties)
            result.Add(new PartyListDto(p.Id, p.Code, p.Name, p.PartyType.ToString(),
                headNames.GetValueOrDefault(p.AccountHeadId, "?"), p.Phone, p.IsActive,
                await BalanceAsync(p.Id, p.PartyType)));
        return Ok(result);
    }

    // ---------- Detail ----------
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PartyDetailDto>> Get(int id)
    {
        var dto = await BuildDetailAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    private async Task<PartyDetailDto?> BuildDetailAsync(int id)
    {
        var p = await _db.Parties.AsNoTracking().Include(x => x.AccountHead)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return null;
        return new PartyDetailDto(p.Id, p.CompanyId, p.PartyType.ToString(), p.Code,
            p.Name, p.AccountHeadId, $"{p.AccountHead.Code} - {p.AccountHead.Name}",
            p.Phone, p.Address, p.TaxNo, p.CreditLimit, p.IsActive,
            await BalanceAsync(p.Id, p.PartyType));
    }

    // ---------- Create (code assigned at save) ----------
    [HttpPost]
    public async Task<ActionResult<PartyDetailDto>> Create(SavePartyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Party name is required." });

        var headId = dto.AccountHeadId ?? await DefaultHeadAsync(dto.CompanyId, dto.PartyType);
        if (headId == 0)
            return BadRequest(new { message = "No default AR/AP head found — pick an account head." });
        var headErr = await ValidateHeadAsync(dto.CompanyId, headId,
            dto.PartyType == PartyType.Customer ? AccountType.Asset : AccountType.Liability);
        if (headErr is not null) return BadRequest(new { message = headErr });

        // Default FY = the one covering today (party codes don't carry a year,
        // but the sequence row is still per-FY for clean yearly resets).
        var fy = await OpenFiscalYearAsync(dto.CompanyId, DateOnly.FromDateTime(DateTime.UtcNow));
        if (fy is null)
            return BadRequest(new { message = "No open fiscal year covers today." });

        var docType = dto.PartyType == PartyType.Customer
            ? DocumentType.Customer : DocumentType.Supplier;
        var p = new Party
        {
            CompanyId = dto.CompanyId,
            PartyType = dto.PartyType,
            Code = await _numbers.NextNumberAsync(dto.CompanyId, docType, fy.Id),
            Name = dto.Name.Trim(),
            AccountHeadId = headId,
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
            TaxNo = dto.TaxNo?.Trim(),
            CreditLimit = dto.CreditLimit,
            IsActive = true
        };
        _db.Parties.Add(p);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = p.Id }, await BuildDetailAsync(p.Id));
    }

    // ---------- Update ----------
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PartyDetailDto>> Update(int id, SavePartyDto dto)
    {
        var p = await _db.Parties.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { message = "Party name is required." });

        if (dto.AccountHeadId.HasValue && dto.AccountHeadId != p.AccountHeadId)
        {
            // Head change is allowed only before any invoice/voucher touched the party.
            var used = await _db.SalesInvoices.AnyAsync(i => i.PartyId == id)
                || await _db.PurchaseInvoices.AnyAsync(i => i.PartyId == id);
            if (used)
                return BadRequest(new { message = "Head cannot change — this party already has invoices." });
            var headErr = await ValidateHeadAsync(p.CompanyId, dto.AccountHeadId.Value,
                p.PartyType == PartyType.Customer ? AccountType.Asset : AccountType.Liability);
            if (headErr is not null) return BadRequest(new { message = headErr });
            p.AccountHeadId = dto.AccountHeadId.Value;
        }

        p.Name = dto.Name.Trim();
        p.Phone = dto.Phone?.Trim();
        p.Address = dto.Address?.Trim();
        p.TaxNo = dto.TaxNo?.Trim();
        p.CreditLimit = dto.CreditLimit;
        p.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return Ok(await BuildDetailAsync(p.Id));
    }

    // ---------- Open invoices (for receipt/payment allocation) ----------
    [HttpGet("{id:int}/open-invoices")]
    public async Task<ActionResult<List<OpenInvoiceDto>>> OpenInvoices(int id)
    {
        var p = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var result = new List<OpenInvoiceDto>();
        if (p.PartyType == PartyType.Customer)
        {
            var invs = await _db.SalesInvoices.AsNoTracking()
                .Include(i => i.Lines)
                .Where(i => i.PartyId == id && i.Status == VoucherStatus.Posted)
                .OrderBy(i => i.InvoiceDate).ToListAsync();
            foreach (var i in invs)
            {
                var paid = await _db.VoucherAllocations.AsNoTracking()
                    .Where(a => a.SalesInvoiceId == i.Id).SumAsync(a => a.Amount);
                var bal = i.GrandTotal - paid;
                if (bal > 0)
                    result.Add(new OpenInvoiceDto(i.Id, i.InvoiceNo, i.InvoiceDate,
                        i.GrandTotal, paid, bal));
            }
        }
        else
        {
            var invs = await _db.PurchaseInvoices.AsNoTracking()
                .Include(i => i.Lines)
                .Where(i => i.PartyId == id && i.Status == VoucherStatus.Posted)
                .OrderBy(i => i.InvoiceDate).ToListAsync();
            foreach (var i in invs)
            {
                var paid = await _db.VoucherAllocations.AsNoTracking()
                    .Where(a => a.PurchaseInvoiceId == i.Id).SumAsync(a => a.Amount);
                var bal = i.GrandTotal - paid;
                if (bal > 0)
                    result.Add(new OpenInvoiceDto(i.Id, i.InvoiceNo, i.InvoiceDate,
                        i.GrandTotal, paid, bal));
            }
        }
        return Ok(result);
    }

    public record OpenInvoiceDto(int Id, string InvoiceNo, DateOnly InvoiceDate,
        decimal GrandTotal, decimal Paid, decimal Balance);

    // ---------- Statement ----------
    [HttpGet("{id:int}/statement")]
    public async Task<ActionResult<PartyStatementDto>> Statement(int id,
        [FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var p = await _db.Parties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        var lines = new List<PartyStatementLine>();
        if (p.PartyType == PartyType.Customer)
        {
            var invs = await _db.SalesInvoices.AsNoTracking()
                .Include(i => i.Lines)
                .Where(i => i.PartyId == id && i.Status == VoucherStatus.Posted)
                .ToListAsync();
            var opening = invs.Where(i => i.InvoiceDate < from).Sum(i => i.GrandTotal)
                - await _db.VoucherAllocations.AsNoTracking()
                    .Where(a => a.SalesInvoice!.PartyId == id
                        && a.Voucher.VoucherDate < from && a.Voucher.Status == VoucherStatus.Posted)
                    .SumAsync(a => a.Amount);
            foreach (var i in invs.Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to))
                lines.Add(new PartyStatementLine(i.InvoiceDate, i.InvoiceNo,
                    $"Sales invoice {i.InvoiceNo}", i.GrandTotal, 0));
            var allocs = await _db.VoucherAllocations.AsNoTracking()
                .Include(a => a.Voucher).Include(a => a.SalesInvoice)
                .Where(a => a.SalesInvoice!.PartyId == id
                    && a.Voucher.VoucherDate >= from && a.Voucher.VoucherDate <= to
                    && a.Voucher.Status == VoucherStatus.Posted)
                .ToListAsync();
            foreach (var a in allocs)
                lines.Add(new PartyStatementLine(a.Voucher.VoucherDate, a.Voucher.VoucherNo,
                    $"Receipt {a.Voucher.VoucherNo} ({a.SalesInvoice!.InvoiceNo})", 0, a.Amount));
            return Ok(BuildStatement(p, opening, lines));
        }
        else
        {
            var invs = await _db.PurchaseInvoices.AsNoTracking()
                .Include(i => i.Lines)
                .Where(i => i.PartyId == id && i.Status == VoucherStatus.Posted)
                .ToListAsync();
            var opening = invs.Where(i => i.InvoiceDate < from).Sum(i => i.GrandTotal)
                - await _db.VoucherAllocations.AsNoTracking()
                    .Where(a => a.PurchaseInvoice!.PartyId == id
                        && a.Voucher.VoucherDate < from && a.Voucher.Status == VoucherStatus.Posted)
                    .SumAsync(a => a.Amount);
            foreach (var i in invs.Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to))
                lines.Add(new PartyStatementLine(i.InvoiceDate, i.InvoiceNo,
                    $"Purchase invoice {i.InvoiceNo}", i.GrandTotal, 0));
            var allocs = await _db.VoucherAllocations.AsNoTracking()
                .Include(a => a.Voucher).Include(a => a.PurchaseInvoice)
                .Where(a => a.PurchaseInvoice!.PartyId == id
                    && a.Voucher.VoucherDate >= from && a.Voucher.VoucherDate <= to
                    && a.Voucher.Status == VoucherStatus.Posted)
                .ToListAsync();
            foreach (var a in allocs)
                lines.Add(new PartyStatementLine(a.Voucher.VoucherDate, a.Voucher.VoucherNo,
                    $"Payment {a.Voucher.VoucherNo} ({a.PurchaseInvoice!.InvoiceNo})", 0, a.Amount));
            return Ok(BuildStatement(p, opening, lines));
        }
    }

    public record PartyStatementLine(DateOnly Date, string DocNo, string Description,
        decimal Debit, decimal Credit);
    public record PartyStatementDto(int PartyId, string PartyCode, string PartyName,
        decimal OpeningBalance, List<PartyStatementLine> Lines, decimal ClosingBalance);

    private static PartyStatementDto BuildStatement(Party p, decimal opening,
        List<PartyStatementLine> lines)
    {
        var ordered = lines.OrderBy(l => l.Date).ThenBy(l => l.DocNo).ToList();
        var bal = opening;
        var withBal = new List<PartyStatementLine>();
        // Running balance is derived client-side from debit/credit; keep lines simple.
        return new PartyStatementDto(p.Id, p.Code, p.Name, opening, ordered, opening
            + ordered.Sum(l => l.Debit) - ordered.Sum(l => l.Credit));
    }

    // ---------- Helpers ----------
    private async Task<decimal> BalanceAsync(int partyId, PartyType type)
    {
        if (type == PartyType.Customer)
        {
            // GrandTotal is a computed C# property — sum the lines in SQL instead.
            var inv = await _db.SalesInvoiceLines.AsNoTracking()
                .Where(l => l.SalesInvoice.PartyId == partyId
                    && l.SalesInvoice.Status == VoucherStatus.Posted)
                .SumAsync(l => l.Amount + l.TaxAmount);
            var paid = await _db.VoucherAllocations.AsNoTracking()
                .Where(a => a.SalesInvoice!.PartyId == partyId
                    && a.Voucher.Status == VoucherStatus.Posted)
                .SumAsync(a => a.Amount);
            return inv - paid;
        }
        var pinv = await _db.PurchaseInvoiceLines.AsNoTracking()
            .Where(l => l.PurchaseInvoice.PartyId == partyId
                && l.PurchaseInvoice.Status == VoucherStatus.Posted)
            .SumAsync(l => l.Amount + l.TaxAmount);
        var ppaid = await _db.VoucherAllocations.AsNoTracking()
            .Where(a => a.PurchaseInvoice!.PartyId == partyId
                && a.Voucher.Status == VoucherStatus.Posted)
            .SumAsync(a => a.Amount);
        return pinv - ppaid;
    }

    private async Task<int> DefaultHeadAsync(int companyId, PartyType type)
    {
        // First active leaf head of the right type under the company (seeded AR/AP control heads).
        var want = type == PartyType.Customer ? AccountType.Asset : AccountType.Liability;
        var head = await _db.AccountHeads.AsNoTracking()
            .Where(h => h.IsTransactional && h.IsActive && h.AccountType == want
                && (h.Name.Contains("Receivable") || h.Name.Contains("Payable")
                    || h.Name.Contains("Debtor") || h.Name.Contains("Creditor")))
            .OrderBy(h => h.Id).FirstOrDefaultAsync();
        if (head is null) return 0;
        var active = await _db.CompanyAccountHeads.AnyAsync(c =>
            c.CompanyId == companyId && c.AccountHeadId == head.Id && c.IsActive);
        return active ? head.Id : 0;
    }

    private async Task<string?> ValidateHeadAsync(int companyId, int headId, AccountType want)
    {
        var h = await _db.AccountHeads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == headId);
        if (h is null) return $"Account head #{headId} not found.";
        if (!h.IsTransactional) return $"'{h.Code} - {h.Name}' is a group head — pick a leaf head.";
        if (h.AccountType != want)
            return $"'{h.Code} - {h.Name}' is not a {(want == AccountType.Asset ? "receivable (asset)" : "payable (liability)")} head.";
        var active = await _db.CompanyAccountHeads.AnyAsync(c =>
            c.CompanyId == companyId && c.AccountHeadId == headId && c.IsActive);
        if (!active) return $"'{h.Code} - {h.Name}' is not active for this company.";
        return null;
    }

    private async Task<FiscalYear?> OpenFiscalYearAsync(int companyId, DateOnly date) =>
        await _db.FiscalYears.FirstOrDefaultAsync(f =>
            f.CompanyId == companyId && f.StartDate <= date && f.EndDate >= date && !f.IsClosed);
}
