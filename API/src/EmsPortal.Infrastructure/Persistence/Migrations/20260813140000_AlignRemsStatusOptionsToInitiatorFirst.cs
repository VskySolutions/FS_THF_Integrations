using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignRemsStatusOptionsToInitiatorFirst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bring the seeded REMS.Status lists in line with the initiator-first lifecycle.
            //
            // Changing DefaultOptionSets only affects lists that do not exist yet — BootstrapSeeder and
            // TenantOptionSetSeeder are idempotent per LIST, not per item, so neither revisits a
            // REMS.Status list already on file. Without this migration an existing tenant keeps the old
            // list: the two new stages have no label at all (their badges fall back to the raw code), and
            // the retired "Submitted" stays on offer as a filter for a stage nothing can reach.
            //
            //   + returned_to_initiator        (admin sent the engagement setup back, with a reason)
            //   + awaiting_admin_confirmation  (initiator revised it; back with the admin to confirm)
            //   ~ customer_submitted relabelled "Admin Review" — the setup is filled before any of this
            //     now, so what happens at that stage is review, not setup
            //   - submitted                    (the Admin Pool it named is gone)
            //
            // Non-destructive where a tenant may have made the list their own: labels and sort orders are
            // only touched while they still hold the seeded defaults, so a rename is never overwritten.
            // Follows AlignRemsRequestStatusOptions, which did the same job for the previous reshape.

            // 1. Add the two new stages wherever they are missing.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [SortOrder], [IsDefault], [IsActive],
                     [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], v.[Value], v.[Label], v.[SortOrder], 0, 1,
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                CROSS JOIN (VALUES
                    (N'returned_to_initiator',       N'Returned to Initiator',       4),
                    (N'awaiting_admin_confirmation', N'Awaiting Admin Confirmation', 5)
                ) AS v([Value], [Label], [SortOrder])
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = v.[Value] AND i.[Deleted] = 0);
                """);

            // 2. Relabel the review stage and close the sort-order gaps the two insertions open. Each
            //    guarded on the value it is replacing, so a tenant's own wording survives untouched.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = N'Admin Review', i.[SortOrder] = 3, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'customer_submitted' AND i.[Label] = N'Engagement Setup';

                UPDATE i
                SET i.[SortOrder] = 2, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'awaiting_customer' AND i.[SortOrder] = 3;

                UPDATE i
                SET i.[SortOrder] = 6, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'pending_approval' AND i.[SortOrder] = 5;

                UPDATE i
                SET i.[SortOrder] = 7, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'changes_requested' AND i.[SortOrder] = 6;

                UPDATE i
                SET i.[SortOrder] = 8, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'approved' AND i.[SortOrder] = 7;
                """);

            // 3. Retire 'submitted'. Any request still holding it was moved to 'draft' by the
            //    RemsInitiatorFirstRebuild migration, so nothing is left pointing at it.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'submitted';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore 'submitted' and withdraw the two added stages. Labels and sort orders are left as
            // they are: which of them this migration actually rewrote is not recoverable afterwards, and
            // the VALUES — what the code branches on — round-trip exactly.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 1
                  AND i.[Value] = N'submitted';

                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] IN (N'returned_to_initiator', N'awaiting_admin_confirmation');
                """);
        }
    }
}
