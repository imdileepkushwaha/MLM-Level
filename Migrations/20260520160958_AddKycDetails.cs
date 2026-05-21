using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLM_Level.Migrations
{
    /// <inheritdoc />
    public partial class AddKycDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IncomeWallet",
                table: "Users",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaidDate",
                table: "UserPackages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdminChargePercent",
                table: "MlmSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TdsPercent",
                table: "MlmSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "KycDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PanCardUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BankPassbookUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KycDetails_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KycDetails_UserId",
                table: "KycDetails",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KycDetails");

            migrationBuilder.DropColumn(
                name: "IncomeWallet",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastPaidDate",
                table: "UserPackages");

            migrationBuilder.DropColumn(
                name: "AdminChargePercent",
                table: "MlmSettings");

            migrationBuilder.DropColumn(
                name: "TdsPercent",
                table: "MlmSettings");
        }
    }
}
