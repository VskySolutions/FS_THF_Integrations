<template>
  <q-page padding>
    <app-list-header
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Users' }]"
      :search="search"
      show-search
      search-placeholder="Search name or email"
      show-filters
      :filter-count="filterChips.length"
      :show-add="canCreate"
      add-label="Create User"
      show-back
      @update:search="search = $event"
      @filters="filterOpen = true"
      @add="openCreate"
      @back="$router.back()"
    />

    <app-filter-drawer v-model="filterOpen" :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <app-select v-model="filters.status" :options="statusFilterOptions" label="Status" />
    </app-filter-drawer>

    <app-data-table
      page-key="users"
      row-key="userId"
      title="All users"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :total-records="totalRecords"
      :pagination="pagination"
      selectable
      @request="onRequest"
      @refresh="load"
      @update:selected="selected = $event"
    >
      <template #bulk-actions="{ selected: sel }">
        <q-btn v-if="has(Permissions.UsersWrite)" flat dense no-caps color="positive" label="Activate" @click="bulkSetStatus(sel, true)" />
        <q-btn v-if="has(Permissions.UsersWrite)" flat dense no-caps color="negative" label="Deactivate" @click="bulkSetStatus(sel, false)" />
      </template>

      <template #body-cell-isActive="cell">
        <q-td :props="cell">
          <q-badge :color="cell.value ? 'positive' : 'grey'">{{ cell.value ? "Active" : "Inactive" }}</q-badge>
        </q-td>
      </template>

      <template #body-cell-actions="cell">
        <q-td :props="cell" class="text-right">
          <q-btn flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 170px;">
                <q-item clickable :to="{ name: 'user_detail', params: { id: cell.row.userId } }">
                  <q-item-section avatar><q-icon name="o_visibility" /></q-item-section>
                  <q-item-section>View / Manage</q-item-section>
                </q-item>
                <q-item v-if="has(Permissions.UsersWrite) && !cell.row.isActive" clickable @click="setStatus(cell.row, true)">
                  <q-item-section avatar><q-icon name="o_check_circle" /></q-item-section>
                  <q-item-section>Activate</q-item-section>
                </q-item>
                <q-item v-if="has(Permissions.UsersWrite) && cell.row.isActive" clickable @click="setStatus(cell.row, false)">
                  <q-item-section avatar><q-icon name="o_block" /></q-item-section>
                  <q-item-section>Deactivate</q-item-section>
                </q-item>
                <q-item v-if="has(Permissions.UsersResetPassword)" clickable @click="resetPassword(cell.row)">
                  <q-item-section avatar><q-icon name="o_lock_reset" /></q-item-section>
                  <q-item-section>Reset Password</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </q-td>
      </template>
    </app-data-table>

    <!-- Create user -->
    <app-form-drawer v-model="formOpen" title="Create User" :saving="saving" @submit="submitForm" @cancel="resetForm">
      <q-form ref="formRef" greedy>
        <q-input
          v-model="form.email" outlined stack-label hide-bottom-space type="email" label="Email *" class="q-mb-md"
          :error="!!emailError" :error-message="emailError"
          :rules="[(v) => !!v || 'Email is required', (v) => /.+@.+\..+/.test(v) || 'Enter a valid email']"
        />
        <div class="row q-col-gutter-md q-mb-md">
          <q-input
            v-model="form.firstName" outlined stack-label hide-bottom-space label="First Name *" class="col"
            :rules="[(v) => !!v || 'First name is required']"
          />
          <q-input
            v-model="form.lastName" outlined stack-label hide-bottom-space label="Last Name *" class="col"
            :rules="[(v) => !!v || 'Last name is required']"
          />
        </div>
        <q-input v-model="form.phoneNumber" outlined stack-label hide-bottom-space label="Phone Number" class="q-mb-md" />
        <app-select
          v-if="canChooseTenant" v-model="form.tenantId" :options="tenantOptions" label="Tenant *"
          :loading="loadingTenants" class="q-mb-md" :clearable="false" @update:model-value="onTenantChange"
        />
        <app-select v-model="form.roleId" :options="roleOptions" label="Role *" class="q-mb-md" :clearable="false" :loading="loadingRoles" />
      </q-form>
    </app-form-drawer>

    <temp-password-dialog v-model="tempPwOpen" :password="tempPassword" />
  </q-page>
</template>

<script setup>
import { ref, reactive, computed } from "vue";
import { userApi, tenantApi, roleApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useTenantStore } from "stores/tenant";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useDateFormat } from "composables/useDateFormat";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import TempPasswordDialog from "components/temp_password_dialog.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const tenantStore = useTenantStore();
const { has } = usePermissions();
// Only platform admins (tenants.write) choose a target tenant; others create within their own.
const canChooseTenant = computed(() => has(Permissions.TenantsWrite));
const canCreate = computed(() => has(Permissions.UsersWrite));
const fmt = useDateFormat();

const columns = [
  { name: "fullName", label: "Name", field: "fullName", align: "left", sortable: true, default: true },
  { name: "email", label: "Email", field: "email", align: "left", sortable: true, default: true },
  { name: "phoneNumber", label: "Phone", field: "phoneNumber", align: "left", sortable: true },
  { name: "isActive", label: "Status", field: "isActive", align: "left", sortable: true, default: true },
  { name: "createdBy", label: "Created By", field: "createdBy", align: "left", sortable: true },
  { name: "updatedBy", label: "Updated By", field: "updatedBy", align: "left", sortable: true },
  { name: "createdOnUtc", label: "Created", field: (r) => fmt.formatDateTime(r.createdOnUtc), align: "left", sortable: true },
  { name: "updatedOnUtc", label: "Updated", field: (r) => fmt.formatDateTime(r.updatedOnUtc), align: "left", sortable: true, default: true },
  { name: "actions", label: "", field: "actions", align: "right" }
];

