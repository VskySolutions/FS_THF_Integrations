<template>
  <q-page padding>
    <app-breadcrumbs :items="[{ label: 'Home', icon: 'o_home', to: '/' }, { label: 'Users' }]" />

    <div class="row items-center q-mb-md q-gutter-sm">
      <div class="text-h5 text-weight-bold">Users</div>
      <q-space />
      <q-input v-model="search" dense outlined debounce="300" placeholder="Search name or email" style="max-width: 280px;">
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>
      <q-btn unelevated no-caps color="primary" icon="o_person_add" label="Create User" @click="openCreate" />
    </div>

    <app-filter-drawer :chips="filterChips" @remove="removeFilter" @clear="clearFilters">
      <q-select v-model="filters.status" outlined dense clearable label="Status" :options="['Active', 'Inactive']" />
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
        <q-btn flat dense no-caps color="positive" label="Activate" @click="bulkSetStatus(sel, true)" />
        <q-btn flat dense no-caps color="negative" label="Deactivate" @click="bulkSetStatus(sel, false)" />
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
                <q-item v-if="!cell.row.isActive" clickable @click="setStatus(cell.row, true)">
                  <q-item-section avatar><q-icon name="o_check_circle" /></q-item-section>
                  <q-item-section>Activate</q-item-section>
                </q-item>
                <q-item v-if="cell.row.isActive" clickable @click="setStatus(cell.row, false)">
                  <q-item-section avatar><q-icon name="o_block" /></q-item-section>
                  <q-item-section>Deactivate</q-item-section>
                </q-item>
                <q-item clickable @click="resetPassword(cell.row)">
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
        <q-input
          v-model="form.displayName" outlined stack-label hide-bottom-space label="Display Name *" class="q-mb-md"
          :rules="[(v) => !!v || 'Display name is required']"
        />
        <app-select v-model="form.role" :options="roleOptions" label="Role *" class="q-mb-md" :clearable="false" />
        <app-select v-if="isSuperAdmin" v-model="form.tenantId" :options="tenantOptions" label="Tenant" :loading="loadingTenants" />
      </q-form>
    </app-form-drawer>

    <temp-password-dialog v-model="tempPwOpen" :password="tempPassword" />
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { userApi, tenantApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { useTenantStore } from "stores/tenant";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppBreadcrumbs from "components/common/AppBreadcrumbs.vue";
import AppSelect from "components/common/AppSelect.vue";
import TempPasswordDialog from "components/temp_password_dialog.vue";

const notify = useNotify();
const { confirm } = useConfirm();
const tenantStore = useTenantStore();
const isSuperAdmin = computed(() => tenantStore.activeRole === "SuperAdmin");

const columns = [
  { name: "displayName", label: "Name", field: "displayName", align: "left", sortable: true },
  { name: "email", label: "Email", field: "email", align: "left", sortable: true },
  { name: "isActive", label: "Status", field: "isActive", align: "left", sortable: true },
  { name: "actions", label: "", field: "actions", align: "right" }
];

const rows = ref([]);
const loading = ref(false);
const totalRecords = ref(0);
const selected = ref([]);
const search = ref("");
const filters = reactive({ status: null });
const pagination = ref({ page: 1, rowsPerPage: 20, sortBy: null, descending: false, rowsNumber: 0 });

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
    result = result.filter((r) => r.displayName?.toLowerCase().includes(q) || r.email?.toLowerCase().includes(q));
  }
  return result;
});

const load = async () => {
  loading.value = true;
  try {
    const resp = await userApi.list({ page: pagination.value.page, limit: pagination.value.rowsPerPage });
    rows.value = resp?.data || [];
    totalRecords.value = resp?.meta?.totalRecords ?? rows.value.length;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const onRequest = (pag) => {
  pagination.value = { ...pagination.value, ...pag };
  load();
};

// ---- Create ----
const formOpen = ref(false);
const saving = ref(false);
const emailError = ref("");
const formRef = ref(null);
const form = reactive({ email: "", displayName: "", role: "Operator", tenantId: null });
const tenantOptions = ref([]);
const loadingTenants = ref(false);

const roleOptions = computed(() =>
  isSuperAdmin.value
    ? [{ label: "Super Admin", value: "SuperAdmin" }, { label: "Tenant Admin", value: "TenantAdmin" }, { label: "Operator", value: "Operator" }]
    : [{ label: "Tenant Admin", value: "TenantAdmin" }, { label: "Operator", value: "Operator" }]);

const loadTenants = async () => {
  if (!isSuperAdmin.value || tenantOptions.value.length) return;
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

const resetForm = () => {
  form.email = "";
  form.displayName = "";
  form.role = "Operator";
  form.tenantId = null;
  emailError.value = "";
};

const openCreate = () => {
  resetForm();
  loadTenants();
  formOpen.value = true;
};

const tempPwOpen = ref(false);
const tempPassword = ref("");

const submitForm = async ({ clearDraft } = {}) => {
  emailError.value = "";
  if (!(await formRef.value?.validate())) return;
  saving.value = true;
  try {
    const payload = { email: form.email, displayName: form.displayName, role: form.role };
    if (isSuperAdmin.value && form.role !== "SuperAdmin") {
      payload.tenantId = form.tenantId;
    }
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

onMounted(load);
</script>
