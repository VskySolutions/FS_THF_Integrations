using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every option-set value somewhere to say what it MEANS, surfaced as its tooltip wherever the
    /// value is offered or displayed — a list whose labels look alike can now explain itself where it is
    /// used rather than in a manual nobody opens.
    /// </summary>
    public partial class AddOptionSetItemDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "OptionSetItems",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            // Fill in REMS.Type, the list this was built for: three answers to "how does this referral
            // relate to THF's records?" that read almost alike to anyone who has not onboarded a client,
            // where the difference decides who gets billed. Across the platform standard list AND every
            // tenant copy.
            //
            // Only where the description is still NULL, so a tenant who has already written their own is
            // never overwritten. Matched on VALUE alone, not on the label — a tenant may have renamed
            // these, and the explanation belongs to the value either way.
            //
            // REMS.ReferralSource needs nothing here: it is a new list, and BootstrapSeeder adds any
            // missing standard list on startup, descriptions included. Tenants without their own copy
            // resolve that standard one.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Description] = v.[Description], i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                CROSS JOIN (VALUES
                    (N'brand_new_client',
                     N'The client/company is working with THF for the first time. No prior record exists in the system.'),
                    (N'existing_client',
                     N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client.'),
                    (N'subsidiary_child_of_existing_client',
                     N'When the client is a child of an already present parent client. All the billing goes to the parent client in this situation.')
                ) AS v([Value], [Description])
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = v.[Value]
                  AND i.[Description] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "OptionSetItems");
        }
    }
}
