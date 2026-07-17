using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrailCreatedDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "AuditTrail",
                newName: "CreatedDate");

            migrationBuilder.RenameIndex(
                name: "IX_AuditTrail_CreatedAtUtc",
                table: "AuditTrail",
                newName: "IX_AuditTrail_CreatedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "AuditTrail",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_AuditTrail_CreatedDate",
                table: "AuditTrail",
                newName: "IX_AuditTrail_CreatedAtUtc");
        }
    }
}
