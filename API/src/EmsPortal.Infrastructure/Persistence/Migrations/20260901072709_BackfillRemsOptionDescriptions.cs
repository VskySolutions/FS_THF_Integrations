using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Writes the platform's descriptions onto every EXISTING copy of REMS.Status and REMS.Type that has
    /// none, so the badges on the request lists carry the explanatory tooltip the EMS State badge beside
    /// them already does.
    /// <para>
    /// AppOptionBadge renders a tooltip only where the option ITEM carries a Description. Both lists were
    /// seeded at WO-110 with labels and nothing else; the descriptions were written into
    /// <c>DefaultOptionSets</c> much later, and <c>TenantOptionSetSeeder</c> is idempotent per LIST — a
    /// tenant that already holds a list is left exactly as they edited it. So every tenant created before
    /// that release has Status and Type lists of bare labels, while REMS.FormStatus (a NEW list, seeded
    /// afterwards) came with its descriptions and gets its tooltip. That is the whole difference a reader
    /// sees between those columns: Status and Type explain nothing on hover, EMS State explains itself.
    /// The colour backfill in LockAndColourRemsStatusOptions filled Status's blank colours and left its
    /// blank descriptions alone, which is what this finishes.
    /// </para>
    /// <para>
    /// Only BLANK descriptions are written. A tenant who has explained a value in their own words keeps
    /// their wording, exactly as the colour backfill kept their colours — this is filling a blank, never
    /// replacing a decision. Down clears only the text this wrote, for the same reason.
    /// </para>
    /// <para>
    /// REMS.Type's <c>subsidiary_child_of_existing_client</c> is deliberately absent. It survives on
    /// tenant copies but is no longer seeded (see MergeRemsExistingClientTypes), so the platform has no
    /// words for it to write; a tenant who still offers it can explain it in Administration → Option Sets.
    /// </para>
    /// </summary>
    public partial class BackfillRemsOptionDescriptions : Migration
    {
        /// <summary>
        /// The seeded (Key, Value, Description) triples, as a SQL VALUES table. Verbatim from
        /// <c>DefaultOptionSets</c> — the two have to stay in step, because a tenant created tomorrow gets
        /// the seeder's copy and one created last year gets this one.
        /// </summary>
        private const string SeededDescriptions =
            """
            (N'REMS.Status', N'draft',
             N'With its initiator. Saved but not yet sent to the client.'),
            (N'REMS.Status', N'awaiting_customer',
             N'The intake form has been emailed. The ball is with the client.'),
            (N'REMS.Status', N'customer_submitted',
             N'The client''s answers are in and the named Admin is reviewing them.'),
            (N'REMS.Status', N'waiting_for_pickup',
             N'The client''s answers are in and the request is with the admins, but no admin has picked it up yet. Until one does, its engagement setup is nobody''s to work.'),
            (N'REMS.Status', N'returned_to_initiator',
             N'The Admin sent the engagement setup back for rework, with a reason. Client intake is read-only.'),
            (N'REMS.Status', N'awaiting_admin_confirmation',
             N'The initiator revised the setup and handed it back for the Admin to confirm.'),
            (N'REMS.Status', N'pending_approval',
             N'Routed to the approvers. Every field is read-only while the approval is open.'),
            (N'REMS.Status', N'changes_requested',
             N'Enough approvers declined. Back with the initiator to rework the setup.'),
            (N'REMS.Status', N'approved',
             N'Fully approved. Permanently read-only.'),
            (N'REMS.Type', N'brand_new_client',
             N'The client/company is working with THF for the first time. No prior record exists in the system.'),
            (N'REMS.Type', N'existing_client',
             N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client.')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[Description] = v.[Description], i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {SeededDescriptions}
                ) AS v([Key], [Value], [Description])
                    ON v.[Key] = s.[Key] AND v.[Value] = i.[Value]
                WHERE s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND (i.[Description] IS NULL OR LTRIM(RTRIM(i.[Description])) = N'');
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(
                $"""
                UPDATE i
                SET i.[Description] = NULL, i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                INNER JOIN (VALUES
                {SeededDescriptions}
                ) AS v([Key], [Value], [Description])
                    ON v.[Key] = s.[Key] AND v.[Value] = i.[Value]
                WHERE s.[Deleted] = 0
                  AND i.[Deleted] = 0
                  AND i.[Description] = v.[Description];
                """);
    }
}
