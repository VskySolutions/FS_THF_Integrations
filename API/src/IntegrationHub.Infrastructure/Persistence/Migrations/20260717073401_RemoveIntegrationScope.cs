using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIntegrationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationLogs");

            migrationBuilder.DropTable(
                name: "JobScheduleConfigurations");

            migrationBuilder.DropTable(
                name: "MappingConfigurations");

            migrationBuilder.DropTable(
                name: "RetryQueue");

            migrationBuilder.DropTable(
                name: "TenantApiConfigurations");

            migrationBuilder.DropTable(
                name: "IntegrationJobs");

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222201"));

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222202"));

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222206"));

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222207"));

            migrationBuilder.DeleteData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222208"));

            migrationBuilder.DropColumn(
                name: "LastSyncError",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "MaconomyCustomerNumber",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "SyncAttempts",
                table: "CustomerRequests");

            migrationBuilder.UpdateData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222203"),
                columns: new[] { "Description", "PermissionKeysJson" },
                values: new object[] { "Configure tenant settings.", "[\"tenants.read\",\"tenants.write\"]" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSyncError",
                table: "CustomerRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaconomyCustomerNumber",
                table: "CustomerRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SyncAttempts",
                table: "CustomerRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "IntegrationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InterfaceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TargetSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationJobs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobScheduleConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    JobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobScheduleConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobScheduleConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MappingConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DestinationField = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InterfaceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceField = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransformationRule = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MappingConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantApiConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EncryptedCredentials = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    System = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantApiConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantApiConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Level = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationLogs_IntegrationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "IntegrationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntegrationLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetryQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextRetryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetryQueue_IntegrationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "IntegrationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetryQueue_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "JobScheduleConfigurations",
                columns: new[] { "Id", "CreatedById", "CreatedOnUtc", "CronExpression", "Deleted", "DeletedOnUtc", "IsActive", "JobName", "TenantId", "UpdatedById", "UpdatedDate", "UpdatedOnUtc" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111101"), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "0 */2 * * *", false, null, true, "ExpenseImportJob", null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("11111111-1111-1111-1111-111111111102"), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "15 */2 * * *", false, null, true, "InvoiceImportJob", null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("11111111-1111-1111-1111-111111111103"), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "30 */2 * * *", false, null, true, "VendorPaymentImportJob", null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { new Guid("11111111-1111-1111-1111-111111111104"), null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "*/5 * * * *", false, null, true, "RetryFailedJobsJob", null, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.UpdateData(
                table: "PermissionGroupTemplates",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222203"),
                columns: new[] { "Description", "PermissionKeysJson" },
                values: new object[] { "Configure tenant settings and credentials.", "[\"tenants.read\",\"tenants.write\",\"tenants.credentials\"]" });

            migrationBuilder.InsertData(
                table: "PermissionGroupTemplates",
                columns: new[] { "Id", "CreatedById", "CreatedOnUtc", "Deleted", "DeletedOnUtc", "Description", "IsSeeded", "Name", "PermissionKeysJson", "UpdatedById", "UpdatedOnUtc" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222201"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Trigger and monitor imports.", true, "Import Operator", "[\"jobs.trigger\",\"jobs.read\",\"logs.read\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222202"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Read and edit field mappings.", true, "Mapping Manager", "[\"mappings.read\",\"mappings.write\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222206"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Read-only access to jobs and logs.", true, "Finance Read-Only", "[\"jobs.read\",\"logs.read\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222207"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Monitor platform health, jobs and schedules.", true, "Platform Monitor", "[\"health.read\",\"jobs.read\",\"logs.read\",\"jobs.schedule\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("22222222-2222-2222-2222-222222222208"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), false, null, "Manage recurring import schedules.", true, "Schedule Admin", "[\"jobs.schedule\"]", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_CorrelationId",
                table: "IntegrationJobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_CreatedOnUtc",
                table: "IntegrationJobs",
                column: "CreatedOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_Status",
                table: "IntegrationJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_TenantId",
                table: "IntegrationJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationLogs_JobId",
                table: "IntegrationLogs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationLogs_TenantId",
                table: "IntegrationLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_JobScheduleConfigurations_JobName_TenantId",
                table: "JobScheduleConfigurations",
                columns: new[] { "JobName", "TenantId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JobScheduleConfigurations_TenantId",
                table: "JobScheduleConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_InterfaceName_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "SourceSystem", "TargetSystem", "InterfaceName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_TenantId_InterfaceName",
                table: "MappingConfigurations",
                columns: new[] { "TenantId", "InterfaceName" });

            migrationBuilder.CreateIndex(
                name: "IX_RetryQueue_JobId",
                table: "RetryQueue",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_RetryQueue_Status_NextRetryDate",
                table: "RetryQueue",
                columns: new[] { "Status", "NextRetryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RetryQueue_TenantId",
                table: "RetryQueue",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantApiConfigurations_TenantId_System",
                table: "TenantApiConfigurations",
                columns: new[] { "TenantId", "System" },
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
