namespace LiteErp.Core.Entities;

/// <summary>Top of the hierarchy: a parent business house.</summary>
public class BusinessGroup
{
    public int Id { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Company> Companies { get; set; } = new List<Company>();
}

/// <summary>Legal entity under a group. Owns its COA activation, fiscal years and base currency.</summary>
public class Company
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string BaseCurrencyCode { get; set; } = "PKR";
    public bool IsActive { get; set; } = true;

    public BusinessGroup Group { get; set; } = null!;
    public Currency BaseCurrency { get; set; } = null!;
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<CostCenter> CostCenters { get; set; } = new List<CostCenter>();
    public ICollection<FiscalYear> FiscalYears { get; set; } = new List<FiscalYear>();
}

/// <summary>Project / location under a company (housing block, mall, branch…).</summary>
public class Project
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
}
