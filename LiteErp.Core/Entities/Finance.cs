namespace LiteErp.Core.Entities;

/// <summary>Dynamic fiscal year (Jul–Jun / Jan–Dec configured per company).</summary>
public class FiscalYear
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;   // e.g. FY-2026
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsClosed { get; set; }

    public Company Company { get; set; } = null!;
}

public class Currency
{
    public string Code { get; set; } = string.Empty;   // PKR, USD, AED…
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
}

public class ExchangeRate
{
    public int Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateOnly RateDate { get; set; }
    public decimal Rate { get; set; }                  // vs base currency

    public Currency Currency { get; set; } = null!;
}

/// <summary>Lightweight configurable tax table (GST/PST/VAT) — day one, no hardcoding.</summary>
public class TaxRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxType { get; set; } = string.Empty; // GST, PST, VAT…
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}