const filters = reactive({ status: null });
const { rows, loading, totalRecords, selected, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    userApi.list({ page, limit }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

const statusFilterOptions = ["Active", "Inactive"].map((s) => ({ label: s, value: s }));
const filterChips = computed(() => (filters.status ? [{ key: "status", label: `Status: ${filters.status}` }] : []));
const removeFilter = () => { filters.status = null; };
const clearFilters = () => { filters.status = null; };

const filteredRows = computed(() => {
  let result = rows.value;
  if (filters.status) {
    const active = filters.status === "Active";
    result = result.filter((r) => r.isActive === active);
  }
  const q = search.value.trim().toLowerCase();
  if (q) {
    result = result.filter((r) => r.fullName?.toLowerCase().includes(q) || r.email?.toLowerCase().includes(q));
  }
  return result;
});

// ---- Create ----
const formOpen = ref(false);
const saving = ref(false);
const emailError = ref("");
const formRef = ref(null);
const form = reactive({ email: "", firstName: "", lastName: "", phoneNumber: "", roleId: null, tenantId: null });
const tenantOptions = ref([]);
const loadingTenants = ref(false);
const roleOptions = ref([]);
const loadingRoles = ref(false);

// The tenant the user is being created in: chosen by platform admins, else the caller's own.
const targetTenantId = computed(() => (canChooseTenant.value ? form.tenantId : tenantStore.activeTenantId));

const loadTenants = async () => {
  if (!canChooseTenant.value || tenantOptions.value.length) return;
  loadingTenants.value = true;
  try {
    const resp = await tenantApi.list({ page: 1, limit: 100 });
    tenantOptions.value = (resp?.data || []).map((t) => ({ label: t.name, value: t.tenantId }));
  } catch {
    // non-fatal
  } finally {
    loadingTenants.value = false;
  }
};

// Role options come from the tenant's assignable roles (system roles + the tenant's custom roles).
const loadRoles = async () => {
  const tid = targetTenantId.value;
  roleOptions.value = [];
  form.roleId = null;
  if (!tid) return;
  loadingRoles.value = true;
  try {
    const roles = await roleApi.tenantRoles(tid);
    roleOptions.value = (roles || []).map((r) => ({ label: r.name, value: r.id }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingRoles.value = false;
  }
};

const onTenantChange = (tenantId) => {
  form.tenantId = tenantId;
  loadRoles();
};

const resetForm = () => {
  form.email = "";
  form.firstName = "";
  form.lastName = "";
  form.phoneNumber = "";
  form.roleId = null;
  form.tenantId = null;
  roleOptions.value = [];
  emailError.value = "";
};

const openCreate = async () => {
  resetForm();
  if (canChooseTenant.value) {
    await loadTenants();
  } else {
    await loadRoles();
  }
  formOpen.value = true;
};

const tempPwOpen = ref(false);
const tempPassword = ref("");

const submitForm = async ({ clearDraft } = {}) => {
  emailError.value = "";
  if (!(await formRef.value?.validate())) return;
  if (!form.roleId) {
    notify.error("Select a role.");
    return;
  }
  const tenantId = targetTenantId.value;
  if (!tenantId) {
    notify.error("Select a tenant.");
    return;
  }
  saving.value = true;
  try {
    const payload = {
      email: form.email,
      firstName: form.firstName,
      lastName: form.lastName,
      phoneNumber: form.phoneNumber,
      displayName: `${form.firstName} ${form.lastName}`,
      roleId: form.roleId,
      tenantId
    };
    const result = await userApi.create(payload);
    clearDraft?.();
    formOpen.value = false;
    resetForm();
    tempPassword.value = result?.temporaryPassword || "";
    tempPwOpen.value = true;
    load();
  } catch (err) {
    if (getApiErrorCode(err) === ApiErrorCodes.DuplicateIdentifier) {
      emailError.value = "This email is already in use.";
    } else {
      notify.error(getApiErrorMessage(err));
    }
  } finally {
    saving.value = false;
  }
};

// ---- Status / reset ----
const setStatus = async (row, isActive) => {
  const ok = await confirm({
    title: isActive ? "Activate user" : "Deactivate user",
    message: `${isActive ? "Activate" : "Deactivate"} ${row.displayName}?`,
    type: isActive ? "primary" : "danger"
  });
  if (!ok) return;
  try {
    await userApi.setStatus(row.userId, isActive);
    notify.success("Status updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const bulkSetStatus = async (sel, isActive) => {
  if (!sel.length) return;
  const ok = await confirm({
    title: isActive ? "Activate users" : "Deactivate users",
    message: `${isActive ? "Activate" : "Deactivate"} ${sel.length} user(s)?`,
    type: isActive ? "primary" : "danger"
  });
  if (!ok) return;
  try {
    await Promise.all(sel.map((r) => userApi.setStatus(r.userId, isActive)));
    notify.success("Users updated.");
    selected.value = [];
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const resetPassword = async (row) => {
  const ok = await confirm({
    title: "Reset password",
    message: `Generate a new temporary password for ${row.displayName}? Their current sessions will end.`,
    confirmLabel: "Reset",
    type: "danger"
  });
  if (!ok) return;
  try {
    const result = await userApi.resetPassword(row.userId);
    tempPassword.value = result?.temporaryPassword || "";
    tempPwOpen.value = true;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

</script>
