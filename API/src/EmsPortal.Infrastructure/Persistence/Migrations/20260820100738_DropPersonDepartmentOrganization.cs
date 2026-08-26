using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops <c>Person.Department</c> and <c>Person.Organization</c>, the two free-text fields on the
    /// Professional part of a person record.
    /// <para>
    /// Neither was the answer to the question it looked like it answered. Which department somebody works
    /// in is held per tenant on their USER account (<c>UserDepartment</c>, whose head is the tenant's REMS
    /// Department Director) — the person-level copy was a second, unread answer that could disagree with
    /// it. The organization is the firm running the portal, which is the same for every person in it.
    /// </para>
    /// <para>
    /// DESTRUCTIVE, and not recoverable: every value in both columns goes with them. <c>Down</c> puts the
    /// columns back so the schema round-trips, but they come back empty — there is nowhere the old values
    /// are kept.
    /// </para>
    /// </summary>
    public partial class DropPersonDepartmentOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "Persons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Persons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "Persons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }
    }
}
