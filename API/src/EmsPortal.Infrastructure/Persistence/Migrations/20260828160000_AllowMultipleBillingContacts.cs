using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lets one entity hold more than one BILLING contact, by excluding that role from the unique index on
    /// (TenantId, REMSEntityId, ContactRole).
    /// <para>
    /// The client intake form asks who should be invoiced and lets the client name several people. They are
    /// all the same role — being named second does not make somebody a different kind of contact — so the
    /// second one hit <c>IX_REMSEntityContact_TenantId_REMSEntityId_ContactRole</c> and the insert failed
    /// with a duplicate key. It failed at the very end of the submit, after the client, the entity, its
    /// three addresses and every other contact had already been staged, and the whole transaction rolled
    /// back: the client filled the entire form and lost all of it.
    /// </para>
    /// <para>
    /// Every other role stays singular — an entity has one Primary Contact, one Financial Contact — which is
    /// why this narrows the index rather than dropping its uniqueness. Nothing about the table changes; only
    /// which rows the index polices.
    /// </para>
    /// </summary>
    public partial class AllowMultipleBillingContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_REMSEntityContact_TenantId_REMSEntityId_ContactRole",
                table: "REMSEntityContact");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityContact_TenantId_REMSEntityId_ContactRole",
                table: "REMSEntityContact",
                columns: new[] { "TenantId", "REMSEntityId", "ContactRole" },
                unique: true,
                filter: "[Deleted] = 0 AND [ContactRole] <> 'BillingContact'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Narrowing back will FAIL on any entity that has since been given a second billing contact,
            // which is the correct outcome: the rows the old index forbids exist, and dropping them to fit
            // it is not a decision a migration should take on the operator's behalf.
            migrationBuilder.DropIndex(
                name: "IX_REMSEntityContact_TenantId_REMSEntityId_ContactRole",
                table: "REMSEntityContact");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityContact_TenantId_REMSEntityId_ContactRole",
                table: "REMSEntityContact",
                columns: new[] { "TenantId", "REMSEntityId", "ContactRole" },
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
