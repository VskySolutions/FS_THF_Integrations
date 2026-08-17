// NotificationType enum mirror (backend EmsPortal.Domain.Enums.NotificationType).
//
// EVERY VALUE HERE IS ONE THE BACKEND ACTUALLY SENDS. This list becomes the type filter on the
// notifications page, so a value the server can never raise is a filter that always finds nothing.
// Retired numbers, never to be reused: 3, 4, 5 (sync notifications, from an integration that no longer
// exists), 6 (a generic "System" catch-all nothing raised) and 14 (approval resubmitted — a resubmitted
// round arrives as RemsApprovalRequested with a different title).
export const NotificationType = Object.freeze({
  Mention: 1,
  ReminderDue: 2,
  // REMS (Phase 15, WO-111..114). All in-app only; each deep-links to the REMS request via EntityType.Rems.
  RemsRequestAssigned: 7,
  RemsCseAssigned: 8,
  RemsFormSent: 9,
  RemsFormSubmitted: 10,
  RemsApprovalRequested: 11,
  RemsEngagementApproved: 12,
  RemsEngagementRejected: 13,
  RemsRequestSubmitted: 15,
  RemsRequestPickedUp: 16
});

const META = {
  [NotificationType.Mention]: { label: "Mention", icon: "o_alternate_email", color: "primary" },
  [NotificationType.ReminderDue]: { label: "Reminder", icon: "o_alarm", color: "orange-8" },
  [NotificationType.RemsRequestAssigned]: { label: "REMS request assigned", icon: "o_assignment_ind", color: "primary" },
  [NotificationType.RemsCseAssigned]: { label: "CSE assigned", icon: "o_support_agent", color: "primary" },
  [NotificationType.RemsFormSent]: { label: "EMS form sent", icon: "o_send", color: "teal-7" },
  [NotificationType.RemsFormSubmitted]: { label: "EMS form submitted", icon: "o_assignment_turned_in", color: "deep-purple-6" },
  [NotificationType.RemsApprovalRequested]: { label: "Approval requested", icon: "o_approval", color: "orange-8" },
  [NotificationType.RemsEngagementApproved]: { label: "Engagement approved", icon: "o_verified", color: "positive" },
  [NotificationType.RemsEngagementRejected]: { label: "Engagement rejected", icon: "o_cancel", color: "negative" },
  // Both are named for what they carry NOW, not for the pool submissions and pickups the numbers were
  // minted for — that pool is gone, and a filter labelled "Waiting for pickup" would find only send-backs.
  [NotificationType.RemsRequestSubmitted]: { label: "Sent back for rework", icon: "o_assignment_return", color: "amber-8" },
  [NotificationType.RemsRequestPickedUp]: { label: "Assignment & rework updates", icon: "o_how_to_reg", color: "teal-7" }
};

const FALLBACK = { label: "Notification", icon: "o_notifications", color: "grey-7" };

export function useNotificationMeta () {
  const metaFor = (type) => META[Number(type)] || FALLBACK;
  // The full set of types for the preferences matrix.
  const allTypes = Object.values(NotificationType).map((value) => ({ value, ...(META[value] || FALLBACK) }));
  return { metaFor, allTypes, NotificationType };
}
