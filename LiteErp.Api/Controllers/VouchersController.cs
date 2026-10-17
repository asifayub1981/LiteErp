using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Api.Controllers;

/// <summary>Voucher entry, posting and reversal. Firm rule: vouchers are never
/// deleted — drafts are editable, posted vouchers are immutable.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VouchersController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IVoucherNumberService _numbers;
    private readonly IVoucherPostingService _posting;

    public VouchersController(ErpDbContext db, IVoucherNumberService numbers,
        IVoucherPostingService posting)
    {
        _db = db; _numbers = numbers; _posting = posting;
    }

    public record VoucherLineDto(int AccountHeadId, int? CostCenterId, decimal Debit,
        decimal Credit, int? TaxRuleId, string? Narration);

    public record AllocationDto(int? SalesInvoiceId, int? PurchaseInvoiceId, decimal Amount);

    public record SaveVoucherDto(int CompanyId, int? ProjectId, VoucherType VoucherType,
        DateOnly VoucherDate, string CurrencyCode, decimal ExchangeRate,
        string? Narration, List<VoucherLineDto> Lines, List<AllocationDto>? Allocations);

    public record VoucherListDto(int Id, string VoucherNo, DateOnly VoucherDate,
        string VoucherType, string TypePrefix, string? ProjectName, string Narration,
        decimal TotalDr, decimal TotalCr, string Status, bool CanReverse);

    public record VoucherLineDetailDto(int Id, int AccountHeadId, string HeadCode,
        string HeadName, int? CostCenterId, string? CostCenterName, decimal Debit,
        decimal Credit, int? TaxRuleId, string Narration);

    public record VoucherDetailDto(int Id, string VoucherNo, int CompanyId, string CompanyName,
        int? ProjectId, string VoucherType, string TypePrefix, DateOnly VoucherDate,
        int FiscalYearId, string CurrencyCode, decimal ExchangeRate, string Narration,
        string Status, int? ReversalOfVoucherId, string? ReversalOfVoucherNo,
        List<VoucherLineDetailDto> Lines, List<AllocationDetailDto> Allocations);

    public record AllocationDetailDto(int? SalesInvoiceId, string? SalesInvoiceNo,
        int? PurchaseInvoiceId, string? PurchaseInvoiceNo, decimal Amount);

    // ---------- List ----------
    [HttpGet]
    public async Task<ActionResult<List<VoucherListDto>>> List(
        [FromQuery] int companyId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] VoucherType? type, [FromQuery] VoucherStatus? status,
        [FromQuery] string? search)
    {
        var q = _db.Vouchers.AsNoTracking()
            .Where(v => v.CompanyId == companyId && v.VoucherDate >= from && v.VoucherDate <= to);

        if (type.HasValue) q = q.Where(v => v.VoucherType == type.Value);
        if (status.HasValue) q = q.Where(v => v.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(v => v.VoucherNo.Contains(search) || v.Narration.Contains(search));

        var reversedIds = await _db.Vouchers.AsNoTracking()
            .Where(v => v.ReversalOfVoucherId != null)
            .Select(v => v.ReversalOfVoucherId!.Value).ToListAsync();

        return Ok(await q
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Select(v => new VoucherListDto(
                v.Id, v.VoucherNo, v.VoucherDate,
                v.VoucherType.ToString(), v.VoucherType.GetPrefix(),
                v.Project != null ? v.Project.ProjectName : null,
                v.Narration,
                v.Lines.Sum(l => l.Debit), v.Lines.Sum(l => l.Credit),
                v.Status.ToString(),
                v.Status == VoucherStatus.Posted && !reversedIds.Contains(v.Id)))
            .ToListAsync());
    }

    // ---------- Detail ----------
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VoucherDetailDto>> Get(int id)
    {
        var dto = await BuildDetailAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    private async Task<VoucherDetailDto?> BuildDetailAsync(int id)
    {
        var v = await _db.Vouchers.AsNoTracking()
            .Include(x => x.Company).Include(x => x.Project).Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return null;

        var headIds = v.Lines.Select(l => l.AccountHeadId).Distinct().ToList();
        var heads = await _db.AccountHeads.AsNoTracking()
            .Where(h => headIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id);
        var ccIds = v.Lines.Where(l => l.CostCenterId.HasValue)
            .Select(l => l.CostCenterId!.Value).Distinct().ToList();
        var ccs = ccIds.Count == 0 ? new Dictionary<int, string>() :
            await _db.CostCenters.AsNoTracking()
                .Where(c => ccIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => $"{c.Code} - {c.Name}");
        string? reversalNo = null;
        if (v.ReversalOfVoucherId.HasValue)
            reversalNo = await _db.Vouchers.AsNoTracking()
                .Where(x => x.Id == v.ReversalOfVoucherId.Value)
                .Select(x => x.VoucherNo).FirstOrDefaultAsync();

        var allocs = await _db.VoucherAllocations.AsNoTracking()
            .Where(a => a.VoucherId == v.Id)
            .Select(a => new AllocationDetailDto(a.SalesInvoiceId,
                a.SalesInvoice != null ? a.SalesInvoice.InvoiceNo : null,
                a.PurchaseInvoiceId,
                a.PurchaseInvoice != null ? a.PurchaseInvoice.InvoiceNo : null,
                a.Amount))
            .ToListAsync();

        return new VoucherDetailDto(
            v.Id, v.VoucherNo, v.CompanyId, v.Company.CompanyName, v.ProjectId,
            v.VoucherType.ToString(), v.VoucherType.GetPrefix(), v.VoucherDate,
            v.FiscalYearId, v.CurrencyCode, v.ExchangeRate, v.Narration,
            v.Status.ToString(), v.ReversalOfVoucherId, reversalNo,
            v.Lines.Select(l => new VoucherLineDetailDto(
                l.Id, l.AccountHeadId,
                heads.TryGetValue(l.AccountHeadId, out var h) ? h.Code : "?",
                heads.TryGetValue(l.AccountHeadId, out var h2) ? h2.Name : "?",
                l.CostCenterId,
                l.CostCenterId.HasValue && ccs.TryGetValue(l.CostCenterId.Value, out var cc) ? cc : null,
                l.Debit, l.Credit, l.TaxRuleId, l.Narration)).ToList(),
            allocs);
    }

    // ---------- Create (draft, number assigned at save) ----------
    [HttpPost]
    public async Task<ActionResult<VoucherDetailDto>> Create(SaveVoucherDto dto)
    {
        var err = await ValidateHeaderAsync(dto);
        if (err is not null) return BadRequest(new { message = err });

        var fy = await FiscalYearForAsync(dto.CompanyId, dto.VoucherDate);
        if (fy is null) return BadRequest(new { message = "No fiscal year covers the voucher date." });
        if (fy.IsClosed) return BadRequest(new { message = "Fiscal year is closed." });

        var v = new Voucher
        {
            CompanyId = dto.CompanyId,
            ProjectId = dto.ProjectId,
            VoucherNo = await _numbers.NextNumberAsync(dto.CompanyId, dto.VoucherType, fy.Id),
            VoucherType = dto.VoucherType,
            VoucherDate = dto.VoucherDate,
            FiscalYearId = fy.Id,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = dto.ExchangeRate <= 0 ? 1m : dto.ExchangeRate,
            Narration = dto.Narration ?? string.Empty,
            Status = VoucherStatus.Draft,
            CreatedBy = User.Identity?.Name ?? "api",
            CreatedOn = DateTime.UtcNow,
            Lines = dto.Lines.Select(l => new VoucherLine
            {
                AccountHeadId = l.AccountHeadId,
                CostCenterId = l.CostCenterId,
                Debit = l.Debit,
                Credit = l.Credit,
                TaxRuleId = l.TaxRuleId,
                Narration = l.Narration ?? string.Empty
            }).ToList()
        };
        _db.Vouchers.Add(v);
        await _db.SaveChangesAsync();

        var allocErr = await SaveAllocationsAsync(v, dto.Allocations);
        if (allocErr is not null)
        {
            // Roll back the voucher — an allocation error means the receipt is wrong.
            _db.Vouchers.Remove(v);
            await _db.SaveChangesAsync();
            return BadRequest(new { message = allocErr });
        }

        return CreatedAtAction(nameof(Get), new { id = v.Id }, await BuildDetailAsync(v.Id));
    }

    // ---------- Update draft (posted vouchers are immutable) ----------
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VoucherDetailDto>> Update(int id, SaveVoucherDto dto)
    {
        var v = await _db.Vouchers.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (v is null) return NotFound();
        if (v.Status == VoucherStatus.Posted)
            return BadRequest(new { message = "Posted vouchers cannot be edited. Create a reversal voucher instead." });

        var err = await ValidateHeaderAsync(dto);
        if (err is not null) return BadRequest(new { message = err });

        var fy = await FiscalYearForAsync(dto.CompanyId, dto.VoucherDate);
        if (fy is null) return BadRequest(new { message = "No fiscal year covers the voucher date." });
        if (fy.IsClosed) return BadRequest(new { message = "Fiscal year is closed." });

        // Type/company are structural — changing them would orphan the number sequence.
        // Only date (within FY rules), project, currency, narration and lines are editable.
        v.VoucherDate = dto.VoucherDate;
        v.FiscalYearId = fy.Id;
        v.ProjectId = dto.ProjectId;
        v.CurrencyCode = dto.CurrencyCode;
        v.ExchangeRate = dto.ExchangeRate <= 0 ? 1m : dto.ExchangeRate;
        v.Narration = dto.Narration ?? string.Empty;

        _db.VoucherLines.RemoveRange(v.Lines);
        v.Lines = dto.Lines.Select(l => new VoucherLine
        {
            AccountHeadId = l.AccountHeadId,
            CostCenterId = l.CostCenterId,
            Debit = l.Debit,
            Credit = l.Credit,
            TaxRuleId = l.TaxRuleId,
            Narration = l.Narration ?? string.Empty
        }).ToList();

        _db.VoucherAllocations.RemoveRange(
            _db.VoucherAllocations.Where(a => a.VoucherId == v.Id));
        await _db.SaveChangesAsync();

        var allocErr = await SaveAllocationsAsync(v, dto.Allocations);
        if (allocErr is not null) return BadRequest(new { message = allocErr });

        return Ok(await BuildDetailAsync(v.Id));
    }

    /// <summary>Validates and saves invoice allocations for a receipt/payment voucher.
    /// Returns null on success, else the error.</summary>
    private async Task<string?> SaveAllocationsAsync(Voucher v, List<AllocationDto>? allocs)
    {
        if (allocs is null || allocs.Count == 0) return null;

        var isReceipt = v.VoucherType is VoucherType.BRV or VoucherType.CRV;
        var isPayment = v.VoucherType is VoucherType.BPV or VoucherType.CPV;
        if (!isReceipt && !isPayment)
            return "Invoice allocation is only allowed on receipt/payment vouchers (BRV/CRV/BPV/CPV).";

        foreach (var a in allocs)
        {
            if (a.Amount <= 0) return "Allocation amount must be greater than zero.";
            if (a.SalesInvoiceId.HasValue == a.PurchaseInvoiceId.HasValue)
                return "Each allocation must reference exactly one invoice.";
            if (!isReceipt && a.SalesInvoiceId.HasValue)
                return "Sales invoices can only be settled by receipt vouchers (BRV/CRV).";
            if (!isPayment && a.PurchaseInvoiceId.HasValue)
                return "Purchase invoices can only be settled by payment vouchers (BPV/CPV).";

            decimal grand, balance;
            if (a.SalesInvoiceId.HasValue)
            {
                var inv = await _db.SalesInvoices.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == a.SalesInvoiceId.Value);
                if (inv is null || inv.CompanyId != v.CompanyId)
                    return $"Sales invoice #{a.SalesInvoiceId} not found for this company.";
                if (inv.Status != VoucherStatus.Posted)
                    return $"Sales invoice {inv.InvoiceNo} is not posted.";
                grand = await _db.SalesInvoiceLines.AsNoTracking()
                    .Where(l => l.SalesInvoiceId == inv.Id).SumAsync(l => l.Amount + l.TaxAmount);
                var paid = await _db.VoucherAllocations.AsNoTracking()
                    .Where(x => x.SalesInvoiceId == inv.Id && x.VoucherId != v.Id)
                    .SumAsync(x => x.Amount);
                balance = grand - paid;
            }
            else
            {
                var inv = await _db.PurchaseInvoices.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == a.PurchaseInvoiceId!.Value);
                if (inv is null || inv.CompanyId != v.CompanyId)
                    return $"Purchase invoice #{a.PurchaseInvoiceId} not found for this company.";
                if (inv.Status != VoucherStatus.Posted)
                    return $"Purchase invoice {inv.InvoiceNo} is not posted.";
                grand = await _db.PurchaseInvoiceLines.AsNoTracking()
                    .Where(l => l.PurchaseInvoiceId == inv.Id).SumAsync(l => l.Amount + l.TaxAmount);
                var paid = await _db.VoucherAllocations.AsNoTracking()
                    .Where(x => x.PurchaseInvoiceId == inv.Id && x.VoucherId != v.Id)
                    .SumAsync(x => x.Amount);
                balance = grand - paid;
            }
            if (a.Amount > balance)
                return $"Allocation {a.Amount:N2} exceeds invoice balance {balance:N2}.";
        }

        foreach (var a in allocs)
            _db.VoucherAllocations.Add(new VoucherAllocation
            {
                VoucherId = v.Id,
                SalesInvoiceId = a.SalesInvoiceId,
                PurchaseInvoiceId = a.PurchaseInvoiceId,
                Amount = a.Amount
            });
        await _db.SaveChangesAsync();
        return null;
    }

    // ---------- Post ----------
    [HttpPost("{id:int}/post")]
    public async Task<ActionResult> Post(int id)
    {
        var err = await _posting.PostAsync(id, User.Identity?.Name ?? "api");
        return err is null ? Ok(new { message = "Voucher posted." })
                           : BadRequest(new { message = err });
    }

    // ---------- Reverse ----------
    [HttpPost("{id:int}/reverse")]
    public async Task<ActionResult> Reverse(int id)
    {
        var (reversalId, err) = await _posting.ReverseAsync(id, User.Identity?.Name ?? "api");
        return err is null ? Ok(new { id = reversalId, message = "Reversal draft created." })
                           : BadRequest(new { message = err });
    }

    // NOTE: no DELETE endpoint — firm rule: documents are never deleted.

    private async Task<string?> ValidateHeaderAsync(SaveVoucherDto dto)
    {
        if (!await _db.Companies.AnyAsync(c => c.Id == dto.CompanyId && c.IsActive))
            return "Company not found or inactive.";
        if (dto.ProjectId.HasValue &&
            !await _db.Projects.AnyAsync(p => p.Id == dto.ProjectId.Value && p.CompanyId == dto.CompanyId))
            return "Project does not belong to the company.";
        if (!await _db.Currencies.AnyAsync(c => c.Code == dto.CurrencyCode))
            return "Currency not found.";
        if (dto.Lines is null || dto.Lines.Count == 0)
            return "Voucher must have at least one line.";
        return null;
    }

    private Task<FiscalYear?> FiscalYearForAsync(int companyId, DateOnly date) =>
        _db.FiscalYears.FirstOrDefaultAsync(f =>
            f.CompanyId == companyId && f.StartDate <= date && f.EndDate >= date);
}
