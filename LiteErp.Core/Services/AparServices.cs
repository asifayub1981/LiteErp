using LiteErp.Core.Enums;

namespace LiteErp.Core.Services;

/// <summary>Atomic per-(Company, DocumentType, FiscalYear) numbering for
/// non-voucher documents (party codes, invoices). Same at-save pattern as vouchers.</summary>
public interface IDocumentNumberService
{
    Task<string> NextNumberAsync(int companyId, DocumentType type, int fiscalYearId,
        CancellationToken ct = default);
}

/// <summary>Invoice posting. A posted invoice auto-creates its voucher
/// (SJV for sales, PJV for purchase) and becomes immutable.</summary>
public interface IInvoicePostingService
{
    /// <summary>Validates and posts a draft sales invoice. Returns null on success, else the error.</summary>
    Task<string?> PostSalesAsync(int invoiceId, string postedBy, CancellationToken ct = default);

    /// <summary>Validates and posts a draft purchase invoice. Returns null on success, else the error.</summary>
    Task<string?> PostPurchaseAsync(int invoiceId, string postedBy, CancellationToken ct = default);
}
