using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToJobSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobScheduleConfigurations_JobName",
                table: "JobScheduleConfigurations");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "JobScheduleConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111101"),
                column: "TenantId",
                value: null);

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111102"),
                column: "TenantId",
                value: null);

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111103"),
                column: "TenantId",
                value: null);

            migrationBuilder.UpdateData(
                table: "JobScheduleConfigurations",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111104"),
                column: "TenantId",
                value: null);

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

            migrationBuilder.AddForeignKey(
                name: "FK_JobScheduleConfigurations_Tenants_TenantId",
                table: "JobScheduleConfigurations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobScheduleConfigurations_Tenants_TenantId",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_JobScheduleConfigurations_JobName_TenantId",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_JobScheduleConfigurations_TenantId",
                table: "JobScheduleConfigurations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "JobScheduleConfigurations");

            migrationBuilder.CreateIndex(
                name: "IX_JobScheduleConfigurations_JobName",
                table: "JobScheduleConfigurations",
                column: "JobName",
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
