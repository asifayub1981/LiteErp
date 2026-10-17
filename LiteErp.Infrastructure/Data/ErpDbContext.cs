using LiteErp.Core.Entities;
using LiteErp.Core.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Infrastructure.Data;

public class ErpDbContext : IdentityDbContext<ApplicationUser>
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options) { }

    // Organization
    public DbSet<BusinessGroup> BusinessGroups => Set<BusinessGroup>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Project> Projects => Set<Project>();

    // Chart of accounts
    public DbSet<AccountHead> AccountHeads => Set<AccountHead>();
    public DbSet<CompanyAccountHead> CompanyAccountHeads => Set<CompanyAccountHead>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();

    // Finance masters
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();

    // Vouchers
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherLine> VoucherLines => Set<VoucherLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ---------- Organization ----------
        b.Entity<BusinessGroup>(e =>
        {
            e.Property(x => x.GroupName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        b.Entity<Company>(e =>
        {
            e.Property(x => x.CompanyName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.BaseCurrencyCode).HasMaxLength(3).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Group).WithMany(g => g.Companies)
                .HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BaseCurrency).WithMany()
                .HasForeignKey(x => x.BaseCurrencyCode).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Project>(e =>
        {
            e.Property(x => x.ProjectName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Company).WithMany(c => c.Projects)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Chart of accounts (dynamic tree) ----------
        b.Entity<AccountHead>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(60).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Parent).WithMany(p => p.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CompanyAccountHead>(e =>
        {
            e.HasKey(x => new { x.CompanyId, x.AccountHeadId });
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId);
            e.HasOne(x => x.AccountHead).WithMany().HasForeignKey(x => x.AccountHeadId);
        });

        b.Entity<CostCenter>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(40).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasOne(x => x.Company).WithMany(c => c.CostCenters)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Parent).WithMany(p => p.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Finance masters ----------
        b.Entity<FiscalYear>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Company).WithMany(c => c.FiscalYears)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Currency>(e =>
        {
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(3);
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Symbol).HasMaxLength(10).IsRequired();
        });

        b.Entity<ExchangeRate>(e =>
        {
            e.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            e.Property(x => x.Rate).HasPrecision(18, 6);
            e.HasOne(x => x.Currency).WithMany()
                .HasForeignKey(x => x.CurrencyCode).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TaxRule>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.TaxType).HasMaxLength(20).IsRequired();
            e.Property(x => x.Rate).HasPrecision(8, 4);
        });

        // ---------- Vouchers ----------
        b.Entity<Voucher>(e =>
        {
            e.Property(x => x.VoucherNo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Narration).HasMaxLength(500);
            e.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
            e.Property(x => x.ExchangeRate).HasPrecision(18, 6);
            e.Property(x => x.CreatedBy).HasMaxLength(100);
            e.Property(x => x.PostedBy).HasMaxLength(100);
            e.HasIndex(x => new { x.CompanyId, x.VoucherType, x.FiscalYearId, x.VoucherNo }).IsUnique();
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.FiscalYear).WithMany().HasForeignKey(x => x.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Currency).WithMany().HasForeignKey(x => x.CurrencyCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<VoucherLine>(e =>
        {
            e.Property(x => x.Debit).HasPrecision(18, 4);
            e.Property(x => x.Credit).HasPrecision(18, 4);
            e.Property(x => x.Narration).HasMaxLength(500);
            e.HasOne(x => x.Voucher).WithMany(v => v.Lines)
                .HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AccountHead).WithMany().HasForeignKey(x => x.AccountHeadId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CostCenter).WithMany().HasForeignKey(x => x.CostCenterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.TaxRule).WithMany().HasForeignKey(x => x.TaxRuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(100);
        });
    }
}
