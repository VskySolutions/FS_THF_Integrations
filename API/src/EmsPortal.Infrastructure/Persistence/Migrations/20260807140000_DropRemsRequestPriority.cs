using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops <c>REMS.Priority</c> and retires the option list behind it.
    /// <para>
    /// DESTRUCTIVE, and not recoverable: the priority every existing request was filed under is deleted
    /// with the column. <c>Down</c> puts the column back so the schema round-trips, but it comes back
    /// empty — there is nowhere the old values are kept.
    /// </para>
    /// </summary>
    public partial class DropRemsRequestPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "REMS");

            // Retire the list that configured the dropped field, across the platform standard copy AND
            // every tenant copy. Soft-delete, per the platform convention. Left behind it would still be
            // editable in Administration → Option Sets, inviting a tenant to configure a value that no
            // longer reaches anything — worse than the list simply being gone.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Priority' AND s.[Deleted] = 0 AND i.[Deleted] = 0;

                UPDATE s
                SET s.[Deleted] = 1, s.[DeletedOnUtc] = SYSUTCDATETIME(),
                    s.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.Priority' AND s.[Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column returns empty — the values are gone. Restoring the list too, so a rolled-back
            // deployment at least has something to repopulate it from.
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "REMS",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.[Deleted] = 0, s.[DeletedOnUtc] = NULL, s.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSets] s
                WHERE s.[Key] = N'REMS.Priority' AND s.[Deleted] = 1;

                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Priority' AND i.[Deleted] = 1;
                """);
        }
    }
}
