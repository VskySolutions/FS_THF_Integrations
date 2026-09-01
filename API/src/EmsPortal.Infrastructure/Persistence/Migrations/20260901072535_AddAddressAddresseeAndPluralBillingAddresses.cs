using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every <c>Address</c> the person it is addressed to, and lets a REMS entity hold more than one
    /// BILLING address.
    /// <para>
    /// The five new columns are the addressee — suffix, first name, last name, email, phone. They are on
    /// the address rather than on a contact record of its own because "where does the invoice go?" and
    /// "who is it addressed to?" are one question with one answer: the client intake form asked them in
    /// two sections, and a client invoiced at two offices came back with two addresses, two names and
    /// nothing saying which went with which. Every column is nullable and every existing row keeps its
    /// nulls: most addresses in the platform are a place and nothing more, and the field-set only asks
    /// for these where a form opts in.
    /// </para>
    /// <para>
    /// The unique index on (tenant, entity, type) now exempts Billing, exactly as REMSEntityContact's
    /// exempts the BillingContact role and for the same reason — being given second does not make an
    /// address a different kind of address, and under the old index the second one failed the insert at
    /// the end of a submit that had already built the client, the entity and every contact, losing the
    /// client's whole form. Physical and Mailing stay singular: an entity operates from one place and
    /// takes post at one.
    /// </para>
    /// <para>
    /// No backfill. A submission written before this carries a single billing address, which is folded
    /// into the list on read (RemsFormPayloadV1.EffectiveBillingAddresses), and its already-materialised
    /// REMSEntityAddress row is one Billing row — which is exactly what the new index allows.
    /// </para>
    /// </summary>
    public partial class AddAddressAddresseeAndPluralBillingAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_REMSEntityAddress_TenantId_REMSEntityId_AddressType",
                table: "REMSEntityAddress");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Addresses",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Addresses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "Addresses",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Suffix",
                table: "Addresses",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityAddress_TenantId_REMSEntityId_AddressType",
                table: "REMSEntityAddress",
                columns: new[] { "TenantId", "REMSEntityId", "AddressType" },
                unique: true,
                filter: "[Deleted] = 0 AND [AddressType] <> 'Billing'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_REMSEntityAddress_TenantId_REMSEntityId_AddressType",
                table: "REMSEntityAddress");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Suffix",
                table: "Addresses");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntityAddress_TenantId_REMSEntityId_AddressType",
                table: "REMSEntityAddress",
                columns: new[] { "TenantId", "REMSEntityId", "AddressType" },
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
