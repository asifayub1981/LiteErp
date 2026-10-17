using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Infrastructure.Services;

/// <summary>
/// Invoice posting. A posted invoice auto-creates its voucher (SJV/PJV) through
/// the normal voucher pipeline — same numbering, same validations — and the
/// invoice itself becomes immutable.
/// </summary>
public class InvoicePostingService(ErpDbContext db, IVoucherNumberService numbers,
    IVoucherPostingService vouchers) : IInvoicePostingService
{
    public async Task<string?> PostSalesAsync(int invoiceId, string postedBy, CancellationToken ct = default)
    {
        var inv = await db.SalesInvoices.Include(x => x.Lines).Include(x => x.Party)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (inv is null) return "Invoice not found.";
        if (inv.Status == VoucherStatus.Posted) return "Invoice is already posted.";

        var err = await ValidateHeaderAsync(inv.CompanyId, inv.Party, inv.FiscalYearId,
            inv.InvoiceDate, inv.Lines.Count, ct);
        if (err is not null) return err;

        var lines = new List<VoucherLine>
        {
            // Dr Customer (receivable)
            new() { AccountHeadId = inv.Party.AccountHeadId, Debit = inv.GrandTotal, Credit = 0,
                    Narration = $"Sales invoice {inv.InvoiceNo}" }
        };
        var lineErr = await BuildInvoiceLinesAsync(lines, inv.CompanyId,
            inv.Lines.Select(l => (l.AccountHeadId, l.Amount, l.TaxRuleId, l.TaxAmount,
                AccountType.Revenue, isSales: true)).ToList(), ct);
        if (lineErr is not null) return lineErr;

        return await PostInvoiceVoucherAsync(inv.CompanyId, inv.ProjectId, VoucherType.SJV,
            inv.InvoiceDate, inv.FiscalYearId, inv.CurrencyCode, inv.ExchangeRate,
            $"Auto SJV for sales invoice {inv.InvoiceNo} - {inv.Party.Name}",
            lines, postedBy,
            setPosted: vid => { inv.PostedVoucherId = vid; inv.Status = VoucherStatus.Posted; }, ct);
    }

    public async Task<string?> PostPurchaseAsync(int invoiceId, string postedBy, CancellationToken ct = default)
    {
        var inv = await db.PurchaseInvoices.Include(x => x.Lines).Include(x => x.Party)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, ct);
        if (inv is null) return "Invoice not found.";
        if (inv.Status == VoucherStatus.Posted) return "Invoice is already posted.";

        var err = await ValidateHeaderAsync(inv.CompanyId, inv.Party, inv.FiscalYearId,
            inv.InvoiceDate, inv.Lines.Count, ct);
        if (err is not null) return err;

        var lines = new List<VoucherLine>();
        var lineErr = await BuildInvoiceLinesAsync(lines, inv.CompanyId,
            inv.Lines.Select(l => (l.AccountHeadId, l.Amount, l.TaxRuleId, l.TaxAmount,
                AccountType.Expense, isSales: false)).ToList(), ct);
        if (lineErr is not null) return lineErr;
        // Cr Supplier (payable)
        lines.Add(new VoucherLine { AccountHeadId = inv.Party.AccountHeadId, Debit = 0,
            Credit = inv.GrandTotal, Narration = $"Purchase invoice {inv.InvoiceNo}" });

        return await PostInvoiceVoucherAsync(inv.CompanyId, inv.ProjectId, VoucherType.PJV,
            inv.InvoiceDate, inv.FiscalYearId, inv.CurrencyCode, inv.ExchangeRate,
            $"Auto PJV for purchase invoice {inv.InvoiceNo} - {inv.Party.Name}",
            lines, postedBy,
            setPosted: vid => { inv.PostedVoucherId = vid; inv.Status = VoucherStatus.Posted; }, ct);
    }

    // Builds Cr (sales) or Dr (purchase) lines for revenue/expense heads + tax heads.
    private async Task<string?> BuildInvoiceLinesAsync(List<VoucherLine> out_,
        int companyId, List<(int headId, decimal amount, int? taxRuleId, decimal taxAmount,
            AccountType expectedType, bool isSales)> src, CancellationToken ct)
    {
        var headIds = src.Select(s => s.headId).Distinct().ToList();
        var heads = await db.AccountHeads.Where(h => headIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, ct);
        var activeIds = await db.CompanyAccountHeads
            .Where(c => c.CompanyId == companyId && headIds.Contains(c.AccountHeadId) && c.IsActive)
            .Select(c => c.AccountHeadId).ToListAsync(ct);
        var ruleIds = src.Where(s => s.taxRuleId.HasValue).Select(s => s.taxRuleId!.Value)
            .Distinct().ToList();
        var rules = ruleIds.Count == 0 ? new Dictionary<int, TaxRule>() :
            await db.TaxRules.Where(r => ruleIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, ct);
        var taxHeadIds = rules.Values.Where(r => r.TaxAccountHeadId.HasValue)
            .Select(r => r.TaxAccountHeadId!.Value).Distinct().ToList();
        var taxHeads = taxHeadIds.Count == 0 ? new Dictionary<int, AccountHead>() :
            await db.AccountHeads.Where(h => taxHeadIds.Contains(h.Id))
                .ToDictionaryAsync(h => h.Id, ct);
        var taxActiveIds = taxHeadIds.Count == 0 ? new List<int>() :
            await db.CompanyAccountHeads
                .Where(c => c.CompanyId == companyId && taxHeadIds.Contains(c.AccountHeadId) && c.IsActive)
                .Select(c => c.AccountHeadId).ToListAsync(ct);

        foreach (var s in src)
        {
            if (s.amount <= 0) return "Every line must have an amount greater than zero.";
            if (!heads.TryGetValue(s.headId, out var h))
                return $"Account head #{s.headId} not found.";
            if (!h.IsTransactional)
                return $"'{h.Code} - {h.Name}' is a group head — invoice lines need leaf heads.";
            if (!h.IsActive || !activeIds.Contains(h.Id))
                return $"'{h.Code} - {h.Name}' is not active for this company.";
            // Sales lines post to Revenue heads; purchase lines to Expense (or Asset) heads.
            var okType = s.expectedType == AccountType.Revenue
                ? h.AccountType == AccountType.Revenue
                : h.AccountType is AccountType.Expense or AccountType.Asset;
            if (!okType)
                return $"'{h.Code} - {h.Name}' is not a {(s.isSales ? "revenue" : "expense/asset")} head.";

            out_.Add(s.isSales
                ? new VoucherLine { AccountHeadId = s.headId, Debit = 0, Credit = s.amount }
                : new VoucherLine { AccountHeadId = s.headId, Debit = s.amount, Credit = 0 });

            if (s.taxRuleId.HasValue)
            {
                if (!rules.TryGetValue(s.taxRuleId.Value, out var rule) || !rule.IsActive)
                    return $"Tax rule #{s.taxRuleId} not found or inactive.";
                if (s.taxAmount < 0) return "Tax amount cannot be negative.";
                if (s.taxAmount > 0)
                {
                    if (!rule.TaxAccountHeadId.HasValue)
                        return $"Tax rule '{rule.Name}' has no posting head configured.";
                    var th = taxHeads[rule.TaxAccountHeadId.Value];
                    if (!th.IsTransactional || !taxActiveIds.Contains(th.Id))
                        return $"Tax head '{th.Code} - {th.Name}' is not a usable leaf head.";
                    out_.Add(s.isSales
                        ? new VoucherLine { AccountHeadId = th.Id, Debit = 0, Credit = s.taxAmount,
                                            TaxRuleId = rule.Id, Narration = rule.Name }
                        : new VoucherLine { AccountHeadId = th.Id, Debit = s.taxAmount, Credit = 0,
                                            TaxRuleId = rule.Id, Narration = rule.Name });
                }
            }
        }
        return null;
    }

    private async Task<string?> ValidateHeaderAsync(int companyId, Party party, int fyId,
        DateOnly date, int lineCount, CancellationToken ct)
    {
        if (lineCount == 0) return "Invoice must have at least one line.";
        if (!party.IsActive) return $"Party '{party.Name}' is inactive.";

        var fy = await db.FiscalYears.FindAsync([fyId], ct);
        if (fy is null) return "Fiscal year not found.";
        if (fy.IsClosed) return "Fiscal year is closed — posting is blocked.";
        if (date < fy.StartDate || date > fy.EndDate)
            return "Invoice date falls outside its fiscal year.";
        return null;
    }

    private async Task<string?> PostInvoiceVoucherAsync(int companyId, int? projectId,
        VoucherType type, DateOnly date, int fyId, string currencyCode, decimal rate,
        string narration, List<VoucherLine> lines, string postedBy,
        Action<int> setPosted, CancellationToken ct)
    {
        var v = new Voucher
        {
            CompanyId = companyId,
            ProjectId = projectId,
            VoucherNo = await numbers.NextNumberAsync(companyId, type, fyId, ct),
            VoucherType = type,
            VoucherDate = date,
            FiscalYearId = fyId,
            CurrencyCode = currencyCode,
            ExchangeRate = rate,
            Narration = narration,
            Status = VoucherStatus.Draft,
            CreatedBy = postedBy,
            CreatedOn = DateTime.UtcNow,
            Lines = lines
        };
        db.Vouchers.Add(v);
        await db.SaveChangesAsync(ct);

        var err = await vouchers.PostAsync(v.Id, postedBy, ct);
        if (err is not null) return $"Auto-voucher failed: {err}";

        setPosted(v.Id);
        await db.SaveChangesAsync(ct);
        return null;
    }
}
