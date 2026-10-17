using LiteErp.Core.Entities;
using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Infrastructure.Services;

/// <summary>
/// Atomic document numbering. The counter row is incremented with a single
/// UPDATE inside a transaction (row lock held to commit), so two users saving
/// at the same instant can never receive the same number — on PostgreSQL and
/// SQL Server alike (double-quoted identifiers work on both).
/// </summary>
public class VoucherNumberService(ErpDbContext db) : IVoucherNumberService
{
    public async Task<string> NextNumberAsync(int companyId, VoucherType type, int fiscalYearId,
        CancellationToken ct = default)
    {
        var fy = await db.FiscalYears.FindAsync([fiscalYearId], ct)
            ?? throw new InvalidOperationException("Fiscal year not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "VoucherSequences" SET "LastNumber" = "LastNumber" + 1 WHERE "CompanyId" = {companyId} AND "VoucherType" = {(int)type} AND "FiscalYearId" = {fiscalYearId}""",
            ct);

        if (rows == 0)
        {
            db.VoucherSequences.Add(new VoucherSequence
            {
                CompanyId = companyId, VoucherType = type,
                FiscalYearId = fiscalYearId, LastNumber = 1
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Lost a race: another saver inserted the row first. Increment it.
                db.ChangeTracker.Clear();
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "VoucherSequences" SET "LastNumber" = "LastNumber" + 1 WHERE "CompanyId" = {companyId} AND "VoucherType" = {(int)type} AND "FiscalYearId" = {fiscalYearId}""",
                    ct);
            }
        }

        var last = await db.VoucherSequences
            .Where(s => s.CompanyId == companyId && s.VoucherType == type && s.FiscalYearId == fiscalYearId)
            .Select(s => s.LastNumber)
            .FirstAsync(ct);

        await tx.CommitAsync(ct);
        return $"{type.GetPrefix()}-{fy.EndDate.Year}-{last:0000}";
    }
}
