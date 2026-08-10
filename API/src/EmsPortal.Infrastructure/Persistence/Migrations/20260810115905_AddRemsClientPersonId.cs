using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives a REMS request a real client: <c>REMS.ClientPersonId</c> → <c>Persons</c>, so the client a
    /// partner types at intake becomes a record the platform holds rather than three columns on one
    /// request. That is what puts them in the client picker on the next request, and what a later
    /// "convert this client into a user" hangs off (<c>User.PersonId</c> already points at a Person).
    /// <para>
    /// The backfill below does the same for every request already on file — without it the picker would
    /// only ever find clients captured from this point on, and the whole point is that a client entered
    /// once is found again.
    /// </para>
    /// </summary>
    public partial class AddRemsClientPersonId : Migration
    {
        /// <summary><c>EntityType.Client</c>. Persons carry the enum as an int.</summary>
        private const int ClientSourceType = 16;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientPersonId",
                table: "REMS",
                type: "uniqueidentifier",
                nullable: true);

            // ---- Backfill, before the FK goes on, so nothing can be left pointing at a row that is not there.

            // 1. Requests that already named an existing client. The reference was always a Person id; it
            //    was simply never a foreign key. Joined rather than copied so a reference to a person since
            //    deleted is skipped instead of becoming an unenforceable link.
            migrationBuilder.Sql($@"
                UPDATE r
                SET    r.ClientPersonId = r.ExistingClientReferenceId
                FROM   REMS r
                JOIN   Persons p ON p.Id = r.ExistingClientReferenceId AND p.Deleted = 0
                WHERE  r.ClientPersonId IS NULL;");

            // 2. Those persons are clients, whatever created them, so they are stamped as such — the picker
            //    filters on it, and a client it cannot offer is a client that gets entered twice. Platform
            //    users are left alone: a colleague named as a client on some request is still a colleague,
            //    and re-stamping them would drop them out of the lists that expect their own provenance.
            migrationBuilder.Sql($@"
                UPDATE p
                SET    p.SourceEntityType = {ClientSourceType}
                FROM   Persons p
                WHERE  p.Deleted = 0
                  AND  p.UserId IS NULL
                  AND  ISNULL(p.SourceEntityType, -1) <> {ClientSourceType}
                  AND  EXISTS (SELECT 1 FROM REMS r WHERE r.Deleted = 0 AND r.ClientPersonId = p.Id);");

            // 3. Requests whose client is already on file under the same name — THF treats one client name
            //    as one client, so they link rather than mint a second record. Two clients under one name is
            //    a real ambiguity, so those are left for step 4 to give a fresh record each, exactly as the
            //    application resolves it. Matched on DisplayName: every person the platform creates carries
            //    the full name there.
            migrationBuilder.Sql($@"
                UPDATE r
                SET    r.ClientPersonId = m.PersonId
                FROM   REMS r
                JOIN   (SELECT   p.TenantId,
                                 LTRIM(RTRIM(p.DisplayName)) AS Name,
                                 MIN(p.Id)                   AS PersonId,
                                 COUNT(*)                    AS Matches
                        FROM     Persons p
                        WHERE    p.Deleted = 0 AND p.SourceEntityType = {ClientSourceType}
                        GROUP BY p.TenantId, LTRIM(RTRIM(p.DisplayName))) m
                       ON  m.TenantId = r.TenantId
                       AND m.Name     = LTRIM(RTRIM(r.RequestedClientName))
                       AND m.Matches  = 1
                WHERE  r.Deleted = 0
                  AND  r.ClientPersonId IS NULL
                  AND  LTRIM(RTRIM(r.RequestedClientName)) <> '';");

            // 4. Everyone left is a client THF has no record of. One person per (tenant, name) — the same
            //    "one name, one client" rule — built from the request best placed to describe them: one that
            //    actually carries an email, most recently updated. Names are split on the first space, as the
            //    application does, and cut to the Person columns' 100 characters.
            migrationBuilder.Sql($@"
                CREATE TABLE #RemsClientSeed (
                    TenantId     uniqueidentifier NOT NULL,
                    Name         nvarchar(200)    NOT NULL,
                    Email        nvarchar(256)    NULL,
                    Mobile       nvarchar(32)     NULL,
                    SourceRemsId uniqueidentifier NOT NULL,
                    PersonId     uniqueidentifier NOT NULL
                );

                INSERT INTO #RemsClientSeed (TenantId, Name, Email, Mobile, SourceRemsId, PersonId)
                SELECT TenantId, Name, CustomerEmail, CustomerMobileNumber, Id, NEWID()
                FROM (
                    SELECT r.TenantId,
                           LTRIM(RTRIM(r.RequestedClientName)) AS Name,
                           r.CustomerEmail,
                           r.CustomerMobileNumber,
                           r.Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY r.TenantId, LTRIM(RTRIM(r.RequestedClientName))
                               ORDER BY CASE WHEN r.CustomerEmail IS NULL THEN 1 ELSE 0 END,
                                        r.UpdatedOnUtc DESC) AS Pick
                    FROM   REMS r
                    WHERE  r.Deleted = 0
                      AND  r.ClientPersonId IS NULL
                      AND  LTRIM(RTRIM(r.RequestedClientName)) <> ''
                ) ranked
                WHERE ranked.Pick = 1;

                INSERT INTO Persons (
                    Id, PersonCode, TenantId, FirstName, LastName, DisplayName,
                    PrimaryEmail, MobileNumber, IsActive, IsProfileVerified, ProfileCompletionPercentage,
                    SourceEntityType, SourceEntityId, LastProfileUpdatedOn,
                    CreatedOnUtc, UpdatedOnUtc, Deleted)
                SELECT s.PersonId,
                       'PER-' + UPPER(LEFT(REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), 10)),
                       s.TenantId,
                       LEFT(CASE WHEN CHARINDEX(' ', s.Name) > 0
                                 THEN LEFT(s.Name, CHARINDEX(' ', s.Name) - 1)
                                 ELSE s.Name END, 100),
                       LEFT(CASE WHEN CHARINDEX(' ', s.Name) > 0
                                 THEN LTRIM(SUBSTRING(s.Name, CHARINDEX(' ', s.Name) + 1, 200))
                                 ELSE '' END, 100),
                       s.Name,
                       s.Email,
                       s.Mobile,
                       1, 0, 0,
                       {ClientSourceType}, s.SourceRemsId, SYSUTCDATETIME(),
                       SYSUTCDATETIME(), SYSUTCDATETIME(), 0
                FROM #RemsClientSeed s;

                UPDATE r
                SET    r.ClientPersonId = s.PersonId
                FROM   REMS r
                JOIN   #RemsClientSeed s
                       ON  s.TenantId = r.TenantId
                       AND s.Name     = LTRIM(RTRIM(r.RequestedClientName))
                WHERE  r.Deleted = 0 AND r.ClientPersonId IS NULL;

                DROP TABLE #RemsClientSeed;");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_ClientPersonId",
                table: "REMS",
                column: "ClientPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_REMS_TenantId_ClientPersonId",
                table: "REMS",
                columns: new[] { "TenantId", "ClientPersonId" });

            migrationBuilder.AddForeignKey(
                name: "FK_REMS_Persons_ClientPersonId",
                table: "REMS",
                column: "ClientPersonId",
                principalTable: "Persons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Reverses the schema only. The person rows the backfill created are deliberately left standing:
        /// by the time anyone rolls this back they are client records in their own right — findable,
        /// editable, possibly already referenced by requests written since — and the provenance they
        /// replaced was not recorded, so there is nothing faithful to restore them to. Deleting them would
        /// throw away real data to undo a column.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_REMS_Persons_ClientPersonId",
                table: "REMS");

            migrationBuilder.DropIndex(
                name: "IX_REMS_ClientPersonId",
                table: "REMS");

            migrationBuilder.DropIndex(
                name: "IX_REMS_TenantId_ClientPersonId",
                table: "REMS");

            migrationBuilder.DropColumn(
                name: "ClientPersonId",
                table: "REMS");
        }
    }
}
