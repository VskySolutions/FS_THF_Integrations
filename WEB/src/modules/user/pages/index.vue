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
      <q-toggle
        v-if="canManageDeleted" v-model="showDeleted" label="Show deleted?" dense class="q-mt-md"
      />
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

      <!-- Department, badged when this user heads it — a head is that department's REMS Director. -->
      <template #body-cell-department="cell">
        <q-td :props="cell">
          <template v-if="cell.value">
            <span>{{ cell.value }}</span>
            <q-icon v-if="cell.row.isDepartmentHead" name="o_workspace_premium" color="primary" size="18px" class="q-ml-xs">
              <q-tooltip>Heads {{ cell.value }} — its REMS Department Director</q-tooltip>
            </q-icon>
          </template>
          <span v-else class="text-grey-6">—</span>
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

    <deleted-records-panel
      v-if="canManageDeleted" :entity-type="EntityType.User" :show="showDeleted" @restored="load"
    />

    <!-- Create user (promote an existing Person to a login account) -->
    <app-form-drawer v-model="formOpen" title="Create User" :saving="saving" @submit="submitForm" @cancel="resetForm">
      <q-form ref="formRef" greedy>
        <app-select
          v-model="form.personId" :options="personOptions" label="Person *" class="q-mb-md"
          :loading="loadingPersons" :clearable="false" :disable="personLocked" use-input
          hint="Persons already linked to a user are disabled."
          info="A user is created by promoting an existing Person record. Anyone already linked to a user account is listed but not selectable — use the + to add a new person."
          @update:model-value="onPersonChange"
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
        <app-select
          v-model="form.roleIds" :options="roleOptions" label="Roles *" multiple class="q-mb-md"
          :loading="loadingRoles" hint="Grouped by category. Assign one or more roles."
          info="The roles assignable in the chosen tenant, grouped System / Operational / Custom. Super Admin is only listed for a Super Admin."
        />

        <!-- Department + groups, the same placements the user's detail page manages. Both are tenant-scoped
             and their endpoints reject a user who holds no assignment in the caller's active tenant, so the
             section only appears when the account is being created there. -->
        <template v-if="inActiveTenant">
          <app-select
            v-model="form.department" :options="departmentOptions" :loading="loadingDepartments"
            label="Department" class="q-mb-md"
            info="From the REMS Department option list. The department's head is its REMS Department Director, which is what prefills that field on an engagement."
            @update:model-value="onDepartmentChange"
          />
          <q-toggle v-model="form.isDepartmentHead" :disable="!form.department" label="Department head" />
          <div class="text-caption text-grey-7 q-mb-md">{{ headHint }}</div>

          <app-select
            v-if="canManageGroups" v-model="form.groupIds" :options="groupOptions" label="Groups" multiple
            class="q-mb-md" :loading="loadingGroups"
            hint="Tenant user groups (segmentation, independent of roles)."
            info="Groups in your active tenant, maintained in Administration → User Groups. Membership is what scopes pickers such as Engagement Executive, Billing Manager and CSE."
          />
        </template>

        <q-toggle
          v-model="form.sendInvitation" color="primary"
          label="Send invitation email with the temporary password"
        />
        <div class="text-caption text-grey-7 q-mb-md">
          Emails the user their login link and temporary password via the tenant's active SMTP account.
        </div>
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
import { userApi, personApi, userGroupApi, getApiErrorMessage, getApiErrorCode, ApiErrorCodes, EntityType } from "services/api";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useTenantOptions } from "composables/useTenantOptions";
import { useRoleOptions } from "composables/useRoleOptions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useListTable } from "composables/useListTable";
import { useColumnFilters } from "composables/useColumnFilters";
import { useDeletedRecords } from "composables/useDeletedRecords";
import { useDateFormat } from "composables/useDateFormat";

