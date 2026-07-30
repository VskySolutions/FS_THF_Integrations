using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REMS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AdminAssignedToId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CSEId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExistingClientReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CustomerMobileNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMS_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMS_Users_AdminAssignedToId",
                        column: x => x.AdminAssignedToId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMS_Users_CSEId",
                        column: x => x.CSEId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSFiles_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSFiles_REMS_REMSId",
                        column: x => x.REMSId,
                        principalTable: "REMS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSFiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSForm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IndustryGroup = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InviteCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InviteLockedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSForm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSForm_REMS_REMSId",
                        column: x => x.REMSId,
                        principalTable: "REMS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSForm_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSForm_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSFormDraft",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DraftPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSavedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSFormDraft", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSFormDraft_REMSForm_REMSFormId",
                        column: x => x.REMSFormId,
                        principalTable: "REMSForm",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_REMSFormDraft_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSFormEmailEvent",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProviderPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSFormEmailEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSFormEmailEvent_REMSForm_REMSFormId",
                        column: x => x.REMSFormId,
                        principalTable: "REMSForm",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSFormEmailEvent_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSFormSubmission",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSFormSubmission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSFormSubmission_REMSForm_REMSFormId",
                        column: x => x.REMSFormId,
                        principalTable: "REMSForm",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSFormSubmission_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSClient",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceFormSubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalClientReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MobileNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ReferralSource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BillingContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BillingAddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSClient", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSClient_Addresses_BillingAddressId",
                        column: x => x.BillingAddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSClient_REMSFormSubmission_SourceFormSubmissionId",
                        column: x => x.SourceFormSubmissionId,
                        principalTable: "REMSFormSubmission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSClient_REMS_REMSId",
                        column: x => x.REMSId,
                        principalTable: "REMS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSClient_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEntityKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EIN = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsMainEntity = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEntity_REMSClient_REMSClientId",
                        column: x => x.REMSClientId,
                        principalTable: "REMSClient",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEntity_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ServiceLine = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DepartmentDirectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EngagementExecutiveId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BillingManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstYearFeeEstimate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RealizationPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagement", x => x.Id);
                    table.CheckConstraint("CK_REMSEngagement_Realization", "[RealizationPercentage] IS NULL OR ([RealizationPercentage] >= 0 AND [RealizationPercentage] <= 100)");
                    table.ForeignKey(
                        name: "FK_REMSEngagement_REMSEntity_REMSEntityId",
                        column: x => x.REMSEntityId,
                        principalTable: "REMSEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagement_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagement_Users_BillingManagerId",
                        column: x => x.BillingManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagement_Users_DepartmentDirectorId",
                        column: x => x.DepartmentDirectorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagement_Users_EngagementExecutiveId",
                        column: x => x.EngagementExecutiveId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEntityAddress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddressType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEntityAddress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEntityAddress_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEntityAddress_REMSEntity_REMSEntityId",
                        column: x => x.REMSEntityId,
                        principalTable: "REMSEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEntityAddress_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEntityContact",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactRole = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEntityContact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEntityContact_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEntityContact_REMSEntity_REMSEntityId",
                        column: x => x.REMSEntityId,
                        principalTable: "REMSEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEntityContact_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSApprovalRound",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSApprovalRound", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSApprovalRound_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSApprovalRound_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSApprovalRound_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagementAuditDetail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientAcceptanceFormMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementAuditDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEngagementAuditDetail_Media_ClientAcceptanceFormMediaId",
                        column: x => x.ClientAcceptanceFormMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementAuditDetail_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementAuditDetail_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagementCommissionSplit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommissionPercentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementCommissionSplit", x => x.Id);
                    table.CheckConstraint("CK_REMSEngagementCommissionSplit_Pct", "[CommissionPercentage] > 0 AND [CommissionPercentage] <= 100");
                    table.ForeignKey(
                        name: "FK_REMSEngagementCommissionSplit_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementCommissionSplit_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementCommissionSplit_Users_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagementGovernmentDetail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalTerm = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RenewalTerms = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PurchaseOrderStartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseOrderEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ContractNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FloridaOnePercentStateFeeApplies = table.Column<bool>(type: "bit", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementGovernmentDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEngagementGovernmentDetail_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementGovernmentDetail_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagementMarketingMethod",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketingMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementMarketingMethod", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEngagementMarketingMethod_OptionSetItems_MarketingMethodId",
                        column: x => x.MarketingMethodId,
                        principalTable: "OptionSetItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementMarketingMethod_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementMarketingMethod_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagementTaxDetail",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    CalculatedDueDates = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementTaxDetail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEngagementTaxDetail_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementTaxDetail_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSApprovalTask",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSApprovalRoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSApprovalTask", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSApprovalTask_REMSApprovalRound_REMSApprovalRoundId",
                        column: x => x.REMSApprovalRoundId,
                        principalTable: "REMSApprovalRound",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSApprovalTask_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSApprovalTask_Users_ApproverId",
                        column: x => x.ApproverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSEngagementTaxForm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementTaxDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxFormId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementTaxForm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEngagementTaxForm_OptionSetItems_TaxFormId",
                        column: x => x.TaxFormId,
                        principalTable: "OptionSetItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementTaxForm_REMSEngagementTaxDetail_REMSEngagementTaxDetailId",
                        column: x => x.REMSEngagementTaxDetailId,
                        principalTable: "REMSEngagementTaxDetail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementTaxForm_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSApprovalChecklistItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSApprovalTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSApprovalChecklistItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSApprovalChecklistItem_REMSApprovalTask_REMSApprovalTaskId",
                        column: x => x.REMSApprovalTaskId,
                        principalTable: "REMSApprovalTask",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSApprovalChecklistItem_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REMS_AdminAssignedToId",
                table: "REMS",
                column: "AdminAssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_CSEId",
                table: "REMS",
                column: "CSEId");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId",
                table: "REMS",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_CreatedById_Status",
                table: "REMS",
                columns: new[] { "TenantId", "CreatedById", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_REMSNumber",
                table: "REMS",
                columns: new[] { "TenantId", "REMSNumber" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_Status_AdminAssignedToId_CreatedOnUtc",
                table: "REMS",
                columns: new[] { "TenantId", "Status", "AdminAssignedToId", "CreatedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalChecklistItem_REMSApprovalTaskId",
                table: "REMSApprovalChecklistItem",
                column: "REMSApprovalTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalChecklistItem_TenantId",
                table: "REMSApprovalChecklistItem",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalChecklistItem_TenantId_REMSApprovalTaskId_DisplayOrder",
                table: "REMSApprovalChecklistItem",
                columns: new[] { "TenantId", "REMSApprovalTaskId", "DisplayOrder" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalRound_REMSEngagementId",
                table: "REMSApprovalRound",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalRound_SentByUserId",
                table: "REMSApprovalRound",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalRound_TenantId",
                table: "REMSApprovalRound",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalRound_TenantId_REMSEngagementId_RoundNumber",
                table: "REMSApprovalRound",
                columns: new[] { "TenantId", "REMSEngagementId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalTask_ApproverId",
                table: "REMSApprovalTask",
                column: "ApproverId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalTask_REMSApprovalRoundId",
                table: "REMSApprovalTask",
                column: "REMSApprovalRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalTask_TenantId",
                table: "REMSApprovalTask",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalTask_TenantId_ApproverId_Status",
                table: "REMSApprovalTask",
                columns: new[] { "TenantId", "ApproverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSApprovalTask_TenantId_REMSApprovalRoundId_ApproverId_ApproverRole",
                table: "REMSApprovalTask",
                columns: new[] { "TenantId", "REMSApprovalRoundId", "ApproverId", "ApproverRole" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSClient_BillingAddressId",
                table: "REMSClient",
                column: "BillingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSClient_REMSId",
                table: "REMSClient",
                column: "REMSId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSClient_SourceFormSubmissionId",
                table: "REMSClient",
                column: "SourceFormSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSClient_TenantId",
                table: "REMSClient",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSClient_TenantId_REMSId",
                table: "REMSClient",
                columns: new[] { "TenantId", "REMSId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_BillingManagerId",
                table: "REMSEngagement",
                column: "BillingManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_DepartmentDirectorId",
                table: "REMSEngagement",
                column: "DepartmentDirectorId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_EngagementExecutiveId",
                table: "REMSEngagement",
                column: "EngagementExecutiveId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_REMSEntityId",
                table: "REMSEngagement",
                column: "REMSEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_TenantId",
                table: "REMSEngagement",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_TenantId_REMSEntityId",
                table: "REMSEngagement",
                columns: new[] { "TenantId", "REMSEntityId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagement_TenantId_Status",
                table: "REMSEngagement",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementAuditDetail_ClientAcceptanceFormMediaId",
                table: "REMSEngagementAuditDetail",
                column: "ClientAcceptanceFormMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementAuditDetail_REMSEngagementId",
                table: "REMSEngagementAuditDetail",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementAuditDetail_TenantId",
                table: "REMSEngagementAuditDetail",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementAuditDetail_TenantId_REMSEngagementId",
                table: "REMSEngagementAuditDetail",
                columns: new[] { "TenantId", "REMSEngagementId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementCommissionSplit_EmployeeId",
                table: "REMSEngagementCommissionSplit",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementCommissionSplit_REMSEngagementId",
                table: "REMSEngagementCommissionSplit",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementCommissionSplit_TenantId",
                table: "REMSEngagementCommissionSplit",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementCommissionSplit_TenantId_REMSEngagementId_EmployeeId",
                table: "REMSEngagementCommissionSplit",
                columns: new[] { "TenantId", "REMSEngagementId", "EmployeeId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementGovernmentDetail_REMSEngagementId",
                table: "REMSEngagementGovernmentDetail",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementGovernmentDetail_TenantId",
                table: "REMSEngagementGovernmentDetail",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementGovernmentDetail_TenantId_REMSEngagementId",
                table: "REMSEngagementGovernmentDetail",
                columns: new[] { "TenantId", "REMSEngagementId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementMarketingMethod_MarketingMethodId",
                table: "REMSEngagementMarketingMethod",
                column: "MarketingMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementMarketingMethod_REMSEngagementId",
                table: "REMSEngagementMarketingMethod",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementMarketingMethod_TenantId",
                table: "REMSEngagementMarketingMethod",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementMarketingMethod_TenantId_REMSEngagementId_MarketingMethodId",
                table: "REMSEngagementMarketingMethod",
                columns: new[] { "TenantId", "REMSEngagementId", "MarketingMethodId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxDetail_REMSEngagementId",
                table: "REMSEngagementTaxDetail",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxDetail_TenantId",
                table: "REMSEngagementTaxDetail",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxDetail_TenantId_REMSEngagementId",
                table: "REMSEngagementTaxDetail",
                columns: new[] { "TenantId", "REMSEngagementId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxForm_REMSEngagementTaxDetailId",
                table: "REMSEngagementTaxForm",
                column: "REMSEngagementTaxDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxForm_TaxFormId",
                table: "REMSEngagementTaxForm",
                column: "TaxFormId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxForm_TenantId",
                table: "REMSEngagementTaxForm",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementTaxForm_TenantId_REMSEngagementTaxDetailId_TaxFormId",
                table: "REMSEngagementTaxForm",
                columns: new[] { "TenantId", "REMSEngagementTaxDetailId", "TaxFormId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntity_REMSClientId",
                table: "REMSEntity",
                column: "REMSClientId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntity_TenantId",
                table: "REMSEntity",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntity_TenantId_REMSClientId",
                table: "REMSEntity",
                columns: new[] { "TenantId", "REMSClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntity_TenantId_REMSClientId_Main",
                table: "REMSEntity",
                columns: new[] { "TenantId", "REMSClientId" },
                unique: true,
                filter: "[IsMainEntity] = 1 AND [Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntity_TenantId_REMSClientId_SourceEntityKey",
                table: "REMSEntity",
                columns: new[] { "TenantId", "REMSClientId", "SourceEntityKey" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityAddress_AddressId",
                table: "REMSEntityAddress",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityAddress_REMSEntityId",
                table: "REMSEntityAddress",
                column: "REMSEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityAddress_TenantId",
                table: "REMSEntityAddress",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityAddress_TenantId_REMSEntityId_AddressType",
                table: "REMSEntityAddress",
                columns: new[] { "TenantId", "REMSEntityId", "AddressType" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityContact_PersonId",
                table: "REMSEntityContact",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityContact_REMSEntityId",
                table: "REMSEntityContact",
                column: "REMSEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityContact_TenantId",
                table: "REMSEntityContact",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityContact_TenantId_REMSEntityId_ContactRole",
                table: "REMSEntityContact",
                columns: new[] { "TenantId", "REMSEntityId", "ContactRole" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFiles_MediaId",
                table: "REMSFiles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFiles_REMSId",
                table: "REMSFiles",
                column: "REMSId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFiles_TenantId",
                table: "REMSFiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFiles_TenantId_REMSId_MediaId",
                table: "REMSFiles",
                columns: new[] { "TenantId", "REMSId", "MediaId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSForm_CreatedByUserId",
                table: "REMSForm",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSForm_REMSId",
                table: "REMSForm",
                column: "REMSId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSForm_TenantId",
                table: "REMSForm",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSForm_TenantId_InviteCode",
                table: "REMSForm",
                columns: new[] { "TenantId", "InviteCode" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSForm_TenantId_REMSId",
                table: "REMSForm",
                columns: new[] { "TenantId", "REMSId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSForm_TenantId_REMSId_Status",
                table: "REMSForm",
                columns: new[] { "TenantId", "REMSId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormDraft_REMSFormId",
                table: "REMSFormDraft",
                column: "REMSFormId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormDraft_TenantId",
                table: "REMSFormDraft",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormDraft_TenantId_REMSFormId",
                table: "REMSFormDraft",
                columns: new[] { "TenantId", "REMSFormId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormEmailEvent_REMSFormId",
                table: "REMSFormEmailEvent",
                column: "REMSFormId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormEmailEvent_TenantId",
                table: "REMSFormEmailEvent",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormEmailEvent_TenantId_ProviderMessageId_EventType",
                table: "REMSFormEmailEvent",
                columns: new[] { "TenantId", "ProviderMessageId", "EventType" },
                unique: true,
                filter: "[ProviderMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormSubmission_REMSFormId",
                table: "REMSFormSubmission",
                column: "REMSFormId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormSubmission_TenantId",
                table: "REMSFormSubmission",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSFormSubmission_TenantId_REMSFormId",
                table: "REMSFormSubmission",
                columns: new[] { "TenantId", "REMSFormId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REMSApprovalChecklistItem");

            migrationBuilder.DropTable(
                name: "REMSEngagementAuditDetail");

            migrationBuilder.DropTable(
                name: "REMSEngagementCommissionSplit");

            migrationBuilder.DropTable(
                name: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropTable(
                name: "REMSEngagementMarketingMethod");

            migrationBuilder.DropTable(
                name: "REMSEngagementTaxForm");

            migrationBuilder.DropTable(
                name: "REMSEntityAddress");

            migrationBuilder.DropTable(
                name: "REMSEntityContact");

            migrationBuilder.DropTable(
                name: "REMSFiles");

            migrationBuilder.DropTable(
                name: "REMSFormDraft");

            migrationBuilder.DropTable(
                name: "REMSFormEmailEvent");

            migrationBuilder.DropTable(
                name: "REMSApprovalTask");

            migrationBuilder.DropTable(
                name: "REMSEngagementTaxDetail");

            migrationBuilder.DropTable(
                name: "REMSApprovalRound");

            migrationBuilder.DropTable(
                name: "REMSEngagement");

            migrationBuilder.DropTable(
                name: "REMSEntity");

            migrationBuilder.DropTable(
                name: "REMSClient");

            migrationBuilder.DropTable(
                name: "REMSFormSubmission");

            migrationBuilder.DropTable(
                name: "REMSForm");

            migrationBuilder.DropTable(
                name: "REMS");
        }
    }
}
