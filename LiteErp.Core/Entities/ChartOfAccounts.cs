using LiteErp.Core.Enums;

namespace LiteErp.Core.Entities;

/// <summary>
/// Functional COA head. Dynamic tree via ParentId (no fixed Level1..5 columns).
/// Only leaf nodes (IsTransactional) accept voucher lines.
/// COA master lives at Group level; per-company activation via CompanyAccountHead.
/// </summary>
public class AccountHead
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;   // path-style: 1-01-003-0002
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public bool IsTransactional { get; set; }
    public bool IsActive { get; set; } = true;

    public AccountHead? Parent { get; set; }
    public ICollection<AccountHead> Children { get; set; } = new List<AccountHead>();
}

/// <summary>Which COA heads a company has activated (companies cannot invent their own heads).</summary>
public class CompanyAccountHead
{
    public int CompanyId { get; set; }
    public int AccountHeadId { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public AccountHead AccountHead { get; set; } = null!;
}

/// <summary>
/// Non-functional dimension (NOT part of the head tree). Own dynamic tree,
/// e.g. Head Office → Sales Dept → Retail Team. Nullable on voucher lines.
/// </summary>
public class CostCenter
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ParentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public CostCenter? Parent { get; set; }
    public ICollection<CostCenter> Children { get; set; } = new List<CostCenter>();
}
