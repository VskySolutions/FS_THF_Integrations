import { ref, computed } from "vue";
import { usePermissions, Permissions } from "composables/usePermissions";

// The "Show deleted?" state and permission gate a list needs to offer Deleted Records Management.
//
//   const { showDeleted, canManageDeleted } = useDeletedRecords();
//
//   <q-toggle v-if="canManageDeleted" v-model="showDeleted" label="Show deleted?" />   (filter drawer)
//   <deleted-records-panel
//     v-if="canManageDeleted" :entity-type="EntityType.Tag" :show="showDeleted" @restored="load" />
//
// Gated on records.adminDelete, which only Super Admin and Tenant Admin hold — restoring a record, and
// especially purging one, is not something an operational role should reach. The panel itself is
// self-contained: it loads only while shown and emits `restored` so the list can refresh.
//
// `showDeleted` deliberately does NOT persist across visits like the pool's scope does. Deleted records
// are somewhere you go on purpose to fix something, not a view to live in, and a list quietly showing a
// purge button on every future visit is the kind of thing that ends in a mis-click.
export function useDeletedRecords () {
  const { has } = usePermissions();
  const showDeleted = ref(false);
  const canManageDeleted = computed(() => has(Permissions.RecordsAdminDelete));
  return { showDeleted, canManageDeleted };
}
