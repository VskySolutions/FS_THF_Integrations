using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the engagement's two sub-classifications: <c>SubServiceLine</c> (below the service line) and
    /// <c>SubIndustry</c> (below the request's industry group). Both hold option-set codes from the new
    /// <c>REMS.SubServiceLine</c> / <c>REMS.SubIndustry</c> lists, which are seeded at startup rather than
    /// here — they are whole new lists, so no tenant holds a copy of either key and every tenant resolves
    /// the platform-standard one until they make their own.
    /// <para>
    /// Nullable, with no backfill. An engagement set up before these existed is not misclassified, it is
    /// unclassified, and neither field is a prerequisite for sending a round for approval.
    /// </para>
    /// </summary>
    public partial class AddRemsEngagementSubClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubIndustry",
                table: "REMSEngagement",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubServiceLine",
                table: "REMSEngagement",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubIndustry",
                table: "REMSEngagement");

            migrationBuilder.DropColumn(
                name: "SubServiceLine",
                table: "REMSEngagement");
        }
    }
}
