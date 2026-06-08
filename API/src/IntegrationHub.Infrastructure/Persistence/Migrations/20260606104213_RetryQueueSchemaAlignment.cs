using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntegrationHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetryQueueSchemaAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetryQueue_NextAttemptUtc",
                table: "RetryQueue");

            migrationBuilder.RenameColumn(
                name: "NextAttemptUtc",
                table: "RetryQueue",
                newName: "NextRetryDate");

            migrationBuilder.RenameColumn(
                name: "AttemptNumber",
                table: "RetryQueue",
                newName: "RetryCount");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RetryQueue",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RetryQueue_Status_NextRetryDate",
                table: "RetryQueue",
                columns: new[] { "Status", "NextRetryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetryQueue_Status_NextRetryDate",
                table: "RetryQueue");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RetryQueue");

            migrationBuilder.RenameColumn(
                name: "RetryCount",
                table: "RetryQueue",
                newName: "AttemptNumber");

            migrationBuilder.RenameColumn(
                name: "NextRetryDate",
                table: "RetryQueue",
                newName: "NextAttemptUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RetryQueue_NextAttemptUtc",
                table: "RetryQueue",
                column: "NextAttemptUtc");
        }
    }
}
