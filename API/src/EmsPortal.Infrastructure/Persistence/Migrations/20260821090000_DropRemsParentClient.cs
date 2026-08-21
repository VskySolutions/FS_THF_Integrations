using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops <c>REMS.ParentClientReferenceId</c> and <c>REMS.ParentClientName</c> — the Parent Client
    /// field — and takes the sentence about it back out of the "New Engagement, Existing Client"
    /// description.
    /// <para>
    /// The field recorded that one client was a subsidiary or child of another, and it was asked of the
    /// partner at intake. REMS records the engagement being raised and the client it is for; how that
    /// client relates to another company on THF's books is not a fact intake was ever in a position to
    /// establish, and nothing downstream — routing, approval, the engagement itself — ever read it. It was
    /// the last trace of the retired "Subsidiary / Child of Existing Client" type
    /// (<c>RetireRemsSubsidiaryType</c>), which absorbed the answer into "New Engagement, Existing Client"
    /// and moved the field onto it. Both are now gone; there is no parent-child relationship in REMS.
    /// </para>
    /// <para>
    /// DESTRUCTIVE, and not recoverable: every parent on file is deleted with the columns. <c>Down</c> puts
    /// them back so the schema round-trips, but they come back empty — there is nowhere the old values are
    /// kept.
    /// </para>
    /// </summary>
    public partial class DropRemsParentClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentClientReferenceId",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "ParentClientName",
                table: "REMS");

            // The surviving type's tooltip told the partner to name the parent. RetireRemsSubsidiaryType
            // added that sentence when it moved the field onto this answer; with the field gone it points
            // at a box that is no longer on the form, so it goes back out — in the platform standard list
            // and in every tenant's own copy.
            //
            // Guarded on the seeded text, per the convention: a tenant who rewrote this description has
            // made it theirs and keeps what they wrote.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client.',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client'
                  AND i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client. Name the parent client if this one is a subsidiary or child of it.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The columns return empty — the parents are gone. Restoring the description too, so a
            // rolled-back deployment asks for the field it has put back.
            migrationBuilder.AddColumn<Guid>(
                name: "ParentClientReferenceId",
                table: "REMS",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentClientName",
                table: "REMS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client. Name the parent client if this one is a subsidiary or child of it.',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client'
                  AND i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client.';
                """);
        }
    }
}
