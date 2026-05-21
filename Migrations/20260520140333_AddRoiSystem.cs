using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLM_Level.Migrations
{
    /// <inheritdoc />
    public partial class AddRoiSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MlmSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Level1Commission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Level2Commission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Level3Commission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Level4Commission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Level5Commission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinWithdrawalLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WithdrawalFeePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CompanyQrCodeUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    BankDetails = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SiteName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupportEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaintenanceMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUsername = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SmtpPassword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SmtpEnableSsl = table.Column<bool>(type: "bit", nullable: false),
                    SmsApiUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SmsApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SmsSenderId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WhatsAppAccessToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    WhatsAppInstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WhatsAppProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MlmSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RoiPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RoiDurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SponsorId = table.Column<int>(type: "int", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    JoinedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsAdmin = table.Column<bool>(type: "bit", nullable: false),
                    WalletBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReferralCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DefaultPlacementId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Users_DefaultPlacementId",
                        column: x => x.DefaultPlacementId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Users_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Users_Users_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: true),
                    TransactionReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentSlipUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivationRequests_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ActivationRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionTrans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionTrans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionTrans_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommissionTrans_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AdminResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RoiPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RoiDurationDays = table.Column<int>(type: "int", nullable: false),
                    DaysPaid = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ActivationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPackages_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPackages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WithdrawalRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRequests_PackageId",
                table: "ActivationRequests",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivationRequests_UserId",
                table: "ActivationRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTrans_FromUserId",
                table: "CommissionTrans",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionTrans_UserId",
                table: "CommissionTrans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPackages_PackageId",
                table: "UserPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPackages_UserId",
                table: "UserPackages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DefaultPlacementId",
                table: "Users",
                column: "DefaultPlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ParentId",
                table: "Users",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ReferralCode",
                table: "Users",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SponsorId",
                table: "Users",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WithdrawalRequests_UserId",
                table: "WithdrawalRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivationRequests");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "CommissionTrans");

            migrationBuilder.DropTable(
                name: "MlmSettings");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "UserPackages");

            migrationBuilder.DropTable(
                name: "WithdrawalRequests");

            migrationBuilder.DropTable(
                name: "Packages");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
