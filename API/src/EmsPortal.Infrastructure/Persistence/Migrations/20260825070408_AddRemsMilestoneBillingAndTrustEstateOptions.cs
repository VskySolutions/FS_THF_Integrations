using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Two new REMS option values: <c>milestone</c> in Billing Frequency, and <c>trust_estate</c>
    /// ("Trust and Estate") in Entity Type.
    /// <para>
    /// No model change — the values live in <c>OptionSetItems</c> rows, not in the schema. Adding them to
    /// <c>DefaultOptionSets</c> alone reaches nobody already running: <c>TenantOptionSetSeeder</c> is
    /// idempotent per LIST, so a tenant that already holds REMS.BillingPeriod or REMS.IndustryGroup keeps
    /// exactly the items it was seeded with. This inserts them into every existing copy — the
    /// platform-standard list (TenantId IS NULL) and each tenant's own.
    /// </para>
    /// <para>
    /// Guarded on the VALUE so re-running adds nothing twice, and so a tenant who has already added an item
    /// under the same code keeps theirs. <c>UpdatedById</c> is left NULL throughout: the platform added
    /// these, not a user — which is also what makes <see cref="Down"/> safe to run, since it withdraws only
    /// the rows nobody has since edited.
    /// </para>
    /// <para>
    /// "Trust and Estate" is asked the same questions as a business — an EIN and the primary / financial /
    /// billing contacts — which is enforced in code by
    /// <c>RemsFormPayloadValidator.BusinessGroups</c>, not by anything here.
    /// </para>
    /// </summary>
    public partial class AddRemsMilestoneBillingAndTrustEstateOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Billed when a piece of work lands rather than when the calendar turns. It sorts after the
            // three frequencies because it is the exception to them.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [Description], [SortOrder],
                     [IsDefault], [IsActive], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], N'milestone', N'Milestone',
                       N'Billed as each agreed milestone is reached, rather than on a calendar cycle. Set out the milestones in the Description of Billing Process.',
                       4, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.BillingPeriod'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = N'milestone' AND i.[Deleted] = 0);
                """);

            // A trust or a decedent's estate. Sorted after Government, which is where the list ends today.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [Description], [SortOrder],
                     [IsDefault], [IsActive], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], N'trust_estate', N'Trust and Estate',
                       N'A trust or a decedent''s estate. Asked the same questions as a business — it has an EIN and is acted for by trustees or personal representatives rather than by an individual.',
                       6, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.IndustryGroup'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = N'trust_estate' AND i.[Deleted] = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Soft-deleted, like every other row in this database, and only where the item is still the one
            // this migration inserted — an <c>UpdatedById</c> means somebody has since relabelled or
            // re-ordered it, and that is their edit to keep. Engagements and forms already recording either
            // code keep it: a value the picker no longer offers is still a value the row holds.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Deleted] = 0 AND i.[Deleted] = 0 AND i.[UpdatedById] IS NULL
                  AND ((s.[Key] = N'REMS.BillingPeriod'  AND i.[Value] = N'milestone')
                    OR (s.[Key] = N'REMS.IndustryGroup' AND i.[Value] = N'trust_estate'));
                """);
        }
    }
}
