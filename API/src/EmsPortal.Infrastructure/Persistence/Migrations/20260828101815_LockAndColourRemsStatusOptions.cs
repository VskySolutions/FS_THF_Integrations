using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Brings every EXISTING copy of REMS.Status up to the shape the front end now reads it in: closed,
    /// locked, coloured, and carrying the "Waiting For Pickup" value.
    /// <para>
    /// A migration is needed because <c>TenantOptionSetSeeder</c> is idempotent per LIST — a tenant that
    /// already holds REMS.Status is left exactly as they edited it, so changing
    /// <c>DefaultOptionSets</c> alone reaches nobody already running. The seven NEW lists this release adds
    /// (REMS.FormStatus, REMS.ApprovalStatus and the rest) need no backfill: their keys are absent, so the
    /// platform seeder inserts them on the next start and every tenant resolves them by the standard
    /// fallback until they take a copy of their own.
    /// </para>
    /// <para>
    /// Nothing here overwrites a choice somebody has made. The colours are written only where the row has
    /// none — the column was never populated for REMS, so this is filling a blank rather than replacing a
    /// decision — and "Waiting For Pickup" is inserted only into a list that does not already have it.
    /// </para>
    /// </summary>
    public partial class LockAndColourRemsStatusOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. The list is one the application branches on: no value may be added or removed, and every
            //    value it already has is one the server writes. Both flags are what the API enforces.
            migrationBuilder.Sql(
                """
                UPDATE [OptionSets]
                SET [IsClosed] = 1, [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.Status' AND [Deleted] = 0 AND [IsClosed] = 0;

                UPDATE i
                SET i.[IsSystem] = 1, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0 AND i.[IsSystem] = 0;
                """);

            // 1b. Three lists that stay OPEN — a firm may add a department, an entity type or a way of
            //     classifying a referral — but whose SEEDED codes the application branches on by name.
            //     Deleting or re-coding `audit` breaks the signed-CAF card, the government contract block
            //     and the approval prerequisites; `individual` and `government` shape the client's intake
            //     form; the two type codes drive the client-lookup marking.
            //
            //     Matched on the seeded VALUES, so a code a tenant added themselves stays fully theirs.
            //     These columns are stored as STRINGS on the REMS rows rather than as foreign keys to the
            //     option item, which is exactly why the guard has to live in the application: the database
            //     cannot refuse the delete on its own.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[IsSystem] = 1, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Deleted] = 0 AND i.[Deleted] = 0 AND i.[IsSystem] = 0
                  AND (
                      (s.[Key] = N'REMS.Department'
                       AND i.[Value] IN (N'cas', N'tax', N'audit', N'gcs', N'assurance', N'admin'))
                   OR (s.[Key] = N'REMS.IndustryGroup'
                       AND i.[Value] IN (N'individual', N'not_for_profit', N'insurance', N'commercial',
                                         N'government', N'trust_estate', N'business'))
                   OR (s.[Key] = N'REMS.Type'
                       AND i.[Value] IN (N'brand_new_client', N'existing_client'))
                  );
                """);

            // 2. The badge colours. These are the exact shades the front end used to hold as hardcoded
            //    Quasar colour names, so a status looks the way it always did — and is now recolourable in
            //    Administration → Option Sets, which it never was. Only rows with no colour are touched.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[BackgroundColor] = v.[Bg],
                    i.[TextColor] = N'#ffffff',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                    (N'draft',                        N'#9e9e9e'),
                    (N'awaiting_customer',            N'#00897b'),
                    (N'customer_submitted',           N'#673ab7'),
                    (N'returned_to_initiator',        N'#ef6c00'),
                    (N'awaiting_admin_confirmation',  N'#9575cd'),
                    (N'pending_approval',             N'#f57c00'),
                    (N'changes_requested',            N'#C10015'),
                    (N'approved',                     N'#21BA45')
                ) AS v([Value], [Bg]) ON v.[Value] = i.[Value]
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[BackgroundColor] IS NULL;
                """);

            // 3. "Waiting For Pickup". It is not a stored status — `customer_submitted` covers both "an
            //    admin has this" and "nobody has picked it up yet" — but it IS a badge a user reads, and
            //    until now its wording and colour were a hardcoded string in the front end. As a value here
            //    a firm can rename it, explain it and recolour it like any other.
            //
            //    Inserted at position 4, where the stage actually falls, with everything from there down
            //    shifted one place. The shift moves the whole tail together, so a tenant who has re-ordered
            //    their list keeps their arrangement.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[SortOrder] = i.[SortOrder] + 1, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[SortOrder] >= 4
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] x
                      WHERE x.[OptionSetId] = s.[Id] AND x.[Deleted] = 0 AND x.[Value] = N'waiting_for_pickup');

                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [ParentItemId], [Value], [Label], [Description],
                     [SortOrder], [IsDefault], [IsActive], [BackgroundColor], [TextColor], [Icon],
                     [IsSystem], [MetadataJson], [CreatedById], [CreatedOnUtc], [UpdatedById],
                     [UpdatedOnUtc], [Deleted], [DeletedOnUtc])
                SELECT
                    NEWID(), s.[Id], s.[TenantId], NULL, N'waiting_for_pickup', N'Waiting For Pickup',
                    N'The client''s answers are in and the request is with the admins, but no admin has picked it up yet. Until one does, its engagement setup is nobody''s to work.',
                    4, 0, 1, N'#ffa000', N'#ffffff', NULL,
                    1, NULL, NULL, SYSUTCDATETIME(), NULL,
                    SYSUTCDATETIME(), 0, NULL
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] x
                      WHERE x.[OptionSetId] = s.[Id] AND x.[Deleted] = 0 AND x.[Value] = N'waiting_for_pickup');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The inserted value goes for real rather than being soft-deleted: it was never a code any row
            // is recorded against, so there is nothing pointing at it to strand. The lock and the colours
            // are lifted the same way they were applied.
            migrationBuilder.Sql(
                """
                DELETE i
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND i.[Value] = N'waiting_for_pickup';

                UPDATE i
                SET i.[SortOrder] = i.[SortOrder] - 1, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0 AND i.[SortOrder] >= 5;

                UPDATE i
                SET i.[BackgroundColor] = NULL, i.[TextColor] = NULL, i.[IsSystem] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Status' AND s.[Deleted] = 0 AND i.[Deleted] = 0;

                UPDATE i
                SET i.[IsSystem] = 0, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND s.[Key] IN (N'REMS.Department', N'REMS.IndustryGroup', N'REMS.Type');

                UPDATE [OptionSets]
                SET [IsClosed] = 0, [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.Status' AND [Deleted] = 0;
                """);
        }
    }
}
