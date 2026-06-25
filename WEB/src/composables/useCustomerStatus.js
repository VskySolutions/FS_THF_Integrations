// Shared customer-workflow status presentation (WO-65). Centralised so the list, detail and any
// future views colour and label the same status string identically.
//
// Status strings (mirror the API contract): Draft, Submitted, UnderReview, PendingApproval,
// PartiallyApproved, Approved, SyncInProgress, Synced, Rejected, Returned, Failed.

import { usePermissions, Permissions } from "composables/usePermissions";

const STATUS_COLORS = Object.freeze({
  Draft: "grey",
  Submitted: "blue",
  UnderReview: "orange",
  PendingApproval: "orange",
  PartiallyApproved: "orange",
  Approved: "positive",
  SyncInProgress: "blue",
  Synced: "positive",
  Rejected: "negative",
  Returned: "warning",
  Failed: "negative"
});

// Human-friendly labels (split PascalCase into words).
const STATUS_LABELS = Object.freeze({
  Draft: "Draft",
  Submitted: "Submitted",
  UnderReview: "Under Review",
  PendingApproval: "Pending Approval",
  PartiallyApproved: "Partially Approved",
  Approved: "Approved",
  SyncInProgress: "Sync In Progress",
  Synced: "Synced",
  Rejected: "Rejected",
  Returned: "Returned",
  Failed: "Failed"
});

// The high-level workflow stages, in order, for the detail-page timeline/stepper.
export const CUSTOMER_STAGES = Object.freeze([
  { key: "Draft", label: "Draft" },
  { key: "Submitted", label: "Submitted" },
  { key: "UnderReview", label: "Under Review" },
  { key: "PendingApproval", label: "Pending Approval" },
  { key: "Approved", label: "Approved" },
  { key: "Synced", label: "Synced" }
]);

export function customerStatusColor (status) {
  return STATUS_COLORS[status] || "grey";
}

export function customerStatusLabel (status) {
  return STATUS_LABELS[status] || status || "—";
}

export function useCustomerStatus () {
  const { has } = usePermissions();

  // A submitted request is the reviewer's queue item: data-entry users see "Submitted" (blue,
  // confirming their action), while users with the review capability see "Waiting For Review" in a
  // distinct colour so it stands out as an item awaiting their action.
  const isReviewerQueueItem = (status) => status === "Submitted" && has(Permissions.CustomersReview);

  const roleAwareStatusLabel = (status) =>
    isReviewerQueueItem(status) ? "Waiting For Review" : customerStatusLabel(status);

  const roleAwareStatusColor = (status) =>
    isReviewerQueueItem(status) ? "purple" : customerStatusColor(status);

  return { customerStatusColor: roleAwareStatusColor, customerStatusLabel: roleAwareStatusLabel, CUSTOMER_STAGES };
}
