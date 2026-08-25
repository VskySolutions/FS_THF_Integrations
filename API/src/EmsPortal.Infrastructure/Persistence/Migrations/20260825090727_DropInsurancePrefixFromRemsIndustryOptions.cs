using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Takes "Insurance -" off the four insurance trades in REMS.SubIndustry — the list the UI shows as
    /// "Industry". The Industry picker is narrowed by the Entity Type beside it, so under an Insurance
    /// entity the list read Insurance, Insurance, Insurance, Insurance and the reader had to get past the
    /// same word four times to reach the one that differs.
    /// <para>
    /// The VALUES are deliberately untouched: <c>insurance_property_casualty</c> and its siblings are the
    /// codes engagements are already recorded against, and renaming a code strands every one of them.
    /// </para>
    /// <para>
    /// No model change — the labels live in <c>OptionSetItems</c> rows. Changing <c>DefaultOptionSets</c>
    /// alone reaches nobody already running: <c>TenantOptionSetSeeder</c> is idempotent per LIST, so a
    /// tenant that already holds REMS.SubIndustry keeps the labels it was seeded with. This rewrites them
    /// in every existing copy — the platform-standard list (TenantId IS NULL) and each tenant's own.
    /// </para>
    /// <para>
    /// Matching on the OLD label is the guard against clobbering somebody's own edit: a tenant who has
    /// already renamed one of these is not carrying the old wording any more, so they are skipped and keep
    /// what they chose. It is what makes this idempotent too — re-running matches nothing.
    /// <c>insurance_health</c> is matched against BOTH its wordings, since a copy seeded before
    /// <c>RenameRemsInsuranceHealthOption</c> still says "Insurance - Health".
    /// </para>
    /// <para>
    /// <c>UpdatedById</c> stays NULL: the platform changed this, not a user.
    /// </para>
    /// </summary>
    public partial class DropInsurancePrefixFromRemsIndustryOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = v.[NewLabel],
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                    (N'insurance_property_casualty', N'Insurance - Property and Casualty', N'Property and Casualty'),
                    (N'insurance_life',              N'Insurance - Life',                  N'Life'),
                    (N'insurance_other',             N'Insurance - Other',                 N'Other'),
                    (N'insurance_health',            N'Insurance - Healthcare',            N'Healthcare'),
                    (N'insurance_health',            N'Insurance - Health',                N'Healthcare')
                ) AS v([Value], [OldLabel], [NewLabel])
                    ON v.[Value] = i.[Value] AND v.[OldLabel] = i.[Label]
                WHERE s.[Key] = N'REMS.SubIndustry'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The same statement the other way round, guarded the same way: only a row still reading the
            // short label goes back, so a tenant who relabelled one after this ran keeps their own wording.
            // insurance_health returns to "Insurance - Healthcare" — the wording it had immediately before
            // this migration, not the older "Insurance - Health" that RenameRemsInsuranceHealthOption owns.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Label] = v.[OldLabel],
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                    (N'insurance_property_casualty', N'Insurance - Property and Casualty', N'Property and Casualty'),
                    (N'insurance_life',              N'Insurance - Life',                  N'Life'),
                    (N'insurance_other',             N'Insurance - Other',                 N'Other'),
                    (N'insurance_health',            N'Insurance - Healthcare',            N'Healthcare')
                ) AS v([Value], [OldLabel], [NewLabel])
                    ON v.[Value] = i.[Value] AND v.[NewLabel] = i.[Label]
                WHERE s.[Key] = N'REMS.SubIndustry'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0;
                """);
        }
    }
}
