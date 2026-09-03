using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Takes the client's own details off <c>REMS</c>. The request keeps <c>ClientPersonId</c> and reads
    /// the name, the generational suffix, the email and the mobile through it.
    ///
    /// <para>
    /// THE LAST STEP OF A MOVE, not a change on its own. <c>AddPersonCorporateName</c> copied every one of
    /// these onto the client's Person and minted a Person for the requests that had none;
    /// <c>AddPersonPartyType</c> and <c>AddPersonClientDisplayName</c> gave that record its shape and its
    /// composed name. Only once every read had been switched across does this run. Expand, backfill,
    /// switch, contract — in that order, because the alternative is a schema change that takes a client's
    /// only email address with it.
    /// </para>
    /// <para>
    /// Two places holding one fact was the problem being solved: editing the person left the request
    /// saying something else, and the request's lists, its emails and its intake link each read whichever
    /// copy they happened to reach. There is one copy now.
    /// </para>
    /// <para>
    /// IT REFUSES TO RUN IF ANYTHING WOULD BE LOST. The two guards below check the exact condition that
    /// makes this destructive — a request still carrying client details that no Person holds — and throw
    /// rather than drop. A migration that silently deletes the only address a client can be reached at is
    /// not one anybody should be able to run by accident, and "the backfill ran" is a claim worth
    /// verifying at the moment it matters rather than trusting.
    /// </para>
    /// </summary>
    public partial class DropRemsClientColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GUARD 1 — a request whose client details live nowhere else. The backfill mints a Person for
            // every request carrying a name, so this can only fire if that migration was skipped, rolled
            // back, or ran before rows that have since been written by an older build.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM [REMS] r
                    WHERE r.[Deleted] = 0
                      AND r.[ClientPersonId] IS NULL
                      AND (LTRIM(RTRIM(ISNULL(r.[RequestedClientName], ''))) <> ''
                        OR LTRIM(RTRIM(ISNULL(r.[CustomerEmail], ''))) <> ''
                        OR LTRIM(RTRIM(ISNULL(r.[CustomerMobileNumber], ''))) <> ''))
                BEGIN
                    THROW 51000, 'DropRemsClientColumns: one or more REMS requests still carry client details with no ClientPersonId. Run AddPersonCorporateName first — dropping these columns now would destroy those clients.', 1;
                END
                """);

            // GUARD 2 — the email specifically. It is the address the client's intake form is sent to and
            // the one field here with no other copy anywhere, so it is checked on its own rather than
            // trusted to the count above.
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1 FROM [REMS] r
                    INNER JOIN [Persons] p ON p.[Id] = r.[ClientPersonId]
                    WHERE r.[Deleted] = 0
                      AND p.[Deleted] = 0
                      AND LTRIM(RTRIM(ISNULL(r.[CustomerEmail], ''))) <> ''
                      AND LTRIM(RTRIM(ISNULL(p.[PrimaryEmail], ''))) = '')
                BEGIN
                    THROW 51001, 'DropRemsClientColumns: one or more REMS requests hold a customer email their client Person does not. Re-run the AddPersonCorporateName backfill — dropping now would lose the address the intake form is sent to.', 1;
                END
                """);

            migrationBuilder.DropColumn(
                name: "ClientNameSuffix",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "CustomerMobileNumber",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "RequestedClientName",
                table: "REMS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientNameSuffix",
                table: "REMS",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                table: "REMS",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerMobileNumber",
                table: "REMS",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedClientName",
                table: "REMS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Filled back in from the client's Person rather than left empty. A rollback puts an older
            // build back in front of these rows, and that build reads these columns — four empty ones
            // would leave every request on screen with no client at all.
            //
            // An organisation's legal name goes into the name column whole, which is where that build
            // expects to find it; the suffix comes back on its own, as it was.
            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.[RequestedClientName] = CASE
                        WHEN p.[PartyType] = 1 THEN LTRIM(RTRIM(ISNULL(p.[CorporateName], '')))
                        ELSE LTRIM(RTRIM(
                            ISNULL(NULLIF(LTRIM(RTRIM(p.[FirstName])), ''), '')
                            + CASE WHEN NULLIF(LTRIM(RTRIM(p.[LastName])), '') IS NULL THEN ''
                                   ELSE ' ' + LTRIM(RTRIM(p.[LastName])) END))
                    END,
                    r.[ClientNameSuffix] = NULLIF(LTRIM(RTRIM(ISNULL(p.[Suffix], ''))), ''),
                    r.[CustomerEmail] = NULLIF(LTRIM(RTRIM(ISNULL(p.[PrimaryEmail], ''))), ''),
                    r.[CustomerMobileNumber] = NULLIF(LTRIM(RTRIM(ISNULL(p.[MobileNumber], ''))), ''),
                    r.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [REMS] r
                INNER JOIN [Persons] p ON p.[Id] = r.[ClientPersonId]
                WHERE r.[Deleted] = 0 AND p.[Deleted] = 0;
                """);
        }
    }
}
