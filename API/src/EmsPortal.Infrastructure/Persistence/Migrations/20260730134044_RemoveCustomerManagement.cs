using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAuditEntries");

            migrationBuilder.DropTable(
                name: "CustomerDocuments");

            migrationBuilder.DropTable(
                name: "CustomerRequests");

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222204"));

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222205"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BusinessSegment = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BusinessUnit = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreditTerms = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    CurrentApprovalStage = table.Column<int>(type: "int", nullable: false),
                    CustomerGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CustomerRequestNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EnrichmentPaymentTerms = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InternalCustomerCategory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InvoiceLanguage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PracticeArea = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequiredApprovalStages = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ReturnNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RiskCategory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SalesRepresentative = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Territory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    UnlockedFields = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerRequests_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FieldsAffected = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerAuditEntries_CustomerRequests_CustomerRequestId",
                        column: x => x.CustomerRequestId,
                        principalTable: "CustomerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StoredPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDocuments_CustomerRequests_CustomerRequestId",
                        column: x => x.CustomerRequestId,
                        principalTable: "CustomerRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "PermissionGroupTemplates",
                columns: new[] { "Id", "CreatedById", "CreatedOnUtc", "Deleted", "DeletedOnUtc", "Description", "IsSeeded", "Name", "PermissionKeysJson", "UpdatedById", "UpdatedOnUtc" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222204"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Enrich and review customer requests.", true, "Customer Reviewer", "[\"customers.review\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222205"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Review and approve customer requests.", true, "Customer Approver", "[\"customers.review\",\"customers.approve\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuditEntries_CustomerRequestId",
                table: "CustomerAuditEntries",
                column: "CustomerRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAuditEntries_TenantId",
                table: "CustomerAuditEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDocuments_CustomerRequestId",
                table: "CustomerDocuments",
                column: "CustomerRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDocuments_TenantId",
                table: "CustomerDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_AddressId",
                table: "CustomerRequests",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_CreatedOnUtc",
                table: "CustomerRequests",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_CustomerRequestNumber",
                table: "CustomerRequests",
                column: "CustomerRequestNumber",
                unique: true,
                filter: "[CustomerRequestNumber] IS NOT NULL AND [Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_Status",
                table: "CustomerRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_TenantId",
                table: "CustomerRequests",
                column: "TenantId");
        }
    }
}
