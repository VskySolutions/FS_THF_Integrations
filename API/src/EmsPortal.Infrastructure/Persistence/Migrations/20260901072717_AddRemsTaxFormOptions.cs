using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the eleven returns the Tax engagement checklist was missing to every EXISTING copy of
    /// REMS.TaxForm, and puts the whole list in form-number order.
    /// <para>
    /// The list was seeded with five values — 1040, 1120, 1120-S, 1065, 990 — and a Tax engagement that
    /// files a 1041, a 5500, a payroll return or a state return had nowhere on the checklist to say so.
    /// A migration is needed because <c>TenantOptionSetSeeder</c> is idempotent per LIST: a tenant that
    /// already holds REMS.TaxForm is left exactly as they edited it, so adding values to
    /// <c>DefaultOptionSets</c> alone reaches only tenants created afterwards.
    /// </para>
    /// <para>
    /// Nothing is overwritten. A value is inserted only where the list does not already carry that code,
    /// so a firm that added their own "1041" keeps theirs and gets no duplicate. The five original values
    /// are renumbered ONLY where each still sits at the sort order it was seeded at — a firm that has
    /// re-ordered their checklist keeps their arrangement, and the new values simply take their canonical
    /// places among it.
    /// </para>
    /// <para>
    /// The list stays OPEN and its values stay non-system: a firm may add, rename, recolour or remove any
    /// of these. They are referenced by item ID (<c>REMSEngagementTaxForm.TaxFormId</c>), which is what
    /// stops one being deleted out from under an engagement recorded against it — and which is why Down
    /// removes only the rows nothing points at.
    /// </para>
    /// </summary>
    public partial class AddRemsTaxFormOptions : Migration
    {
        /// <summary>
        /// The eleven new returns, as a SQL VALUES table of (Value, Label, SortOrder). Verbatim from the
        /// REMS.TaxForm list in <c>DefaultOptionSets</c>, which is what a tenant created tomorrow gets.
        /// </summary>
        private const string NewValues =
            """
            (N'1040_es',     N'1040-ES — Estimated Tax',                        2),
            (N'1041_trust',  N'1041 — Trust',                                   3),
            (N'1041_estate', N'1041 — Estate',                                  4),
            (N'1120_pc',     N'1120-PC — Property & Casualty Insurance',        7),
            (N'1120_pol',    N'1120-POL — Political Organization',              8),
            (N'990_t',       N'990-T — Exempt Organization Business Income',   11),
            (N'5500',        N'5500 — Employee Benefit Plan',                  12),
            (N'tpp',         N'TPP — Tangible Personal Property',              13),
            (N'payroll',     N'Payroll',                                       14),
            (N'other',       N'Other',                                         15),
            (N'states',      N'States',                                        16)
            """;

        /// <summary>The five originals: (Value, SeededSortOrder, NewSortOrder).</summary>
        private const string Renumbered =
            """
            (N'1040',   1,  1),
            (N'1120',   2,  6),
            (N'1120_s', 3,  9),
            (N'1065',   4,  5),
            (N'990',    5, 10)
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Make room: the originals move to where they fall among the sixteen. Guarded per item on
            //    its ORIGINAL sort order, so this only ever touches a list nobody has re-ordered.
            migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[SortOrder] = v.[NewSort], i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {Renumbered}
                ) AS v([Value], [SeededSort], [NewSort]) ON v.[Value] = i.[Value]
                WHERE s.[Key] = N'REMS.TaxForm'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[SortOrder] = v.[SeededSort];
                """);

            // 2. The eleven that were missing. Skipped per value where the list already has that code, so
            //    a firm that added their own is left alone and never ends up with two.
            migrationBuilder.Sql(
                $"""
                INSERT INTO [OptionSetItems]
                    ([Id], [OptionSetId], [TenantId], [ParentItemId], [Value], [Label], [Description],
                     [SortOrder], [IsDefault], [IsActive], [BackgroundColor], [TextColor], [Icon],
                     [IsSystem], [MetadataJson], [CreatedById], [CreatedOnUtc], [UpdatedById],
                     [UpdatedOnUtc], [Deleted], [DeletedOnUtc])
                SELECT
                    NEWID(), s.[Id], s.[TenantId], NULL, v.[Value], v.[Label], NULL,
                    v.[SortOrder], 0, 1, NULL, NULL, NULL,
                    0, NULL, NULL, SYSUTCDATETIME(), NULL,
                    SYSUTCDATETIME(), 0, NULL
                FROM [OptionSets] s
                CROSS JOIN (VALUES
                {NewValues}
                ) AS v([Value], [Label], [SortOrder])
                WHERE s.[Key] = N'REMS.TaxForm'
                  AND s.[Deleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [OptionSetItems] x
                      WHERE x.[OptionSetId] = s.[Id] AND x.[Deleted] = 0 AND x.[Value] = v.[Value]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removed for real rather than soft-deleted: these were never codes an engagement had to be
            // recorded against. One that HAS been picked since is left standing — a hard delete would fail
            // the foreign key, and a soft delete would strand the engagement's checklist row.
            migrationBuilder.Sql(
                $"""
                DELETE i
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {NewValues}
                ) AS v([Value], [Label], [SortOrder]) ON v.[Value] = i.[Value]
                WHERE s.[Key] = N'REMS.TaxForm'
                  AND NOT EXISTS (
                      SELECT 1 FROM [REMSEngagementTaxForm] f WHERE f.[TaxFormId] = i.[Id]);
                """);

            migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[SortOrder] = v.[SeededSort], i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {Renumbered}
                ) AS v([Value], [SeededSort], [NewSort]) ON v.[Value] = i.[Value]
                WHERE s.[Key] = N'REMS.TaxForm'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[SortOrder] = v.[NewSort];
                """);
        }
    }
}
