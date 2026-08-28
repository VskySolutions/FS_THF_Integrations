using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rewords the "Not started" value on REMS.FormStatus, which now covers a form that has been prepared
    /// as well as one that was never raised.
    /// <para>
    /// The dashboard's EMS State column used to read "Saved" for a request an admin had merely opened and
    /// filled the first tab of. That is not a step anybody takes: the request page saves itself, so the
    /// form row is minted the moment a CSE and an entity type are both chosen, and the column changed
    /// under the admin while they were still typing. Both Draft and Saved mean the client has not been
    /// written to, so <c>RemsWorkspaceMapper.FormState</c> now reports them as Not started and the column
    /// moves only when the form is actually sent — which is what it read before the request page began
    /// saving itself.
    /// </para>
    /// <para>
    /// A migration is needed because both seeders are idempotent per LIST: a platform row or tenant copy
    /// that already holds REMS.FormStatus is left exactly as it is, so editing
    /// <c>DefaultOptionSets</c> alone reaches nobody already running.
    /// </para>
    /// <para>
    /// Matched on the ORIGINAL seeded text, so a tenant who has reworded this value keeps their wording.
    /// Draft and Saved keep their own rows and descriptions: <c>REMSForm.Status</c> still stores them and
    /// the send guard still reads them — they are simply no longer what the dashboard shows.
    /// </para>
    /// </summary>
    public partial class RemsFormStatusNotStartedWording : Migration
    {
        private const string OldText = "No intake form has been raised for this request yet.";
        private const string NewText =
            "The intake form has not gone out to the client yet \u2014 whether or not staff have prepared it.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[Description] = N'{NewText}', i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.FormStatus' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'NotStarted' AND i.[Description] = N'{OldText}';
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[Description] = N'{OldText}', i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.FormStatus' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'NotStarted' AND i.[Description] = N'{NewText}';
                """);
    }
}
