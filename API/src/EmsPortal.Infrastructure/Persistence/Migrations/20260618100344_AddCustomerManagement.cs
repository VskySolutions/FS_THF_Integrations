using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerRequestNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StateProvince = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AddressLine2 = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    InternalCustomerCategory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Territory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PracticeArea = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SalesRepresentative = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EnrichmentPaymentTerms = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreditTerms = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CustomerType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BusinessSegment = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RiskCategory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BusinessUnit = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    CustomerGroup = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PaymentTerms = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Industry = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InvoiceLanguage = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MaconomyCustomerNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentApprovalStage = table.Column<int>(type: "int", nullable: false),
                    RequiredApprovalStages = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReturnNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UnlockedFields = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SyncAttempts = table.Column<int>(type: "int", nullable: false),
                    LastSyncError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRequests", x => x.Id);
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    PerformedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FieldsAffected = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerAuditEntries");

            migrationBuilder.DropTable(
                name: "CustomerDocuments");

            migrationBuilder.DropTable(
                name: "CustomerRequests");
        }
    }
}
