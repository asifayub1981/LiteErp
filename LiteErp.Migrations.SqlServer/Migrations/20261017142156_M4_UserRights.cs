using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiteErp.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class M4_UserRights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFormRights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FormCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CanView = table.Column<bool>(type: "bit", nullable: false),
                    CanAdd = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CanDelete = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFormRights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFormRights_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFormRights_UserId_FormCode",
                table: "UserFormRights",
                columns: new[] { "UserId", "FormCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFormRights");
        }
    }
}
