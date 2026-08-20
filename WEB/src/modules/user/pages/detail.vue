<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'Home', icon: 'o_home', to: '/' },
        { label: 'Users', to: { name: 'users' } },
        { label: user?.displayName || 'User' }
      ]"
      :back-to="{ name: 'users' }"
    />

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <div v-else-if="user">
      <!-- Basic info -->
      <q-card flat bordered class="user-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Basic information</q-card-section>
        <q-separator />
        <q-card-section class="row q-col-gutter-md">
          <q-input v-model="firstName" outlined dense stack-label label="First Name" class="col-12 col-sm-6" :readonly="!canEdit" />
          <q-input v-model="lastName" outlined dense stack-label label="Last Name" class="col-12 col-sm-6" :readonly="!canEdit" />
          <!-- This IS the sign-in credential, not a contact address — labelled so whoever edits it knows. -->
          <q-input
            v-model="email" outlined dense stack-label label="Username (Email)" class="col-12 col-sm-6"
            :readonly="!canEdit" hint="Used to sign in. Changing it signs the user out of their sessions."
          />
          <q-input v-model="phoneNumber" outlined dense stack-label label="Phone Number" class="col-12 col-sm-6" :readonly="!canEdit" />
        </q-card-section>
        <q-card-actions v-if="canEdit" align="right">
          <q-btn unelevated no-caps color="primary" label="Save" :loading="saving" @click="save" />
        </q-card-actions>
      </q-card>

      <!-- Department + REMS approval roles. Both are per-tenant: the department head is that department's
           REMS Director, and the managing shareholder approves every engagement in the tenant. -->
      <q-card flat bordered class="user-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Department &amp; approval roles</q-card-section>
        <q-separator />
        <q-card-section v-if="!inActiveTenant" class="text-grey-6">
          Assign this user to the active tenant before setting a department or approval role.
        </q-card-section>
        <template v-else>
          <q-card-section class="row q-col-gutter-md items-start">
            <app-select
              v-model="department" :options="departmentOptions" :loading="loadingDepartments" label="Department"
              class="col-12 col-sm-6" :readonly="!canEdit"
              info="From the REMS Department option list (Administration → Option Sets). A department has one head, and that head is its REMS Department Director."
              @update:model-value="onDepartmentChange"
            />
            <div class="col-12 col-sm-6">
              <q-toggle v-model="isDepartmentHead" :disable="!canEdit || !department" label="Department head" />
              <div class="text-caption text-grey-7 q-mt-xs">{{ headHint }}</div>
            </div>
          </q-card-section>
          <q-separator inset />
          <q-card-section>
            <q-toggle v-model="isManagingShareholder" :disable="!canEdit" label="Managing shareholder" />
            <div class="text-caption text-grey-7 q-mt-xs">{{ shareholderHint }}</div>
          </q-card-section>
        </template>
        <q-card-actions v-if="canEdit && inActiveTenant" align="right">
          <q-btn
            unelevated no-caps color="primary" label="Save" :loading="savingRoles"
            :disable="!rolesDirty" @click="saveRoles"
          />
        </q-card-actions>
      </q-card>

      <!-- Status + reset -->
      <q-card flat bordered class="user-card q-mb-md">
        <q-card-section class="row items-center">
          <div class="text-subtitle1 text-weight-medium">Status</div>
          <q-badge :color="user.isActive ? 'positive' : 'grey'" class="q-ml-md">{{ user.isActive ? "Active" : "Inactive" }}</q-badge>
          <q-space />
          <q-btn v-if="canResetPassword" flat no-caps color="primary" icon="o_lock_reset" label="Reset password" :disable="!canManageTarget" class="q-mr-sm" @click="resetPassword">
            <q-tooltip v-if="!canManageTarget">Only a Super Admin can reset this user.</q-tooltip>
          </q-btn>
          <q-btn v-if="canEdit" outline no-caps :color="user.isActive ? 'negative' : 'positive'" :label="user.isActive ? 'Deactivate' : 'Activate'" :disable="!canManageTarget" @click="toggleStatus">
            <q-tooltip v-if="!canManageTarget">Only a Super Admin can manage this user.</q-tooltip>
          </q-btn>
        </q-card-section>
      </q-card>

      <!-- Tenant assignments (requires roles.assign — Super Admins tenant-wide, Tenant Admins within their
           own tenant only, and never on a Super Admin target; the API enforces the same boundary). -->
      <q-card v-if="canManageAssignments" flat bordered class="user-card">
        <q-card-section class="row items-center">
          <div class="text-subtitle1 text-weight-medium">Tenant assignments</div>
          <q-space />
          <q-btn unelevated no-caps color="primary" icon="o_add" label="Add" :disable="!canManageTarget" @click="openAssign">
            <q-tooltip v-if="!canManageTarget">Only a Super Admin can manage this user.</q-tooltip>
          </q-btn>
        </q-card-section>
        <q-separator />
        <q-banner v-if="visibleAssignments.length === 1" dense class="bg-orange-1 text-orange-9">
          <template #avatar><q-icon name="o_warning" color="orange" /></template>
          This is the user's only tenant assignment.
        </q-banner>
        <q-list>
          <q-item v-for="a in visibleAssignments" :key="a.tenantId">
            <q-item-section>
              <q-item-label>{{ tenantName(a.tenantId) }}</q-item-label>
              <!-- All roles held in this tenant, each chip coloured by its category (AC-ADM-006.6). -->
              <q-item-label caption>
                <div class="row items-center q-gutter-xs q-mt-xs">
                  <q-chip
                    v-for="r in (a.roles || [])" :key="r.roleId" dense size="sm"
                    :color="roleCategoryChip(r.roleName || r.role).color" text-color="white" class="text-capitalize"
                  >
                    {{ r.roleName || r.role }}
                    <q-tooltip>{{ roleCategoryChip(r.roleName || r.role).category }}</q-tooltip>
                  </q-chip>
                  <span v-if="!(a.roles || []).length" class="text-grey-6">No roles</span>
                </div>
              </q-item-label>
            </q-item-section>
            <q-item-section side>
              <div class="row items-center no-wrap">
                <q-btn
                  flat no-caps dense color="primary" icon="o_manage_accounts" label="Manage Roles" class="q-px-sm"
                  :disable="!canManageTarget" @click="openChangeRole(a)"
                >
                  <q-tooltip v-if="!canManageTarget">Only a Super Admin can manage this user.</q-tooltip>
                </q-btn>
                <q-btn
                  flat round dense color="negative" icon="o_delete" class="q-ml-xs"
                  :disable="!canManageTarget" @click="removeAssignment(a)"
                >
                  <q-tooltip>{{ canManageTarget ? "Remove" : "Only a Super Admin can manage this user." }}</q-tooltip>
                </q-btn>
              </div>
            </q-item-section>
          </q-item>
          <q-item v-if="!visibleAssignments.length">
            <q-item-section class="text-grey-6">No assignments.</q-item-section>
          </q-item>
        </q-list>
      </q-card>

      <!-- Groups -->
      <q-card flat bordered class="user-card q-mt-md">
        <q-card-section class="row items-center">
          <div class="text-subtitle1 text-weight-medium">Groups</div>
          <q-space />
          <q-btn v-if="canManageGroups" outline no-caps color="primary" icon="o_group_add" label="Manage groups" @click="openGroups" />
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div v-if="(user.groups || []).length" class="row q-gutter-sm">
            <q-chip v-for="g in user.groups" :key="g.id" color="teal-1" text-color="primary" icon="o_groups">{{ g.name }}</q-chip>
          </div>
          <div v-else class="text-grey-6">Not a member of any group.</div>
        </q-card-section>
      </q-card>
    </div>

    <!-- Manage groups dialog -->
    <q-dialog v-model="groupsOpen" persistent>
      <q-card style="min-width: 420px; max-width: 92vw;">
        <q-card-section class="text-h6">Manage groups</q-card-section>
        <q-separator />
        <q-card-section>
          <!-- Create a new group inline. -->
          <div class="row items-center q-col-gutter-sm q-mb-md">
            <app-text-field v-model="newGroupName" label="Create a new group" placeholder="e.g. Finance Team" class="col" @keyup.enter="createGroup" />
            <q-btn outline no-caps color="primary" icon="o_add" label="Create" :loading="creatingGroup" :disable="!newGroupName.trim()" @click="createGroup" />
          </div>
          <!-- Tick the groups this user belongs to. The info icon shows who created each group and when. -->
          <q-list bordered separator class="rounded-borders" style="max-height: 320px; overflow: auto;">
            <q-item v-for="g in groupList" :key="g.id" v-ripple tag="label">
              <q-item-section avatar>
                <q-checkbox v-model="selectedGroupIds" :val="g.id" />
              </q-item-section>
              <q-item-section>
                <q-item-label class="row items-center">
                  {{ g.name }}
                  <q-icon name="o_info" size="16px" color="grey-6" class="q-ml-xs cursor-pointer">
                    <q-tooltip>
                      Created by {{ g.createdBy || "Unknown" }}<template v-if="g.createdOnUtc"> · {{ fmt.formatDateTime(g.createdOnUtc) }}</template>
                    </q-tooltip>
                  </q-icon>
                </q-item-label>
                <q-item-label v-if="g.memberCount != null" caption>{{ g.memberCount }} member(s)</q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-btn flat round dense color="negative" icon="o_delete" @click.prevent.stop="deleteGroup(g)">
                  <q-tooltip>Delete group</q-tooltip>
                </q-btn>
              </q-item-section>
            </q-item>
            <q-item v-if="!loadingGroups && !groupList.length">
              <q-item-section class="text-grey-6">No groups yet. Create one above.</q-item-section>
            </q-item>
          </q-list>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps color="grey-8" label="Cancel" @click="groupsOpen = false" />
          <q-btn unelevated no-caps color="primary" label="Save" :loading="savingGroups" @click="saveGroups" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Add assignment -->
    <app-form-drawer v-model="assignOpen" :title="assignTitle" :saving="assignSaving" @submit="submitAssign" @cancel="assignOpen = false">
      <q-form ref="assignForm" greedy>
        <app-select
          v-if="isPlatformAdmin" v-model="assign.tenantId" :options="tenantOptions" :loading="loadingTenants"
          label="Tenant *" class="q-mb-md" :clearable="false" :disable="assignMode === 'edit'" @update:model-value="onAssignTenantChange"
        />
        <app-select
          v-model="assign.roleIds" :options="roleOptions" :loading="loadingRoles" label="Roles *" multiple
          hint="Grouped by category. The assignment is reconciled to exactly these roles."
          info="The roles assignable in the selected tenant, grouped System / Operational / Custom. Super Admin is only listed for a Super Admin."
        />
      </q-form>
    </app-form-drawer>

    <temp-password-dialog v-model="tempPwOpen" :password="tempPassword" />
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { userApi, tenantApi, userGroupApi, getApiErrorMessage } from "services/api";
import { useTenantStore } from "stores/tenant";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useRoleOptions, roleCategoryChip } from "composables/useRoleOptions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import AppTextField from "components/common/AppTextField.vue";
import TempPasswordDialog from "components/temp_password_dialog.vue";

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();
const tenantStore = useTenantStore();
const { has } = usePermissions();
// Platform admins (tenants.write) manage any user/tenant; tenant admins (roles.assign) are scoped.
const isPlatformAdmin = computed(() => has(Permissions.TenantsWrite));
const canEdit = computed(() => has(Permissions.UsersWrite));
const canManageAssignments = computed(() => has(Permissions.RolesAssign));
const canResetPassword = computed(() => has(Permissions.UsersResetPassword));
const canManageGroups = computed(() => has(Permissions.UsersGroupManagement));

