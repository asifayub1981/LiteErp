using LiteErp.Core.Enums;

namespace LiteErp.Core.Entities;

/// <summary>
/// Voucher header. Dimensions: CompanyId (required) + ProjectId (nullable —
/// company-level vouchers allowed). GroupId is derived via Company join, never stored.
/// </summary>
public class Voucher
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ProjectId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;  // per (Company, Type, FY): JV-2025-0001
    public VoucherType VoucherType { get; set; }
    public DateOnly VoucherDate { get; set; }
    public int FiscalYearId { get; set; }
    public string CurrencyCode { get; set; } = "PKR";
    public decimal ExchangeRate { get; set; } = 1m;
    public string Narration { get; set; } = string.Empty;
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedOn { get; set; }

    public Company Company { get; set; } = null!;
    public Project? Project { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
    public ICollection<VoucherLine> Lines { get; set; } = new List<VoucherLine>();
}

public class VoucherLine
{
    public int Id { get; set; }
    public int VoucherId { get; set; }
    public int AccountHeadId { get; set; }
    public int? CostCenterId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? TaxRuleId { get; set; }
    public string Narration { get; set; } = string.Empty;

    public Voucher Voucher { get; set; } = null!;
    public AccountHead AccountHead { get; set; } = null!;
    public CostCenter? CostCenter { get; set; }
    public TaxRule? TaxRule { get; set; }
}
