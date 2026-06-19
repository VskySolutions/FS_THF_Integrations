using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAddressId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "City",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "StateProvince",
                table: "CustomerRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "AddressId",
                table: "CustomerRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerRequests_AddressId",
                table: "CustomerRequests",
                column: "AddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerRequests_Addresses_AddressId",
                table: "CustomerRequests",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerRequests_Addresses_AddressId",
                table: "CustomerRequests");

            migrationBuilder.DropIndex(
                name: "IX_CustomerRequests_AddressId",
                table: "CustomerRequests");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "CustomerRequests");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "CustomerRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "CustomerRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "CustomerRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "CustomerRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "CustomerRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateProvince",
                table: "CustomerRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
