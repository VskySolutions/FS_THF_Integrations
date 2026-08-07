using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionGroupCapacityLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CapacityLimit",
                table: "PermissionGroups",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapacityLimit",
                table: "PermissionGroups");
        }
    }
}
