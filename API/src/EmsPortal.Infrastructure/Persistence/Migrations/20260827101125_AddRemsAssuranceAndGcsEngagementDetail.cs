using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes the engagement setup ask a different set of questions per department (WO-REMS Phase 17).
    /// <para>
    /// Schema: the Assurance fee on the engagement itself, the client's fiscal year end and administrative
    /// fees on the attest detail Assurance shares with Audit, the two tax due dates as real columns now
    /// that they are editable rather than only computed, and the GCS purchase order on the government
    /// detail — the same row that already carries the PO dates a government client answers for at intake,
    /// because it is the same purchase order.
    /// </para>
    /// <para>
    /// Data: the new <c>assurance</c> Department value and the whole new <c>REMS.PersonnelLevel</c> list.
    /// Adding either to <c>DefaultOptionSets</c> alone reaches nobody already running —
    /// <c>TenantOptionSetSeeder</c> runs once, at tenant creation — so both are inserted here into every
    /// existing copy: the platform-standard list (TenantId IS NULL) and each tenant's own. Everything is
    /// guarded on the value / key so re-running adds nothing twice and a tenant who has already made their
    /// own keeps theirs, and <c>UpdatedById</c> is left NULL because the platform added these, not a user
    /// — which is what makes <see cref="Down"/> safe: it withdraws only what nobody has since edited.
    /// </para>
    /// </summary>
    public partial class AddRemsAssuranceAndGcsEngagementDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FirstExtensionDueDate",
                table: "REMSEngagementTaxDetail",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OriginalDueDate",
                table: "REMSEngagementTaxDetail",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillRatePerHour",
                table: "REMSEngagementGovernmentDetail",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonnelLevel",
                table: "REMSEngagementGovernmentDetail",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseOrderAmount",
                table: "REMSEngagementGovernmentDetail",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseOrderMediaId",
                table: "REMSEngagementGovernmentDetail",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseOrderNumber",
                table: "REMSEngagementGovernmentDetail",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdminFeesAmount",
                table: "REMSEngagementAuditDetail",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminFeesApply",
                table: "REMSEngagementAuditDetail",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClientFiscalYearEnd",
                table: "REMSEngagementAuditDetail",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EngagementFee",
                table: "REMSEngagement",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementGovernmentDetail_PurchaseOrderMediaId",
                table: "REMSEngagementGovernmentDetail",
                column: "PurchaseOrderMediaId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_REMSEngagementGovernmentDetail_PoAmounts",
                table: "REMSEngagementGovernmentDetail",
                sql: "([PurchaseOrderAmount] IS NULL OR [PurchaseOrderAmount] >= 0) AND ([BillRatePerHour] IS NULL OR [BillRatePerHour] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_REMSEngagementAuditDetail_AdminFeesAmount",
                table: "REMSEngagementAuditDetail",
                sql: "[AdminFeesAmount] IS NULL OR [AdminFeesAmount] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_REMSEngagementGovernmentDetail_Media_PurchaseOrderMediaId",
                table: "REMSEngagementGovernmentDetail",
                column: "PurchaseOrderMediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ---- The Assurance department ----
            // Slotted at 5, in front of Admin, which is the firm's own internal work and belongs after the
            // client-facing departments. Admin is bumped only where it is still the row the seeder wrote:
            // a tenant who has re-ordered their own list has said where they want it.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[SortOrder] = 6, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Department' AND s.[Deleted] = 0
                  AND i.[Value] = N'admin' AND i.[Deleted] = 0
                  AND i.[SortOrder] = 5 AND i.[UpdatedById] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [Description], [SortOrder],
                     [IsDefault], [IsActive], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], N'assurance', N'Assurance', NULL, 5, 0, 1,
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.Department'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = N'assurance' AND i.[Deleted] = 0);
                """);

            // ---- The Personnel Level list ----
            // A whole new list, so it needs its OptionSets row per scope before its items. The scopes are
            // taken from REMS.Department: whoever holds that list is running REMS and needs this one.
            // IsSystem is copied from it rather than assumed, so the platform-standard row stays read-only
            // and each tenant's copy stays theirs to edit.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSets]
                    ([Id], [TenantId], [EntityType], [Key], [Name], [ParentSetId], [ItemSortMode],
                     [IsSystem], [IsActive], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), d.[TenantId], d.[EntityType], N'REMS.PersonnelLevel', N'REMS Personnel Level',
                       NULL, N'Custom', d.[IsSystem], 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] d
                WHERE d.[Key] = N'REMS.Department'
                  AND d.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSets] p
                      WHERE p.[Key] = N'REMS.PersonnelLevel' AND p.[Deleted] = 0
                        AND p.[EntityType] = d.[EntityType]
                        AND ((p.[TenantId] IS NULL AND d.[TenantId] IS NULL) OR p.[TenantId] = d.[TenantId]));
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [Value], [Label], [Description], [SortOrder],
                     [IsDefault], [IsActive], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), s.[Id], s.[TenantId], v.[Value], v.[Label], NULL, v.[SortOrder], 0, 1,
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] s
                CROSS JOIN (VALUES
                    (N'principal',             N'Principal',                          1),
                    (N'senior_consultant',     N'Senior Consultant',                  2),
                    (N'consultant',            N'Consultant',                         3),
                    (N'junior_consultant',     N'Junior Consultant',                  4),
                    (N'project_analyst',       N'Project Analyst',                    5),
                    (N'program_admin_support', N'Program and Administrative Support',  6)
                ) AS v([Value], [Label], [SortOrder])
                WHERE s.[Key] = N'REMS.PersonnelLevel'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Value] = v.[Value] AND i.[Deleted] = 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Soft-deleted, like every other row here, and only where nobody has since edited it. An
            // engagement already filed under `assurance` or holding a personnel level keeps its value: a
            // code the picker no longer offers is still a code the row holds.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Deleted] = 0 AND i.[Deleted] = 0 AND i.[UpdatedById] IS NULL
                  AND ((s.[Key] = N'REMS.Department' AND i.[Value] = N'assurance')
                    OR  s.[Key] = N'REMS.PersonnelLevel');
                """);

            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.[Deleted] = 1, s.[DeletedOnUtc] = SYSUTCDATETIME(), s.[IsActive] = 0,
                    s.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.PersonnelLevel' AND s.[Deleted] = 0 AND s.[UpdatedById] IS NULL;
                """);

            // Admin goes back to where the seeder had it, and only if it is still where this migration put it.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[SortOrder] = 5, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Department' AND s.[Deleted] = 0
                  AND i.[Value] = N'admin' AND i.[Deleted] = 0
                  AND i.[SortOrder] = 6 AND i.[UpdatedById] IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_REMSEngagementGovernmentDetail_Media_PurchaseOrderMediaId",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropIndex(
                name: "IX_REMSEngagementGovernmentDetail_PurchaseOrderMediaId",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropCheckConstraint(
                name: "CK_REMSEngagementGovernmentDetail_PoAmounts",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropCheckConstraint(
                name: "CK_REMSEngagementAuditDetail_AdminFeesAmount",
                table: "REMSEngagementAuditDetail");

            migrationBuilder.DropColumn(
                name: "FirstExtensionDueDate",
                table: "REMSEngagementTaxDetail");

            migrationBuilder.DropColumn(
                name: "OriginalDueDate",
                table: "REMSEngagementTaxDetail");

            migrationBuilder.DropColumn(
                name: "BillRatePerHour",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropColumn(
                name: "PersonnelLevel",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderAmount",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderMediaId",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderNumber",
                table: "REMSEngagementGovernmentDetail");

            migrationBuilder.DropColumn(
                name: "AdminFeesAmount",
                table: "REMSEngagementAuditDetail");

            migrationBuilder.DropColumn(
                name: "AdminFeesApply",
                table: "REMSEngagementAuditDetail");

            migrationBuilder.DropColumn(
                name: "ClientFiscalYearEnd",
                table: "REMSEngagementAuditDetail");

            migrationBuilder.DropColumn(
                name: "EngagementFee",
                table: "REMSEngagement");
        }
    }
}
