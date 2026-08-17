using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropRemsRequestTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Title asked the initiator to invent a name for something that already had two — its REMS
            // number and its client — and nothing downstream needed a third. Every place that showed it
            // (the lists, the notifications, the form-link and reminder emails) now reads
            // "REMS-12 — Meridian Retail Group", which identifies the request without anyone having to
            // have thought of a good title first.
            //
            // The per-client uniqueness rule it carried goes with it: a client's requests are told apart
            // by number and date.
            migrationBuilder.DropColumn(
                name: "Title",
                table: "REMS");

            // The client-facing form-link and reminder templates carried a "Request: {{RequestTitle}}"
            // line. The token no longer resolves, so the line would render with a literal placeholder in
            // front of a client. Removed only where the template still holds the seeded wording — a tenant
            // who rewrote their template owns it, and a blind replace would undo their edit.
            migrationBuilder.Sql(
                """
                UPDATE [EmailTemplates]
                SET [Body] = REPLACE([Body], '<p><strong>Request:</strong> {{RequestTitle}}</p>', ''),
                    [UpdatedOnUtc] = SYSUTCDATETIME()
                WHERE [Body] LIKE '%{{RequestTitle}}%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restored as nullable-with-default rather than required: the values are gone, and re-adding a
            // NOT NULL column with no default would fail against any existing row.
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "REMS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
