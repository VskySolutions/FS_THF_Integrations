using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameRemsInsuranceHealthOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Insurance - Health" is relabelled "Insurance - Healthcare" in REMS.SubIndustry — the list
            // the UI shows as "Industry". The VALUE is deliberately untouched: `insurance_health` is the
            // code engagements already record against, and renaming a code strands every one of them.
            //
            // Changing DefaultOptionSets alone reaches nobody already running. TenantOptionSetSeeder is
            // idempotent per LIST, so a tenant that already holds REMS.SubIndustry keeps the labels it was
            // seeded with and never sees an edit made to the defaults afterwards. This rewrites the label
            // in every existing copy — the platform-standard list (TenantId IS NULL) and each tenant's own.
            //
            // Matching on the OLD LABEL is the guard against clobbering somebody's own edit: a tenant who
            // has already renamed this item is not carrying "Insurance - Health" any more, so they are
            // skipped and keep what they chose. It is what makes this idempotent, too — re-running matches
            // nothing.
            //
            // UpdatedById stays NULL: the platform changed this, not a user. That also keeps the preceding
            // BackfillRemsIndustryAndDepartmentOptions rollback working, since its Down() only removes rows
            // whose UpdatedById is still null.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = N'Insurance - Healthcare',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.SubIndustry'
                  AND s.[Deleted] = 0
                  AND i.[Value] = N'insurance_health'
                  AND i.[Label] = N'Insurance - Health'
                  AND i.[Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The same statement the other way round, guarded the same way: only a row still reading the
            // new label goes back, so a tenant who relabelled it after this ran keeps their own wording.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = N'Insurance - Health',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.SubIndustry'
                  AND s.[Deleted] = 0
                  AND i.[Value] = N'insurance_health'
                  AND i.[Label] = N'Insurance - Healthcare'
                  AND i.[Deleted] = 0;
                """);
        }
    }
}
