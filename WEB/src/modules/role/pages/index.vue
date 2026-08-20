<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Roles' }]"
      :search="search"
      show-search
      search-placeholder="Search roles"
      show-filters
      :filter-count="filterChips.length"
      show-add
      add-label="Create Role"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @add="openCreate"
      @back="$router.back()"
    />

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-column-filters v-model="filters" :columns="filterableColumns" />
      <q-toggle
        v-if="canManageDeleted" v-model="showDeleted" label="Show deleted?" dense class="q-mt-md"
      />
    </app-filter-drawer>

    <app-data-table
      page-key="roles"
      row-key="id"
      title="Roles"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :total-records="filteredRows.length"
      :pagination="pagination"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-isSystem="cell">
        <q-td :props="cell">
          <q-badge :color="cell.value ? 'blue-grey' : 'primary'">{{ cell.value ? "System" : "Custom" }}</q-badge>
        </q-td>
      </template>

      <!-- A role the caller does not own opens read-only: seeing what a platform role grants is part of
           deciding who to give it to, changing it is a Super Admin's call. -->
      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn
            flat round dense color="primary" :icon="cell.row.canManage ? 'o_edit' : 'o_visibility'"
            @click="openEdit(cell.row)"
          >
            <q-tooltip>{{ actionTooltip(cell.row) }}</q-tooltip>
          </q-btn>
          <q-btn v-if="cell.row.canManage && !cell.row.isSystem" flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 180px;">
                <!-- Availability spans tenants, so it is the platform owner's to set. -->
                <q-item v-if="isSuperAdmin && !cell.row.tenantId" clickable @click="openTenants(cell.row)">
                  <q-item-section avatar><q-icon name="o_apartment" /></q-item-section>
                  <q-item-section>Manage Tenants</q-item-section>
                </q-item>
                <q-item clickable @click="removeRole(cell.row)">
                  <q-item-section avatar><q-icon name="o_delete" color="negative" /></q-item-section>
                  <q-item-section class="text-negative">Delete</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>
    </app-data-table>

    <deleted-records-panel
      v-if="canManageDeleted" :entity-type="EntityType.Role" :show="showDeleted" @restored="load"
    />

    <!-- Create / edit role, or view one that belongs to somebody else -->
    <app-form-drawer
      v-model="formOpen" :title="drawerTitle" :hide-save="viewOnly"
      :saving="saving" save-label="Save" @submit="submitForm" @cancel="resetForm"
    >
      <div v-if="viewOnly" class="text-body2 text-grey-7 q-mb-md">
        This role belongs to the platform and is offered in every tenant, so only a Super Admin can
        change it. Create a role of your own to grant a different set of permissions.
      </div>

      <q-form ref="formRef" greedy>
        <q-input
          v-model="form.name" outlined stack-label hide-bottom-space label="Name *" class="q-mb-md"
          :readonly="nameLocked || viewOnly" :hint="nameHint"
          :rules="[(v) => !!v || 'Name is required']"
        />
        <app-rich-text-field
          v-model="form.description" label="Description" class="q-mb-md" :readonly="viewOnly"
        />
        <!-- Read-only, the keys are listed rather than put in a picker: the catalogue a tenant admin is
             offered stops at their ceiling, so a platform role's wider set has no options to map to. -->
        <div v-if="viewOnly">
          <div class="text-caption text-grey-7 q-mb-xs">Permissions</div>
          <div v-if="form.permissions.length" class="row q-gutter-xs">
            <q-chip
              v-for="permission in form.permissions" :key="permission"
              dense square color="grey-3" text-color="grey-9"
            >
              {{ prettyPermission(permission) }}
            </q-chip>
          </div>
          <div v-else class="text-body2 text-grey-6">This role grants no permissions of its own.</div>
        </div>
        <app-select
          v-else v-model="form.permissions" :options="permissionOptions" label="Permissions" multiple
          :loading="loadingPermissions"
          :info="isSuperAdmin ? '' : 'The list stops at what your own tenant can hand out.'"
        />
      </q-form>

      <!-- Role ↔ Permission Group composition (WO-70): only for an existing role you may change. -->
      <template v-if="editingId && !viewOnly">
        <q-separator class="q-my-md" />
        <role-permission-groups-panel :role-id="editingId" />
      </template>
    </app-form-drawer>

    <!-- Tenant availability -->
    <app-form-drawer v-model="tenantsOpen" title="Available to tenants" :saving="tenantsSaving" @submit="submitTenants" @cancel="resetTenants">
      <div class="text-body2 text-grey-7 q-mb-md">Select the tenants this role can be assigned within.</div>
      <app-select
        v-model="selectedTenantIds" :options="tenantOptions" label="Tenants" multiple
        :loading="loadingTenants"
      />
    </app-form-drawer>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, watch } from "vue";
