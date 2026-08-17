using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameRemsEngagementClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename what the Engagement Setup classifications are CALLED, and retire the one that was
            // asking a question another already answered:
            //
            //   REMS.IndustryGroup   "REMS Industry Group"    -> "REMS Entity Type"
            //   REMS.SubIndustry     "REMS Sub-Industry"      -> "REMS Industry"
            //   REMS.SubServiceLine  "REMS Sub-Service Line"  -> "REMS Service Line"
            //   REMS.ServiceLine     retired
            //
            // The KEYS are deliberately untouched. A tenant's own copy of a list is keyed by them, as are
            // the engagement columns and every resolve call; renaming a key would orphan each tenant's copy
            // and strand the codes already stored against it. So this changes only the NAME an admin sees
            // in Administration -> Option Sets, which is the half that was wrong.
            //
            // Needed because editing DefaultOptionSets alone would not reach anybody: BootstrapSeeder and
            // TenantOptionSetSeeder each insert a list only when its key is ABSENT, so an existing tenant
            // keeps the name it was seeded with and would go looking for "Industry" under "Sub-Industry".
            //
            // Guarded on the old name in every case, so a tenant who had already renamed a list to
            // something of their own keeps their wording.
            migrationBuilder.Sql(
                """
                UPDATE [OptionSets]
                SET [Name] = N'REMS Entity Type', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.IndustryGroup' AND [Deleted] = 0 AND [Name] = N'REMS Industry Group';

                UPDATE [OptionSets]
                SET [Name] = N'REMS Industry', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.SubIndustry' AND [Deleted] = 0 AND [Name] = N'REMS Sub-Industry';

                UPDATE [OptionSets]
                SET [Name] = N'REMS Service Line', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.SubServiceLine' AND [Deleted] = 0 AND [Name] = N'REMS Sub-Service Line';
                """);

            // Retire the dropped list. It classified the CLIENT (Commercial / Non-Profit / Government /
            // Individual) under the name of a service, which is what the entity type says — so every
            // engagement answered one question twice and the two answers could disagree. Soft-deleted
            // rather than removed: the rows survive, Down puts the list back, and the codes already stored
            // on REMSEngagement.ServiceLine are not touched by any of this. Retired rather than left live
            // because "REMS Service Line" is now the name of a DIFFERENT list, and two lists under one
            // name in the admin screen is worse than one list too few.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.ServiceLine' AND i.[Deleted] = 0;

                UPDATE [OptionSets]
                SET [Deleted] = 1, [DeletedOnUtc] = SYSUTCDATETIME(), [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.ServiceLine' AND [Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Put the service-line list back, then restore the three names. Each guarded on the NEW name,
            // so a tenant who renamed a list after this ran keeps their own wording here too.
            migrationBuilder.Sql(
                """
                UPDATE [OptionSets]
                SET [Deleted] = 0, [DeletedOnUtc] = NULL, [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.ServiceLine' AND [Deleted] = 1;

                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.ServiceLine' AND i.[Deleted] = 1;
                """);

            migrationBuilder.Sql(
                """
                UPDATE [OptionSets]
                SET [Name] = N'REMS Sub-Service Line', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.SubServiceLine' AND [Deleted] = 0 AND [Name] = N'REMS Service Line';

                UPDATE [OptionSets]
                SET [Name] = N'REMS Sub-Industry', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.SubIndustry' AND [Deleted] = 0 AND [Name] = N'REMS Industry';

                UPDATE [OptionSets]
                SET [Name] = N'REMS Industry Group', [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Key] = N'REMS.IndustryGroup' AND [Deleted] = 0 AND [Name] = N'REMS Entity Type';
                """);
        }
    }
}
