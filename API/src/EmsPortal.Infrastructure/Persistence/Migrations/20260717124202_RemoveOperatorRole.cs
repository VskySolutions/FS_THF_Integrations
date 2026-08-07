using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOperatorRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WO-109: the Operator role is removed from the platform. This data migration keeps the
            // legacy UserTenantRole.Role enum string parseable (Operator -> Custom, the neutral
            // permission-based sentinel) and deletes the seeded Operator system role. There is no
            // schema change — UserRole is stored as its enum name (see UserConfiguration).

            // 1. Relabel the legacy enum string on every assignment that carried the removed Operator
            //    tier — both former Operator-system-role users and custom-role users (whose legacy
            //    enum was also stored as "Operator"). "Custom" grants no fixed-tier permissions;
            //    custom-role users keep their RoleId and therefore their RBAC permissions.
            migrationBuilder.Sql(
                "UPDATE UserTenantRoles SET Role = 'Custom' WHERE Role = 'Operator';");

            // 2. Detach assignments that pointed at the seeded Operator *system* role. Operator
            //    granted no permissions, so their effective (empty) access is unchanged. Custom-role
            //    assignments reference their own role and are left untouched.
            migrationBuilder.Sql(@"
                UPDATE utr
                SET utr.RoleId = NULL
                FROM UserTenantRoles utr
                INNER JOIN Roles r ON r.Id = utr.RoleId
                WHERE r.Name = 'Operator' AND r.IsSystem = 1;");

            // 3. Clear the composition and tenant-availability references, then delete the Operator
            //    system role itself (FKs into Roles are Restrict, so references are removed first).
            migrationBuilder.Sql(@"
                DELETE rpg
                FROM RolePermissionGroups rpg
                INNER JOIN Roles r ON r.Id = rpg.RoleId
                WHERE r.Name = 'Operator' AND r.IsSystem = 1;");

            migrationBuilder.Sql(@"
                DELETE tr
                FROM TenantRoles tr
                INNER JOIN Roles r ON r.Id = tr.RoleId
                WHERE r.Name = 'Operator' AND r.IsSystem = 1;");

            migrationBuilder.Sql(
                "DELETE FROM Roles WHERE Name = 'Operator' AND IsSystem = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberate one-way cleanup: the Operator role and its assignments are not restored.
            // If pre-WO-109 code is redeployed, BootstrapSeeder re-creates the Operator system role
            // on startup, so no schema action is required here.
        }
    }
}