import { debounce } from "quasar";
import { roleApi, tenantApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes, EntityType } from "services/api";
import { useAuthStore } from "stores/auth";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDeletedRecords } from "composables/useDeletedRecords";
import { useAuditColumns } from "composables/useAuditColumns";

import AppDataTable from "components/common/AppDataTable.vue";
import DeletedRecordsPanel from "components/universal/DeletedRecordsPanel.vue";
import { stripHtml } from "utils/richText";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppRichTextField from "components/common/AppRichTextField.vue";
import RolePermissionGroupsPanel from "modules/permission-group/components/RolePermissionGroupsPanel.vue";

const auditColumns = useAuditColumns();
const { showDeleted, canManageDeleted } = useDeletedRecords();
const notify = useNotify();
const { confirm } = useConfirm();
const authStore = useAuthStore();

// The list shows every role this caller may see: the platform ones (theirs to read, a Super Admin's to
// change) and the ones their own tenant created. Which of the two a row is comes from the server as
// `canManage` — the same rule it enforces on save, rather than a second copy of it here.
const isSuperAdmin = computed(() => authStore.roles.includes("SuperAdmin"));

const columns = [
  { name: "name", label: "Name", field: "name", align: "left", sortable: true, default: true },
  // Descriptions are rich text; the cell shows the text without its markup (see utils/richText).
  { name: "description", label: "Description", field: (r) => stripHtml(r.description), align: "left", default: true },
  {
    name: "isSystem",
    label: "Type",
    field: "isSystem",
    align: "left",
    sortable: true,
    default: true,
    filterOptions: [{ label: "System", value: true }, { label: "Custom", value: false }]
  },
  // Who the role belongs to. A platform role is offered in every tenant and only a Super Admin may
  // change it; the rest were created by a tenant for itself and are its own to maintain.
  {
    name: "scope",
    label: "Scope",
    field: (r) => r.tenantName || "Platform",
    align: "left",
    sortable: true,
    default: true
  },
  { name: "permissionCount", label: "Permissions", field: "permissionCount", align: "left", sortable: true, default: true, filterable: false },
  ...auditColumns(),
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

const { rows, loading, search, pagination, load, onRequest } = useListTable({
  fetcher: () => roleApi.list({ search: search.value || undefined }).then((r) => ({ data: r || [], total: (r || []).length })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Client-side column filters (the list loads all roles); badge/count standard via AppListHeader.
const filterOpen = ref(false);
const { filters, filterableColumns, filteredRows, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: false });

// Server-side search: reload (debounced, first page) when it changes.
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch(search, reload);

// ---- Permission catalogue ----
const permissionOptions = ref([]);
const loadingPermissions = ref(false);
const prettyPermission = (key) => key.replace(/_/g, " ").replace(/\./g, " · ");

const loadPermissions = async () => {
  if (permissionOptions.value.length) return;
  loadingPermissions.value = true;
  try {
    const perms = await roleApi.permissions();
    permissionOptions.value = (perms || []).map((p) => ({ label: prettyPermission(p), value: p }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingPermissions.value = false;
  }
};

// ---- Create / edit ----
const formOpen = ref(false);
const saving = ref(false);
const formRef = ref(null);
const editingId = ref(null);
const nameLocked = ref(false); // system role names are fixed, but permissions are still editable
const viewOnly = ref(false); // a platform role opened by someone who may read it but not change it
const form = reactive({ name: "", description: "", permissions: [] });

const drawerTitle = computed(() => {
  if (viewOnly.value) return "Role";
  return editingId.value ? "Edit Role" : "Create Role";
});

const nameHint = computed(() =>
  nameLocked.value && !viewOnly.value ? "System role names are fixed; permissions can still be tuned." : undefined);

const actionTooltip = (row) => {
  if (!row.canManage) return "View";
  return row.isSystem ? "Edit permissions" : "Edit";
};

const resetForm = () => {
  editingId.value = null;
  nameLocked.value = false;
  viewOnly.value = false;
  form.name = "";
  form.description = "";
  form.permissions = [];
};

const openCreate = async () => {
  resetForm();
  await loadPermissions();
  formOpen.value = true;
};

const openEdit = async (row) => {
  resetForm();
  viewOnly.value = !row.canManage;
  // Nothing to pick from in view mode: the keys are listed as they are.
  if (!viewOnly.value) {
    await loadPermissions();
  }
  editingId.value = row.id;
  nameLocked.value = !!row.isSystem; // system role names are fixed; permissions stay editable
  try {
    const role = await roleApi.get(row.id);
    form.name = role.name;
    form.description = role.description || "";
    form.permissions = role.permissions || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    return;
  }
  formOpen.value = true;
};

const submitForm = async ({ clearDraft } = {}) => {
  if (viewOnly.value) return; // no Save is rendered in view mode; this is the belt to that brace
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    const payload = { name: form.name, description: form.description, permissions: form.permissions };
    if (editingId.value) {
      await roleApi.update(editingId.value, payload);
    } else {
      await roleApi.create(payload);
    }
    clearDraft?.();
    formOpen.value = false;
    notify.success("Role saved.");
    load();
  } catch (err) {
    if (getApiErrorCode(err) === ApiErrorCodes.DuplicateIdentifier) {
      notify.error("A role with that name already exists.");
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    saving.value = false;
  }
};

const removeRole = async (row) => {
  const ok = await confirm({
    title: "Delete role",
    message: `Delete the "${row.name}" role? Users keeping this role will lose its permissions.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await roleApi.remove(row.id);
    notify.success("Role deleted.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Tenant availability ----
const tenantsOpen = ref(false);
const tenantsSaving = ref(false);
const tenantOptions = ref([]);
const loadingTenants = ref(false);
const selectedTenantIds = ref([]);
const originalTenantIds = ref([]);
const tenantsRoleId = ref(null);

// Clear the tenant-availability editor (closing/cancelling discards the selection).
const resetTenants = () => {
  tenantsRoleId.value = null;
  selectedTenantIds.value = [];
  originalTenantIds.value = [];
};

const openTenants = async (row) => {
  tenantsRoleId.value = row.id;
  selectedTenantIds.value = [];
  originalTenantIds.value = [];
  loadingTenants.value = true;
  tenantsOpen.value = true;
  try {
    const [tenantsResp, current] = await Promise.all([
      tenantApi.list({ page: 1, limit: 100 }),
      roleApi.roleTenants(row.id)
    ]);
    tenantOptions.value = (tenantsResp?.data || []).map((t) => ({ label: t.name, value: t.tenantId }));
    originalTenantIds.value = current || [];
    selectedTenantIds.value = [...originalTenantIds.value];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingTenants.value = false;
  }
};

const submitTenants = async () => {
  const roleId = tenantsRoleId.value;
  const selected = selectedTenantIds.value;
  const original = originalTenantIds.value;
  const toAdd = selected.filter((id) => !original.includes(id));
  const toRemove = original.filter((id) => !selected.includes(id));
  tenantsSaving.value = true;
  try {
    await Promise.all([
      ...toAdd.map((tenantId) => roleApi.assignToTenant(tenantId, roleId)),
      ...toRemove.map((tenantId) => roleApi.unassignFromTenant(tenantId, roleId))
    ]);
    tenantsOpen.value = false;
    notify.success("Tenant availability updated.");
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    tenantsSaving.value = false;
  }
};
</script>
