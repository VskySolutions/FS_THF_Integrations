using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveObsoleteCustomerEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WO-124 data cleanup. EmailTemplates.TemplateKey is persisted as the enum name (string).
            // WO-118 removed the Customer Management EmailTemplateKey values but its migration only dropped
            // the customer tables and permission-group templates — it left any EmailTemplate rows (platform
            // defaults and tenant overrides) seeded before WO-118 with keys that no longer map to the enum.
            // Delete those obsolete rows. Account-security + REMS templates are untouched.
            migrationBuilder.Sql(
                """
                DELETE FROM [EmailTemplates]
                WHERE [TemplateKey] IN (
                    N'CustomerSubmitted',
                    N'CustomerSentForApproval',
                    N'CustomerApproved',
                    N'CustomerRejected',
                    N'CustomerReturned'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible data cleanup: the obsolete customer email-template rows are not restored on rollback.
        }
    }
}
