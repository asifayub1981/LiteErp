using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Infrastructure.Services;

/// <summary>Posting validation + reversal. Posted vouchers are immutable;
/// wrong entries are corrected with a reversal voucher, never edits or deletes.</summary>
public class VoucherPostingService(ErpDbContext db, IVoucherNumberService numbers) : IVoucherPostingService
{
    public async Task<string?> PostAsync(int voucherId, string postedBy, CancellationToken ct = default)
    {
        var v = await db.Vouchers.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == voucherId, ct);
        if (v is null) return "Voucher not found.";
        if (v.Status == VoucherStatus.Posted) return "Voucher is already posted.";

        var err = await ValidateAsync(v, ct);
        if (err is not null) return err;

        v.Status = VoucherStatus.Posted;
        v.PostedBy = postedBy;
        v.PostedOn = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<(int reversalId, string? error)> ReverseAsync(int voucherId, string createdBy,
        CancellationToken ct = default)
    {
        var v = await db.Vouchers.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == voucherId, ct);
        if (v is null) return (0, "Voucher not found.");
        if (v.Status != VoucherStatus.Posted) return (0, "Only posted vouchers can be reversed.");
        if (await db.Vouchers.AnyAsync(x => x.ReversalOfVoucherId == voucherId, ct))
            return (0, "This voucher has already been reversed.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fy = await db.FiscalYears.FirstOrDefaultAsync(
            f => f.CompanyId == v.CompanyId && f.StartDate <= today && f.EndDate >= today && !f.IsClosed, ct);
        if (fy is null)
            return (0, "No open fiscal year covers today — the reversal cannot be dated.");

        var reversal = new Voucher
        {
            CompanyId = v.CompanyId,
            ProjectId = v.ProjectId,
            VoucherNo = await numbers.NextNumberAsync(v.CompanyId, v.VoucherType, fy.Id, ct),
            VoucherType = v.VoucherType,
            VoucherDate = today,
            FiscalYearId = fy.Id,
            CurrencyCode = v.CurrencyCode,
            ExchangeRate = v.ExchangeRate,
            Narration = $"Reversal of {v.VoucherNo}",
            Status = VoucherStatus.Draft,
            ReversalOfVoucherId = v.Id,
            CreatedBy = createdBy,
            CreatedOn = DateTime.UtcNow,
            Lines = v.Lines.Select(l => new VoucherLine
            {
                AccountHeadId = l.AccountHeadId,
                CostCenterId = l.CostCenterId,
                Debit = l.Credit,    // swapped
                Credit = l.Debit,    // swapped
                TaxRuleId = l.TaxRuleId,
                Narration = l.Narration
            }).ToList()
        };
        db.Vouchers.Add(reversal);
        await db.SaveChangesAsync(ct);
        return (reversal.Id, null);
    }

    private async Task<string?> ValidateAsync(Voucher v, CancellationToken ct)
    {
        if (v.Lines.Count == 0) return "Voucher must have at least one line.";
        var dr = v.Lines.Sum(l => l.Debit);
        var cr = v.Lines.Sum(l => l.Credit);
        if (dr <= 0 || dr != cr)
            return "Total debit must equal total credit and be greater than zero.";

        var fy = await db.FiscalYears.FindAsync([v.FiscalYearId], ct);
        if (fy is null) return "Fiscal year not found.";
        if (fy.IsClosed) return "Fiscal year is closed — posting is blocked.";
        if (v.VoucherDate < fy.StartDate || v.VoucherDate > fy.EndDate)
            return "Voucher date falls outside its fiscal year.";

        var headIds = v.Lines.Select(l => l.AccountHeadId).Distinct().ToList();
        var heads = await db.AccountHeads
            .Where(h => headIds.Contains(h.Id)).ToListAsync(ct);
        var activeIds = await db.CompanyAccountHeads
            .Where(c => c.CompanyId == v.CompanyId && headIds.Contains(c.AccountHeadId) && c.IsActive)
            .Select(c => c.AccountHeadId).ToListAsync(ct);
        var ccIds = v.Lines.Where(l => l.CostCenterId.HasValue)
            .Select(l => l.CostCenterId!.Value).Distinct().ToList();
        var validCcIds = ccIds.Count == 0 ? [] :
            await db.CostCenters
                .Where(c => ccIds.Contains(c.Id) && c.CompanyId == v.CompanyId && c.IsActive)
                .Select(c => c.Id).ToListAsync(ct);

        foreach (var l in v.Lines)
        {
            var h = heads.FirstOrDefault(x => x.Id == l.AccountHeadId);
            if (h is null) return $"Account head #{l.AccountHeadId} not found.";
            if (!h.IsTransactional)
                return $"'{h.Code} - {h.Name}' is a group head — posting is allowed on leaf heads only.";
            if (!h.IsActive || !activeIds.Contains(h.Id))
                return $"'{h.Code} - {h.Name}' is not active for this company.";
            if (l.Debit < 0 || l.Credit < 0) return "Amounts cannot be negative.";
            if (l.Debit > 0 && l.Credit > 0)
                return "A line cannot have both debit and credit.";
            if (l.Debit == 0 && l.Credit == 0)
                return "Every line must have a debit or a credit amount.";
            if (l.CostCenterId.HasValue && !validCcIds.Contains(l.CostCenterId.Value))
                return "Invalid or inactive cost center on a line.";
        }
        return null;
    }
}
