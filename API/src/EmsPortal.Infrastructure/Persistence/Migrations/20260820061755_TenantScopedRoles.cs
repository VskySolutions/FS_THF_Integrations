using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives a role an owner. <c>Roles.TenantId</c> is null for a PLATFORM role — every seeded system
    /// role, and anything else a Super Admin creates — and is the tenant that created it otherwise, now
    /// that a Tenant Admin can build roles for their own firm. Everything already in the table belongs to
    /// the platform, which is exactly what the new null column says of it.
    /// <para>
    /// The unique index moves with the ownership: a name is unique within its scope rather than across
    /// the platform, so two firms may each have a "Reviewer". SQL Server treats the NULLs in the new
    /// index as equal, which keeps the platform names unique among themselves.
    /// </para>
    /// </summary>
    public partial class TenantScopedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Roles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
