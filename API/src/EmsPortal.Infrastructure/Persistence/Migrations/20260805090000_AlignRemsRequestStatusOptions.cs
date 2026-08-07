using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignRemsRequestStatusOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The REMS.Status list was seeded with a lifecycle the code never actually walked: nothing ever
            // set 'sent', 'awaiting_customer', 'approved' or 'rejected', so a request sat on
            // 'customer_submitted' from the moment the client's form arrived until forever — through
            // engagement setup, approval and full approval alike. RemsRequestStatuses now drives the whole
            // lifecycle, and this brings the seeded lists in line with it:
            //
            //   + pending_approval / changes_requested  (new stages, set by the approval workflow)
            //   ~ customer_submitted relabelled "Engagement Setup" (the stage, not the past event)
            //   - sent      (a duplicate of awaiting_customer that nothing ever set)
            //   - rejected  (an approval rejection is a rework loop, now 'changes_requested')
            //
            // BootstrapSeeder and TenantOptionSetSeeder are idempotent per LIST, not per item, so neither
            // reaches a REMS.Status list that already exists — hence doing it here, across the platform
            // standard list AND every tenant copy. Deliberately non-destructive where a tenant may have made
            // the list their own: labels/sort orders are only touched while they still hold the seeded
            // defaults, so a rename is never overwritten.

            // 1. Add the two new lifecycle values wherever they are missing.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [SortOrder], [IsDefault], [IsActive],
                     [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], v.[Value], v.[Label], v.[SortOrder], 0, 1,
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                CROSS JOIN (VALUES
                    (N'pending_approval',   N'Pending Approval',   5),
                    (N'changes_requested',  N'Changes Requested',  6)
                ) AS v([Value], [Label], [SortOrder])
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = v.[Value] AND i.[Deleted] = 0);
                """);

            // 2. Relabel 'customer_submitted' to the stage it represents, and close the sort-order gaps left
            //    by dropping 'sent'. Each guarded on the seeded value so tenant edits survive.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = N'Engagement Setup', i.[SortOrder] = 4, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'customer_submitted' AND i.[Label] = N'Customer Submitted';

                UPDATE i
                SET i.[SortOrder] = 3, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'awaiting_customer' AND i.[SortOrder] = 4;

                UPDATE i
                SET i.[SortOrder] = 7, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'approved' AND i.[SortOrder] = 6;
                """);

            // 3. Retire the two dead values. Soft-delete, per the platform convention — and safe because no
            //    REMS row can hold either code: no code path has ever written them.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] IN (N'sent', N'rejected');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the retired values and withdraw the added ones. Labels/sort orders are left as they
            // are: which of them this migration actually rewrote is not recoverable after the fact, and the
            // values themselves — what the code branches on — round-trip exactly.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 1
                  AND i.[Value] IN (N'sent', N'rejected');

                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] IN (N'pending_approval', N'changes_requested');
                """);
        }
    }
}
