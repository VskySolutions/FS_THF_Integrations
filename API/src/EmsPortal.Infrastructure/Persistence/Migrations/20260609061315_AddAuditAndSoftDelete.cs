using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTenantRoles_UserId_TenantId",
                table: "UserTenantRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Identifier",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_TenantApiConfigurations_TenantId_System",
                table: "TenantApiConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_JobScheduleConfigurations_JobName",
                table: "JobScheduleConfigurations");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "UserTenantRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "UserTenantRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "UserTenantRoles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "UserTenantRoles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "UserTenantRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "UserTenantRoles",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "Tenants",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "TenantApiConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "TenantApiConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "TenantApiConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "TenantApiConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "TenantApiConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "TenantApiConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "RetryQueue",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "RetryQueue",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "RetryQueue",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "RetryQueue",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "RetryQueue",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "RetryQueue",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "RefreshTokens",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "MappingConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "MappingConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "MappingConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "MappingConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "MappingConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "MappingConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "JobScheduleConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "JobScheduleConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "JobScheduleConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "JobScheduleConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "JobScheduleConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "JobScheduleConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "IntegrationLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "IntegrationLogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "IntegrationLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "IntegrationLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "IntegrationLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "IntegrationLogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "IntegrationJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "IntegrationJobs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "IntegrationJobs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "IntegrationJobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "IntegrationJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "IntegrationJobs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                table: "AuditTrail",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "AuditTrail",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "AuditTrail",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "AuditTrail",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "AuditTrail",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "AuditTrail",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                columns: new[] { "CreatedById", "CreatedOnUtc", "Deleted", "DeletedOnUtc", "UpdatedById", "UpdatedOnUtc" },
                values: new object[] { null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                columns: new[] { "CreatedById", "CreatedOnUtc", "Deleted", "DeletedOnUtc", "UpdatedById", "UpdatedOnUtc" },
                values: new object[] { null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                columns: new[] { "CreatedById", "CreatedOnUtc", "Deleted", "DeletedOnUtc", "UpdatedById", "UpdatedOnUtc" },
                values: new object[] { null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                columns: new[] { "CreatedById", "CreatedOnUtc", "Deleted", "DeletedOnUtc", "UpdatedById", "UpdatedOnUtc" },
                values: new object[] { null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null, new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_UserId_TenantId",
                table: "UserTenantRoles",
                columns: new[] { "UserId", "TenantId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Identifier",
                table: "Tenants",
                column: "Identifier",
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TenantApiConfigurations_TenantId_System",
                table: "TenantApiConfigurations",
                columns: new[] { "TenantId", "System" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JobScheduleConfigurations_JobName",
                table: "JobScheduleConfigurations",
                column: "JobName",
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTenantRoles_UserId_TenantId",
                table: "UserTenantRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Identifier",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_TenantApiConfigurations_TenantId_System",
                table: "TenantApiConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_JobScheduleConfigurations_JobName",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "TenantApiConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "TenantApiConfigurations");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "TenantApiConfigurations");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "TenantApiConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "TenantApiConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "TenantApiConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "DeletedOnUtc",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "UpdatedOnUtc",
                table: "AuditTrail");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_UserId_TenantId",
                table: "UserTenantRoles",
                columns: new[] { "UserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Identifier",
                table: "Tenants",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantApiConfigurations_TenantId_System",
                table: "TenantApiConfigurations",
                columns: new[] { "TenantId", "System" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobScheduleConfigurations_JobName",
                table: "JobScheduleConfigurations",
                column: "JobName",
                unique: true);
        }
    }
}
