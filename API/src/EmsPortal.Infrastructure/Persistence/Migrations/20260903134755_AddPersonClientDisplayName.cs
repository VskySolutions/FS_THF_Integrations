using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonClientDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientDisplayName",
                table: "Persons",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                computedColumnSql: "COALESCE(NULLIF(LTRIM(RTRIM(\n    CASE WHEN [PartyType] = 1 THEN ISNULL([CorporateName], N'')\n         ELSE ISNULL(NULLIF(LTRIM(RTRIM([LastName])), N''), N'')\n            + CASE WHEN NULLIF(LTRIM(RTRIM([FirstName])), N'') IS NULL THEN N''\n                   ELSE N' ' + LTRIM(RTRIM([FirstName])) END\n            + CASE WHEN NULLIF(LTRIM(RTRIM([Suffix])), N'') IS NULL THEN N''\n                   ELSE N' ' + LTRIM(RTRIM([Suffix])) END\n    END)), N''), [DisplayName])",
                stored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientDisplayName",
                table: "Persons");
        }
    }
}
