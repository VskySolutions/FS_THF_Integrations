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
      <app-column-filters v-model="filters" :columns="filterableColumns" />
    </app-filter-drawer>

    <app-data-table
      page-key="users"
      row-key="userId"
      title="All users"
      :rows="rows"
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
          <q-btn flat round dense color="primary" icon="o_visibility" :to="{ name: 'user_detail', params: { id: cell.row.userId } }">
            <q-tooltip>View / Manage</q-tooltip>
          </q-btn>
          <q-btn v-if="has(Permissions.UsersWrite) || has(Permissions.UsersResetPassword)" flat round dense icon="o_more_vert">
            <q-menu auto-close>
              <q-list style="min-width: 170px;">
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

    <!-- Create user (promote an existing Person to a login account) -->
    <app-form-drawer v-model="formOpen" title="Create User" :saving="saving" @submit="submitForm" @cancel="resetForm">
      <q-form ref="formRef" greedy>
        <app-select
          v-model="form.personId" :options="personOptions" label="Person *" class="q-mb-md"
          :loading="loadingPersons" :clearable="false" :disable="personLocked" use-input
          hint="Persons already linked to a user are disabled."
          @filter="filterPersons" @update:model-value="onPersonChange"
        >
          <template #after>
            <q-btn round dense flat icon="o_add" color="primary" :disable="personLocked" @click="personDialogOpen = true">
              <q-tooltip>Add a new person</q-tooltip>
            </q-btn>
          </template>
        </app-select>
        <q-input
          v-model="form.email" outlined stack-label hide-bottom-space type="email" label="Username *" class="q-mb-md"
          hint="The user signs in with this email."
          :error="!!emailError" :error-message="emailError"
          :rules="[(v) => !!v || 'Username is required', (v) => /.+@.+\..+/.test(v) || 'Enter a valid email']"
        />
        <app-select
          v-if="canChooseTenant" v-model="form.tenantId" :options="tenantOptions" label="Tenant *"
          :loading="loadingTenants" class="q-mb-md" :clearable="false" @update:model-value="onTenantChange"
        />
        <app-select v-model="form.roleId" :options="roleOptions" label="Role *" class="q-mb-md" :clearable="false" :loading="loadingRoles" />
      </q-form>
    </app-form-drawer>

    <!-- Quick-add Person (the "+" beside the Person dropdown) -->
    <person-form-dialog v-model="personDialogOpen" @created="onPersonCreated" />

    <temp-password-dialog v-model="tempPwOpen" :password="tempPassword" />
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { debounce } from "quasar";
import { userApi, personApi, roleApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useTenantOptions } from "composables/useTenantOptions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDateFormat } from "composables/useDateFormat";

import AppDataTable from "components/common/AppDataTable.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import PersonFormDialog from "components/person/PersonFormDialog.vue";
import TempPasswordDialog from "components/temp_password_dialog.vue";

const route = useRoute();
const router = useRouter();

