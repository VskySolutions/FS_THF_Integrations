using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives <c>Persons</c> a corporate name, and moves every REMS client's details onto the Person the
    /// request already points at.
    ///
    /// <para>
    /// THE FIRST HALF OF A MOVE. A REMS request has carried its own copy of the client — the name, the
    /// generational suffix, the email and the mobile — beside a <c>ClientPersonId</c> pointing at the
    /// Person record for the same client. Two places holding one fact is one place too many: editing the
    /// person left the request saying something else, and the request's list, its emails and its intake
    /// link each read whichever copy they happened to reach.
    /// </para>
    /// <para>
    /// This migration only ADDS and FILLS. The four REMS columns are still there and still written when
    /// it finishes, so an API of either version runs against this schema. The drop is a later migration,
    /// once every read has moved across — expand, backfill, switch, contract, in that order, because the
    /// alternative is a schema change that takes a client's only email address with it.
    /// </para>
    /// <para>
    /// Three things happen, in order:
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     Every request with NO Person gets one, minted from its own columns. These are requests written
    ///     before <c>ClientPersonId</c> existed and never saved since; without this they would be the rows
    ///     that lose their client when the columns go.
    ///   </item>
    ///   <item>
    ///     A client that is not an INDIVIDUAL has its name moved into <c>CorporateName</c>, and the
    ///     first/last split that was guessed for it is cleared. The entity type comes from the request's
    ///     own intake form; a request with no form yet is treated as a person, which is what the guess
    ///     already assumed.
    ///   </item>
    ///   <item>
    ///     Any Person field still BLANK is filled from the request. Blank only — a Person that already
    ///     carries an email is the record somebody has maintained, and the request's copy is the stale
    ///     one by definition.
    ///   </item>
    /// </list>
    /// </summary>
    public partial class AddPersonCorporateName : Migration
    {
        /// <summary>
        /// EntityType.Client. Written out because a migration cannot reference the enum — and it must not,
        /// since a renumbering later would silently re-point rows this wrote.
        /// </summary>
        private const int ClientEntityType = 16;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorporateName",
                table: "Persons",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // 1. A Person for every request that has none. The name is split the way the application
            //    splits it (first word given, the rest family), so a row minted here and a row minted by
            //    the API land identically. PersonCode matches the API's own shape and is unique by
            //    construction.
            //
            //    No temp table and no OUTPUT clause: the minted row already points back at the request it
            //    came from (SourceEntityType = Client, SourceEntityId = the REMS id), which is the link
            //    the next statement joins on. A #temp would have had to survive between two batches EF is
            //    free to send separately.
            migrationBuilder.Sql(
                $"""
                INSERT INTO [Persons]
                    ([Id], [TenantId], [PersonCode], [SourceEntityType], [SourceEntityId],
                     [FirstName], [MiddleName], [LastName], [Suffix], [DisplayName],
                     [PrimaryEmail], [MobileNumber], [IsActive], [LastProfileUpdatedOn],
                     [CreatedOnUtc], [UpdatedOnUtc], [Deleted])
                SELECT
                    NEWID(), r.[TenantId],
                    'PER-' + UPPER(LEFT(REPLACE(CAST(NEWID() AS nvarchar(36)), '-', ''), 10)),
                    {ClientEntityType}, r.[Id],
                    LEFT(LTRIM(RTRIM(r.[RequestedClientName])),
                         CHARINDEX(' ', LTRIM(RTRIM(r.[RequestedClientName])) + ' ') - 1),
                    NULL,
                    LTRIM(SUBSTRING(LTRIM(RTRIM(r.[RequestedClientName])),
                          CHARINDEX(' ', LTRIM(RTRIM(r.[RequestedClientName])) + ' '), 4000)),
                    NULLIF(LTRIM(RTRIM(ISNULL(r.[ClientNameSuffix], ''))), ''),
                    LTRIM(RTRIM(r.[RequestedClientName]
                        + CASE WHEN LTRIM(RTRIM(ISNULL(r.[ClientNameSuffix], ''))) = '' THEN ''
                               ELSE ' ' + LTRIM(RTRIM(r.[ClientNameSuffix])) END)),
                    NULLIF(LTRIM(RTRIM(ISNULL(r.[CustomerEmail], ''))), ''),
                    NULLIF(LTRIM(RTRIM(ISNULL(r.[CustomerMobileNumber], ''))), ''),
                    1, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM [REMS] r
                WHERE r.[ClientPersonId] IS NULL
                  AND LTRIM(RTRIM(ISNULL(r.[RequestedClientName], ''))) <> ''
                  -- A request may already have an unlinked Person of its own from an older save. Link to
                  -- that one rather than minting a second record for the same client.
                  AND NOT EXISTS (
                      SELECT 1 FROM [Persons] x
                      WHERE x.[Deleted] = 0
                        AND x.[SourceEntityType] = {ClientEntityType}
                        AND x.[SourceEntityId] = r.[Id]);
                """);

            // Point each of those requests at its Person — the one just minted, or the one that was
            // already there unlinked.
            migrationBuilder.Sql(
                $"""
                UPDATE r
                SET r.[ClientPersonId] = p.[Id], r.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [REMS] r
                INNER JOIN [Persons] p
                    ON p.[SourceEntityType] = {ClientEntityType}
                   AND p.[SourceEntityId] = r.[Id]
                   AND p.[Deleted] = 0
                WHERE r.[ClientPersonId] IS NULL;
                """);

            // 2. A client that is not an Individual is an ORGANISATION: its whole name belongs in
            //    CorporateName, and the first/last split guessed for it was never a name. The entity type
            //    is the one on the request's intake form; a request with no form is left as a person.
            migrationBuilder.Sql(
                """
                UPDATE p
                SET p.[CorporateName] = LTRIM(RTRIM(r.[RequestedClientName])),
                    p.[FirstName] = '',
                    p.[LastName] = '',
                    p.[Suffix] = NULL,
                    p.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Persons] p
                INNER JOIN [REMS] r ON r.[ClientPersonId] = p.[Id]
                INNER JOIN [REMSForm] f ON f.[REMSId] = r.[Id] AND f.[Deleted] = 0
                INNER JOIN [OptionSetItems] g ON g.[Id] = f.[IndustryGroupId]
                WHERE p.[Deleted] = 0
                  AND r.[Deleted] = 0
                  AND p.[CorporateName] IS NULL
                  AND g.[Value] <> N'individual'
                  AND LTRIM(RTRIM(ISNULL(r.[RequestedClientName], ''))) <> '';
                """);

            // 3. Fill what is still blank on the Person from the request. BLANK ONLY: a Person that
            //    already carries a value is the record somebody has maintained, and the request's copy is
            //    the stale one by definition.
            migrationBuilder.Sql(
                """
                UPDATE p
                SET p.[PrimaryEmail] = COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(p.[PrimaryEmail], ''))), ''),
                                                NULLIF(LTRIM(RTRIM(ISNULL(r.[CustomerEmail], ''))), '')),
                    p.[MobileNumber] = COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(p.[MobileNumber], ''))), ''),
                                                NULLIF(LTRIM(RTRIM(ISNULL(r.[CustomerMobileNumber], ''))), '')),
                    p.[Suffix] = COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(p.[Suffix], ''))), ''),
                                          NULLIF(LTRIM(RTRIM(ISNULL(r.[ClientNameSuffix], ''))), '')),
                    p.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Persons] p
                INNER JOIN [REMS] r ON r.[ClientPersonId] = p.[Id]
                WHERE p.[Deleted] = 0
                  AND r.[Deleted] = 0
                  AND (
                      LTRIM(RTRIM(ISNULL(p.[PrimaryEmail], ''))) = ''
                   OR LTRIM(RTRIM(ISNULL(p.[MobileNumber], ''))) = ''
                   OR LTRIM(RTRIM(ISNULL(p.[Suffix], ''))) = ''
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the column goes. The Persons this minted and the fields it filled are left standing:
            // the REMS columns they were copied FROM are still there and still authoritative at this
            // point, so nothing is lost — and deleting client records on a rollback would be a far worse
            // outcome than a few Person rows nobody asked for.
            //
            // The one thing that cannot be put back is the first/last split on an organisation, which
            // step 2 cleared. That split was a guess ("Falcon" / "Manufacturing Group") and the request's
            // own RequestedClientName still holds the real name, so re-deriving it is the API's job on
            // the next save rather than this migration's.
            migrationBuilder.DropColumn(
                name: "CorporateName",
                table: "Persons");
        }
    }
}
