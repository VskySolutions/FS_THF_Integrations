<template>
  <div class="role-pg-panel">
    <div class="row items-center q-mb-sm">
      <div class="section-subhead">Permission Groups</div>
      <q-space />
      <q-btn outline no-caps dense color="primary" icon="o_preview" label="Preview Effective Permissions" class="q-mr-sm" @click="openPreview" />
      <q-btn unelevated no-caps dense color="primary" icon="o_add" label="Add Groups" @click="openAdd" />
    </div>

    <q-list bordered separator class="rounded-borders">
      <q-item v-for="group in assignedGroups" :key="group.id">
        <q-item-section avatar><q-icon name="o_workspaces" color="grey-7" /></q-item-section>
        <q-item-section>
          <q-item-label>
            {{ group.name }}
            <q-badge v-if="!group.isActive" color="grey" class="q-ml-xs">Inactive</q-badge>
          </q-item-label>
          <q-item-label caption>{{ group.permissionCount }} keys</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-btn flat round dense color="negative" icon="o_remove_circle_outline" @click="removeGroup(group)">
            <q-tooltip>Remove</q-tooltip>
          </q-btn>
        </q-item-section>
      </q-item>
      <q-item v-if="!assignedGroups.length">
        <q-item-section class="text-grey-6">No permission groups assigned to this role.</q-item-section>
      </q-item>
    </q-list>

    <!-- Add Groups dialog -->
    <q-dialog v-model="addOpen">
      <q-card style="min-width: 420px; max-width: 92vw;">
        <q-card-section class="text-h6">Add permission groups</q-card-section>
        <q-separator />
        <q-card-section>
          <app-select
            v-model="selectedToAdd" :options="availableOptions" label="Groups" multiple
            :loading="loadingAvailable"
          />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="addOpen = false" />
          <q-btn unelevated no-caps color="primary" label="Add" :loading="adding" :disable="!selectedToAdd.length" @click="confirmAdd" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Preview dialog: union of effective permissions, grouped by category. -->
    <q-dialog v-model="previewOpen">
      <q-card style="min-width: 460px; max-width: 92vw;">
        <q-card-section class="text-h6">Effective permissions</q-card-section>
        <q-separator />
        <q-card-section>
          <div v-if="loadingPreview" class="row flex-center q-pa-md"><q-spinner color="primary" size="28px" /></div>
          <template v-else>
            <div v-if="!previewKeys.length" class="text-grey-6">This role has no effective permissions.</div>
            <div v-for="group in previewGroups" :key="group.category" class="q-mb-md" data-test="preview-category">
              <div class="section-subhead q-mb-xs">{{ group.category }}</div>
              <div class="row q-gutter-xs">
                <q-badge v-for="key in group.keys" :key="key" color="teal-1" text-color="primary" class="pg-key">
                  {{ humanizeKey(key) }}
                  <q-tooltip>{{ key }}</q-tooltip>
                </q-badge>
              </div>
            </div>
          </template>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="primary" label="Close" @click="previewOpen = false" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
import { ref, computed, watch } from "vue";
import { roleApi, permissionGroupApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { humanizeKey, groupKeysByCategory } from "composables/usePermissionCategories";
import AppSelect from "components/common/AppSelect.vue";

const props = defineProps({
  roleId: { type: String, default: null }
});

const notify = useNotify();
const { confirm } = useConfirm();

const assignedGroups = ref([]);
const previewKeys = ref([]);

const load = async () => {
  if (!props.roleId) {
    assignedGroups.value = [];
    previewKeys.value = [];
    return;
  }
  try {
    const data = await roleApi.getGroups(props.roleId);
    assignedGroups.value = data?.groups || [];
    previewKeys.value = data?.effectivePermissions || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

watch(() => props.roleId, load, { immediate: true });

// ---- Add Groups ----
const addOpen = ref(false);
const adding = ref(false);
const selectedToAdd = ref([]);
const availableOptions = ref([]);
const loadingAvailable = ref(false);

const openAdd = async () => {
  selectedToAdd.value = [];
  addOpen.value = true;
  loadingAvailable.value = true;
  try {
    const resp = await permissionGroupApi.list({ page: 1, limit: 100, isActive: true });
    const assignedIds = new Set(assignedGroups.value.map((g) => g.id));
    availableOptions.value = (resp?.data || [])
      .filter((g) => !assignedIds.has(g.id))
      .map((g) => ({ label: g.name, value: g.id }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingAvailable.value = false;
  }
};

const confirmAdd = async () => {
  const ids = [...selectedToAdd.value];
  adding.value = true;
  try {
    const data = await roleApi.assignGroups(props.roleId, ids);
    // Optimistic-friendly: trust the server's returned snapshot, then reconcile.
    assignedGroups.value = data?.groups || assignedGroups.value;
    previewKeys.value = data?.effectivePermissions || previewKeys.value;
    addOpen.value = false;
    notify.success("Groups added to role.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    adding.value = false;
  }
};

// ---- Remove ----
const removeGroup = async (group) => {
  const ok = await confirm({
    title: "Remove permission group",
    message: `Remove "${group.name}" from this role? The role will lose any permissions only it provided.`,
    confirmLabel: "Remove",
    type: "danger"
  });
  if (!ok) return;
  // Optimistic update.
  const previous = assignedGroups.value;
  assignedGroups.value = assignedGroups.value.filter((g) => g.id !== group.id);
  try {
    const data = await roleApi.removeGroup(props.roleId, group.id);
    if (data?.effectivePermissions) previewKeys.value = data.effectivePermissions;
    notify.success("Group removed from role.");
    load();
  } catch (err) {
    assignedGroups.value = previous; // rollback
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Preview ----
const previewOpen = ref(false);
const loadingPreview = ref(false);
const previewGroups = computed(() => groupKeysByCategory(previewKeys.value));

const openPreview = async () => {
  previewOpen.value = true;
  loadingPreview.value = true;
  try {
    const data = await roleApi.previewPermissions(props.roleId);
    previewKeys.value = data?.permissions || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingPreview.value = false;
  }
};

defineExpose({ load, openPreview, removeGroup, confirmAdd, selectedToAdd, previewKeys, previewGroups, assignedGroups });
</script>

<style scoped>
.section-subhead {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: var(--q-primary);
}
.pg-key {
  font-size: 12px;
  padding: 4px 8px;
}
</style>
