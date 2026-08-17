using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemsParentClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The client a subsidiary referral hangs off. Two columns rather than one because the name
            // travels with the id: every list that shows a request would otherwise join out to Person for
            // a single column, and REMS already keeps the client's own name the same way
            // (RequestedClientName). Both stay null on every request type but the subsidiary one.
            //
            // Hand-authored rather than scaffolded — the API was running and holding its build output, so
            // `dotnet ef migrations add` could not build the startup project. The snapshot and this
            // migration's Designer carry the same two properties; regenerate both if either drifts.
            migrationBuilder.AddColumn<Guid>(
                name: "ParentClientReferenceId",
                table: "REMS",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentClientName",
                table: "REMS",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentClientReferenceId",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "ParentClientName",
                table: "REMS");
        }
    }
}
