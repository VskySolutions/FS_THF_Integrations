using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds <c>REMS.ClientNameSuffix</c> and renames the business contact roles stored on
    /// <c>REMSEntityContact.ContactRole</c>.
    /// <para>
    /// The SUFFIX is the generational particle on a client's name — Jr., Sr., II, III, IV — asked beside
    /// the client at intake and kept out of the name itself, so that the two can be told apart afterwards:
    /// a Person is filed under a given name and a family name, and "Jr." belongs to neither.
    /// </para>
    /// <para>
    /// The ROLES are renamed for what the firm needs from the person rather than for the office they hold.
    /// Not every client has a CEO or a CFO, and a two-partner practice asked for both was left guessing
    /// which of them to put where. <c>CEO</c> → <c>PrimaryClientContact</c>, <c>CFO</c> →
    /// <c>FinancialContact</c>, <c>AccountsPayable</c> → <c>BillingContact</c>. The column stores the enum
    /// NAME as a string, so the rows have to move with the enum or every read of the contact they name
    /// resolves to nothing.
    /// </para>
    /// <para>
    /// The submitted-form payloads are deliberately NOT rewritten. A submission is the immutable record of
    /// what a client actually sent, and its role keys are part of that record; the payload type reads the
    /// old keys and folds them into their successors (<c>RemsRolesPayload.Normalized</c>), so an old form
    /// renders and an old DRAFT can still be finished under the new names.
    /// </para>
    /// <para>
    /// Banker and Lawyer are retired from the form but left alone here: the contact a client gave is a
    /// contact whether or not the box is still on the page.
    /// </para>
    /// </summary>
    public partial class RemsClientNameSuffixAndContactRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientNameSuffix",
                table: "REMS",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            // Soft-deleted rows move too. A restored contact has to come back under a role the application
            // still recognises, and Deleted Records restores REMS graphs.
            migrationBuilder.Sql(
                """
                UPDATE [REMSEntityContact]
                SET [ContactRole] = CASE [ContactRole]
                        WHEN N'CEO' THEN N'PrimaryClientContact'
                        WHEN N'CFO' THEN N'FinancialContact'
                        WHEN N'AccountsPayable' THEN N'BillingContact'
                    END,
                    [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [ContactRole] IN (N'CEO', N'CFO', N'AccountsPayable');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The suffixes are gone with the column — there is nowhere else they are kept.
            migrationBuilder.Sql(
                """
                UPDATE [REMSEntityContact]
                SET [ContactRole] = CASE [ContactRole]
                        WHEN N'PrimaryClientContact' THEN N'CEO'
                        WHEN N'FinancialContact' THEN N'CFO'
                        WHEN N'BillingContact' THEN N'AccountsPayable'
                    END,
                    [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [ContactRole] IN (N'PrimaryClientContact', N'FinancialContact', N'BillingContact');
                """);

            // OtherContact has no predecessor to go back to. A contact captured under it after this
            // migration ran keeps its code through a rollback: the row is better left naming a role the
            // old application does not know than folded into one it would read as somebody else.

            migrationBuilder.DropColumn(
                name: "ClientNameSuffix",
                table: "REMS");
        }
    }
}
