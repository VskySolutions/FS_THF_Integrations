using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeRemsExistingClientTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // REMS.Type offered 'new_engagement' ("New Engagement") and 'existing_client' ("Existing
            // Client") as separate answers to one question — how does this referral relate to THF's
            // records? — but every new engagement for a client we already have is both, so the split
            // only ever asked the partner to guess. They become a single value, "New Engagement,
            // Existing Client".
            //
            // 'existing_client' is the code that survives, because it is the one the app already reasons
            // about: REMS_EXISTING_CLIENT_TYPES marks it as "an existing client is referenced", and the
            // intake form auto-selects it the moment a client is picked from the lookup. Keeping it means
            // no row and no rule needs translating — only the rows that held the retired code do.
            //
            // Order matters: re-point the data first, so no request is left pointing at a value that is
            // about to disappear from the list.

            // 1. Re-point every request that held the retired code. UpdatedOnUtc is deliberately left
            //    alone — this is a rename of a value, not an edit anybody made to the request.
            migrationBuilder.Sql(
                """
                UPDATE [REMS] SET [Type] = N'existing_client' WHERE [Type] = N'new_engagement';
                """);

            // 2. Relabel the surviving item and move it up into the freed slot, across the platform
            //    standard list AND every tenant copy. Guarded on the seeded label / sort order, per the
            //    convention set by AlignRemsRequestStatusOptions: a tenant that renamed or re-ordered the
            //    value has made the list their own and must not be overwritten.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = N'New Engagement, Existing Client', i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client' AND i.[Label] = N'Existing Client';

                UPDATE i
                SET i.[SortOrder] = 2, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client' AND i.[SortOrder] = 3;

                UPDATE i
                SET i.[SortOrder] = 3, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'subsidiary_child_of_existing_client' AND i.[SortOrder] = 4;
                """);

            // 3. Retire the merged-away value. Soft-delete, per the platform convention — and safe now
            //    that step 1 has left no request holding it.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'new_engagement';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the retired value and the seeded label/sort orders. The requests re-pointed by
            // step 1 stay on 'existing_client': which of them started out as 'new_engagement' is not
            // recoverable once merged, and 'existing_client' is a valid answer for every one of them.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 1
                  AND i.[Value] = N'new_engagement';

                UPDATE i
                SET i.[Label] = N'Existing Client', i.[SortOrder] = 3, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client' AND i.[Label] = N'New Engagement, Existing Client';

                UPDATE i
                SET i.[SortOrder] = 4, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'subsidiary_child_of_existing_client' AND i.[SortOrder] = 3;
                """);
        }
    }
}
