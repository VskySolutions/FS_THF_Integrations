using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves the generational particle from the FRONT of each person's <c>DisplayName</c> to the end of
    /// it — "Jr. Jane Smith" becomes "Jane Smith Jr.".
    /// <para>
    /// The platform now reads a name the way a name is written, with the particle after the surname; every
    /// box that asks for one sits to the right of Last Name, and every surface that shows one draws it
    /// there. The code that WRITES this column was changed with it (<c>RemsRolePayload.NameWithSuffix</c>
    /// and <c>REMS.ClientDisplayName</c>), so every person minted from here on is stored the new way. This
    /// is the same answer for the people already on file — without it a REMS contact captured last week
    /// reads "Jr. Jane Smith" in the same list as one captured today reading "Jane Smith Jr.".
    /// </para>
    /// <para>
    /// The guard is exact: a row is rewritten only where <c>DisplayName</c> literally begins with that
    /// person's own <c>Suffix</c> followed by a space, and only where something is left after removing it.
    /// A name that already trails its particle is untouched, and so is one that has been edited by hand
    /// into some other shape — this moves a particle, it does not reformat names.
    /// </para>
    /// <para>
    /// <c>FirstName</c> / <c>LastName</c> are deliberately not read or written. The particle was never in
    /// them: it is what a person is FILED under that those two hold, and "Smith Jr." in a surname column
    /// is somebody nobody finds by searching for their name.
    /// </para>
    /// </summary>
    public partial class MovePersonDisplayNameSuffixToEnd : Migration
    {
        /// <summary>
        /// The particle, and the name with it taken off whichever end it is on. Written once and used by
        /// both directions, so <c>Down</c> cannot drift from <c>Up</c>.
        /// <para>
        /// <c>LEFT(...) = suffix + ' '</c> rather than a LIKE: the suffix is free text, and a client who
        /// typed one containing <c>%</c> or <c>[</c> would otherwise match rows that have nothing to do
        /// with them.
        /// </para>
        /// </summary>
        private const string Parts =
            """
            CROSS APPLY (SELECT LTRIM(RTRIM(p.[Suffix])) AS [Particle]) s
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                $"""
                UPDATE p
                SET p.[DisplayName] = n.[Name] + N' ' + s.[Particle], p.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Persons] p
                {Parts}
                CROSS APPLY (
                    SELECT LTRIM(RTRIM(SUBSTRING(p.[DisplayName], LEN(s.[Particle]) + 2, 200))) AS [Name]
                ) n
                WHERE p.[Deleted] = 0
                  AND p.[DisplayName] IS NOT NULL
                  AND s.[Particle] <> N''
                  AND LEFT(p.[DisplayName], LEN(s.[Particle]) + 1) = s.[Particle] + N' '
                  AND n.[Name] <> N'';
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            // The exact inverse, with the same guard read from the other end. It cannot tell a name this
            // migration moved from one that already trailed its particle before it ran, so rolling back
            // puts the particle in front of both — which is what the platform read as correct at the point
            // this is rolling back to.
            => migrationBuilder.Sql(
                $"""
                UPDATE p
                SET p.[DisplayName] = s.[Particle] + N' ' + n.[Name], p.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Persons] p
                {Parts}
                CROSS APPLY (
                    SELECT LTRIM(RTRIM(LEFT(
                        p.[DisplayName],
                        CASE WHEN LEN(p.[DisplayName]) > LEN(s.[Particle]) + 1
                             THEN LEN(p.[DisplayName]) - LEN(s.[Particle]) - 1
                             ELSE 0 END))) AS [Name]
                ) n
                WHERE p.[Deleted] = 0
                  AND p.[DisplayName] IS NOT NULL
                  AND s.[Particle] <> N''
                  AND RIGHT(p.[DisplayName], LEN(s.[Particle]) + 1) = N' ' + s.[Particle]
                  AND n.[Name] <> N'';
                """);
    }
}
