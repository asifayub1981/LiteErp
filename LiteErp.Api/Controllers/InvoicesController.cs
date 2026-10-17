using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

/// <summary>Sales/Purchase invoices. Draft → Posted (auto-creates SJV/PJV).
/// Firm rule: invoices are never deleted; posted invoices are immutable.</summary>
[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IDocumentNumberService _numbers;
    private readonly IInvoicePostingService _posting;

    public InvoicesController(ErpDbContext db, IDocumentNumberService numbers,
        IInvoicePostingService posting)
    {
        _db = db; _numbers = numbers; _posting = posting;
    }

    public record InvoiceLineDto(int AccountHeadId, string? Description, decimal Qty,
        decimal Rate, int? TaxRuleId);

    public record SaveInvoiceDto(int CompanyId, int? ProjectId, DateOnly InvoiceDate,
        int PartyId, string CurrencyCode, decimal ExchangeRate, string? Narration,
        List<InvoiceLineDto> Lines);

    public record InvoiceListDto(int Id, string InvoiceNo, DateOnly InvoiceDate,
        string PartyName, decimal GrandTotal, decimal Paid, decimal Balance,
        string Status, string? PostedVoucherNo);

    public record InvoiceLineDetailDto(int Id, int AccountHeadId, string HeadCode,
        string HeadName, string Description, decimal Qty, decimal Rate, decimal Amount,
        int? TaxRuleId, string? TaxRuleName, decimal TaxAmount);

    public record InvoiceDetailDto(int Id, string InvoiceNo, int CompanyId,
        int? ProjectId, string? ProjectName, DateOnly InvoiceDate, int FiscalYearId,
        int PartyId, string PartyName, string CurrencyCode, decimal ExchangeRate,
        string Narration, string Status, string? PostedVoucherNo,
        decimal GrandTotal, decimal Paid, decimal Balance,
        List<InvoiceLineDetailDto> Lines);

    // ================= SALES =================

    [HttpGet("sales")]
    public async Task<ActionResult<List<InvoiceListDto>>> ListSales(
        [FromQuery] int companyId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] VoucherStatus? status, [FromQuery] string? search)
    {
        var q = _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.InvoiceDate >= from && i.InvoiceDate <= to);
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(i => i.InvoiceNo.Contains(search) || i.Narration.Contains(search));

        var list = await q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Select(i => new { i.Id, i.InvoiceNo, i.InvoiceDate, i.Status,
                PartyName = i.Party.Name, PostedNo = i.PostedVoucher != null ? i.PostedVoucher.VoucherNo : null })
            .ToListAsync();

        var result = new List<InvoiceListDto>();
        foreach (var i in list)
        {
            var (grand, paid) = await SalesTotalsAsync(i.Id);
            result.Add(new InvoiceListDto(i.Id, i.InvoiceNo, i.InvoiceDate, i.PartyName,
                grand, paid, grand - paid, i.Status.ToString(), i.PostedNo));
        }
        return Ok(result);
    }

    [HttpGet("sales/{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> GetSales(int id)
    {
        var dto = await BuildSalesDetailAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("sales")]
    public async Task<ActionResult<InvoiceDetailDto>> CreateSales(SaveInvoiceDto dto)
    {
        var (inv, error) = await BuildSalesAsync(dto);
        if (error is not null) return BadRequest(new { message = error });
        _db.SalesInvoices.Add(inv!);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSales), new { id = inv!.Id },
            await BuildSalesDetailAsync(inv.Id));
    }

    [HttpPut("sales/{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> UpdateSales(int id, SaveInvoiceDto dto)
    {
        var inv = await _db.SalesInvoices.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (inv is null) return NotFound();
        if (inv.Status != VoucherStatus.Draft)
            return BadRequest(new { message = "Posted invoices cannot be edited." });

        var error = await ApplyInvoiceEditAsync(dto, inv.CompanyId,
            PartyType.Customer, inv.PartyId);
        if (error is not null) return BadRequest(new { message = error });

        _db.SalesInvoiceLines.RemoveRange(inv.Lines);
        inv.InvoiceDate = dto.InvoiceDate;
        inv.PartyId = dto.PartyId;
        inv.ProjectId = dto.ProjectId;
        inv.CurrencyCode = dto.CurrencyCode;
        inv.ExchangeRate = dto.ExchangeRate;
        inv.Narration = dto.Narration ?? string.Empty;
        inv.FiscalYearId = (await FiscalYearForAsync(inv.CompanyId, dto.InvoiceDate))?.Id
            ?? inv.FiscalYearId;
        foreach (var l in dto.Lines)
            inv.Lines.Add(await ToSalesLineAsync(l));
        await _db.SaveChangesAsync();
        return Ok(await BuildSalesDetailAsync(inv.Id));
    }

    [HttpPost("sales/{id:int}/post")]
    public async Task<ActionResult> PostSales(int id)
    {
        var err = await _posting.PostSalesAsync(id, User.Identity?.Name ?? "api");
        return err is null ? Ok(new { message = "Invoice posted." })
            : BadRequest(new { message = err });
    }

    // ================= PURCHASE =================

    [HttpGet("purchase")]
    public async Task<ActionResult<List<InvoiceListDto>>> ListPurchase(
        [FromQuery] int companyId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] VoucherStatus? status, [FromQuery] string? search)
    {
        var q = _db.PurchaseInvoices.AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.InvoiceDate >= from && i.InvoiceDate <= to);
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(i => i.InvoiceNo.Contains(search) || i.Narration.Contains(search));

        var list = await q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Select(i => new { i.Id, i.InvoiceNo, i.InvoiceDate, i.Status,
                PartyName = i.Party.Name, PostedNo = i.PostedVoucher != null ? i.PostedVoucher.VoucherNo : null })
            .ToListAsync();

        var result = new List<InvoiceListDto>();
        foreach (var i in list)
        {
            var (grand, paid) = await PurchaseTotalsAsync(i.Id);
            result.Add(new InvoiceListDto(i.Id, i.InvoiceNo, i.InvoiceDate, i.PartyName,
                grand, paid, grand - paid, i.Status.ToString(), i.PostedNo));
        }
        return Ok(result);
    }

    [HttpGet("purchase/{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> GetPurchase(int id)
    {
        var dto = await BuildPurchaseDetailAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("purchase")]
    public async Task<ActionResult<InvoiceDetailDto>> CreatePurchase(SaveInvoiceDto dto)
    {
        var (inv, error) = await BuildPurchaseAsync(dto);
        if (error is not null) return BadRequest(new { message = error });
        _db.PurchaseInvoices.Add(inv!);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPurchase), new { id = inv!.Id },
            await BuildPurchaseDetailAsync(inv.Id));
    }

    [HttpPut("purchase/{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> UpdatePurchase(int id, SaveInvoiceDto dto)
    {
        var inv = await _db.PurchaseInvoices.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (inv is null) return NotFound();
        if (inv.Status != VoucherStatus.Draft)
            return BadRequest(new { message = "Posted invoices cannot be edited." });

        var error = await ApplyInvoiceEditAsync(dto, inv.CompanyId,
            PartyType.Supplier, inv.PartyId);
        if (error is not null) return BadRequest(new { message = error });

        _db.PurchaseInvoiceLines.RemoveRange(inv.Lines);
        inv.InvoiceDate = dto.InvoiceDate;
        inv.PartyId = dto.PartyId;
        inv.ProjectId = dto.ProjectId;
        inv.CurrencyCode = dto.CurrencyCode;
        inv.ExchangeRate = dto.ExchangeRate;
        inv.Narration = dto.Narration ?? string.Empty;
        inv.FiscalYearId = (await FiscalYearForAsync(inv.CompanyId, dto.InvoiceDate))?.Id
            ?? inv.FiscalYearId;
        foreach (var l in dto.Lines)
            inv.Lines.Add(await ToPurchaseLineAsync(l));
        await _db.SaveChangesAsync();
        return Ok(await BuildPurchaseDetailAsync(inv.Id));
    }

    [HttpPost("purchase/{id:int}/post")]
    public async Task<ActionResult> PostPurchase(int id)
    {
        var err = await _posting.PostPurchaseAsync(id, User.Identity?.Name ?? "api");
        return err is null ? Ok(new { message = "Invoice posted." })
            : BadRequest(new { message = err });
    }

    // ================= Builders =================

    private async Task<(SalesInvoice? inv, string? error)> BuildSalesAsync(SaveInvoiceDto dto)
    {
        var error = await ValidateInvoiceDtoAsync(dto, PartyType.Customer);
        if (error is not null) return (null, error);
        var fy = await FiscalYearForAsync(dto.CompanyId, dto.InvoiceDate);
        var inv = new SalesInvoice
        {
            CompanyId = dto.CompanyId,
            ProjectId = dto.ProjectId,
            InvoiceNo = await _numbers.NextNumberAsync(dto.CompanyId,
                DocumentType.SalesInvoice, fy!.Id),
            InvoiceDate = dto.InvoiceDate,
            FiscalYearId = fy.Id,
            PartyId = dto.PartyId,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = dto.ExchangeRate,
            Narration = dto.Narration ?? string.Empty,
            Status = VoucherStatus.Draft,
            CreatedBy = User.Identity?.Name ?? "api",
            CreatedOn = DateTime.UtcNow
        };
        foreach (var l in dto.Lines) inv.Lines.Add(await ToSalesLineAsync(l));
        return (inv, null);
    }

    private async Task<(PurchaseInvoice? inv, string? error)> BuildPurchaseAsync(SaveInvoiceDto dto)
    {
        var error = await ValidateInvoiceDtoAsync(dto, PartyType.Supplier);
        if (error is not null) return (null, error);
        var fy = await FiscalYearForAsync(dto.CompanyId, dto.InvoiceDate);
        var inv = new PurchaseInvoice
        {
            CompanyId = dto.CompanyId,
            ProjectId = dto.ProjectId,
            InvoiceNo = await _numbers.NextNumberAsync(dto.CompanyId,
                DocumentType.PurchaseInvoice, fy!.Id),
            InvoiceDate = dto.InvoiceDate,
            FiscalYearId = fy.Id,
            PartyId = dto.PartyId,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = dto.ExchangeRate,
            Narration = dto.Narration ?? string.Empty,
            Status = VoucherStatus.Draft,
            CreatedBy = User.Identity?.Name ?? "api",
            CreatedOn = DateTime.UtcNow
        };
        foreach (var l in dto.Lines) inv.Lines.Add(await ToPurchaseLineAsync(l));
        return (inv, null);
    }

    private async Task<SalesInvoiceLine> ToSalesLineAsync(InvoiceLineDto l)
    {
        var rule = l.TaxRuleId.HasValue
            ? await _db.TaxRules.FindAsync(l.TaxRuleId.Value) : null;
        var amount = l.Qty * l.Rate;
        return new SalesInvoiceLine
        {
            AccountHeadId = l.AccountHeadId,
            Description = l.Description ?? string.Empty,
            Qty = l.Qty, Rate = l.Rate, Amount = amount,
            TaxRuleId = l.TaxRuleId,
            TaxAmount = rule is null ? 0 : Math.Round(amount * rule.Rate / 100, 2)
        };
    }

    private async Task<PurchaseInvoiceLine> ToPurchaseLineAsync(InvoiceLineDto l)
    {
        var rule = l.TaxRuleId.HasValue
            ? await _db.TaxRules.FindAsync(l.TaxRuleId.Value) : null;
        var amount = l.Qty * l.Rate;
        return new PurchaseInvoiceLine
        {
            AccountHeadId = l.AccountHeadId,
            Description = l.Description ?? string.Empty,
            Qty = l.Qty, Rate = l.Rate, Amount = amount,
            TaxRuleId = l.TaxRuleId,
            TaxAmount = rule is null ? 0 : Math.Round(amount * rule.Rate / 100, 2)
        };
    }

    private async Task<string?> ValidateInvoiceDtoAsync(SaveInvoiceDto dto, PartyType wantType)
    {
        if (dto.Lines.Count == 0) return "Invoice must have at least one line.";
        var party = await _db.Parties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == dto.PartyId && p.CompanyId == dto.CompanyId);
        if (party is null) return "Party not found.";
        if (party.PartyType != wantType)
            return $"Party '{party.Name}' is not a {(wantType == PartyType.Customer ? "customer" : "supplier")}.";
        if (!party.IsActive) return $"Party '{party.Name}' is inactive.";

        var fy = await FiscalYearForAsync(dto.CompanyId, dto.InvoiceDate);
        if (fy is null) return "No open fiscal year covers the invoice date.";

        var headIds = dto.Lines.Select(l => l.AccountHeadId).Distinct().ToList();
        var heads = await _db.AccountHeads.AsNoTracking()
            .Where(h => headIds.Contains(h.Id)).ToDictionaryAsync(h => h.Id);
        var activeIds = await _db.CompanyAccountHeads
            .Where(c => c.CompanyId == dto.CompanyId && headIds.Contains(c.AccountHeadId) && c.IsActive)
            .Select(c => c.AccountHeadId).ToListAsync();
        var wantHead = wantType == PartyType.Customer ? AccountType.Revenue : AccountType.Expense;

        foreach (var l in dto.Lines)
        {
            if (l.Qty <= 0 || l.Rate < 0) return "Qty must be positive and rate cannot be negative.";
            if (!heads.TryGetValue(l.AccountHeadId, out var h))
                return $"Account head #{l.AccountHeadId} not found.";
            if (!h.IsTransactional)
                return $"'{h.Code} - {h.Name}' is a group head — pick a leaf head.";
            if (!activeIds.Contains(h.Id))
                return $"'{h.Code} - {h.Name}' is not active for this company.";
            var okType = wantHead == AccountType.Revenue
                ? h.AccountType == AccountType.Revenue
                : h.AccountType is AccountType.Expense or AccountType.Asset;
            if (!okType)
                return $"'{h.Code} - {h.Name}' is not a {(wantType == PartyType.Customer ? "revenue" : "expense/asset")} head.";
            if (l.TaxRuleId.HasValue)
            {
                var rule = await _db.TaxRules.FindAsync(l.TaxRuleId.Value);
                if (rule is null || !rule.IsActive)
                    return $"Tax rule #{l.TaxRuleId} not found or inactive.";
            }
        }
        if (dto.ProjectId.HasValue)
        {
            var prj = await _db.Projects.AnyAsync(p =>
                p.Id == dto.ProjectId && p.CompanyId == dto.CompanyId);
            if (!prj) return "Project not found for this company.";
        }
        return null;
    }

    private async Task<string?> ApplyInvoiceEditAsync(SaveInvoiceDto dto, int companyId,
        PartyType wantType, int currentPartyId)
    {
        // Party change on edit: keep the invoice's company, validate the new party.
        var check = dto with { CompanyId = companyId };
        return await ValidateInvoiceDtoAsync(check, wantType);
    }

    private async Task<FiscalYear?> FiscalYearForAsync(int companyId, DateOnly date) =>
        await _db.FiscalYears.FirstOrDefaultAsync(f =>
            f.CompanyId == companyId && f.StartDate <= date && f.EndDate >= date && !f.IsClosed);

    // ================= Details =================

    private async Task<(decimal grand, decimal paid)> SalesTotalsAsync(int id)
    {
        var grand = await _db.SalesInvoiceLines.AsNoTracking()
            .Where(l => l.SalesInvoiceId == id).SumAsync(l => l.Amount + l.TaxAmount);
        var paid = await _db.VoucherAllocations.AsNoTracking()
            .Where(a => a.SalesInvoiceId == id && a.Voucher.Status == VoucherStatus.Posted)
            .SumAsync(a => a.Amount);
        return (grand, paid);
    }

    private async Task<(decimal grand, decimal paid)> PurchaseTotalsAsync(int id)
    {
        var grand = await _db.PurchaseInvoiceLines.AsNoTracking()
            .Where(l => l.PurchaseInvoiceId == id).SumAsync(l => l.Amount + l.TaxAmount);
        var paid = await _db.VoucherAllocations.AsNoTracking()
            .Where(a => a.PurchaseInvoiceId == id && a.Voucher.Status == VoucherStatus.Posted)
            .SumAsync(a => a.Amount);
        return (grand, paid);
    }

    private async Task<InvoiceDetailDto?> BuildSalesDetailAsync(int id)
    {
        var i = await _db.SalesInvoices.AsNoTracking()
            .Include(x => x.Party).Include(x => x.Project)
            .Include(x => x.PostedVoucher).Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return null;
        var headIds = i.Lines.Select(l => l.AccountHeadId).Distinct().ToList();
        var heads = await _db.AccountHeads.AsNoTracking()
            .Where(h => headIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => $"{h.Code} - {h.Name}");
        var ruleIds = i.Lines.Where(l => l.TaxRuleId.HasValue)
            .Select(l => l.TaxRuleId!.Value).Distinct().ToList();
        var rules = ruleIds.Count == 0 ? new Dictionary<int, string>() :
            await _db.TaxRules.AsNoTracking()
                .Where(r => ruleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name);
        var (grand, paid) = await SalesTotalsAsync(id);
        return new InvoiceDetailDto(i.Id, i.InvoiceNo, i.CompanyId, i.ProjectId,
            i.Project?.ProjectName, i.InvoiceDate, i.FiscalYearId, i.PartyId, i.Party.Name,
            i.CurrencyCode, i.ExchangeRate, i.Narration, i.Status.ToString(),
            i.PostedVoucher?.VoucherNo, grand, paid, grand - paid,
            i.Lines.Select(l =>
            {
                var hn = heads.GetValueOrDefault(l.AccountHeadId, "?").Split(" - ", 2);
                return new InvoiceLineDetailDto(l.Id, l.AccountHeadId,
                    hn.Length > 0 ? hn[0] : "?", hn.Length > 1 ? hn[1] : "?",
                    l.Description, l.Qty, l.Rate, l.Amount, l.TaxRuleId,
                    l.TaxRuleId.HasValue ? rules.GetValueOrDefault(l.TaxRuleId.Value) : null,
                    l.TaxAmount);
            }).ToList());
    }

    private async Task<InvoiceDetailDto?> BuildPurchaseDetailAsync(int id)
    {
        var i = await _db.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Party).Include(x => x.Project)
            .Include(x => x.PostedVoucher).Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (i is null) return null;
        var headIds = i.Lines.Select(l => l.AccountHeadId).Distinct().ToList();
        var heads = await _db.AccountHeads.AsNoTracking()
            .Where(h => headIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => $"{h.Code} - {h.Name}");
        var ruleIds = i.Lines.Where(l => l.TaxRuleId.HasValue)
            .Select(l => l.TaxRuleId!.Value).Distinct().ToList();
        var rules = ruleIds.Count == 0 ? new Dictionary<int, string>() :
            await _db.TaxRules.AsNoTracking()
                .Where(r => ruleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.Name);
        var (grand, paid) = await PurchaseTotalsAsync(id);
        return new InvoiceDetailDto(i.Id, i.InvoiceNo, i.CompanyId, i.ProjectId,
            i.Project?.ProjectName, i.InvoiceDate, i.FiscalYearId, i.PartyId, i.Party.Name,
            i.CurrencyCode, i.ExchangeRate, i.Narration, i.Status.ToString(),
            i.PostedVoucher?.VoucherNo, grand, paid, grand - paid,
            i.Lines.Select(l =>
            {
                var hn = heads.GetValueOrDefault(l.AccountHeadId, "?").Split(" - ", 2);
                return new InvoiceLineDetailDto(l.Id, l.AccountHeadId,
                    hn.Length > 0 ? hn[0] : "?", hn.Length > 1 ? hn[1] : "?",
                    l.Description, l.Qty, l.Rate, l.Amount, l.TaxRuleId,
                    l.TaxRuleId.HasValue ? rules.GetValueOrDefault(l.TaxRuleId.Value) : null,
                    l.TaxAmount);
            }).ToList());
    }
}
