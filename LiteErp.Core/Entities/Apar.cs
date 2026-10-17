using LiteErp.Core.Enums;

namespace LiteErp.Core.Entities;

/// <summary>
/// APAR party master — Customer or Supplier.
/// Each party is linked to one AR/AP leaf head in the COA; invoices and
/// receipts post against that head. A party that is both customer and
/// supplier is kept as two records (standard small-ERP practice).
/// </summary>
public class Party
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public PartyType PartyType { get; set; }
    public string Code { get; set; } = string.Empty;   // CUST-0001 / SUPP-0001 (per company+type)
    public string Name { get; set; } = string.Empty;
    public int AccountHeadId { get; set; }             // AR/AP leaf head (defaults to control head)
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNo { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public AccountHead AccountHead { get; set; } = null!;
}

/// <summary>
/// Sales invoice (receivable). Draft → Posted. Posting auto-creates an SJV:
/// Dr Party head, Cr revenue heads, Cr tax heads.
/// </summary>
public class SalesInvoice
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ProjectId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;  // SI-2027-0001
    public DateOnly InvoiceDate { get; set; }
    public int FiscalYearId { get; set; }
    public int PartyId { get; set; }
    public string CurrencyCode { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1m;
    public string Narration { get; set; } = string.Empty;
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;
    public int? PostedVoucherId { get; set; }              // auto-created SJV

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }

    public Company Company { get; set; } = null!;
    public Project? Project { get; set; }
    public Party Party { get; set; } = null!;
    public FiscalYear FiscalYear { get; set; } = null!;
    public Voucher? PostedVoucher { get; set; }
    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();

    public decimal GrandTotal => Lines.Sum(l => l.Amount + l.TaxAmount);
}

/// <summary>
/// Purchase invoice (payable). Draft → Posted. Posting auto-creates a PJV:
/// Dr expense/asset heads, Dr tax heads, Cr Party head.
/// </summary>
public class PurchaseInvoice
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ProjectId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;  // PI-2027-0001
    public DateOnly InvoiceDate { get; set; }
    public int FiscalYearId { get; set; }
    public int PartyId { get; set; }
    public string CurrencyCode { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1m;
    public string Narration { get; set; } = string.Empty;
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;
    public int? PostedVoucherId { get; set; }              // auto-created PJV

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }

    public Company Company { get; set; } = null!;
    public Project? Project { get; set; }
    public Party Party { get; set; } = null!;
    public FiscalYear FiscalYear { get; set; } = null!;
    public Voucher? PostedVoucher { get; set; }
    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();

    public decimal GrandTotal => Lines.Sum(l => l.Amount + l.TaxAmount);
}

public class SalesInvoiceLine
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public int AccountHeadId { get; set; }   // revenue leaf head
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; } = 1m;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }      // Qty × Rate (net)
    public int? TaxRuleId { get; set; }
    public decimal TaxAmount { get; set; }

    public SalesInvoice SalesInvoice { get; set; } = null!;
    public AccountHead AccountHead { get; set; } = null!;
    public TaxRule? TaxRule { get; set; }
}

public class PurchaseInvoiceLine
{
    public int Id { get; set; }
    public int PurchaseInvoiceId { get; set; }
    public int AccountHeadId { get; set; }   // expense/asset leaf head
    public string Description { get; set; } = string.Empty;
    public decimal Qty { get; set; } = 1m;
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public int? TaxRuleId { get; set; }
    public decimal TaxAmount { get; set; }

    public PurchaseInvoice PurchaseInvoice { get; set; } = null!;
    public AccountHead AccountHead { get; set; } = null!;
    public TaxRule? TaxRule { get; set; }
}

/// <summary>
/// Links a receipt/payment voucher (BRV/CRV/BPV/CPV) to the invoices it settles.
/// Invoice balance = GrandTotal − Σ allocations.
/// </summary>
public class VoucherAllocation
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public int? SalesInvoiceId { get; set; }
    public int? PurchaseInvoiceId { get; set; }
    public decimal Amount { get; set; }

    public Voucher Voucher { get; set; } = null!;
    public SalesInvoice? SalesInvoice { get; set; }
    public PurchaseInvoice? PurchaseInvoice { get; set; }
}

/// <summary>
/// Per-(Company, DocumentType, FiscalYear) running counter for non-voucher
/// documents (invoices, party codes). Same atomic at-save pattern as vouchers.
/// </summary>
public class DocumentSequence
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public DocumentType DocumentType { get; set; }
    public int FiscalYearId { get; set; }
    public int LastNumber { get; set; }

    public Company Company { get; set; } = null!;
    public FiscalYear FiscalYear { get; set; } = null!;
}
