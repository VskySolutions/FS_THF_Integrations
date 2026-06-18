<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Roles' }]"
      :search="search"
      show-search
      search-placeholder="Search roles"
      show-add
      add-label="Create Role"
      show-back
      @update:search="search = $event"
      @add="openCreate"
      @back="$router.back()"
    />

    <app-data-table
      page-key="roles"
      row-key="id"
      title="Roles"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      @request="onRequest"
      @refresh="load"
    >
      <template #body-cell-isSystem="cell">
        <q-td :props="cell">
          <q-badge :color="cell.value ? 'blue-grey' : 'primary'">{{ cell.value ? "System" : "Custom" }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense color="primary" icon="o_edit" @click="openEdit(cell.row)">
            <q-tooltip>{{ cell.row.isSystem ? "Edit permissions" : "Edit" }}</q-tooltip>
          </q-btn>
          <q-btn v-if="!cell.row.isSystem" flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 180px;">
                <q-item clickable @click="openTenants(cell.row)">
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

    <!-- Create / edit role -->
    <app-form-drawer
      v-model="formOpen" :title="editingId ? 'Edit Role' : 'Create Role'"
      :saving="saving" save-label="Save" @submit="submitForm" @cancel="resetForm"
    >
      <q-form ref="formRef" greedy>
        <q-input
          v-model="form.name" outlined stack-label hide-bottom-space label="Name *" class="q-mb-md"
          :readonly="nameLocked" :hint="nameLocked ? 'System role names are fixed; permissions can still be tuned.' : undefined"
          :rules="[(v) => !!v || 'Name is required']"
        />
        <q-input
          v-model="form.description" outlined stack-label hide-bottom-space label="Description" class="q-mb-md"
          type="textarea" autogrow
        />
        <app-select
          v-model="form.permissions" :options="permissionOptions" label="Permissions" multiple
          :loading="loadingPermissions"
        />
      </q-form>

      <!-- Role ↔ Permission Group composition (WO-70): only for an existing role. -->
      <template v-if="editingId">
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
import { ref, reactive, watch } from "vue";
import { debounce } from "quasar";
import { roleApi, tenantApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import RolePermissionGroupsPanel from "modules/permission-group/components/RolePermissionGroupsPanel.vue";

const notify = useNotify();
const { confirm } = useConfirm();

const columns = [
  { name: "name", label: "Name", field: "name", align: "left", sortable: true, default: true },
  { name: "description", label: "Description", field: "description", align: "left", default: true },
  { name: "isSystem", label: "Type", field: "isSystem", align: "left", sortable: true, default: true },
  { name: "permissionCount", label: "Permissions", field: "permissionCount", align: "left", sortable: true, default: true },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
];

const { rows, loading, totalRecords, search, pagination, load, onRequest } = useListTable({
  fetcher: () => roleApi.list({ search: search.value || undefined }).then((r) => ({ data: r || [], total: (r || []).length })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

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
const form = reactive({ name: "", description: "", permissions: [] });

const resetForm = () => {
  editingId.value = null;
  nameLocked.value = false;
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
  await loadPermissions();
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
