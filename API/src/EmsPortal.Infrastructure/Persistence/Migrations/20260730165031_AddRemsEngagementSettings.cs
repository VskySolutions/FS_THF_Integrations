using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRemsEngagementSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RemsSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ManagingShareholderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemsSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemsSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemsSettings_Users_ManagingShareholderUserId",
                        column: x => x.ManagingShareholderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemsDepartmentDirector",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RemsSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DirectorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemsDepartmentDirector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemsDepartmentDirector_RemsSettings_RemsSettingsId",
                        column: x => x.RemsSettingsId,
                        principalTable: "RemsSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemsDepartmentDirector_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RemsDepartmentDirector_Users_DirectorUserId",
                        column: x => x.DirectorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemsDepartmentDirector_DirectorUserId",
                table: "RemsDepartmentDirector",
                column: "DirectorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemsDepartmentDirector_RemsSettingsId",
                table: "RemsDepartmentDirector",
                column: "RemsSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_RemsDepartmentDirector_TenantId",
                table: "RemsDepartmentDirector",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RemsDepartmentDirector_TenantId_Department",
                table: "RemsDepartmentDirector",
                columns: new[] { "TenantId", "Department" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RemsSettings_ManagingShareholderUserId",
                table: "RemsSettings",
                column: "ManagingShareholderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemsSettings_TenantId",
                table: "RemsSettings",
                column: "TenantId",
                unique: true,
                filter: "[Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RemsDepartmentDirector");

            migrationBuilder.DropTable(
                name: "RemsSettings");
        }
    }
}
