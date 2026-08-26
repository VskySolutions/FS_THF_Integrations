using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Retires the Managing Shareholder — the seat whose holder was added to EVERY approval round the firm
    /// routed, on top of the CSE, the department director and the commission recipients.
    /// <para>
    /// It existed twice over: as a seeded seat ROLE, and as the tenant-wide
    /// <c>RemsSettings.ManagingShareholderUserId</c> set from a user's own detail page. Both go. An
    /// engagement is signed off by the people it names, and a signature it needs from anyone else is added
    /// on its own Approval tab, which offers the whole tenant.
    /// </para>
    /// <para>
    /// DESTRUCTIVE and not recoverable: the column recording who the shareholder was is dropped, and
    /// <c>Down</c> puts it back empty. Approval TASKS that carried the role are rewritten to
    /// <c>Approver</c> rather than left orphaned — <c>RemsApproverRole</c> no longer has the member, and a
    /// stored string with no member behind it would fail every read of the round it belongs to. What those
    /// people decided, when, and the checklist they worked is untouched; only the name of their seat moves.
    /// </para>
    /// <para>
    /// The role's assignments are soft-deleted with the role, so a user whose ONLY role in a tenant was
    /// Managing Shareholder loses their access to that tenant. The seat granted nothing, so there is no
    /// capability to lose, but a firm that used it as a login marker should give those people a real role.
    /// </para>
    /// </summary>
    public partial class DropManagingShareholder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Approval tasks first. The enum member is gone, so a row still carrying the string would
            //    break every read of its round. 'Approver' is what such a person is now: somebody reviewing
            //    an engagement with no other standing on it.
            migrationBuilder.Sql(
                """
                UPDATE [REMSApprovalTask]
                SET [ApproverRole] = N'Approver', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [ApproverRole] = N'ManagingShareholder';
                """);

            // 2. The seat role and every assignment of it, in every tenant. Soft-delete, per the platform
            //    convention: the rows stay readable, they just stop counting. The assignments go first —
            //    afterwards the join to the role row no longer finds it.
            migrationBuilder.Sql(
                """
                UPDATE a
                SET a.[Deleted] = 1, a.[DeletedOnUtc] = SYSUTCDATETIME(), a.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserTenantRoles] a
                INNER JOIN [Roles] r ON r.[Id] = a.[RoleId]
                WHERE r.[Name] = N'Managing Shareholder' AND a.[Deleted] = 0;

                UPDATE [Roles]
                SET [Deleted] = 1, [DeletedOnUtc] = SYSUTCDATETIME(), [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Name] = N'Managing Shareholder' AND [Deleted] = 0;
                """);

            // 3. The tenant-wide setting behind it.
            migrationBuilder.DropForeignKey(
                name: "FK_RemsSettings_Users_ManagingShareholderUserId",
                table: "RemsSettings");

            migrationBuilder.DropIndex(
                name: "IX_RemsSettings_ManagingShareholderUserId",
                table: "RemsSettings");

            migrationBuilder.DropColumn(
                name: "ManagingShareholderUserId",
                table: "RemsSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagingShareholderUserId",
                table: "RemsSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemsSettings_ManagingShareholderUserId",
                table: "RemsSettings",
                column: "ManagingShareholderUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RemsSettings_Users_ManagingShareholderUserId",
                table: "RemsSettings",
                column: "ManagingShareholderUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The role comes back so the name is usable again (a rolled-back deployment brings the seeder
            // back with it, which would recreate it anyway). Its ASSIGNMENTS deliberately do not: nothing
            // records which of the soft-deleted ones this migration removed, and blanket-restoring them
            // would resurrect the ones a firm had already withdrawn by hand. Re-assign the seat instead.
            //
            // The approval tasks stay as they are for the same reason: 'Approver' and 'ManagingShareholder'
            // are indistinguishable afterwards, and inventing the difference back would be a guess about
            // who signed what.
            migrationBuilder.Sql(
                """
                UPDATE [Roles]
                SET [Deleted] = 0, [DeletedOnUtc] = NULL, [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Name] = N'Managing Shareholder' AND [Deleted] = 1;
                """);
        }
    }
}
