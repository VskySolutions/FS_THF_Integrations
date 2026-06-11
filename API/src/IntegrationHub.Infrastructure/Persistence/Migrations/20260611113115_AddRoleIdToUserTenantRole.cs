using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleIdToUserTenantRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "UserTenantRoles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_RoleId",
                table: "UserTenantRoles",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantRoles_Roles_RoleId",
                table: "UserTenantRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill existing assignments to the matching seeded system role. The legacy Role
            // column stores the enum name (SuperAdmin/TenantAdmin/Operator), which equals the
            // system role Name. No-op on a fresh database (system roles are seeded post-migration).
            migrationBuilder.Sql(@"
                UPDATE utr
                SET utr.RoleId = r.Id
                FROM UserTenantRoles utr
                INNER JOIN Roles r ON r.Name = utr.Role AND r.IsSystem = 1 AND r.Deleted = 0
                WHERE utr.RoleId IS NULL AND utr.Deleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantRoles_Roles_RoleId",
                table: "UserTenantRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantRoles_RoleId",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "UserTenantRoles");
        }
    }
}
