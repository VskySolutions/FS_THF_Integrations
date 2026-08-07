using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiRoleTenantAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WO-122: convert tenant assignments from one-role-per-(user,tenant) to MULTI-role, with a
            // required RoleId. Order matters: drop the old unique index, backfill RoleId, make it NOT
            // NULL, then create the new (user, tenant, role) unique index. Touches only UserTenantRoles.

            // (1) Drop the old one-role-per-(user,tenant) unique index so a user can hold several roles
            //     in the same tenant.
            migrationBuilder.DropIndex(
                name: "IX_UserTenantRoles_UserId_TenantId",
                table: "UserTenantRoles");

            // (2) Backfill: RoleId is becoming required. Point any assignment that still has a NULL
            //     RoleId at the seeded system role whose Name matches the legacy enum string
            //     (SuperAdmin/TenantAdmin). No-op on a fresh database (rows are seeded post-migration).
            migrationBuilder.Sql(@"
                UPDATE utr
                SET utr.RoleId = r.Id
                FROM UserTenantRoles utr
                INNER JOIN Roles r ON r.Name = utr.Role AND r.IsSystem = 1 AND r.Deleted = 0
                WHERE utr.RoleId IS NULL;");

            // (2b) Any rows still NULL reference no resolvable role — orphaned former-Operator
            //      assignments (WO-109) whose legacy enum is the neutral 'Custom' sentinel and which
            //      grant no access. RoleId is now required, so remove them (physical delete: this
            //      junction is a leaf table with no dependents). Without this, step (3) would fail.
            migrationBuilder.Sql(
                "DELETE FROM UserTenantRoles WHERE RoleId IS NULL;");

            // (3) RoleId → NOT NULL: every assignment now references exactly one concrete role.
            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "UserTenantRoles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // (4) New unique index — one row per (user, tenant, role), excluding soft-deleted rows.
            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_UserId_TenantId_RoleId",
                table: "UserTenantRoles",
                columns: new[] { "UserId", "TenantId", "RoleId" },
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema-only reversal. The data operations in Up() are one-way: backfilled RoleId values and
            // deleted orphan rows are not restored. Reverting to the old unique index requires that each
            // (user, tenant) hold at most one active role — ensure this before applying Down.

            // Reverse (4): drop the (user, tenant, role) unique index.
            migrationBuilder.DropIndex(
                name: "IX_UserTenantRoles_UserId_TenantId_RoleId",
                table: "UserTenantRoles");

            // Reverse (3): RoleId → nullable again.
            migrationBuilder.AlterColumn<Guid>(
                name: "RoleId",
                table: "UserTenantRoles",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // Reverse (1): recreate the old one-role-per-(user, tenant) unique index.
            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_UserId_TenantId",
                table: "UserTenantRoles",
                columns: new[] { "UserId", "TenantId" },
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
