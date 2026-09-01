using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Copies each client's generational particle from the REMS request that minted them onto their own
    /// <c>Person.Suffix</c>, where every client record that already existed has a blank one.
    /// <para>
    /// <c>RemsRequestsController.ResolveClientPersonAsync</c> filed the particle into the person's
    /// DisplayName and nowhere else — the Suffix column was never written. So the client picker, which
    /// reads that column, could only ever offer "John Smith", and the Suffix box beside the search stayed
    /// empty however the request that created him was filled in. Two clients of the same name were
    /// indistinguishable in the one list whose whole job is telling them apart. The controller now writes
    /// the column; this is the same answer for the clients already on file.
    /// </para>
    /// <para>
    /// Read through <c>REMS.ClientPersonId</c>, which is the request's own pointer at the person it
    /// minted. Where several requests name one client and disagree about the particle, the most recently
    /// touched one wins — they cannot all be right, and the latest is the firm's latest word on it.
    /// </para>
    /// <para>
    /// Only BLANK suffixes are written: a particle already on a person was put there by hand and is
    /// theirs. DisplayName is deliberately left alone — it is editable, a person may have been renamed
    /// since, and the picker does not read it.
    /// </para>
    /// </summary>
    public partial class BackfillClientPersonSuffix : Migration
    {
        /// <summary>The request's own particle for a person, newest first. Blank suffixes never match.</summary>
        private const string LatestSuffixForPerson =
            """
            SELECT TOP 1 r.[ClientNameSuffix]
            FROM [REMS] r
            WHERE r.[ClientPersonId] = p.[Id]
              AND r.[Deleted] = 0
              AND r.[ClientNameSuffix] IS NOT NULL
              AND LTRIM(RTRIM(r.[ClientNameSuffix])) <> N''
            ORDER BY r.[UpdatedOnUtc] DESC, r.[CreatedOnUtc] DESC
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                $"""
                UPDATE p
                SET p.[Suffix] = LTRIM(RTRIM(v.[ClientNameSuffix])), p.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Persons] p
                CROSS APPLY (
                {LatestSuffixForPerson}
                ) AS v
                WHERE p.[Deleted] = 0
                  AND (p.[Suffix] IS NULL OR LTRIM(RTRIM(p.[Suffix])) = N'');
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            // Cleared only where it still holds exactly what Up wrote — anything edited since is somebody's
            // own answer, and there is no record of which suffixes were blank before this ran.
            => migrationBuilder.Sql(
                $"""
                UPDATE p
                SET p.[Suffix] = NULL, p.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [Persons] p
                CROSS APPLY (
                {LatestSuffixForPerson}
                ) AS v
                WHERE p.[Deleted] = 0
                  AND p.[Suffix] = LTRIM(RTRIM(v.[ClientNameSuffix]));
                """);
    }
}
