using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmsPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRemsRequestApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AlignRemsRequestStatusOptions taught the request lifecycle its approval stages, but only for
            // transitions made from then on: RemsApprovalController advances REMS.Status when a round is
            // sent, resubmitted, approved or rejected. Requests routed for approval BEFORE that are still
            // sitting on 'customer_submitted' — which is why the EMS Inbox shows "Engagement Setup" for an
            // engagement that has been with its approvers for days.
            //
            // Recompute those rows from the engagements underneath them, applying exactly the roll-up in
            // RemsApprovalController.SyncRequestStatusAsync — all approved wins, then any rejected (rework
            // is the state someone has to act on), then any pending:
            //
            //   every engagement Approved        -> approved
            //   else any engagement Rejected     -> changes_requested
            //   else any engagement PendingApproval -> pending_approval
            //   else                             -> left alone (still genuinely in setup)
            //
            // Scoped to requests currently on 'customer_submitted', so a status the new code has already set
            // correctly is never rewritten. REMSEngagement.Status is persisted as the enum NAME.
            //
            // UpdatedOnUtc/UpdatedById are deliberately NOT touched: this corrects a status the system should
            // have been keeping itself, and stamping it would attribute an edit at deploy time to whoever
            // happened to touch the request last.
            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.[Status] = CASE
                        WHEN s.Approved = s.Total THEN N'approved'
                        WHEN s.Rejected > 0       THEN N'changes_requested'
                        ELSE N'pending_approval'
                    END
                FROM [REMS] r
                INNER JOIN (
                    SELECT c.[REMSId] AS RemsId,
                           COUNT(*) AS Total,
                           SUM(CASE WHEN e.[Status] = N'Approved'        THEN 1 ELSE 0 END) AS Approved,
                           SUM(CASE WHEN e.[Status] = N'Rejected'        THEN 1 ELSE 0 END) AS Rejected,
                           SUM(CASE WHEN e.[Status] = N'PendingApproval' THEN 1 ELSE 0 END) AS Pending
                    FROM [REMSEngagement] e
                    INNER JOIN [REMSEntity] n ON n.[Id] = e.[REMSEntityId] AND n.[Deleted] = 0
                    INNER JOIN [REMSClient] c ON c.[Id] = n.[REMSClientId] AND c.[Deleted] = 0
                    WHERE e.[Deleted] = 0
                    GROUP BY c.[REMSId]
                ) s ON s.RemsId = r.[Id]
                WHERE r.[Deleted] = 0
                  AND r.[Status] = N'customer_submitted'
                  AND (s.Approved = s.Total OR s.Rejected > 0 OR s.Pending > 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Put the corrected rows back to the stage they were stuck on. Requests that reached these
            // statuses legitimately are indistinguishable from backfilled ones after the fact, so this
            // returns every approval-stage request to 'customer_submitted' — which is exactly the state the
            // pre-AlignRemsRequestStatusOptions code produced, and what its own Down() assumes.
            migrationBuilder.Sql(
                """
                UPDATE [REMS]
                SET [Status] = N'customer_submitted'
                WHERE [Deleted] = 0
                  AND [Status] IN (N'pending_approval', N'changes_requested', N'approved');
                """);
        }
    }
}
