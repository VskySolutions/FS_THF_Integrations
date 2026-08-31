using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>Persons.Prefix</c> → <c>Persons.Suffix</c>. The platform asked for a courtesy title (Mr., Mrs.,
    /// Dr.) beside every First Name; it asks for a generational particle (Jr., Sr., II, III, IV) beside
    /// the name instead, everywhere — the Person screens, a user's account, somebody's own profile and the
    /// REMS client intake form. One particle per name, and it is the suffix.
    /// <para>
    /// The column is RENAMED rather than dropped and re-added, so the type, the 16-character cap and the
    /// row's identity survive. The VALUES are not carried across: a title is not a suffix, and "Dr." left
    /// standing in the new column would make the name read "Jane Smith Dr." on every screen that joins the
    /// particle back on. Clearing them is the point of the change, not a casualty of it.
    /// </para>
    /// <para>
    /// <c>Down()</c> renames the column back but cannot restore the titles — they are gone with the UPDATE
    /// below. Rolling back gives an empty <c>Prefix</c> column.
    /// </para>
    /// </summary>
    public partial class RenamePersonPrefixToSuffix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Prefix",
                table: "Persons",
                newName: "Suffix");

            // Every value in the column is an answer to the question the column no longer asks.
            migrationBuilder.Sql("UPDATE [Persons] SET [Suffix] = NULL WHERE [Suffix] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Suffix",
                table: "Persons",
                newName: "Prefix");
        }
    }
}
