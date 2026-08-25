using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Drops twenty-two columns and one index that nothing reads. Each was traced across the whole of
    /// API/src and WEB/src before being listed here.
    /// <list type="bullet">
    ///   <item>
    ///     Never referenced at all — <c>Persons.ManagerPersonId</c>, <c>Media.Duration</c>,
    ///     <c>Resolution</c>, <c>ThumbnailUrl</c>, <c>PreviewUrl</c>, <c>IsArchived</c> (media processing
    ///     that was never built), and <c>Addresses.IsValidated</c> / <c>ValidationSource</c>, which no
    ///     code path has ever assigned.
    ///   </item>
    ///   <item>
    ///     Written but never read — <c>REMSClient.ExternalClientReferenceId</c>, superseded by
    ///     <c>REMS.ClientPersonId</c> which is what callers actually use; and
    ///     <c>REMSEntity.SourceEntityKey</c>, always the literal "main" since a request carries exactly
    ///     one entity. Its unique index goes with it — the filtered one-main-entity-per-client index
    ///     already enforces what is left to enforce.
    ///   </item>
    ///   <item>
    ///     Accepted and returned by the profile API but bound by no screen — the five social URLs,
    ///     <c>Persons.TimeZone</c> / <c>Language</c>, and <c>Addresses.Area</c>, <c>Latitude</c>,
    ///     <c>Longitude</c>, <c>CityCode</c>.
    ///   </item>
    ///   <item>
    ///     <c>REMSEngagement.ServiceLine</c>, retired when the entity type took over the question it
    ///     asked. Historical engagements lose the code recorded against it — a deliberate call.
    ///   </item>
    /// </list>
    /// <para>
    /// <c>Down()</c> restores the columns and the index but NOT their data: a dropped column cannot be
    /// un-dropped with its values. Rolling back gives empty columns.
    /// </para>
    /// </summary>
    public partial class DropUnusedColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_REMSEntity_TenantId_REMSClientId_SourceEntityKey",
                table: "REMSEntity");

            migrationBuilder.DropColumn(
                name: "SourceEntityKey",
                table: "REMSEntity");

            migrationBuilder.DropColumn(
                name: "ServiceLine",
                table: "REMSEngagement");

            migrationBuilder.DropColumn(
                name: "ExternalClientReferenceId",
                table: "REMSClient");

            migrationBuilder.DropColumn(
                name: "FacebookUrl",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "InstagramUrl",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "ManagerPersonId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "TwitterUrl",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "PreviewUrl",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "CityCode",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "IsValidated",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "ValidationSource",
                table: "Addresses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceEntityKey",
                table: "REMSEntity",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ServiceLine",
                table: "REMSEngagement",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalClientReferenceId",
                table: "REMSClient",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookUrl",
                table: "Persons",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramUrl",
                table: "Persons",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Persons",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Persons",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerPersonId",
                table: "Persons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Persons",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterUrl",
                table: "Persons",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "Persons",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Duration",
                table: "Media",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Media",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreviewUrl",
                table: "Media",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "Media",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "Media",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Addresses",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityCode",
                table: "Addresses",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsValidated",
                table: "Addresses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Addresses",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Addresses",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationSource",
                table: "Addresses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_REMSEntity_TenantId_REMSClientId_SourceEntityKey",
                table: "REMSEntity",
                columns: new[] { "TenantId", "REMSClientId", "SourceEntityKey" },
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