const userId = route.params.id;
const user = ref(null);
const loading = ref(false);
const firstName = ref("");
const lastName = ref("");
const email = ref("");
const phoneNumber = ref("");
const saving = ref(false);
const tenantOptions = ref([]);
const loadingTenants = ref(false);

// Grouped, category-labelled multi-role options (SuperAdmin excluded for non-Super-Admin callers).
const { roleOptions, loading: loadingRoles, loadForTenant } = useRoleOptions();

const targetIsSuperAdmin = computed(() =>
  !!user.value?.assignments?.some((a) => (a.roles || []).some((r) => r.role === "SuperAdmin")));
const canManageTarget = computed(() => isPlatformAdmin.value || !targetIsSuperAdmin.value);

// Platform admins see every assignment; tenant admins see only their active tenant's.
const visibleAssignments = computed(() => {
  const all = user.value?.assignments || [];
  return isPlatformAdmin.value ? all : all.filter((a) => a.tenantId === tenantStore.activeTenantId);
});

const tenantName = (id) => {
  const opt = tenantOptions.value.find((t) => t.value === id);
  if (opt) return opt.label;
  // Fall back to the signed-in user's own tenant list (it carries names) — covers Tenant Admins, who
  // cannot list all tenants, and any case where the tenant options failed to load.
  const mine = (tenantStore.assignments || []).find((t) => t.tenantId === id);
  return mine?.name || mine?.identifier || id;
};

