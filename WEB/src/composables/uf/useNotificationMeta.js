// NotificationType enum mirror (backend EmsPortal.Domain.Enums.NotificationType).
export const NotificationType = Object.freeze({
  Mention: 1,
  ReminderDue: 2,
  SyncCompleted: 4,
  SyncFailed: 5,
  System: 6
});

const META = {
  [NotificationType.Mention]: { label: "Mention", icon: "o_alternate_email", color: "primary" },
  [NotificationType.ReminderDue]: { label: "Reminder", icon: "o_alarm", color: "orange-8" },
  [NotificationType.SyncCompleted]: { label: "Sync completed", icon: "o_cloud_done", color: "positive" },
  [NotificationType.SyncFailed]: { label: "Sync failed", icon: "o_error", color: "negative" },
  [NotificationType.System]: { label: "System", icon: "o_notifications", color: "grey-7" }
};

const FALLBACK = { label: "Notification", icon: "o_notifications", color: "grey-7" };

export function useNotificationMeta () {
  const metaFor = (type) => META[Number(type)] || FALLBACK;
  // The full set of types for the preferences matrix.
  const allTypes = Object.values(NotificationType).map((value) => ({ value, ...(META[value] || FALLBACK) }));
  return { metaFor, allTypes, NotificationType };
}
