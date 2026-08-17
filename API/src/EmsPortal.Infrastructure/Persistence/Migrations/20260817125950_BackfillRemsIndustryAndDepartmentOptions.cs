using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRemsIndustryAndDepartmentOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two new option values:
            //
            //   REMS.SubIndustry  (shown as "Industry")     + insurance_health  "Insurance - Health"
            //   REMS.Department                             + admin             "Admin"
            //
            // Adding them to DefaultOptionSets alone would reach nobody who is already running.
            // BootstrapSeeder and TenantOptionSetSeeder each insert a LIST only when its key is absent, so
            // a tenant seeded before today keeps the items it was seeded with and never sees a value added
            // to the defaults afterwards. This inserts the two into every existing copy of those lists —
            // the platform-standard one (TenantId IS NULL) and each tenant's own.
            //
            // Purely additive, and idempotent. It touches no existing row: a tenant who had already added
            // a value under either code — or who reordered, relabelled or retired items of their own — is
            // skipped by the NOT EXISTS guard and keeps exactly what they had. Re-running changes nothing.
            //
            // SortOrder is MAX + 1 within each list rather than a fixed number, because tenants have
            // edited these lists and a hard-coded position would either collide with an existing item or
            // land somewhere arbitrary. That does mean the new value appears LAST for everyone, including
            // in the seeded defaults, which is why DefaultOptionSets appends rather than slotting
            // "Insurance - Health" in beside the other Insurance trades.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [ParentItemId], [Value], [Label], [Description],
                     [SortOrder], [IsDefault], [IsActive], [BackgroundColor], [TextColor], [MetadataJson],
                     [CreatedById], [CreatedOnUtc], [UpdatedById], [UpdatedOnUtc], [Deleted], [DeletedOnUtc])
                SELECT
                    NEWID(), s.[Id], s.[TenantId], NULL, v.[Value], v.[Label], NULL,
                    ISNULL((SELECT MAX(x.[SortOrder]) FROM [OptionSetItems] x
                            WHERE x.[OptionSetId] = s.[Id] AND x.[Deleted] = 0), 0) + 1,
                    0, 1, NULL, NULL, NULL,
                    NULL, SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), 0, NULL
                FROM [OptionSets] s
                CROSS APPLY (VALUES
                    (N'REMS.SubIndustry', N'insurance_health', N'Insurance - Health'),
                    (N'REMS.Department',  N'admin',            N'Admin')
                ) AS v([SetKey], [Value], [Label])
                WHERE s.[Key] = v.[SetKey]
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id]
                        AND i.[Value] = v.[Value]
                        AND i.[Deleted] = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hard-delete rather than soft: these rows did not exist before this migration, so removing
            // them restores the previous state exactly. Guarded on the audit columns being untouched
            // (UpdatedById IS NULL) so a tenant who has since relabelled or reordered the item keeps it —
            // rolling a migration back should not throw away somebody's edit.
            //
            // An engagement already recording one of these codes keeps it: the code is a plain string on
            // the engagement, and a missing option list entry renders as the raw code rather than blank.
            migrationBuilder.Sql(
                """
                DELETE i
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE ((s.[Key] = N'REMS.SubIndustry' AND i.[Value] = N'insurance_health')
                    OR (s.[Key] = N'REMS.Department'  AND i.[Value] = N'admin'))
                  AND i.[UpdatedById] IS NULL
                  AND i.[Deleted] = 0;
                """);
        }
    }
}
