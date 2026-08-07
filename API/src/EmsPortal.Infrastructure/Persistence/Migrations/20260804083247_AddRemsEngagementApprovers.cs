using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemsEngagementApprovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "REMSEngagementApprover",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    REMSEngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REMSEngagementApprover", x => x.Id);
                    table.ForeignKey(
                        name: "FK_REMSEngagementApprover_REMSEngagement_REMSEngagementId",
                        column: x => x.REMSEngagementId,
                        principalTable: "REMSEngagement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementApprover_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_REMSEngagementApprover_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementApprover_REMSEngagementId",
                table: "REMSEngagementApprover",
                column: "REMSEngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementApprover_TenantId",
                table: "REMSEngagementApprover",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementApprover_TenantId_REMSEngagementId_UserId",
                table: "REMSEngagementApprover",
                columns: new[] { "TenantId", "REMSEngagementId", "UserId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_REMSEngagementApprover_UserId",
                table: "REMSEngagementApprover",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "REMSEngagementApprover");
        }
    }
}
