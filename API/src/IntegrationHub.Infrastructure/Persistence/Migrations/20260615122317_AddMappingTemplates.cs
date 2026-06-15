using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMappingTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "IX_MappingConfigurations_MappingTemplateId_IsActive",
                table: "MappingConfigurations",
                columns: new[] { "MappingTemplateId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId",
                table: "MappingTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MappingTemplates_TenantId_SourceSystem_TargetSystem",
                table: "MappingTemplates",
                columns: new[] { "TenantId", "SourceSystem", "TargetSystem" },
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

            // Data migration: wrap existing flat mappings into one Default template per
            // (tenant, source, target) pair, and point those rows at the new template.
            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSUTCDATETIME();

SELECT NEWID() AS Id, TenantId, SourceSystem, TargetSystem
INTO #new_mapping_templates
FROM MappingConfigurations
WHERE Deleted = 0
GROUP BY TenantId, SourceSystem, TargetSystem;

INSERT INTO MappingTemplates
    (Id, TenantId, Name, Description, SourceSystem, TargetSystem, IsDefault, IsActive, CreatedAtUtc, CreatedOnUtc, UpdatedOnUtc, Deleted)
SELECT
    Id, TenantId, CONCAT(SourceSystem, ' -> ', TargetSystem, ' Default'), NULL,
    SourceSystem, TargetSystem, 1, 1, @now, @now, @now, 0
FROM #new_mapping_templates;

UPDATE mc
SET mc.MappingTemplateId = nt.Id
FROM MappingConfigurations mc
JOIN #new_mapping_templates nt
    ON mc.TenantId = nt.TenantId
   AND mc.SourceSystem = nt.SourceSystem
   AND mc.TargetSystem = nt.TargetSystem
WHERE mc.Deleted = 0 AND mc.MappingTemplateId IS NULL;

DROP TABLE #new_mapping_templates;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MappingConfigurations_MappingTemplates_MappingTemplateId",
                table: "MappingConfigurations");

            migrationBuilder.DropTable(
                name: "MappingTemplates");

            migrationBuilder.DropIndex(
                name: "IX_MappingConfigurations_MappingTemplateId_IsActive",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "MappingTemplateId",
                table: "MappingConfigurations");

            migrationBuilder.DropColumn(
                name: "MappingTemplateId",
                table: "IntegrationJobs");
        }
    }
}
