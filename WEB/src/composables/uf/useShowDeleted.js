import { ref } from "vue";
import { usePermissions, Permissions } from "composables/usePermissions";

// Drives the admin-only "Show Deleted" toggle on a list page. When enabled, the host page re-fetches
// with includeDeleted=true. Exposes helpers to detect deleted rows and retention-overdue rows.
//
//   const { canShowDeleted, showDeleted, toggle, isDeleted } = useShowDeleted(reload);
//
// The host page passes showDeleted.value into its fetcher (as includeDeleted) and reloads via `reload`.
export function useShowDeleted (reload) {
  const { has } = usePermissions();
  const canShowDeleted = has(Permissions.RecordsAdminDelete);
  const showDeleted = ref(false);

  const toggle = (value) => {
    showDeleted.value = typeof value === "boolean" ? value : !showDeleted.value;
    if (reload) reload();
  };

  const isDeleted = (row) => !!(row?.deleted || row?.isDeleted || row?.deletedOnUtc);
  const isOverdue = (row) => !!row?.isRetentionOverdue;

  return { canShowDeleted, showDeleted, toggle, isDeleted, isOverdue };
}
