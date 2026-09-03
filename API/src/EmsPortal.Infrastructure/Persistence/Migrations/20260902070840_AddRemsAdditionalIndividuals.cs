using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The other people on an individual client's return — a spouse, a child, anyone else the firm is
    /// preparing for — declared on the intake form's new "Spouse &amp; More Individuals" card.
    /// <para>
    /// A table of its own rather than more <c>REMSEntityContact</c> rows, for two reasons. An entity holds
    /// at most ONE contact per role (the unique index on (tenant, entity, role) exempts only
    /// BillingContact), and a client with three children has three people of one kind. And a contact
    /// record answers "who do we speak to?", which is not the question: what the firm needs to know about
    /// a second person on a return is how it is FILED and who is INVOICED for it, and neither is a
    /// property of a contact. Both columns are here, alongside the minor flag that decides the second one
    /// for a child.
    /// </para>
    /// <para>
    /// Each row points at a <c>Person</c> as well, so these people are findable in the CRM like anybody
    /// else the platform captures — and duplicates their name, email and phone, deliberately: this row is
    /// the record of what was DECLARED, and a Person edited afterwards must not silently rewrite the
    /// client's own answer.
    /// </para>
    /// <para>
    /// Nothing is backfilled. The card did not exist before this, so no submission carries an answer to
    /// it; the retired <c>spouseName</c> / <c>spouseEmail</c> / <c>spousePhone</c> payload fields and the
    /// retired <c>self</c> / <c>spouse</c> contact roles stay exactly where they are, in the submissions
    /// that carry them.
    /// </para>
    /// </summary>
    public partial class AddRemsAdditionalIndividuals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REMSAdditionalIndividual",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RelationType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FilingType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsMinor = table.Column<bool>(type: "bit", nullable: true),
                    BillingPreference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BillingFirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillingLastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSAdditionalIndividual", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSAdditionalIndividual_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSAdditionalIndividual_REMSEntity_REMSEntityId",
                        column: x => x.REMSEntityId,
                        principalTable: "REMSEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSAdditionalIndividual_REMS_REMSId",
                        column: x => x.REMSId,
                        principalTable: "REMS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSAdditionalIndividual_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_PersonId",
                table: "REMSAdditionalIndividual",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_REMSEntityId",
                table: "REMSAdditionalIndividual",
                column: "REMSEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_REMSId",
                table: "REMSAdditionalIndividual",
                column: "REMSId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_TenantId",
                table: "REMSAdditionalIndividual",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_TenantId_REMSEntityId",
                table: "REMSAdditionalIndividual",
                columns: new[] { "TenantId", "REMSEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSAdditionalIndividual_TenantId_REMSId_SourceKey",
                table: "REMSAdditionalIndividual",
                columns: new[] { "TenantId", "REMSId", "SourceKey" },
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REMSAdditionalIndividual");
        }
    }
}
