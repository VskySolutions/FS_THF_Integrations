using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every EXISTING copy of REMS.IndustryGroup — the list shown as "Entity Type" — the badge
    /// colours it was seeded without.
    /// <para>
    /// The list was a word in a picker until the Related Entities board started showing it on every row.
    /// There it is a CATEGORY worth seeing at a glance: it is what decided which question the client's
    /// intake asked them — an individual's "Spouse &amp; More Individuals", or everybody else's "Other
    /// Entities" — and therefore what kind of related clients the row holds. A colourless badge would
    /// render neutral grey on every row and say nothing.
    /// </para>
    /// <para>
    /// Six distinct hues rather than a ramp, because these are categories and not stages: Commercial is
    /// not further along than Insurance. Verbatim from <c>DefaultOptionSets</c>, which is what a tenant
    /// created tomorrow gets.
    /// </para>
    /// <para>
    /// A migration is needed because <c>TenantOptionSetSeeder</c> is idempotent per LIST: a tenant that
    /// already holds REMS.IndustryGroup is left exactly as they edited it, so colouring the seed alone
    /// would reach only tenants created afterwards.
    /// </para>
    /// <para>
    /// NOTHING IS OVERWRITTEN. Only rows whose <c>BackgroundColor</c> is still null are touched — this is
    /// filling a blank, not replacing a decision — so a firm that has already picked a colour for an
    /// entity type keeps it, and a value they added themselves is left alone entirely.
    /// </para>
    /// </summary>
    public partial class ColourRemsEntityTypeOptions : Migration
    {
        /// <summary>The six seeded entity types and their badge backgrounds, as a SQL VALUES table.</summary>
        private const string Colours =
            """
            (N'individual',     N'#00897b'),
            (N'not_for_profit', N'#673ab7'),
            (N'insurance',      N'#1f6478'),
            (N'commercial',     N'#0277bd'),
            (N'government',     N'#546e7a'),
            (N'trust_estate',   N'#6d4c41')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[BackgroundColor] = v.[Bg],
                    i.[TextColor] = N'#ffffff',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {Colours}
                ) AS v([Value], [Bg]) ON v.[Value] = i.[Value]
                WHERE s.[Key] = N'REMS.IndustryGroup'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[BackgroundColor] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cleared only where the row still holds exactly what Up wrote. A firm that has recoloured one
            // since has made that colour theirs, and rolling a migration back is not a reason to take it
            // away — the badge simply falls back to neutral grey for the ones this put back to null.
            migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[BackgroundColor] = NULL,
                    i.[TextColor] = NULL,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {Colours}
                ) AS v([Value], [Bg]) ON v.[Value] = i.[Value]
                WHERE s.[Key] = N'REMS.IndustryGroup'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[BackgroundColor] = v.[Bg]
                  AND i.[TextColor] = N'#ffffff';
                """);
        }
    }
}
