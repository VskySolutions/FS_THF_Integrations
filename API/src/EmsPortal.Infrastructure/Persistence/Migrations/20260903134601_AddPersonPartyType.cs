using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Says on the row whether a Person is a human being or an organisation, instead of leaving it to be
    /// inferred from whether <c>CorporateName</c> happens to be filled in.
    /// <para>
    /// Two readers need the answer and neither can afford to guess. The client picker must offer only
    /// individuals when the request being raised is for an individual — a question that was going to be
    /// answered by joining Person → REMS → REMSForm → the entity-type option, which breaks the moment one
    /// person appears on two requests of different types and costs three joins on every keystroke. And
    /// <c>Person.ClientDisplayName</c> reads a human surname-first ("Smith John Jr.") and an organisation
    /// as its plain legal name, so it has to know which it is holding before it has a name to read.
    /// </para>
    /// <para>
    /// Everything defaults to <c>Individual</c> (0), which is what every row written before this is: the
    /// firm's colleagues, the role contacts captured off intake forms, the other individuals on a return.
    /// The one thing that is NOT a person is the client of a request whose entity type is not Individual,
    /// and the preceding migration has already put those names in <c>CorporateName</c> — so that column is
    /// exactly the set to flip, and this migration needs no second look at the entity type to find them.
    /// </para>
    /// </summary>
    public partial class AddPersonPartyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartyType",
                table: "Persons",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_TenantId_PartyType_LastName",
                table: "Persons",
                columns: new[] { "TenantId", "PartyType", "LastName" });

            // PartyType.Organisation = 1. Written out rather than referenced: a migration must keep saying
            // what it said on the day it ran, whatever the enum is renumbered to afterwards.
            migrationBuilder.Sql(
                """
                UPDATE [Persons]
                SET [PartyType] = 1, [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Deleted] = 0
                  AND LTRIM(RTRIM(ISNULL([CorporateName], ''))) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Persons_TenantId_PartyType_LastName",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "PartyType",
                table: "Persons");
        }
    }
}
