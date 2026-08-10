using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Splits the REMS.IndustryGroup value "Business" into the three kinds THF actually onboards —
    /// Not-for-Profit, Insurance and Commercial. They ask the client exactly the same questions the single
    /// group did (EIN, CEO/CFO/Accounts Payable, banker, lawyer), so this names what the client IS without
    /// changing the form; <c>RemsFormPayloadValidator.IsBusinessGroup</c> is what keeps them one family.
    /// <para>
    /// No model change — the values live in OptionSetItems rows, not in the schema.
    /// </para>
    /// </summary>
    public partial class SplitRemsBusinessIndustryGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add the three replacements wherever they are missing, across the platform standard list
            //    AND every tenant copy. Guarded on the value so re-running adds nothing twice.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [SortOrder], [IsDefault], [IsActive],
                     [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], v.[Value], v.[Label], v.[SortOrder], 0, 1,
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                CROSS JOIN (VALUES
                    (N'not_for_profit', N'Not-for-Profit', 2),
                    (N'insurance',      N'Insurance',      3),
                    (N'commercial',     N'Commercial',     4)
                ) AS v([Value], [Label], [SortOrder])
                WHERE s.[Key] = N'REMS.IndustryGroup'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = v.[Value] AND i.[Deleted] = 0);
                """);

            // 2. Move Government to the end of the list, behind the three new values. Guarded on the
            //    seeded position so a tenant who re-ordered their list keeps their order.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[SortOrder] = 5, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.IndustryGroup' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'government' AND i.[SortOrder] = 3;
                """);

            // 3. Retire "business" from the picker — DEACTIVATED, not deleted, and deliberately so.
            //    REMSForm rows sent before this split still hold the code: a client part-way through one
            //    of those forms has to be able to finish it, and the row it came from must stay readable.
            //    The server still accepts the code (RemsFormValidators.IndustryGroups); it just stops
            //    being offered for new forms.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[IsActive] = 0, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.IndustryGroup' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'business';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Puts "business" back in the picker and withdraws the three replacements. Any form already
            // sent under one of the new codes keeps it — the code stays valid, it is simply not offered.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[IsActive] = 1, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.IndustryGroup' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'business';

                UPDATE i
                SET i.[SortOrder] = 3, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.IndustryGroup' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'government' AND i.[SortOrder] = 5;

                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.IndustryGroup' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] IN (N'not_for_profit', N'insurance', N'commercial');
                """);
        }
    }
}
