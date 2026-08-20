using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Turns the four REMS seats — CSE, Engagement Executive, Billing Manager, Managing Shareholder —
    /// from USER GROUPS into ROLES, and retires the <c>Approver</c> role.
    /// <para>
    /// Data only; there is no schema change. The four names were user groups a tenant created by hand and
    /// the code looked up by name; they are seeded system roles now, so everyone already in one of those
    /// groups is given the matching role in the same tenant and the group is retired behind them. Nobody
    /// has to be re-picked, and the engagement pickers keep offering the same people.
    /// </para>
    /// <para>
    /// The <c>Approver</c> role goes with them: the "add approvers" picker offers every user in the tenant
    /// now, so nothing reads the role. Its assignments are soft-deleted, which means a user whose ONLY role
    /// in a tenant was Approver loses their access to that tenant — they held a role that granted nothing
    /// and did nothing but appear in one picker, so there was no access to lose, but a firm that used it as
    /// a login-only marker should re-assign those people a real role.
    /// </para>
    /// <para>
    /// Soft-delete throughout, per the platform convention: nothing here is physically removed, so the
    /// groups and their memberships stay readable in the database if a firm needs to see what moved.
    /// </para>
    /// </summary>
    public partial class RemsSeatRolesReplaceUserGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the four seat roles if they are not already there. BootstrapSeeder creates them on
            //    the next start too, but that runs AFTER migrations — step 2 needs the rows to exist now.
            //    Permissions is the JSON array column; empty, because a seat role grants nothing.
            //    A tenant that already has a CUSTOM role under one of these names is adopted rather than
            //    duplicated: the name is unique platform-wide, and the seeder would take it over anyway.
            migrationBuilder.Sql(
                """
                INSERT INTO [Roles] ([Id], [Name], [Description], [IsSystem], [Permissions],
                                     [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), v.[Name], v.[Description], 1, N'[]',
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM (VALUES
                    (N'CSE',                  N'REMS CSE: offerable as the Client Service Executive on an engagement, and as a commission recipient.'),
                    (N'Engagement Executive', N'REMS Engagement Executive: offerable as the Engagement Executive on an engagement.'),
                    (N'Billing Manager',      N'REMS Billing Manager: offerable as the Billing Manager on an engagement.'),
                    (N'Managing Shareholder', N'REMS Managing Shareholder: approves every engagement the firm routes.')
                ) AS v([Name], [Description])
                WHERE NOT EXISTS (
                    SELECT 1 FROM [Roles] r WHERE r.[Name] = v.[Name] AND r.[Deleted] = 0);
                """);

            // 2. Move the people. Every active member of one of the four groups gets the same-named role in
            //    the group's OWN tenant — that is what makes this a move rather than a rename. The legacy
            //    fixed-tier shadow is 'Custom', which is what every non-SuperAdmin/TenantAdmin role carries
            //    (see UsersController.MapLegacyRole). Guarded on the unique (user, tenant, role) index, so a
            //    user who somehow already holds the role is skipped instead of failing the migration.
            migrationBuilder.Sql(
                """
                INSERT INTO [UserTenantRoles] ([Id], [UserId], [TenantId], [Role], [RoleId],
                                               [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT DISTINCT NEWID(), m.[UserId], g.[TenantId], N'Custom', r.[Id],
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [UserGroupMembers] m
                INNER JOIN [UserGroups] g ON g.[Id] = m.[UserGroupId]
                INNER JOIN [Roles] r ON r.[Name] = g.[Name] AND r.[Deleted] = 0
                INNER JOIN [Users] u ON u.[Id] = m.[UserId]
                WHERE m.[Deleted] = 0
                  AND g.[Deleted] = 0
                  AND u.[Deleted] = 0
                  AND g.[Name] IN (N'CSE', N'Engagement Executive', N'Billing Manager', N'Managing Shareholder')
                  AND NOT EXISTS (
                      SELECT 1 FROM [UserTenantRoles] x
                      WHERE x.[UserId] = m.[UserId] AND x.[TenantId] = g.[TenantId]
                        AND x.[RoleId] = r.[Id] AND x.[Deleted] = 0);
                """);

            // 3. Retire the four groups and their memberships, in every tenant that made them. Left behind
            //    they would still be editable in Administration → User Groups, inviting somebody to add a
            //    person to a list nothing reads any more — the failure would be silent and would look like
            //    the picker being broken.
            migrationBuilder.Sql(
                """
                UPDATE m
                SET m.[Deleted] = 1, m.[DeletedOnUtc] = SYSUTCDATETIME(), m.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserGroupMembers] m
                INNER JOIN [UserGroups] g ON g.[Id] = m.[UserGroupId]
                WHERE m.[Deleted] = 0 AND g.[Deleted] = 0
                  AND g.[Name] IN (N'CSE', N'Engagement Executive', N'Billing Manager', N'Managing Shareholder');

                UPDATE g
                SET g.[Deleted] = 1, g.[DeletedOnUtc] = SYSUTCDATETIME(), g.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserGroups] g
                WHERE g.[Deleted] = 0
                  AND g.[Name] IN (N'CSE', N'Engagement Executive', N'Billing Manager', N'Managing Shareholder');
                """);

            // 4. Retire the Approver role. Its assignments go first: the FK into Roles is Restrict, and a
            //    role still holding live assignments is one the pickers would keep resolving.
            migrationBuilder.Sql(
                """
                UPDATE utr
                SET utr.[Deleted] = 1, utr.[DeletedOnUtc] = SYSUTCDATETIME(),
                    utr.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserTenantRoles] utr
                INNER JOIN [Roles] r ON r.[Id] = utr.[RoleId]
                WHERE r.[Name] = N'Approver' AND r.[IsSystem] = 1 AND utr.[Deleted] = 0;

                UPDATE r
                SET r.[Deleted] = 1, r.[DeletedOnUtc] = SYSUTCDATETIME(), r.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Roles] r
                WHERE r.[Name] = N'Approver' AND r.[IsSystem] = 1 AND r.[Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverses the retirements so the schema and the seeded catalogue round-trip: the groups and
            // their memberships come back, and so does Approver with the assignments it held.
            //
            // The seat-role ASSIGNMENTS created in step 2 are deliberately left in place. They are the same
            // fact the restored group memberships state, they grant nothing, and deleting them would also
            // remove any assignment somebody made by hand after the migration ran — a rollback should not
            // undo work that was never this migration's.
            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.[Deleted] = 0, r.[DeletedOnUtc] = NULL, r.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Roles] r
                WHERE r.[Name] = N'Approver' AND r.[IsSystem] = 1 AND r.[Deleted] = 1;

                UPDATE utr
                SET utr.[Deleted] = 0, utr.[DeletedOnUtc] = NULL, utr.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserTenantRoles] utr
                INNER JOIN [Roles] r ON r.[Id] = utr.[RoleId]
                WHERE r.[Name] = N'Approver' AND r.[IsSystem] = 1 AND utr.[Deleted] = 1;

                UPDATE g
                SET g.[Deleted] = 0, g.[DeletedOnUtc] = NULL, g.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserGroups] g
                WHERE g.[Deleted] = 1
                  AND g.[Name] IN (N'CSE', N'Engagement Executive', N'Billing Manager', N'Managing Shareholder');

                UPDATE m
                SET m.[Deleted] = 0, m.[DeletedOnUtc] = NULL, m.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [UserGroupMembers] m
                INNER JOIN [UserGroups] g ON g.[Id] = m.[UserGroupId]
                WHERE m.[Deleted] = 1 AND g.[Deleted] = 0
                  AND g.[Name] IN (N'CSE', N'Engagement Executive', N'Billing Manager', N'Managing Shareholder');
                """);
        }
    }
}
