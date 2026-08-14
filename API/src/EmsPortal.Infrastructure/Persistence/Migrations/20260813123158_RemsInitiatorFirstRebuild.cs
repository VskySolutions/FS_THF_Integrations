using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemsInitiatorFirstRebuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Admin Pool is gone, and with it the status that named it. A request sitting in the pool
            // was one its initiator had finished with but nobody had picked up — under the new flow that
            // is simply a draft, waiting for its initiator to send the intake link.
            migrationBuilder.Sql(
                "UPDATE [REMS] SET [Status] = 'draft' WHERE [Status] = 'submitted';");

            migrationBuilder.DropForeignKey(
                name: "FK_REMSClient_Addresses_BillingAddressId",
                table: "REMSClient");

            migrationBuilder.DropForeignKey(
                name: "FK_REMSEngagement_REMSEntity_REMSEntityId",
                table: "REMSEngagement");

            migrationBuilder.DropIndex(
                name: "IX_REMSClient_BillingAddressId",
                table: "REMSClient");

            migrationBuilder.DropColumn(
                name: "BillingAddressId",
                table: "REMSClient");

            migrationBuilder.RenameColumn(
                name: "REMSEntityId",
                table: "REMSEngagement",
                newName: "REMSId");

            // ---- Translate the renamed values from entity ids to request ids ----
            //
            // A rename carries the VALUES across, so every existing row now holds an ENTITY id in a column
            // that has to hold a REQUEST id. Left alone that is exactly what the new foreign key rejects:
            // "The ALTER TABLE statement conflicted with the FOREIGN KEY constraint".
            //
            // The translation is available — entity → client → request — so the engagements are carried
            // over rather than thrown away, keeping their approval rounds, marketing and commission with
            // them. An approved engagement is a real record of a decision and should survive a reshape of
            // where it is filed.
            migrationBuilder.Sql(@"
                UPDATE e
                SET e.[REMSId] = c.[REMSId]
                FROM [REMSEngagement] e
                INNER JOIN [REMSEntity] n ON n.[Id] = e.[REMSId]
                INNER JOIN [REMSClient] c ON c.[Id] = n.[REMSClientId];
            ");

            // Two cases the translation cannot carry, both removed with their dependents so the foreign
            // key and the one-per-request unique index can be created.
            //
            //  1. An engagement whose entity or client no longer resolves — its REMSId is still an entity
            //     id and points at no request at all.
            //  2. A client that had SEVERAL entities with engagements: they all collapse onto the one
            //     request, and only one may survive. The oldest is kept, being the main entity's in every
            //     realistic case, and the rest go. Nothing can merge them — they are different engagements
            //     that now have nowhere separate to live.
            migrationBuilder.Sql(@"
                CREATE TABLE #Doomed ([Id] UNIQUEIDENTIFIER PRIMARY KEY);

                INSERT INTO #Doomed ([Id])
                SELECT e.[Id] FROM [REMSEngagement] e
                WHERE NOT EXISTS (SELECT 1 FROM [REMS] r WHERE r.[Id] = e.[REMSId]);

                INSERT INTO #Doomed ([Id])
                SELECT [Id] FROM (
                    SELECT e.[Id],
                           ROW_NUMBER() OVER (PARTITION BY e.[REMSId] ORDER BY e.[CreatedOnUtc], e.[Id]) AS rn
                    FROM [REMSEngagement] e
                    WHERE e.[Deleted] = 0
                      AND EXISTS (SELECT 1 FROM [REMS] r WHERE r.[Id] = e.[REMSId])
                ) ranked
                WHERE ranked.rn > 1;

                DELETE FROM [REMSApprovalChecklistItem]
                WHERE [REMSApprovalTaskId] IN (
                    SELECT t.[Id] FROM [REMSApprovalTask] t
                    INNER JOIN [REMSApprovalRound] rd ON rd.[Id] = t.[REMSApprovalRoundId]
                    WHERE rd.[REMSEngagementId] IN (SELECT [Id] FROM #Doomed));
                DELETE FROM [REMSApprovalTask]
                WHERE [REMSApprovalRoundId] IN (
                    SELECT [Id] FROM [REMSApprovalRound] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed));
                DELETE FROM [REMSApprovalRound] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagementTaxForm]
                WHERE [REMSEngagementTaxDetailId] IN (
                    SELECT [Id] FROM [REMSEngagementTaxDetail] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed));
                DELETE FROM [REMSEngagementTaxDetail] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagementGovernmentDetail] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagementAuditDetail] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagementMarketingMethod] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagementCommissionSplit] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagementApprover] WHERE [REMSEngagementId] IN (SELECT [Id] FROM #Doomed);
                DELETE FROM [REMSEngagement] WHERE [Id] IN (SELECT [Id] FROM #Doomed);

                DROP TABLE #Doomed;
            ");

            migrationBuilder.RenameIndex(
                name: "IX_REMSEngagement_TenantId_REMSEntityId",
                table: "REMSEngagement",
                newName: "IX_REMSEngagement_TenantId_REMSId");

            migrationBuilder.RenameIndex(
                name: "IX_REMSEngagement_REMSEntityId",
                table: "REMSEngagement",
                newName: "IX_REMSEngagement_REMSId");

            migrationBuilder.AddColumn<string>(
                name: "BillingPeriod",
                table: "REMSEngagement",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfBills",
                table: "REMSEngagement",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "REMS",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "REMSAdditionalEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedREMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSAdditionalEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSAdditionalEntity_REMS_REMSId",
                        column: x => x.REMSId,
                        principalTable: "REMS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSAdditionalEntity_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REMSSendBack",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResolvedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSSendBack", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSSendBack_REMS_REMSId",
                        column: x => x.REMSId,
                        principalTable: "REMS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSSendBack_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_REMSEngagement_NumberOfBills",
                table: "REMSEngagement",
                sql: "[NumberOfBills] IS NULL OR [NumberOfBills] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalEntity_REMSId",
                table: "REMSAdditionalEntity",
                column: "REMSId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalEntity_TenantId",
                table: "REMSAdditionalEntity",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalEntity_TenantId_REMSId_CreatedREMSId",
                table: "REMSAdditionalEntity",
                columns: new[] { "TenantId", "REMSId", "CreatedREMSId" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalEntity_TenantId_REMSId_SourceKey",
                table: "REMSAdditionalEntity",
                columns: new[] { "TenantId", "REMSId", "SourceKey" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSSendBack_REMSId",
                table: "REMSSendBack",
                column: "REMSId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSSendBack_TenantId",
                table: "REMSSendBack",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSSendBack_TenantId_REMSId",
                table: "REMSSendBack",
                columns: new[] { "TenantId", "REMSId" },
                unique: true,
                filter: "[Deleted] = 0 AND [ResolvedOnUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_REMSSendBack_TenantId_REMSId_CreatedOnUtc",
                table: "REMSSendBack",
                columns: new[] { "TenantId", "REMSId", "CreatedOnUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_REMSEngagement_REMS_REMSId",
                table: "REMSEngagement",
                column: "REMSId",
                principalTable: "REMS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ---- Give every remaining request the engagement it now assumes ----
            //
            // A request carries exactly one engagement, created the moment the request is
            // (RemsRequestsController.Create). Requests that predate this — and any whose engagement the
            // translation above could not carry — would otherwise open the form on "no engagement yet"
            // with nothing able to produce one, because editing a request does not create it.
            //
            // Empty of setup on purpose: the department, team, fee and billing schedule are the
            // initiator's to fill in, and inventing values here would look like somebody already had.
            // Soft-deleted requests are skipped: the unique index only covers live rows, and a deleted
            // request has nothing to set up.
            migrationBuilder.Sql(@"
                INSERT INTO [REMSEngagement] ([Id], [TenantId], [REMSId], [Status], [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT NEWID(), r.[TenantId], r.[Id], 'Draft', SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [REMS] r
                WHERE r.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [REMSEngagement] e
                      WHERE e.[REMSId] = r.[Id] AND e.[Deleted] = 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_REMSEngagement_REMS_REMSId",
                table: "REMSEngagement");

            migrationBuilder.DropTable(
                name: "REMSAdditionalEntity");

            migrationBuilder.DropTable(
                name: "REMSSendBack");

            migrationBuilder.DropCheckConstraint(
                name: "CK_REMSEngagement_NumberOfBills",
                table: "REMSEngagement");

            migrationBuilder.DropColumn(
                name: "BillingPeriod",
                table: "REMSEngagement");

            migrationBuilder.DropColumn(
                name: "NumberOfBills",
                table: "REMSEngagement");

            migrationBuilder.RenameColumn(
                name: "REMSId",
                table: "REMSEngagement",
                newName: "REMSEntityId");

            migrationBuilder.RenameIndex(
                name: "IX_REMSEngagement_TenantId_REMSId",
                table: "REMSEngagement",
                newName: "IX_REMSEngagement_TenantId_REMSEntityId");

            migrationBuilder.RenameIndex(
                name: "IX_REMSEngagement_REMSId",
                table: "REMSEngagement",
                newName: "IX_REMSEngagement_REMSEntityId");

            migrationBuilder.AddColumn<Guid>(
                name: "BillingAddressId",
                table: "REMSClient",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "REMS",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMSClient_BillingAddressId",
                table: "REMSClient",
                column: "BillingAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_REMSClient_Addresses_BillingAddressId",
                table: "REMSClient",
                column: "BillingAddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_REMSEngagement_REMSEntity_REMSEntityId",
                table: "REMSEngagement",
                column: "REMSEntityId",
                principalTable: "REMSEntity",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
