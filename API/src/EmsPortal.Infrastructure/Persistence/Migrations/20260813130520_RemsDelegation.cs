using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemsDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OnBehalfOfUserId",
                table: "REMS",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "REMSDelegation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrincipalUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegateUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanPrepare = table.Column<bool>(type: "bit", nullable: false),
                    CanSend = table.Column<bool>(type: "bit", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSDelegation", x => x.Id);
                    table.CheckConstraint("CK_REMSDelegation_Dates", "[StartsOn] IS NULL OR [EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
                    table.ForeignKey(
                        name: "FK_REMSDelegation_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSDelegation_Users_DelegateUserId",
                        column: x => x.DelegateUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSDelegation_Users_PrincipalUserId",
                        column: x => x.PrincipalUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REMS_OnBehalfOfUserId",
                table: "REMS",
                column: "OnBehalfOfUserId");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_OnBehalfOfUserId_Status",
                table: "REMS",
                columns: new[] { "TenantId", "OnBehalfOfUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSDelegation_DelegateUserId",
                table: "REMSDelegation",
                column: "DelegateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSDelegation_PrincipalUserId",
                table: "REMSDelegation",
                column: "PrincipalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSDelegation_TenantId",
                table: "REMSDelegation",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSDelegation_TenantId_DelegateUserId",
                table: "REMSDelegation",
                columns: new[] { "TenantId", "DelegateUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_REMSDelegation_TenantId_PrincipalUserId_DelegateUserId",
                table: "REMSDelegation",
                columns: new[] { "TenantId", "PrincipalUserId", "DelegateUserId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_REMS_Users_OnBehalfOfUserId",
                table: "REMS",
                column: "OnBehalfOfUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_REMS_Users_OnBehalfOfUserId",
                table: "REMS");

            migrationBuilder.DropTable(
                name: "REMSDelegation");

            migrationBuilder.DropIndex(
                name: "IX_REMS_OnBehalfOfUserId",
                table: "REMS");

            migrationBuilder.DropIndex(
                name: "IX_REMS_TenantId_OnBehalfOfUserId_Status",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfUserId",
                table: "REMS");
        }
    }
}
