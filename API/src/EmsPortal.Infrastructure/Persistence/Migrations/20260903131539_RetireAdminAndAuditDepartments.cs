using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Takes <c>audit</c> and <c>admin</c> off the Department picker in every EXISTING copy of
    /// REMS.Department.
    /// <para>
    /// HIDDEN, NOT DELETED, and the distinction is the whole of this migration. Both codes are branched
    /// on across the engagement setup — the signed client-acceptance form and the government contract
    /// block key off <c>audit</c> by name, and the approval prerequisites read it too — and engagements
    /// are already filed under both. Deleting either would strand those records and break the conditional
    /// cards that render them. Deactivating stops anything NEW being booked into them while every
    /// engagement already there keeps reading exactly as it did.
    /// </para>
    /// <para>
    /// Attest work goes to Assurance now, and the firm's own internal jobs are not booked through an
    /// engagement at all. A firm that disagrees turns either one back on in Administration → Option Sets,
    /// which is the point of doing this as a value on their list rather than as a filter in the code.
    /// </para>
    /// <para>
    /// A migration is needed because <c>TenantOptionSetSeeder</c> is idempotent per LIST: a tenant that
    /// already holds REMS.Department is left exactly as they edited it, so seeding the two as inactive
    /// reaches only tenants created afterwards.
    /// </para>
    /// <para>
    /// Nothing a firm has decided is overwritten. Only rows nobody has edited are touched
    /// (<c>UpdatedById IS NULL</c>) — a tenant who has deliberately renamed, recoloured or re-enabled one
    /// of these has said what they want, and this is not the place to argue.
    /// </para>
    /// </summary>
    public partial class RetireAdminAndAuditDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[IsActive] = 0, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Department'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[IsActive] = 1
                  AND i.[UpdatedById] IS NULL
                  AND i.[Value] IN (N'audit', N'admin');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Offered again, and only where nobody has touched the row since — the same guard Up applies,
            // so a firm that has since made a deliberate decision about either value keeps it.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[IsActive] = 1, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Department'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[IsActive] = 0
                  AND i.[UpdatedById] IS NULL
                  AND i.[Value] IN (N'audit', N'admin');
                """);
        }
    }
}
