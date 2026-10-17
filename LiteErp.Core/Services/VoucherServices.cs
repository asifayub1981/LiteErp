using LiteErp.Core.Enums;

namespace LiteErp.Core.Services;

/// <summary>Atomic per-(Company, Type, FiscalYear) document numbering. Numbers are
/// consumed at save time via a single UPDATE — safe under concurrent users.</summary>
public interface IVoucherNumberService
{
    Task<string> NextNumberAsync(int companyId, VoucherType type, int fiscalYearId,
        CancellationToken ct = default);
}

/// <summary>Posting + reversal. Posted vouchers are immutable; corrections go
/// through reversal vouchers. Nothing is ever deleted.</summary>
public interface IVoucherPostingService
{
    /// <summary>Validates and posts a draft. Returns null on success, else the error.</summary>
    Task<string?> PostAsync(int voucherId, string postedBy, CancellationToken ct = default);

    /// <summary>Creates a draft reversal (Dr/Cr swapped) of a posted voucher.
    /// Returns the new voucher id, or an error.</summary>
    Task<(int reversalId, string? error)> ReverseAsync(int voucherId, string createdBy,
        CancellationToken ct = default);
}
