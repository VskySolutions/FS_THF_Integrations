using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Two unrelated columns that happen to be about how somebody is named and how somebody is billed.
    /// <list type="bullet">
    ///   <item>
    ///     <c>Persons.Prefix</c> — the title a person is addressed by (Mr., Mrs., Ms., Dr.), asked beside
    ///     every First Name in the app. Held apart from the name for the same reason the REMS client's
    ///     generational suffix is: a person is FILED under a given name and a family name, and "Dr." is
    ///     neither, so folding it in would put a title into the field everything searches by.
    ///   </item>
    ///   <item>
    ///     <c>REMSEngagement.NumberOfBills</c> → <c>BillingProcessDescription</c> — a count becomes prose.
    ///     "4" said how many invoices without saying what triggered one, and a schedule that does not
    ///     reduce to a number ("three progress bills, the balance on delivery") could not be recorded at
    ///     all. The existing counts are CARRIED ACROSS as a sentence rather than dropped — see below.
    ///   </item>
    /// </list>
    /// </summary>
    public partial class PersonPrefixAndBillingProcessDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prefix",
                table: "Persons",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            // The description arrives first, empty, so the counts have somewhere to go before the column
            // holding them is dropped.
            migrationBuilder.AddColumn<string>(
                name: "BillingProcessDescription",
                table: "REMSEngagement",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Carry the counts across. "4 bills" is a poorer answer than the sentence somebody would write
            // today, but it is the answer that was actually given, and dropping it would quietly empty a
            // field on every engagement already set up. Singular where the count is one, because a row
            // reading "1 bills" is the kind of thing a reader notices and the system never explains.
            migrationBuilder.Sql(
                """
                UPDATE [REMSEngagement]
                SET [BillingProcessDescription] =
                    CONVERT(nvarchar(1000), [NumberOfBills])
                    + CASE WHEN [NumberOfBills] = 1 THEN N' bill' ELSE N' bills' END
                WHERE [NumberOfBills] IS NOT NULL;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_REMSEngagement_NumberOfBills",
                table: "REMSEngagement");

            migrationBuilder.DropColumn(
                name: "NumberOfBills",
                table: "REMSEngagement");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prefix",
                table: "Persons");

            migrationBuilder.AddColumn<int>(
                name: "NumberOfBills",
                table: "REMSEngagement",
                type: "int",
                nullable: true);

            // Whatever the count column can still be read back out of the prose — which is exactly the rows
            // Up() wrote and nothing anybody has typed since. TRY_CONVERT rather than a cast: the column is
            // free text now, and most of it will not be a number. Zero and negatives are excluded because
            // the CHECK constraint restored below would refuse them.
            migrationBuilder.Sql(
                """
                UPDATE [REMSEngagement]
                SET [NumberOfBills] = TRY_CONVERT(int, REPLACE(REPLACE([BillingProcessDescription], N' bills', N''), N' bill', N''))
                WHERE [BillingProcessDescription] IS NOT NULL
                  AND TRY_CONVERT(int, REPLACE(REPLACE([BillingProcessDescription], N' bills', N''), N' bill', N'')) > 0;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_REMSEngagement_NumberOfBills",
                table: "REMSEngagement",
                sql: "[NumberOfBills] IS NULL OR [NumberOfBills] > 0");

            migrationBuilder.DropColumn(
                name: "BillingProcessDescription",
                table: "REMSEngagement");
        }
    }
}
