namespace LiteErp.Core.Enums;

/// <summary>Functional COA heads — the five classic account types.</summary>
public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Capital = 3,
    Revenue = 4,
    Expense = 5
}

/// <summary>Voucher document types.</summary>
public enum VoucherType
{
    JV = 1,  // Journal voucher
    PV = 2,  // Payment voucher
    RV = 3,  // Receipt voucher
    CV = 4   // Contra voucher
}

/// <summary>Voucher lifecycle: editable draft, then immutable posted.</summary>
public enum VoucherStatus
{
    Draft = 1,
    Posted = 2
}
