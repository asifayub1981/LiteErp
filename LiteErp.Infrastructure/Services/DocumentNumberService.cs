using LiteErp.Core.Enums;
using LiteErp.Core.Services;
using LiteErp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiteErp.Infrastructure.Services;

/// <summary>
/// Atomic numbering for party codes and invoices — same single-UPDATE pattern
/// as vouchers (works on PostgreSQL and SQL Server alike).
/// </summary>
public class DocumentNumberService(ErpDbContext db) : IDocumentNumberService
{
    public async Task<string> NextNumberAsync(int companyId, DocumentType type, int fiscalYearId,
        CancellationToken ct = default)
    {
        var fy = await db.FiscalYears.FindAsync([fiscalYearId], ct)
            ?? throw new InvalidOperationException("Fiscal year not found.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "DocumentSequences" SET "LastNumber" = "LastNumber" + 1 WHERE "CompanyId" = {companyId} AND "DocumentType" = {(int)type} AND "FiscalYearId" = {fiscalYearId}""",
            ct);

        if (rows == 0)
        {
            db.DocumentSequences.Add(new Core.Entities.DocumentSequence
            {
                CompanyId = companyId, DocumentType = type,
                FiscalYearId = fiscalYearId, LastNumber = 1
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "DocumentSequences" SET "LastNumber" = "LastNumber" + 1 WHERE "CompanyId" = {companyId} AND "DocumentType" = {(int)type} AND "FiscalYearId" = {fiscalYearId}""",
                    ct);
            }
        }

        var last = await db.DocumentSequences
            .Where(s => s.CompanyId == companyId && s.DocumentType == type && s.FiscalYearId == fiscalYearId)
            .Select(s => s.LastNumber)
            .FirstAsync(ct);

        await tx.CommitAsync(ct);

        // Party codes don't carry the year (CUST-0001); invoices do (SI-2027-0001).
        return type is DocumentType.Customer or DocumentType.Supplier
            ? $"{type.GetPrefix()}-{last:0000}"
            : $"{type.GetPrefix()}-{fy.EndDate.Year}-{last:0000}";
    }
}
