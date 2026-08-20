using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops <c>Person.JobTitle</c> and retires the <c>User.JobTitle</c> option list behind it.
    /// <para>
    /// The title was asked for in two different ways — a mandatory picker on the user forms, backed by the
    /// option list, and a free-text box on the Person form and My Profile — both writing the same column,
    /// and read again by the People list and the REMS extra-approver picker. All of it goes.
    /// </para>
    /// <para>
    /// DESTRUCTIVE, and not recoverable: every title on file is deleted with the column. <c>Down</c> puts
    /// the column back so the schema round-trips, but it comes back empty — there is nowhere the old
    /// values are kept.
    /// </para>
    /// </summary>
    public partial class DropPersonJobTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobTitle",
                table: "Persons");

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
                WHERE s.[Key] = N'User.JobTitle' AND s.[Deleted] = 0 AND i.[Deleted] = 0;

                UPDATE s
                SET s.[Deleted] = 1, s.[DeletedOnUtc] = SYSUTCDATETIME(),
                    s.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSets] s
                WHERE s.[Key] = N'User.JobTitle' AND s.[Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column returns empty — the values are gone. Restoring the list too, so a rolled-back
            // deployment at least has something to repopulate it from.
            migrationBuilder.AddColumn<string>(
                name: "JobTitle",
                table: "Persons",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.[Deleted] = 0, s.[DeletedOnUtc] = NULL, s.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSets] s
                WHERE s.[Key] = N'User.JobTitle' AND s.[Deleted] = 1;

                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'User.JobTitle' AND i.[Deleted] = 1;
                """);
        }
    }
}
