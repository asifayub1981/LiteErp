using LiteErp.Core.Identity;

namespace LiteErp.Core.Entities;

/// <summary>
/// Per-form permissions for a user. FormCode is one of the codes in <see cref="FormCodes"/>.
/// </summary>
public class UserFormRight
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }

    public ApplicationUser? User { get; set; }
}

/// <summary>All securable forms in the application.</summary>
public static class FormCodes
{
    public const string Dashboard = "DASHBOARD";
    public const string Coa = "COA";
    public const string Vouchers = "VOUCHERS";
    public const string Parties = "PARTIES";
    public const string SalesInvoices = "SALES_INV";
    public const string PurchaseInvoices = "PURCH_INV";
    public const string GlReport = "GL_REPORT";
    public const string TrialBalance = "TB_REPORT";
    public const string PartyStatement = "PARTY_STMT";
    public const string Users = "USERS";

    public static readonly (string Code, string Label)[] All =
    [
        (Dashboard, "Dashboard"),
        (Coa, "COA Definition"),
        (Vouchers, "Vouchers"),
        (Parties, "Parties"),
        (SalesInvoices, "Sales Invoices"),
        (PurchaseInvoices, "Purchase Invoices"),
        (GlReport, "General Ledger Report"),
        (TrialBalance, "Trial Balance Report"),
        (PartyStatement, "Party Statement Report"),
        (Users, "User Management"),
    ];
}