import AppDataTable from "components/common/AppDataTable.vue";
import DeletedRecordsPanel from "components/universal/DeletedRecordsPanel.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppFilterDrawer from "components/common/AppFilterDrawer.vue";
import AppColumnFilters from "components/common/AppColumnFilters.vue";
import AppListHeader from "components/common/AppListHeader.vue";
import AppSelect from "components/common/AppSelect.vue";
import PersonFormDialog from "components/person/PersonFormDialog.vue";
import TempPasswordDialog from "components/temp_password_dialog.vue";

const route = useRoute();
const router = useRouter();

const { showDeleted, canManageDeleted } = useDeletedRecords();
const notify = useNotify();
const { confirm } = useConfirm();
const { has } = usePermissions();
// Only platform/super admins (tenants.write) choose a target tenant; others create within their own.
const { canChooseTenant, activeTenantId, tenantOptions, loadingTenants, loadTenants } = useTenantOptions();
const canCreate = computed(() => has(Permissions.UsersWrite));
const canManageGroups = computed(() => has(Permissions.UsersGroupManagement));
const fmt = useDateFormat();

// Filterable columns are server-side; text/computed/audit/date columns are covered by the search box.
const columns = computed(() => [
  // No Tenant column: the list is scoped to the active tenant, so every row would repeat the same name.
  // A Super Admin changes which tenant they are looking at with the toolbar's tenant scope.
  { name: "fullName", label: "Name", field: "fullName", align: "left", sortable: true, default: true },
  { name: "email", label: "Email", field: "email", align: "left", sortable: true, default: true },
  { name: "phoneNumber", label: "Phone", field: "phoneNumber", align: "left", sortable: true },
  { name: "roles", label: "Role", field: (r) => (r.roles || []).join(", "), align: "left", sortable: false, default: true },
  { name: "groups", label: "Groups", field: (r) => (r.groups || []).map((g) => g.name).join(", "), align: "left", sortable: false, default: true },
  // Department placement in the active tenant. Read-only here (it is set on the user's detail page), so
  // there is no server-side filter behind it — the search box and the detail page cover that.
  { name: "department", label: "Department", field: "department", align: "left", sortable: true, default: true, filterable: false },
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
      isActive: typeof filters.isActive === "boolean" ? filters.isActive : undefined,
      name: filters.fullName || undefined,
      email: filters.email || undefined,
      phone: filters.phoneNumber || undefined,
      role: filters.roles || undefined,
      group: filters.groups || undefined
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
const form = reactive({
  personId: null,
  email: "",
  roleIds: [],
  tenantId: null,
  sendInvitation: false,
  // Tenant-scoped placements, applied through their own endpoints once the account exists.
  department: null,
  isDepartmentHead: false,
  groupIds: []
});
const personDialogOpen = ref(false);
// Grouped, category-labelled multi-role options (SuperAdmin excluded for non-Super-Admin callers).
const { roleOptions, loading: loadingRoles, loadForTenant } = useRoleOptions();

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

// ---- Department & groups (as on the user's detail page) ----
// Both live in the caller's ACTIVE tenant: the pickers are loaded from it and the endpoints require the
// user to hold an assignment there. A platform admin creating an account in some other tenant therefore
// sets neither here — they do it after switching into that tenant.
const inActiveTenant = computed(() => !!activeTenantId.value && targetTenantId.value === activeTenantId.value);

const departmentOptions = ref([]);
const departmentHeads = ref([]);
const loadingDepartments = ref(false);
const groupOptions = ref([]);
const loadingGroups = ref(false);

const departmentLabel = (code) => departmentOptions.value.find((o) => o.value === code)?.label || code;
const currentHead = computed(() => departmentHeads.value.find((h) => h.department === form.department) || null);

const headHint = computed(() => {
  if (!form.department) return "Pick a department to set a head.";
  if (!currentHead.value) return `${departmentLabel(form.department)} has no head yet.`;
  return `${currentHead.value.fullName} currently heads ${departmentLabel(form.department)}.`;
});

// Headship is meaningless without a department, and is never carried across a change of one.
const onDepartmentChange = (value) => {
  form.department = value;
  form.isDepartmentHead = false;
};

const loadDepartments = async () => {
  loadingDepartments.value = true;
  try {
    const result = await userApi.departments();
    departmentOptions.value = result?.departments || [];
    departmentHeads.value = result?.heads || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingDepartments.value = false;
  }
};

const loadGroups = async () => {
  loadingGroups.value = true;
  try {
    const groups = (await userGroupApi.list()) || [];
    groupOptions.value = groups.map((g) => ({ label: g.name, value: g.id }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingGroups.value = false;
  }
};

// Applied once the account exists, through the same endpoints the detail page uses. Reported but never
// fatal: the user has already been created, and the temporary password below is shown only once.
const applyPlacements = async (newUserId) => {
  if (!newUserId || !inActiveTenant.value) return "";
  const notes = [];
  try {
    if (canManageGroups.value && form.groupIds.length) {
      await userApi.setGroups(newUserId, form.groupIds);
    }
    if (form.department) {
      const result = await userApi.setDepartment(newUserId, {
        department: form.department,
        isHead: form.isDepartmentHead
      });
      if (result?.demotedHeadName) notes.push(`${result.demotedHeadName} is no longer the department head.`);
    }
  } catch (err) {
    notify.warning(`User created, but the department/groups could not be applied: ${getApiErrorMessage(err)}`);
  }
  return notes.join(" ");
};

// Role options come from the tenant's assignable roles (system + custom), grouped by category.
const loadRoles = async () => {
  form.roleIds = [];
  try {
    await loadForTenant(targetTenantId.value);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const onTenantChange = (tenantId) => {
  form.tenantId = tenantId;
  loadRoles();
};

const resetForm = () => {
  form.personId = null;
  form.email = "";
  form.roleIds = [];
  form.tenantId = null;
  form.sendInvitation = false;
  form.department = null;
  form.isDepartmentHead = false;
  form.groupIds = [];
  emailError.value = "";
  personLocked.value = false;
};

const openCreate = async (presetPersonId = null) => {
  resetForm();
  await Promise.all([
    loadPersons(),
    canChooseTenant.value ? loadTenants() : loadRoles(),
    // Both pickers come from the caller's active tenant, so they are pointless without one (a Super Admin
    // who has not switched in) — that is also exactly when the section stays hidden.
    activeTenantId.value ? loadDepartments() : Promise.resolve(),
    activeTenantId.value && canManageGroups.value ? loadGroups() : Promise.resolve()
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
  if (!form.roleIds.length) {
    notify.error("Select at least one role.");
    return;
  }
  const tenantId = targetTenantId.value;
  if (!tenantId) {
    notify.error("Select a tenant.");
    return;
  }
  // A department has one head, so taking it demotes the incumbent — name them before anything is created.
  if (inActiveTenant.value && form.isDepartmentHead && currentHead.value) {
    const ok = await confirm({
      title: "Change department head",
      message: `${currentHead.value.fullName} currently heads ${departmentLabel(form.department)}. Make the ` +
        `new user the head instead? ${currentHead.value.fullName} will no longer be the Department Director ` +
        "on new engagements.",
      confirmLabel: "Make head"
    });
    if (!ok) return;
  }
  saving.value = true;
  try {
    const payload = {
      personId: form.personId,
      email: form.email,
      roleIds: form.roleIds,
      tenantId,
      sendInvitation: form.sendInvitation
    };
    const recipientEmail = form.email;
    const wantedInvite = form.sendInvitation;
    const result = await userApi.create(payload);
    // Before resetForm() clears the picks it reads.
    const placementNote = await applyPlacements(result?.userId);
    clearDraft?.();
    formOpen.value = false;
    resetForm();
    tempPassword.value = result?.temporaryPassword || "";
    tempPwOpen.value = true;
    if (placementNote) notify.info(placementNote);
    if (wantedInvite) {
      if (result?.invitationEmailSent) {
        notify.success(`Invitation email sent to ${recipientEmail}.`);
      } else {
        notify.warning("User created, but the invitation email could not be sent (no active SMTP account). Share the temporary password manually.");
      }
    }
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
