using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDepartments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsHead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDepartments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_TenantId_Department_Head",
                table: "UserDepartments",
                columns: new[] { "TenantId", "Department" },
                unique: true,
                filter: "[Deleted] = 0 AND [IsHead] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_TenantId_UserId",
                table: "UserDepartments",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "[Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserDepartments_UserId",
                table: "UserDepartments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDepartments");
        }
    }
}
