using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnmappedEmailTemplateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EmailTemplates.TemplateKey is persisted as the enum NAME, so a row whose key is no longer a
            // member of EmailTemplateKey cannot be read at all — EF throws
            // "Cannot convert string value '<key>' ... to any value in the mapped 'EmailTemplateKey' enum"
            // and the whole list query fails, not just that row.
            //
            // RemoveObsoleteCustomerEmailTemplates deleted five such keys by name and missed at least
            // 'CustomerSynced'. Rather than chase them one at a time, delete every row that does not map to
            // a current enum member: the allow-list below IS EmailTemplateKey, so anything else is
            // unreadable by definition. Tenant overrides for those keys go too — they are already
            // unloadable, so there is nothing to preserve.
            migrationBuilder.Sql(
                """
                DELETE FROM [EmailTemplates]
                WHERE [TemplateKey] NOT IN (
                    N'UserInvitation',
                    N'PasswordReset',
                    N'PasswordChanged',
                    N'Welcome',
                    N'MentionReceived',
                    N'ReminderDue',
                    N'RemsFormLink',
                    N'RemsFormSubmitted'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible data cleanup: rows that no longer map to the enum are not restored on rollback.
        }
    }
}
