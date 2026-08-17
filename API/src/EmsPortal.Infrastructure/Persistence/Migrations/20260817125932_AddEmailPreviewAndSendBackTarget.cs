using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailPreviewAndSendBackTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReturnedToUserId",
                table: "REMSSendBack",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "REMSFormEmailEvent",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "REMSFormEmailEvent",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnedToUserId",
                table: "REMSSendBack");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "REMSFormEmailEvent");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "REMSFormEmailEvent");
        }
    }
}
