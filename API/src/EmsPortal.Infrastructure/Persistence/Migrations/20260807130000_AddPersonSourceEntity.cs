using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records where each <c>Person</c> came from. Persons are minted from several places — the Person
    /// screen, a REMS engagement's role contacts, a client's submitted EMS form — and once they are in
    /// one list nothing tells them apart, though a contact a client typed into a public form is not a
    /// colleague somebody onboarded deliberately.
    /// </summary>
    public partial class AddPersonSourceEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceEntityId",
                table: "Persons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceEntityType",
                table: "Persons",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_SourceEntityType_SourceEntityId",
                table: "Persons",
                columns: new[] { "SourceEntityType", "SourceEntityId" });

            // Backfill only what the data already proves: a person wired to a REMS entity contact was
            // created by that request's engagement setup or its client's EMS form. 6 is EntityType.Rems
            // (values are stable integers seeded in application code, never renumbered).
            //
            // Everything else is deliberately left NULL. Most remaining rows were almost certainly typed
            // on the Person screen, but "almost certainly" is not provenance — a column that answers
            // "where did this come from" has to be silent where it does not know, or it is worse than
            // having no column. New rows carry their source from creation.
            //
            // GROUP BY, not a plain join: a person can be the contact on several entities of one client.
            // Where those resolve to more than one request the source is ambiguous, so it stays NULL.
            migrationBuilder.Sql(
                """
                WITH [Sourced] AS (
                    SELECT ec.[PersonId], MIN(c.[REMSId]) AS [REMSId], COUNT(DISTINCT c.[REMSId]) AS [Requests]
                    FROM [REMSEntityContact] ec
                    INNER JOIN [REMSEntity] e ON e.[Id] = ec.[REMSEntityId]
                    INNER JOIN [REMSClient] c ON c.[Id] = e.[REMSClientId]
                    WHERE ec.[Deleted] = 0 AND e.[Deleted] = 0 AND c.[Deleted] = 0
                    GROUP BY ec.[PersonId]
                )
                UPDATE p
                SET p.[SourceEntityType] = 6, p.[SourceEntityId] = s.[REMSId]
                FROM [Persons] p
                INNER JOIN [Sourced] s ON s.[PersonId] = p.[Id]
                WHERE s.[Requests] = 1 AND p.[SourceEntityType] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Persons_SourceEntityType_SourceEntityId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "SourceEntityId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "SourceEntityType",
                table: "Persons");
        }
    }
}
