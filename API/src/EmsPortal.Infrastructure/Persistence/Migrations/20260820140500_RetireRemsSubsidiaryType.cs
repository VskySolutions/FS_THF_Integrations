using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Retires the <c>REMS.Type</c> answer "Subsidiary / Child of Existing Client", leaving two ways a
    /// referral can relate to THF's records: a brand-new client, or a new engagement for one we already
    /// have.
    /// <para>
    /// The distinction was carried by a FIELD, not by the type. What made a request a subsidiary was the
    /// Parent Client named on it, and that field now hangs off "New Engagement, Existing Client" — where a
    /// subsidiary always belonged, since a child of an existing client IS an engagement for a client we
    /// already have. Naming a parent is what says the referral is a child; the third answer only ever asked
    /// the partner to say the same thing twice, and to say it BEFORE they had looked the parent up.
    /// </para>
    /// <para>
    /// Nothing is lost in the move: every re-pointed request keeps its ParentClientReferenceId and
    /// ParentClientName, which is the whole of what the retired answer recorded. Reversible, though the
    /// requests re-pointed here stay on <c>existing_client</c> — see <c>Down</c>.
    /// </para>
    /// </summary>
    public partial class RetireRemsSubsidiaryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters, as it did in MergeRemsExistingClientTypes: re-point the data first, so no
            // request is left holding a value that is about to disappear from the list.

            // 1. Every request that answered "subsidiary" becomes "new engagement, existing client" — the
            //    answer it was always a special case of. UpdatedOnUtc is deliberately left alone: this is
            //    a change to what the platform offers, not an edit anybody made to the request.
            migrationBuilder.Sql(
                """
                UPDATE [REMS]
                SET [Type] = N'existing_client'
                WHERE [Type] = N'subsidiary_child_of_existing_client';
                """);

            // 2. Retire the value itself, in the platform standard list and in every tenant's own copy.
            //    Soft-delete, per the platform convention.
            //
            //    Guarded on the VALUE alone — unlike the relabels in MergeRemsExistingClientTypes, which
            //    spare a tenant who had made the list their own. A tenant who renamed this item still
            //    cannot keep it: the API no longer accepts the code behind it, so leaving it on their
            //    picker would only offer an answer that fails to save.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 1, i.[DeletedOnUtc] = SYSUTCDATETIME(), i.[IsActive] = 0,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'subsidiary_child_of_existing_client';
                """);

            // 3. The surviving answer's tooltip now has to mention the parent, since that is where a
            //    subsidiary is recorded. Guarded on the seeded text, per the convention: a tenant who
            //    rewrote this description has made it theirs and keeps what they wrote.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client. Name the parent client if this one is a subsidiary or child of it.',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client'
                  AND i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the value to every list it was retired from. The requests re-pointed by step 1 stay
            // on 'existing_client': which of them started out as subsidiaries is not recoverable once
            // merged — and each one still carries the parent that says so, which reads correctly under
            // either answer.
            migrationBuilder.Sql(
                """
                UPDATE i
                SET i.[Deleted] = 0, i.[DeletedOnUtc] = NULL, i.[IsActive] = 1,
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 1
                  AND i.[Value] = N'subsidiary_child_of_existing_client';

                UPDATE i
                SET i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client.',
                    i.[UpdatedOnUtc] = SYSUTCDATETIME()
                FROM [OptionSetItems] i
                INNER JOIN [OptionSets] s ON s.[Id] = i.[OptionSetId]
                WHERE s.[Key] = N'REMS.Type' AND s.[Deleted] = 0 AND i.[Deleted] = 0
                  AND i.[Value] = N'existing_client'
                  AND i.[Description] = N'The person or company already has an active client record with THF, and this request creates an additional engagement under that same client. Name the parent client if this one is a subsidiary or child of it.';
                """);
        }
    }
}
