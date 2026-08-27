using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Takes the word "round" out of the two REMS.Status descriptions that carried it. Approval rounds are
    /// still exactly what the server runs — numbered, immutable, one per submission — but they are no
    /// longer named anywhere a user reads, so a description explaining a status in terms of "a round" now
    /// explains it in terms of something that is nowhere else on screen.
    /// <para>
    /// No model change — the descriptions live in <c>OptionSetItems</c> rows. Changing
    /// <c>DefaultOptionSets</c> alone reaches nobody already running: <c>TenantOptionSetSeeder</c> is
    /// idempotent per LIST, so a tenant that already holds REMS.Status keeps the descriptions it was seeded
    /// with. This rewrites them in every existing copy — the platform-standard list (TenantId IS NULL) and
    /// each tenant's own.
    /// </para>
    /// <para>
    /// Matching on the OLD description is the guard against clobbering somebody's own edit: a tenant who
    /// has already reworded one of these is not carrying the old text any more, so they are skipped and
    /// keep what they chose. It is what makes this idempotent too — re-running matches nothing.
    /// </para>
    /// <para>
    /// The VALUES are deliberately untouched: <c>pending_approval</c> and <c>changes_requested</c> are the
    /// codes requests are already recorded against, and renaming a code strands every one of them.
    /// <c>UpdatedById</c> stays NULL: the platform changed this, not a user.
    /// </para>
    /// </summary>
    public partial class DropRoundWordingFromRemsStatusOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Description] = v.[NewDescription],
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                    (N'pending_approval',
                     N'Routed to the approvers. Every field is read-only while a round is open.',
                     N'Routed to the approvers. Every field is read-only while the approval is open.'),
                    (N'changes_requested',
                     N'Enough approvers declined to close the round. Back with the initiator to rework the setup.',
                     N'Enough approvers declined. Back with the initiator to rework the setup.')
                ) AS v([Value], [OldDescription], [NewDescription])
                    ON v.[Value] = i.[Value] AND v.[OldDescription] = i.[Description]
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The same statement the other way round, guarded the same way: only a row still reading the
            // new wording goes back, so a tenant who reworded one after this ran keeps their own text.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Description] = v.[OldDescription],
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                    (N'pending_approval',
                     N'Routed to the approvers. Every field is read-only while a round is open.',
                     N'Routed to the approvers. Every field is read-only while the approval is open.'),
                    (N'changes_requested',
                     N'Enough approvers declined to close the round. Back with the initiator to rework the setup.',
                     N'Enough approvers declined. Back with the initiator to rework the setup.')
                ) AS v([Value], [OldDescription], [NewDescription])
                    ON v.[Value] = i.[Value] AND v.[NewDescription] = i.[Description]
                WHERE s.[Key] = N'REMS.Status'
                  AND s.[Deleted] = 0
                  AND i.[Deleted] = 0;
                """);
        }
    }
}
