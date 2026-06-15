using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMappingTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve existing rules: copy each template's flow onto its field rows before the
            // template link/table are dropped, so (tenant, flow) resolution keeps working.
            migrationBuilder.Sql(@"
UPDATE mc
SET mc.InterfaceName = t.InterfaceName
FROM MappingConfigurations mc
JOIN MappingTemplates t ON mc.MappingTemplateId = t.Id
WHERE mc.MappingTemplateId IS NOT NULL
  AND (mc.InterfaceName IS NULL OR mc.InterfaceName = '');");

            migrationBuilder.DropForeignKey(
                name: "FK_MappingConfigurations_MappingTemplates_MappingTemplateId",
                table: "MappingConfigurations");

            migrationBuilder.DropTable(
                name: "MappingTemplates");

            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_InterfaceName_IsActive",
                table: "MappingConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_MappingTemplateId_IsActive",
                table: "MappingConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_IsActive",
                table: "MappingConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_TenantId",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "MappingTemplateId",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "MappingTemplateId",
                table: "IntegrationJobs");

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_InterfaceName_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "SourceSystem", "TargetSystem", "InterfaceName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_TenantId_InterfaceName",
                table: "MappingConfigurations",
                columns: new[] { "TenantId", "InterfaceName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_InterfaceName_IsActive",
                table: "MappingConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_TenantId_InterfaceName",
                table: "MappingConfigurations");

            migrationBuilder.AddColumn<Guid>(
                name: "MappingTemplateId",
                table: "MappingConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MappingTemplateId",
                table: "IntegrationJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MappingTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InterfaceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappingTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MappingTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_InterfaceName_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "InterfaceName", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_MappingTemplateId_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "MappingTemplateId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_SourceSystem_TargetSystem_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "SourceSystem", "TargetSystem", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingConfigurations_TenantId",
                table: "MappingConfigurations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId",
                table: "MappingTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem_InterfaceName",
                table: "MappingTemplates",
                columns: new[] { "TenantId", "SourceSystem", "TargetSystem", "InterfaceName" },
                unique: true,
                filter: "[IsDefault] = 1 AND [Deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem_IsActive",
                table: "MappingTemplates",
                columns: new[] { "TenantId", "SourceSystem", "TargetSystem", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_MappingConfigurations_MappingTemplates_MappingTemplateId",
                table: "MappingConfigurations",
                column: "MappingTemplateId",
                principalTable: "MappingTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
