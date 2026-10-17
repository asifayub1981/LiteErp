using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LiteErp.Migrations.PostgreSQL.Migrations
{
    /// <inheritdoc />
    public partial class M2_VoucherEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReversalOfVoucherId",
                table: "Vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VoucherSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    VoucherType = table.Column<int>(type: "integer", nullable: false),
                    FiscalYearId = table.Column<int>(type: "integer", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherSequences_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherSequences_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalTable: "FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_ReversalOfVoucherId",
                table: "Vouchers",
                column: "ReversalOfVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherSequences_CompanyId_VoucherType_FiscalYearId",
                table: "VoucherSequences",
                columns: new[] { "CompanyId", "VoucherType", "FiscalYearId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherSequences_FiscalYearId",
                table: "VoucherSequences",
                column: "FiscalYearId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Vouchers_ReversalOfVoucherId",
                table: "Vouchers",
                column: "ReversalOfVoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Vouchers_ReversalOfVoucherId",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "VoucherSequences");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_ReversalOfVoucherId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "ReversalOfVoucherId",
                table: "Vouchers");
        }
    }
}
