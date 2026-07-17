using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToCoreTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RetryQueue",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "IntegrationLogs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "IntegrationJobs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AuditTrail",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_RetryQueue_TenantId",
                table: "RetryQueue",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationLogs_TenantId",
                table: "IntegrationLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_TenantId",
                table: "IntegrationJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrail_TenantId",
                table: "AuditTrail",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrail_Tenants_TenantId",
                table: "AuditTrail",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationJobs_Tenants_TenantId",
                table: "IntegrationJobs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IntegrationLogs_Tenants_TenantId",
                table: "IntegrationLogs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RetryQueue_Tenants_TenantId",
                table: "RetryQueue",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrail_Tenants_TenantId",
                table: "AuditTrail");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationJobs_Tenants_TenantId",
                table: "IntegrationJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_IntegrationLogs_Tenants_TenantId",
                table: "IntegrationLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_RetryQueue_Tenants_TenantId",
                table: "RetryQueue");

            migrationBuilder.DropIndex(
                name: "IX_RetryQueue_TenantId",
                table: "RetryQueue");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationLogs_TenantId",
                table: "IntegrationLogs");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationJobs_TenantId",
                table: "IntegrationJobs");

            migrationBuilder.DropIndex(
                name: "IX_AuditTrail_TenantId",
                table: "AuditTrail");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditTrail");
        }
    }
}