const loadTenants = async () => {
  if (!isPlatformAdmin.value) return;
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

// Roles assignable within a tenant (system + the tenant's custom roles), grouped by category.
const loadRoles = async (tenantId) => {
  try {
    await loadForTenant(tenantId);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const load = async () => {
  loading.value = !user.value;
  try {
    user.value = await userApi.get(userId);
    firstName.value = user.value.firstName || "";
    lastName.value = user.value.lastName || "";
    phoneNumber.value = user.value.phoneNumber || "";
    email.value = user.value.email;
    department.value = user.value.department || null;
    isDepartmentHead.value = !!user.value.isDepartmentHead;
    isManagingShareholder.value = !!user.value.isManagingShareholder;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const save = async () => {
  // The username is the sign-in credential: the API only rejects duplicates, so the format is checked here
  // rather than letting an unusable address be saved.
  const nextEmail = (email.value || "").trim();
  if (!/^\S+@\S+\.\S+$/.test(nextEmail)) {
    notify.error("Enter a valid username (email address).");
    return;
  }

  // Changing it bumps TokenVersion server-side, ending every session the user has — confirm by name first
  // rather than signing someone out as a side effect of editing their profile.
  const currentEmail = user.value?.email || "";
  if (nextEmail.toLowerCase() !== currentEmail.toLowerCase()) {
    const ok = await confirm({
      title: "Change username",
      message: `Change the sign-in username for ${user.value?.displayName} from "${currentEmail}" to ` +
        `"${nextEmail}"? They will be signed out and must sign in with the new username.`,
      confirmLabel: "Change username"
    });
    if (!ok) return;
  }

  saving.value = true;
  try {
    await userApi.update(userId, {
      firstName: firstName.value,
      lastName: lastName.value,
      phoneNumber: phoneNumber.value,
      email: nextEmail
    });
    notify.success("User updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
  }
};

// ---- Department & approval roles ----
// Both are scoped to the active tenant and both are singletons. A department has exactly one head, and
// that head IS its REMS Department Director — saving the flag repoints the tenant's department-director
// mapping, which is what prefills an engagement's Department Director. The managing shareholder is the
// firm-wide equivalent: one per tenant, a required approver on every engagement. Taking either role from
// someone is confirmed by name first.
const department = ref(null);
const isDepartmentHead = ref(false);
const isManagingShareholder = ref(false);
const departmentOptions = ref([]);
const departmentHeads = ref([]);
const managingShareholder = ref(null);
const loadingDepartments = ref(false);
const savingRoles = ref(false);

const sameId = (a, b) => String(a || "").toLowerCase() === String(b || "").toLowerCase();

// These roles only mean something inside a tenant, so the section needs an assignment in the active one.
const inActiveTenant = computed(() =>
  (user.value?.assignments || []).some((a) => a.tenantId === tenantStore.activeTenantId));

const departmentLabel = (code) => departmentOptions.value.find((o) => o.value === code)?.label || code;

// Who holds the selected department today (possibly this same user).
const currentHead = computed(() => departmentHeads.value.find((h) => h.department === department.value) || null);

const headHint = computed(() => {
  if (!department.value) return "Pick a department to set a head.";
  if (!currentHead.value) return `${departmentLabel(department.value)} has no head yet.`;
  if (sameId(currentHead.value.userId, userId)) return "Heads this department, and is its REMS Department Director.";
  return `${currentHead.value.fullName} currently heads ${departmentLabel(department.value)}.`;
});

const shareholderHint = computed(() => {
  if (!managingShareholder.value) return "No managing shareholder set — engagement approvals will stall without one.";
  if (sameId(managingShareholder.value.userId, userId)) return "Approves every engagement in this tenant.";
  return `${managingShareholder.value.fullName} is currently the managing shareholder.`;
});

const departmentDirty = computed(() =>
  (department.value || null) !== (user.value?.department || null) ||
  isDepartmentHead.value !== !!user.value?.isDepartmentHead);

const shareholderDirty = computed(() => isManagingShareholder.value !== !!user.value?.isManagingShareholder);

const rolesDirty = computed(() => departmentDirty.value || shareholderDirty.value);

// Moving to a different department never carries headship across — it has to be granted deliberately.
const onDepartmentChange = (value) => {
  department.value = value;
  isDepartmentHead.value = value && value === user.value?.department ? !!user.value.isDepartmentHead : false;
};

const loadDepartments = async () => {
  loadingDepartments.value = true;
  try {
    const result = await userApi.departments();
    departmentOptions.value = result?.departments || [];
    departmentHeads.value = result?.heads || [];
    managingShareholder.value = result?.managingShareholder || null;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingDepartments.value = false;
  }
};

// Saves whichever of the two roles changed. Both are one-holder roles, so each takeover is confirmed by
// naming the person who loses it before anything is written.
const saveRoles = async () => {
  if (departmentDirty.value && isDepartmentHead.value && currentHead.value && !sameId(currentHead.value.userId, userId)) {
    const ok = await confirm({
      title: "Change department head",
      message: `${currentHead.value.fullName} currently heads ${departmentLabel(department.value)}. Make ` +
        `${user.value.displayName} the head instead? ${currentHead.value.fullName} will no longer be the ` +
        "Department Director on new engagements.",
      confirmLabel: "Make head"
    });
    if (!ok) return;
  }
  if (shareholderDirty.value && isManagingShareholder.value && managingShareholder.value &&
    !sameId(managingShareholder.value.userId, userId)) {
    const ok = await confirm({
      title: "Change managing shareholder",
      message: `${managingShareholder.value.fullName} is currently the managing shareholder. Make ` +
        `${user.value.displayName} the managing shareholder instead? ${managingShareholder.value.fullName} ` +
        "will no longer approve new engagements.",
      confirmLabel: "Make managing shareholder"
    });
    if (!ok) return;
  }

  savingRoles.value = true;
  try {
    const notes = [];
    if (departmentDirty.value) {
      const result = await userApi.setDepartment(userId, {
        department: department.value,
        isHead: isDepartmentHead.value
      });
      if (result?.demotedHeadName) notes.push(`${result.demotedHeadName} is no longer the department head.`);
    }
    if (shareholderDirty.value) {
      const result = await userApi.setManagingShareholder(userId, isManagingShareholder.value);
      if (result?.displacedName) notes.push(`${result.displacedName} is no longer the managing shareholder.`);
    }
    notify.success(["Approval roles updated.", ...notes].join(" "));
    await Promise.all([loadDepartments(), load()]);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
    // A partial save (one role written, the next rejected) still has to show what actually landed.
    await Promise.all([loadDepartments(), load()]);
  } finally {
    savingRoles.value = false;
  }
};

const toggleStatus = async () => {
  const activate = !user.value.isActive;
  const ok = await confirm({
    title: activate ? "Activate user" : "Deactivate user",
    message: `${activate ? "Activate" : "Deactivate"} ${user.value.displayName}?`,
    type: activate ? "primary" : "danger"
  });
  if (!ok) return;
  try {
    await userApi.setStatus(userId, activate);
    notify.success("Status updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const tempPwOpen = ref(false);
const tempPassword = ref("");
const resetPassword = async () => {
  const ok = await confirm({
    title: "Reset password",
    message: `Generate a new temporary password for ${user.value.displayName}? Their current sessions will end.`,
    confirmLabel: "Reset",
    type: "danger"
  });
  if (!ok) return;
  try {
    const result = await userApi.resetPassword(userId);
    tempPassword.value = result?.temporaryPassword || "";
    tempPwOpen.value = true;
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// Assignments
const assignOpen = ref(false);
const assignSaving = ref(false);
const assignForm = ref(null);
const assign = reactive({ tenantId: null, roleIds: [] });
// "add" = new tenant assignment; "edit" = manage the roles on an existing assignment (tenant locked).
const assignMode = ref("add");
const assignTitle = computed(() => (assignMode.value === "edit" ? "Manage Roles" : "Add assignment"));

const onAssignTenantChange = (tenantId) => {
  assign.tenantId = tenantId;
  assign.roleIds = [];
  loadRoles(assign.tenantId);
};

// Manage the roles on an existing assignment: pre-fill the tenant (locked) + its current role set, then
// reuse the assign endpoint which reconciles the assignment to exactly the submitted roles.
const openChangeRole = async (a) => {
  assignMode.value = "edit";
  assign.tenantId = a.tenantId;
  await loadRoles(a.tenantId);
  assign.roleIds = (a.roles || []).map((r) => r.roleId);
  assignOpen.value = true;
};

const openAssign = async () => {
  assignMode.value = "add";
  assign.roleIds = [];
  if (isPlatformAdmin.value) {
    assign.tenantId = null;
    await loadRoles(null); // clear any roles carried over from a previous open
    await loadTenants();
  } else {
    // Tenant Admin: assignment is scoped to their active tenant.
    assign.tenantId = tenantStore.activeTenantId;
    await loadRoles(assign.tenantId);
  }
  assignOpen.value = true;
};

const submitAssign = async ({ clearDraft } = {}) => {
  if (!(await assignForm.value?.validate())) return;
  if (!assign.tenantId) {
    notify.error("Select a tenant.");
    return;
  }
  if (!assign.roleIds.length) {
    notify.error("Select at least one role.");
    return;
  }
  assignSaving.value = true;
  try {
    // Reconcile: the endpoint adds/removes so the tenant's role set matches exactly (no remove/re-add).
    await userApi.assignTenantRole(userId, { tenantId: assign.tenantId, roleIds: assign.roleIds });
    notify.success("Assignment saved.");
    clearDraft?.();
    assignOpen.value = false;
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    assignSaving.value = false;
  }
};

const removeAssignment = async (a) => {
  const roleLabel = (a.roles || []).map((r) => r.roleName || r.role).join(", ") || "all roles";
  const ok = await confirm({
    title: "Remove assignment",
    message: `Remove ${roleLabel} on ${tenantName(a.tenantId)}?`,
    confirmLabel: "Remove",
    type: "danger"
  });
  if (!ok) return;
  try {
    await userApi.removeTenantRole(userId, a.tenantId);
    notify.success("Assignment removed.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Groups ----
const groupsOpen = ref(false);
const groupList = ref([]);
const loadingGroups = ref(false);
const selectedGroupIds = ref([]);
const newGroupName = ref("");
const creatingGroup = ref(false);
const savingGroups = ref(false);

const loadGroups = async () => {
  loadingGroups.value = true;
  try {
    groupList.value = (await userGroupApi.list()) || [];
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingGroups.value = false;
  }
};

const openGroups = async () => {
  selectedGroupIds.value = (user.value?.groups || []).map((g) => g.id);
  newGroupName.value = "";
  groupsOpen.value = true;
  await loadGroups();
};

// Create a group inline and add it to the current selection.
const createGroup = async () => {
  const name = newGroupName.value.trim();
  if (!name) return;
  creatingGroup.value = true;
  try {
    const created = await userGroupApi.create({ name });
    if (!groupList.value.some((g) => g.id === created.id)) {
      groupList.value = [...groupList.value, created];
    }
    if (!selectedGroupIds.value.includes(created.id)) {
      selectedGroupIds.value = [...selectedGroupIds.value, created.id];
    }
    newGroupName.value = "";
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    creatingGroup.value = false;
  }
};

// Delete a group entirely (removes it for every user in the tenant).
const deleteGroup = async (g) => {
  const ok = await confirm({
    title: "Delete group",
    message: `Delete the group "${g.name}"? It will be removed from all users.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await userGroupApi.remove(g.id);
    groupList.value = groupList.value.filter((x) => x.id !== g.id);
    selectedGroupIds.value = selectedGroupIds.value.filter((id) => id !== g.id);
    notify.success("Group deleted.");
    load(); // refresh the user's chips in case they were a member
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

const saveGroups = async () => {
  savingGroups.value = true;
  try {
    await userApi.setGroups(userId, selectedGroupIds.value);
    notify.success("Groups updated.");
    groupsOpen.value = false;
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    savingGroups.value = false;
  }
};

onMounted(async () => {
  await Promise.all([loadTenants(), loadDepartments()]);
  await load();
});
</script>

<style scoped>
.user-card {
  border-radius: 12px;
}
</style>
