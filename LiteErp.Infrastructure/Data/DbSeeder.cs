using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Identity;
using LiteErp.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LiteErp.Infrastructure.Data;

/// <summary>
/// Seeds roles, the admin user and a small demo dataset (group/company/project,
/// COA, fiscal year, currencies, a few posted vouchers) so Milestone 1 reports
/// are demonstrable on first run.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<ErpDbContext>();

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Database not reachable — skipping migrations and seed. Start PostgreSQL/SQL Server and restart.");
            return;
        }

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Admin", "Accountant" })
            if (!await roleMgr.RoleExistsAsync(role))
                await roleMgr.CreateAsync(new IdentityRole(role));

        var admin = await userMgr.FindByNameAsync("admin@liteerp.local");
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin@liteerp.local",
                Email = "admin@liteerp.local",
                DisplayName = "Administrator",
                EmailConfirmed = true
            };
            await userMgr.CreateAsync(admin, "Admin123!");
            await userMgr.AddToRoleAsync(admin, "Admin");
            log.LogInformation("Seeded admin user: admin@liteerp.local / Admin123!");
        }

        if (await db.BusinessGroups.AnyAsync()) return; // demo data already seeded

        // ---------- Organization ----------
        var group = new BusinessGroup { GroupName = "Demo Group", Code = "DG" };
        db.BusinessGroups.Add(group);

        db.Currencies.AddRange(
            new Currency { Code = "PKR", Name = "Pakistani Rupee", Symbol = "Rs" },
            new Currency { Code = "USD", Name = "US Dollar", Symbol = "$" },
            new Currency { Code = "AED", Name = "UAE Dirham", Symbol = "د.إ" });
        await db.SaveChangesAsync();

        var company = new Company
        {
            GroupId = group.Id, CompanyName = "Demo Traders (Pvt) Ltd",
            Code = "DT", BaseCurrencyCode = "PKR"
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var project = new Project { CompanyId = company.Id, ProjectName = "Gulberg Project", Code = "GP" };
        db.Projects.Add(project);

        var fy = new FiscalYear
        {
            CompanyId = company.Id, Name = "FY-2027",
            StartDate = new DateOnly(2026, 7, 1), EndDate = new DateOnly(2027, 6, 30),
            IsClosed = false
        };
        db.FiscalYears.Add(fy);

        var cc = new CostCenter { CompanyId = company.Id, Code = "SALES", Name = "Sales Dept" };
        db.CostCenters.Add(cc);
        await db.SaveChangesAsync();

        // ---------- COA ----------
        var heads = new List<AccountHead>();
        AccountHead H(string code, string name, AccountType t, bool txn, AccountHead? p = null)
        {
            var h = new AccountHead { Code = code, Name = name, AccountType = t, IsTransactional = txn, Parent = p };
            heads.Add(h); return h;
        }

        var assets = H("1", "Assets", AccountType.Asset, false);
        var curAssets = H("1-01", "Current Assets", AccountType.Asset, false, assets);
        var cash = H("1-01-001", "Cash in Hand", AccountType.Asset, true, curAssets);
        var bank = H("1-01-002", "Bank - Meezan Main", AccountType.Asset, true, curAssets);
        var debtors = H("1-02", "Trade Debtors", AccountType.Asset, true, assets);

        var liab = H("2", "Liabilities", AccountType.Liability, false);
        var creditors = H("2-01", "Trade Creditors", AccountType.Liability, true, liab);

        var cap = H("3", "Capital", AccountType.Capital, false);
        var ownerCap = H("3-01", "Owner Capital", AccountType.Capital, true, cap);

        var rev = H("4", "Revenue", AccountType.Revenue, false);
        var sales = H("4-01", "Sales Revenue", AccountType.Revenue, true, rev);

        var exp = H("5", "Expenses", AccountType.Expense, false);
        var rent = H("5-01", "Rent Expense", AccountType.Expense, true, exp);
        var salary = H("5-02", "Salary Expense", AccountType.Expense, true, exp);

        db.AccountHeads.AddRange(heads);
        await db.SaveChangesAsync();

        db.CompanyAccountHeads.AddRange(heads.Select(h =>
            new CompanyAccountHead { CompanyId = company.Id, AccountHeadId = h.Id, IsActive = true }));
        await db.SaveChangesAsync();

        // ---------- Demo vouchers (posted, balanced) ----------
        var seq = 0;
        string NextNo(string type) => $"{type}-2027-{++seq:0000}";

        Voucher V(VoucherType t, DateOnly d, string narr, int? projId, params (AccountHead h, decimal dr, decimal cr)[] lines)
        {
            var v = new Voucher
            {
                CompanyId = company.Id, ProjectId = projId, VoucherNo = NextNo(t.ToString()),
                VoucherType = t, VoucherDate = d, FiscalYearId = fy.Id,
                CurrencyCode = "PKR", ExchangeRate = 1m, Narration = narr,
                Status = VoucherStatus.Posted, CreatedBy = "seed",
                CreatedOn = DateTime.UtcNow, PostedBy = "seed", PostedOn = DateTime.UtcNow
            };
            foreach (var (h, dr, cr) in lines)
                v.Lines.Add(new VoucherLine { AccountHeadId = h.Id, Debit = dr, Credit = cr });
            return v;
        }

        db.Vouchers.AddRange(
            // Owner introduces capital (company-level — no project)
            V(VoucherType.JV, new DateOnly(2026, 7, 5), "Owner capital introduced", null,
                (bank, 1000000m, 0m), (ownerCap, 0m, 1000000m)),
            // Cash sale (project)
            V(VoucherType.RV, new DateOnly(2026, 8, 12), "Cash sale - Gulberg", project.Id,
                (cash, 50000m, 0m), (sales, 0m, 50000m)),
            // Rent paid (company-level)
            V(VoucherType.PV, new DateOnly(2026, 9, 1), "Head office rent - Sep", null,
                (rent, 30000m, 0m), (cash, 0m, 30000m)),
            // Salaries (project)
            V(VoucherType.PV, new DateOnly(2026, 9, 30), "Site staff salary - Sep", project.Id,
                (salary, 100000m, 0m), (cash, 0m, 100000m))
        );
        await db.SaveChangesAsync();

        log.LogInformation("Seeded demo organization, COA and vouchers.");
    }
}