const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
// Only platform/super admins (tenants.write) choose a target tenant; others create within their own.
const { canChooseTenant, activeTenantId, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();
const canCreate = computed(() => has(Permissions.UsersWrite));
const fmt = useDateFormat();

// Tenant dropdown filter for platform/super admins (option value is the tenant id, sent to the API).
const tenantFilterOptions = computed(() =>
  (canChooseTenant.value && tenantOptions.value.length ? tenantOptions.value : null));

// Filterable columns are server-side; text/computed/audit/date columns are covered by the search box.
const columns = computed(() => [
  {
    name: "tenantName",
    label: "Tenant",
    field: "tenantName",
    align: "left",
    sortable: true,
    default: true,
    ...(tenantFilterOptions.value ? { filterOptions: tenantFilterOptions.value } : { filterable: false })
  },
  { name: "fullName", label: "Name", field: "fullName", align: "left", sortable: true, default: true },
  { name: "email", label: "Email", field: "email", align: "left", sortable: true, default: true },
  { name: "phoneNumber", label: "Phone", field: "phoneNumber", align: "left", sortable: true },
  { name: "roles", label: "Role", field: (r) => (r.roles || []).join(", "), align: "left", sortable: false, default: true },
  { name: "isActive", label: "Status", field: "isActive", align: "left", sortable: true, default: true, filterOptions: [{ label: "Active", value: true }, { label: "Inactive", value: false }] },
  { name: "createdBy", label: "Created By", field: "createdBy", align: "left", sortable: true, filterable: false },
  { name: "updatedBy", label: "Updated By", field: "updatedBy", align: "left", sortable: true, filterable: false },
  { name: "createdOnUtc", label: "Created", field: (r) => fmt.formatDateTime(r.createdOnUtc), align: "left", sortable: true, filterable: false },
  { name: "updatedOnUtc", label: "Updated", field: (r) => fmt.formatDateTime(r.updatedOnUtc), align: "left", sortable: true, default: true, filterable: false },
  { name: "actions", label: "Actions", field: "actions", align: "right" }
]);

const { rows, loading, totalRecords, selected, search, filterOpen, pagination, load, onRequest } = useListTable({
  fetcher: ({ page, limit }) =>
    userApi.list({
      page,
      limit,
      search: search.value || undefined,
      tenantId: filters.tenantName || undefined,
      isActive: typeof filters.isActive === "boolean" ? filters.isActive : undefined,
      name: filters.fullName || undefined,
      email: filters.email || undefined,
      phone: filters.phoneNumber || undefined,
      role: filters.roles || undefined
    }).then((r) => ({ data: r?.data, total: r?.meta?.totalRecords })),
  onError: (err) => notify.error(getApiErrorMessage(err))
});

// Server-side per-column filters + search box: reload (debounced, first page) whenever they change.
const { filters, filterableColumns, filterChips, removeFilter, clearFilters } = useColumnFilters(columns, rows, { server: true });
const reload = debounce(() => { pagination.value.page = 1; load(); }, 300);
watch([search, filters], reload, { deep: true });

// ---- Create ----
const formOpen = ref(false);
const saving = ref(false);
const emailError = ref("");
const formRef = ref(null);
const form = reactive({ personId: null, email: "", roleId: null, tenantId: null });
const personDialogOpen = ref(false);
const roleOptions = ref([]);
const loadingRoles = ref(false);

// ---- Person dropdown (the user is created by promoting an existing person) ----
const allPersons = ref([]);
const personOptions = ref([]);
const loadingPersons = ref(false);
const personLocked = ref(false); // locked when opened via "Convert to User" deep-link

const personOption = (p) => ({
  label: p.primaryEmail ? `${p.fullName} — ${p.primaryEmail}` : p.fullName,
  value: p.id,
  disable: p.isUser // already a user: cannot promote again
});

const loadPersons = async () => {
  loadingPersons.value = true;
  try {
    const people = await personApi.selectable();
    allPersons.value = people || [];
    personOptions.value = allPersons.value.map(personOption);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingPersons.value = false;
  }
};

const filterPersons = (val, update) => {
  const needle = (val || "").toLowerCase();
  update(() => {
    personOptions.value = allPersons.value
      .filter((p) => personOption(p).label.toLowerCase().includes(needle))
      .map(personOption);
  });
};

// Pre-fill the username from the chosen person (person is the source of truth), and for tenant
// choosers default the user's tenant to the person's owning tenant.
const onPersonChange = (personId) => {
  const p = allPersons.value.find((x) => x.id === personId);
  if (!p) return;
  form.email = p.primaryEmail || "";
  if (canChooseTenant.value && p.tenantId) {
    form.tenantId = p.tenantId;
    loadRoles();
  }
};

// A person was just created via the inline "+" dialog: add it, select it, and prefill.
const onPersonCreated = (detail) => {
  const p = detail?.profile;
  if (!p) return;
  const item = {
    id: p.id,
    fullName: p.fullName,
    primaryEmail: p.primaryEmail,
    mobileNumber: p.mobileNumber,
    countryCode: p.countryCode,
    tenantId: p.tenantId,
    isUser: false
  };
  allPersons.value = [item, ...allPersons.value.filter((x) => x.id !== p.id)];
  personOptions.value = allPersons.value.map(personOption);
  form.personId = p.id;
  onPersonChange(p.id);
};

// The tenant the user is being created in: chosen by platform admins, else the caller's own.
const targetTenantId = computed(() => (canChooseTenant.value ? form.tenantId : activeTenantId.value));

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
  form.personId = null;
  form.email = "";
  form.roleId = null;
  form.tenantId = null;
  roleOptions.value = [];
  emailError.value = "";
  personLocked.value = false;
};

const openCreate = async (presetPersonId = null) => {
  resetForm();
  await Promise.all([
    loadPersons(),
    canChooseTenant.value ? loadTenants() : loadRoles()
  ]);
  if (presetPersonId) {
    form.personId = presetPersonId;
    personLocked.value = true;
    onPersonChange(presetPersonId);
  }
  formOpen.value = true;
};

// "Convert to User" from the People list deep-links here with ?personId=...
onMounted(() => {
  // Load tenant options so the Tenant dropdown filter is available to platform/super admins.
  if (canChooseTenant.value) loadTenants();
  const presetPersonId = route.query.personId;
  if (presetPersonId && canCreate.value) {
    openCreate(presetPersonId);
    // Drop the query param so a refresh doesn't re-open the drawer.
    router.replace({ query: {} });
  }
});

const tempPwOpen = ref(false);
const tempPassword = ref("");

const submitForm = async ({ clearDraft } = {}) => {
  emailError.value = "";
  if (!form.personId) {
    notify.error("Select a person.");
    return;
  }
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
      personId: form.personId,
      email: form.email,
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
    message: `${isActive ? "Activate" : "Deactivate"} ${row.fullName}?`,
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
    message: `Generate a new temporary password for ${row.fullName}? Their current sessions will end.`,
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
