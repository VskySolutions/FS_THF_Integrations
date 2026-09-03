using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Related Entities list: a hand-set progress status on every client a submitted intake declared
    /// ALONGSIDE the client the request was raised for.
    /// <para>
    /// Two nullable columns rather than one new table, because the rows already exist in two:
    /// <c>REMSAdditionalIndividual</c> holds the other people on an individual's return ("Spouse &amp;
    /// More Individuals"), <c>REMSAdditionalEntity</c> holds the other businesses every other entity type
    /// names ("Other Entities"). They share nothing but the request they hang off — one is about how a
    /// return is filed and who is invoiced for it, the other about a business awaiting an EMS of its own —
    /// so folding them together for the sake of one column would be inventing a table to hold a status.
    /// </para>
    /// <para>
    /// NOTHING IS BACKFILLED and nothing needs to be. Null means nobody has answered for the row yet, which
    /// is exactly what the list's first value says (<c>not_initiated</c>), so every row already on file
    /// reads correctly on the day this runs and only a deliberate change writes anything.
    /// </para>
    /// <para>
    /// The list itself is added per SCOPE, taking its scopes from <c>REMS.Status</c> — whoever holds that
    /// list is running REMS and needs this one — because <c>TenantOptionSetSeeder</c> is idempotent per
    /// list and would otherwise reach only tenants created after today. <c>IsSystem</c> is copied from the
    /// donor so the platform-standard row stays read-only and each tenant's copy stays theirs to edit.
    /// The list is NOT closed: nothing branches on the set of values, so a firm that tracks a fifth
    /// position may add it. Its four seeded values ARE system values — <c>not_initiated</c> is the one the
    /// server writes — so they cannot be deleted or re-coded, while their labels, descriptions, colours
    /// and order stay the firm's.
    /// </para>
    /// </summary>
    public partial class AddRemsRelatedEntityStatus : Migration
    {
        /// <summary>
        /// The four positions, as a SQL VALUES table. Verbatim from the REMS.RelatedEntityStatus list in
        /// <c>DefaultOptionSets</c>, which is what a tenant created tomorrow gets — the colours included,
        /// so a list arrives already looking the way the screen was designed for.
        /// </summary>
        private const string Values =
            """
            (N'not_initiated',   N'Not Initiated',
             N'Nothing has been raised for this related client yet. Every row starts here.',
             1, N'#9e9e9e', N'#ffffff'),
            (N'rems_initiated',  N'REMS Initiated',
             N'A REMS request has been raised for this related client and is being worked.',
             2, N'#00897b', N'#ffffff'),
            (N'pending_approval', N'Pending Approval',
             N'Their request has reached the approvers and is waiting on their decisions.',
             3, N'#ffa000', N'#ffffff'),
            (N'approved',        N'Approved',
             N'Their engagement is approved — the end of the road for this row.',
             4, N'#1f6478', N'#ffffff')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RelatedStatusId",
                table: "REMSAdditionalIndividual",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedStatusId",
                table: "REMSAdditionalEntity",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_RelatedStatusId",
                table: "REMSAdditionalIndividual",
                column: "RelatedStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalEntity_RelatedStatusId",
                table: "REMSAdditionalEntity",
                column: "RelatedStatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_REMSAdditionalEntity_OptionSetItems_RelatedStatusId",
                table: "REMSAdditionalEntity",
                column: "RelatedStatusId",
                principalTable: "OptionSetItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_REMSAdditionalIndividual_OptionSetItems_RelatedStatusId",
                table: "REMSAdditionalIndividual",
                column: "RelatedStatusId",
                principalTable: "OptionSetItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ---- The Related Entity Status list ----
            // One OptionSets row per scope that already runs REMS, then its four values. Skipped per scope
            // where the list is already there, so re-running this — or running it against a tenant created
            // after the seeder learned about the list — changes nothing.
            migrationBuilder.Sql(
                """
                INSERT INTO [OptionSets]
                    ([Id], [TenantId], [EntityType], [Key], [Name], [ParentSetId], [ItemSortMode],
                     [IsSystem], [IsClosed], [IsActive], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), d.[TenantId], d.[EntityType], N'REMS.RelatedEntityStatus',
                       N'REMS Related Entity Status', NULL, N'Custom',
                       d.[IsSystem], 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [OptionSets] d
                WHERE d.[Key] = N'REMS.Status'
                  AND d.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSets] p
                      WHERE p.[Key] = N'REMS.RelatedEntityStatus' AND p.[Deleted] = 0
                        AND p.[EntityType] = d.[EntityType]
                        AND ((p.[TenantId] IS NULL AND d.[TenantId] IS NULL) OR p.[TenantId] = d.[TenantId]));
                """);

            // IsSystem = 1 on the values: `not_initiated` is what the server writes for a row nobody has
            // answered for, so none of the four may be deleted or re-coded. Everything a firm would want to
            // change about them — the label, the description, the colours, the order — stays open.
            migrationBuilder.Sql(
                $"""
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [ParentItemId], [Value], [Label], [Description],
                     [SortOrder], [IsDefault], [IsActive], [BackgroundColor], [TextColor], [Icon],
                     [IsSystem], [MetadataJson], [CreatedById], [CreatedOnUtc], [UpdatedById],
                     [UpdatedOnUtc], [Deleted], [DeletedOnUtc])
                SELECT
                    NEWID(), s.[Id], s.[TenantId], NULL, v.[Value], v.[Label], v.[Description],
                    v.[SortOrder], 0, 1, v.[BackgroundColor], v.[TextColor], NULL,
                    1, NULL, NULL, SYSUTCDATETIME(), NULL,
                    SYSUTCDATETIME(), 0, NULL
                FROM [OptionSets] s
                CROSS JOIN (VALUES
                {Values}
                ) AS v([Value], [Label], [Description], [SortOrder], [BackgroundColor], [TextColor])
                WHERE s.[Key] = N'REMS.RelatedEntityStatus'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] i
                      WHERE i.[OptionSetId] = s.[Id] AND i.[Deleted] = 0 AND i.[Value] = v.[Value]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The list goes before the columns that reference it: the foreign keys below are dropped after
            // this, so a hard DELETE here would fail on any row already recorded against a value. Soft-
            // deleted like every other option-set rollback in this folder, and only where nobody has edited
            // the row since — a firm that has relabelled or recoloured one of these has made it theirs.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.RelatedEntityStatus'
                  AND s.[Deleted] = 0 AND i.[Deleted] = 0 AND i.[UpdatedById] IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.[Deleted] = 1, s.[DeletedOnUtc] = SYSUTCDATETIME(), s.[IsActive] = 0,
                    s.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.RelatedEntityStatus'
                  AND s.[Deleted] = 0 AND s.[UpdatedById] IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_REMSAdditionalEntity_OptionSetItems_RelatedStatusId",
                table: "REMSAdditionalEntity");

            migrationBuilder.DropForeignKey(
                name: "FK_REMSAdditionalIndividual_OptionSetItems_RelatedStatusId",
                table: "REMSAdditionalIndividual");

            migrationBuilder.DropIndex(
                name: "IX_REMSAdditionalIndividual_RelatedStatusId",
                table: "REMSAdditionalIndividual");

            migrationBuilder.DropIndex(
                name: "IX_REMSAdditionalEntity_RelatedStatusId",
                table: "REMSAdditionalEntity");

            migrationBuilder.DropColumn(
                name: "RelatedStatusId",
                table: "REMSAdditionalIndividual");

            migrationBuilder.DropColumn(
                name: "RelatedStatusId",
                table: "REMSAdditionalEntity");
        }
    }
}
