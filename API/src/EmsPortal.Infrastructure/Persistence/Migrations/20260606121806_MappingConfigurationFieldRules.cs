using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MappingConfigurationFieldRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MappingJson",
                table: "MappingConfigurations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "DestinationField",
                table: "MappingConfigurations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceField",
                table: "MappingConfigurations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TransformationRule",
                table: "MappingConfigurations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "SourceSystem", "TargetSystem", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_IsActive",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "DestinationField",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "SourceField",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "TransformationRule",
                table: "MappingConfigurations");

            migrationBuilder.AlterColumn<string>(
                name: "MappingJson",
                table: "MappingConfigurations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
