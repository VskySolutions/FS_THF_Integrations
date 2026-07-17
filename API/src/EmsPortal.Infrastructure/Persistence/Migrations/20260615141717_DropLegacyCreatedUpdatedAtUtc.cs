using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyCreatedUpdatedAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationJobs_CreatedAtUtc",
                table: "IntegrationJobs");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "IntegrationLogs");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "IntegrationJobs");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_CreatedOnUtc",
                table: "IntegrationJobs",
                column: "CreatedOnUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationJobs_CreatedOnUtc",
                table: "IntegrationJobs");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "RetryQueue",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "MappingConfigurations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "MappingConfigurations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "IntegrationLogs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "IntegrationJobs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationJobs_CreatedAtUtc",
                table: "IntegrationJobs",
                column: "CreatedAtUtc");
        }
    }
}
