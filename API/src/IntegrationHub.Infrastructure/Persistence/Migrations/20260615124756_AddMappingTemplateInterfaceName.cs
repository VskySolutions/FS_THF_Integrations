using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMappingTemplateInterfaceName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem",
                table: "MappingTemplates");

            migrationBuilder.AddColumn<string>(
                name: "InterfaceName",
                table: "MappingTemplates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Templates created by the previous migration (from flow-agnostic flat mappings) are
            // assigned the Expense Reports flow; admins can re-point or split them afterwards.
            migrationBuilder.Sql("UPDATE MappingTemplates SET InterfaceName = 'ExpenseImport' WHERE InterfaceName = '';");

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem_InterfaceName",
                table: "MappingTemplates",
                columns: new[] { "TenantId", "SourceSystem", "TargetSystem", "InterfaceName" },
                unique: true,
                filter: "[IsDefault] = 1 AND [Deleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem_InterfaceName",
                table: "MappingTemplates");

            migrationBuilder.DropColumn(
                name: "InterfaceName",
                table: "MappingTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem",
                table: "MappingTemplates",
                columns: new[] { "TenantId", "SourceSystem", "TargetSystem" },
                unique: true,
                filter: "[IsDefault] = 1 AND [Deleted] = 0");
        }
    }
}
