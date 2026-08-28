using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves every REMS option-set value from a CODE STRING to a foreign key on <c>OptionSetItems.Id</c>.
    ///
    /// <para>
    /// Nine columns across five tables. Before this only the marketing methods and the tax forms were real
    /// references; everything else — the request's type and status, the form's entity type, the
    /// engagement's department, service line, industry and billing frequency, the GCS personnel level, the
    /// client's referral source — stored the code as text, which the database could not enforce anything
    /// about. Deleting a value out from under the rows holding it was a thing the schema permitted.
    /// </para>
    /// <para>
    /// Written by hand rather than left as EF scaffolded it: the generated version dropped the string
    /// columns and added empty guid columns beside them, which would have silently emptied every REMS
    /// record on the estate. The order here is add → BACKFILL → constrain → drop, so no value is lost.
    /// </para>
    /// </summary>
    public partial class MoveRemsOptionValuesToForeignKeys : Migration
    {
        /// <summary>(table, old code column, new id column, option-set key, required).</summary>
        private static readonly (string Table, string OldColumn, string NewColumn, string SetKey, bool Required)[] Columns =
        {
            ("REMS", "Type", "TypeId", "REMS.Type", true),
            ("REMS", "Status", "StatusId", "REMS.Status", true),
            ("REMSForm", "IndustryGroup", "IndustryGroupId", "REMS.IndustryGroup", true),
            ("REMSEngagement", "Department", "DepartmentId", "REMS.Department", false),
            ("REMSEngagement", "SubServiceLine", "SubServiceLineId", "REMS.SubServiceLine", false),
            ("REMSEngagement", "SubIndustry", "SubIndustryId", "REMS.SubIndustry", false),
            ("REMSEngagement", "BillingPeriod", "BillingPeriodId", "REMS.BillingPeriod", false),
            ("REMSEngagementGovernmentDetail", "PersonnelLevel", "PersonnelLevelId", "REMS.PersonnelLevel", false),
            ("REMSClient", "ReferralSource", "ReferralSourceId", "REMS.ReferralSource", false),
            // The department-director map keys off a department too, so it moves with the rest. Required:
            // a mapping row exists only to name a department's head.
            ("RemsDepartmentDirector", "Department", "DepartmentId", "REMS.Department", true),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The three composite indexes that lead with the status STRING. They come back keyed on the id
            // at the end, once the column they cover exists.
            migrationBuilder.DropIndex(name: "IX_REMS_TenantId_CreatedById_Status", table: "REMS");
            migrationBuilder.DropIndex(name: "IX_REMS_TenantId_OnBehalfOfUserId_Status", table: "REMS");
            migrationBuilder.DropIndex(name: "IX_REMS_TenantId_Status_AdminAssignedToId_CreatedOnUtc", table: "REMS");
            // The director map's uniqueness is per (tenant, department); it comes back on the id below.
            migrationBuilder.DropIndex(name: "IX_RemsDepartmentDirector_TenantId_Department", table: "RemsDepartmentDirector");

            // 1. Every new column NULLABLE to begin with, required ones included — there is nothing to put
            //    in them until the backfill below has run.
            foreach (var (table, _, newColumn, _, _) in Columns)
            {
                migrationBuilder.AddColumn<Guid>(name: newColumn, table: table, type: "uniqueidentifier", nullable: true);
            }

            foreach (var (table, oldColumn, newColumn, setKey, required) in Columns)
            {
                // 2. Point each row at the item its code names, in the list that is EFFECTIVE for that
                //    row's tenant — their own copy where they have one, the platform standard otherwise,
                //    which is the same precedence GetEffectiveSetAsync applies at runtime.
                migrationBuilder.Sql(
                    $"""
                    UPDATE t
                    SET t.[{newColumn}] = (
                        SELECT TOP 1 i.[Id]
                        FROM [OptionSetItems] i
                        INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                        WHERE s.[Key] = N'{setKey}' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                          AND (s.[TenantId] = t.[TenantId] OR s.[TenantId] IS NULL)
                          AND i.[Value] = t.[{oldColumn}]
                        ORDER BY CASE WHEN s.[TenantId] IS NULL THEN 1 ELSE 0 END)
                    FROM [{table}] t
                    WHERE t.[{oldColumn}] IS NOT NULL AND LTRIM(RTRIM(t.[{oldColumn}])) <> '';
                    """);

                // 3. Self-heal. A code with no item behind it is a value somebody deleted before the lists
                //    were locked — the very thing this migration exists to make impossible. Rather than
                //    strand the rows (or fail the deployment), the missing value is put back: added to the
                //    tenant's effective list, marked system so it cannot be removed again, and INACTIVE so
                //    it is not offered on new records. The label is the code, for somebody to tidy up.
                migrationBuilder.Sql(
                    $"""
                    INSERT INTO [OptionSetItems]
                        ([Id], [OptionSetId], [TenantId], [ParentItemId], [Value], [Label], [Description],
                         [SortOrder], [IsDefault], [IsActive], [BackgroundColor], [TextColor], [Icon],
                         [IsSystem], [MetadataJson], [CreatedById], [CreatedOnUtc], [UpdatedById],
                         [UpdatedOnUtc], [Deleted], [DeletedOnUtc])
                    SELECT
                        NEWID(), x.[SetId], x.[SetTenantId], NULL, x.[Code], x.[Code],
                        N'Recovered when option values became references: records held this code but the list no longer had it.',
                        1000, 0, 0, NULL, NULL, NULL,
                        1, NULL, NULL, SYSUTCDATETIME(), NULL,
                        SYSUTCDATETIME(), 0, NULL
                    FROM (
                        SELECT DISTINCT
                            t.[{oldColumn}] AS [Code],
                            s.[Id] AS [SetId],
                            s.[TenantId] AS [SetTenantId]
                        FROM [{table}] t
                        CROSS APPLY (
                            SELECT TOP 1 s2.[Id], s2.[TenantId]
                            FROM [OptionSets] s2
                            WHERE s2.[Key] = N'{setKey}' AND s2.[Deleted] = 0
                              AND (s2.[TenantId] = t.[TenantId] OR s2.[TenantId] IS NULL)
                            ORDER BY CASE WHEN s2.[TenantId] IS NULL THEN 1 ELSE 0 END) s
                        WHERE t.[{newColumn}] IS NULL
                          AND t.[{oldColumn}] IS NOT NULL AND LTRIM(RTRIM(t.[{oldColumn}])) <> ''
                    ) x;
                    """);

                // 4. …then point the rows that were waiting on it at the value just recovered.
                migrationBuilder.Sql(
                    $"""
                    UPDATE t
                    SET t.[{newColumn}] = (
                        SELECT TOP 1 i.[Id]
                        FROM [OptionSetItems] i
                        INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                        WHERE s.[Key] = N'{setKey}' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                          AND (s.[TenantId] = t.[TenantId] OR s.[TenantId] IS NULL)
                          AND i.[Value] = t.[{oldColumn}]
                        ORDER BY CASE WHEN s.[TenantId] IS NULL THEN 1 ELSE 0 END)
                    FROM [{table}] t
                    WHERE t.[{newColumn}] IS NULL
                      AND t.[{oldColumn}] IS NOT NULL AND LTRIM(RTRIM(t.[{oldColumn}])) <> '';
                    """);

                if (!required)
                {
                    continue;
                }

                // 5. A REQUIRED column with nothing to point at — a row whose code column was blank, which
                //    the old string schema allowed and the new one cannot. It falls to the first value on
                //    the list, which for the status list is Draft and for the type list is the first type.
                //    A guess, but a visible one on a record that was already missing the answer, and the
                //    alternative is a deployment that stops here.
                migrationBuilder.Sql(
                    $"""
                    UPDATE t
                    SET t.[{newColumn}] = (
                        SELECT TOP 1 i.[Id]
                        FROM [OptionSetItems] i
                        INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                        WHERE s.[Key] = N'{setKey}' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                          AND (s.[TenantId] = t.[TenantId] OR s.[TenantId] IS NULL)
                        ORDER BY CASE WHEN s.[TenantId] IS NULL THEN 1 ELSE 0 END, i.[SortOrder])
                    FROM [{table}] t
                    WHERE t.[{newColumn}] IS NULL;
                    """);

                migrationBuilder.AlterColumn<Guid>(
                    name: newColumn, table: table, type: "uniqueidentifier", nullable: false,
                    oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            }

            // 6. The references themselves. Restrict throughout: a value a record is recorded against is
            //    not one anybody may delete, and that is now the database's rule rather than a hope.
            foreach (var (table, _, newColumn, _, _) in Columns)
            {
                migrationBuilder.CreateIndex(name: $"IX_{table}_{newColumn}", table: table, column: newColumn);
                migrationBuilder.AddForeignKey(
                    name: $"FK_{table}_OptionSetItems_{newColumn}",
                    table: table,
                    column: newColumn,
                    principalTable: "OptionSetItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            }

            // 7. The composite indexes, back on the id.
            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_CreatedById_StatusId", table: "REMS",
                columns: new[] { "TenantId", "CreatedById", "StatusId" });
            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_OnBehalfOfUserId_StatusId", table: "REMS",
                columns: new[] { "TenantId", "OnBehalfOfUserId", "StatusId" });
            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_StatusId_AdminAssignedToId_CreatedOnUtc", table: "REMS",
                columns: new[] { "TenantId", "StatusId", "AdminAssignedToId", "CreatedOnUtc" });
            migrationBuilder.CreateIndex(
                name: "IX_RemsDepartmentDirector_TenantId_DepartmentId", table: "RemsDepartmentDirector",
                columns: new[] { "TenantId", "DepartmentId" }, unique: true, filter: "[Deleted] = 0");

            // 8. Only now are the code columns expendable.
            foreach (var (table, oldColumn, _, _, _) in Columns)
            {
                migrationBuilder.DropColumn(name: oldColumn, table: table);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_REMS_TenantId_CreatedById_StatusId", table: "REMS");
            migrationBuilder.DropIndex(name: "IX_REMS_TenantId_OnBehalfOfUserId_StatusId", table: "REMS");
            migrationBuilder.DropIndex(name: "IX_REMS_TenantId_StatusId_AdminAssignedToId_CreatedOnUtc", table: "REMS");
            migrationBuilder.DropIndex(name: "IX_RemsDepartmentDirector_TenantId_DepartmentId", table: "RemsDepartmentDirector");

            // The codes come back before the references go, the same way round: add the column, read the
            // value off the item each row points at, then drop the id.
            foreach (var (table, oldColumn, newColumn, _, required) in Columns)
            {
                migrationBuilder.AddColumn<string>(
                    name: oldColumn, table: table, type: "nvarchar(64)", maxLength: 64,
                    nullable: !required, defaultValue: required ? "" : null);

                migrationBuilder.Sql(
                    $"""
                    UPDATE t
                    SET t.[{oldColumn}] = i.[Value]
                    FROM [{table}] t
                    INNER JOIN [OptionSetItems] i ON i.[Id] = t.[{newColumn}];
                    """);

                migrationBuilder.DropForeignKey(name: $"FK_{table}_OptionSetItems_{newColumn}", table: table);
                migrationBuilder.DropIndex(name: $"IX_{table}_{newColumn}", table: table);
                migrationBuilder.DropColumn(name: newColumn, table: table);
            }

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_CreatedById_Status", table: "REMS",
                columns: new[] { "TenantId", "CreatedById", "Status" });
            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_OnBehalfOfUserId_Status", table: "REMS",
                columns: new[] { "TenantId", "OnBehalfOfUserId", "Status" });
            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_Status_AdminAssignedToId_CreatedOnUtc", table: "REMS",
                columns: new[] { "TenantId", "Status", "AdminAssignedToId", "CreatedOnUtc" });
            migrationBuilder.CreateIndex(
                name: "IX_RemsDepartmentDirector_TenantId_Department", table: "RemsDepartmentDirector",
                columns: new[] { "TenantId", "Department" }, unique: true, filter: "[Deleted] = 0");
        }
    }
}
