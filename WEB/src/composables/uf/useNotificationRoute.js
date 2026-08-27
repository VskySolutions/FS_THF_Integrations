// Where a notification sends its reader.
//
// Most of them go to the record they name, which is what `useEntityMeta.routeFor` already answers. The
// REMS approval ones are the exception: a REMS notification carries the REQUEST id — the one id every
// recipient of it has in common — and for the initiator, the CSE and the admin that is exactly right.
// For an APPROVER it is not. They were written to because a decision is being asked of them, and the
// request detail is not where they make it; their own task is. Which task that is differs per reader, so
// it cannot travel on the notification and is resolved here instead.
//
// The lookup doubles as the test of who is reading. "Approved" and "Declined" go out to the initiator,
// the CSE, the admin AND every approver on the round, and nothing on the row says which of them opened
// it — so rather than guess, this asks for a task and lets the answer decide: one comes back for an
// approver, a 404 for everybody else, who go to the request as before.
import { useEntityMeta } from "composables/uf/useEntityMeta";
import { NotificationType } from "composables/uf/useNotificationMeta";
import { remsApi, EntityType } from "services/api";

// The three REMS types an approver can be a recipient of. RemsApprovalRequested is only ever sent to
// approvers; the other two are sent to a mixed set, and the lookup sorts them out.
const APPROVER_TYPES = new Set([
  NotificationType.RemsApprovalRequested,
  NotificationType.RemsEngagementApproved,
  NotificationType.RemsEngagementRejected
]);

export function useNotificationRoute () {
  const { routeFor } = useEntityMeta();

  // The route to open for a notification row. Async because the approver case needs the task resolved;
  // every other notification answers without a request.
  const routeForNotification = async (n) => {
    const fallback = routeFor(n.entityType, n.entityId);
    if (Number(n.entityType) !== EntityType.Rems || !APPROVER_TYPES.has(Number(n.type))) {
      return fallback;
    }
    // A failure here is not worth an error: the request is a correct destination for this reader too,
    // just not the best one. Falling back beats stranding them on the notification they clicked.
    const ref = await remsApi.myApprovalTaskForRequest(n.entityId).catch(() => null);
    return ref?.taskId
      ? { name: "rems_approval_task", params: { taskId: ref.taskId } }
      : fallback;
  };

  return { routeForNotification };
}
