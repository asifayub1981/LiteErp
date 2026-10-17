namespace LiteErp.Core.Enums;

/// <summary>Display prefixes for voucher document numbers (JV-2027-0001, BPV-2027-0001 …).</summary>
public static class VoucherTypeExtensions
{
    public static string GetPrefix(this VoucherType type) => type switch
    {
        VoucherType.JV => "JV",
        VoucherType.BPV => "BPV",
        VoucherType.CPV => "CPV",
        VoucherType.PJV => "PJV",
        VoucherType.SJV => "SJV",
        VoucherType.BRV => "BRV",
        VoucherType.CRV => "CRV",
        _ => type.ToString()
    };
}

/// <summary>Display prefixes for non-voucher document numbers.</summary>
public static class DocumentTypeExtensions
{
    public static string GetPrefix(this DocumentType type) => type switch
    {
        DocumentType.Customer => "CUST",
        DocumentType.Supplier => "SUPP",
        DocumentType.SalesInvoice => "SI",
        DocumentType.PurchaseInvoice => "PI",
        _ => type.ToString()
    };
}
