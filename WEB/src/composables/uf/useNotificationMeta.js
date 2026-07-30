// NotificationType enum mirror (backend EmsPortal.Domain.Enums.NotificationType).
export const NotificationType = Object.freeze({
  Mention: 1,
  ReminderDue: 2,
  SyncCompleted: 4,
  SyncFailed: 5,
  System: 6,
  // REMS (Phase 15, WO-111..114). All in-app only; each deep-links to the REMS request via EntityType.Rems.
  RemsRequestAssigned: 7,
  RemsCseAssigned: 8,
  RemsFormSent: 9,
  RemsFormSubmitted: 10,
  RemsApprovalRequested: 11,
  RemsEngagementApproved: 12,
  RemsEngagementRejected: 13,
  RemsApprovalResubmitted: 14
});

const META = {
  [NotificationType.Mention]: { label: "Mention", icon: "o_alternate_email", color: "primary" },
  [NotificationType.ReminderDue]: { label: "Reminder", icon: "o_alarm", color: "orange-8" },
  [NotificationType.SyncCompleted]: { label: "Sync completed", icon: "o_cloud_done", color: "positive" },
  [NotificationType.SyncFailed]: { label: "Sync failed", icon: "o_error", color: "negative" },
  [NotificationType.System]: { label: "System", icon: "o_notifications", color: "grey-7" },
  [NotificationType.RemsRequestAssigned]: { label: "REMS request assigned", icon: "o_assignment_ind", color: "primary" },
  [NotificationType.RemsCseAssigned]: { label: "CSE assigned", icon: "o_support_agent", color: "primary" },
  [NotificationType.RemsFormSent]: { label: "EMS form sent", icon: "o_send", color: "teal-7" },
  [NotificationType.RemsFormSubmitted]: { label: "EMS form submitted", icon: "o_assignment_turned_in", color: "deep-purple-6" },
  [NotificationType.RemsApprovalRequested]: { label: "Approval requested", icon: "o_approval", color: "orange-8" },
  [NotificationType.RemsEngagementApproved]: { label: "Engagement approved", icon: "o_verified", color: "positive" },
  [NotificationType.RemsEngagementRejected]: { label: "Engagement rejected", icon: "o_cancel", color: "negative" },
  [NotificationType.RemsApprovalResubmitted]: { label: "Approval resubmitted", icon: "o_restart_alt", color: "primary" }
};

const FALLBACK = { label: "Notification", icon: "o_notifications", color: "grey-7" };

export function useNotificationMeta () {
  const metaFor = (type) => META[Number(type)] || FALLBACK;
  // The full set of types for the preferences matrix.
  const allTypes = Object.values(NotificationType).map((value) => ({ value, ...(META[value] || FALLBACK) }));
  return { metaFor, allTypes, NotificationType };
}
