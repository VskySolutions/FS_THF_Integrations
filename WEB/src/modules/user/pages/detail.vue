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
          <q-input v-model="email" outlined dense stack-label label="Email" class="col-12 col-sm-6" :readonly="!canEdit" />
          <q-input v-model="phoneNumber" outlined dense stack-label label="Phone Number" class="col-12 col-sm-6" :readonly="!canEdit" />
        </q-card-section>
        <q-card-actions v-if="canEdit" align="right">
          <q-btn unelevated no-caps color="primary" label="Save" :loading="saving" @click="save" />
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

      <!-- Tenant assignments (requires roles.assign) -->
      <q-card v-if="canManageAssignments" flat bordered class="user-card">
        <q-card-section class="row items-center">
          <div class="text-subtitle1 text-weight-medium">Tenant assignments</div>
          <q-space />
          <q-btn unelevated no-caps color="primary" icon="o_add" label="Add" @click="openAssign" />
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
              <q-item-label caption class="text-capitalize">{{ a.roleName || a.role }}</q-item-label>
            </q-item-section>
            <q-item-section side>
              <q-btn flat round dense color="negative" icon="o_delete" @click="removeAssignment(a)" />
            </q-item-section>
          </q-item>
          <q-item v-if="!visibleAssignments.length">
            <q-item-section class="text-grey-6">No assignments.</q-item-section>
          </q-item>
        </q-list>
      </q-card>
    </div>

    <!-- Add assignment -->
    <app-form-drawer v-model="assignOpen" title="Add assignment" :saving="assignSaving" @submit="submitAssign" @cancel="assignOpen = false">
      <q-form ref="assignForm" greedy>
        <app-select
          v-if="isPlatformAdmin" v-model="assign.tenantId" :options="tenantOptions" :loading="loadingTenants"
          label="Tenant *" class="q-mb-md" :clearable="false" @update:model-value="onAssignTenantChange"
        />
        <app-select v-model="assign.roleId" :options="roleOptions" :loading="loadingRoles" label="Role *" :clearable="false" />
      </q-form>
    </app-form-drawer>

    <temp-password-dialog v-model="tempPwOpen" :password="tempPassword" />
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import { userApi, tenantApi, roleApi, getApiErrorMessage } from "services/api";
import { useTenantStore } from "stores/tenant";
import { usePermissions, Permissions } from "composables/usePermissions";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import AppFormDrawer from "components/common/AppFormDrawer.vue";
import AppSelect from "components/common/AppSelect.vue";
import TempPasswordDialog from "components/temp_password_dialog.vue";

const route = useRoute();
const notify = useNotify();
const { confirm } = useConfirm();
const tenantStore = useTenantStore();
const { has } = usePermissions();
// Platform admins (tenants.write) manage any user/tenant; tenant admins (roles.assign) are scoped.
const isPlatformAdmin = computed(() => has(Permissions.TenantsWrite));
const canEdit = computed(() => has(Permissions.UsersWrite));
const canManageAssignments = computed(() => has(Permissions.RolesAssign));
const canResetPassword = computed(() => has(Permissions.UsersResetPassword));

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

const roleOptions = ref([]);
const loadingRoles = ref(false);

const targetIsSuperAdmin = computed(() => !!user.value?.assignments?.some((a) => a.role === "SuperAdmin"));
const canManageTarget = computed(() => isPlatformAdmin.value || !targetIsSuperAdmin.value);

// Platform admins see every assignment; tenant admins see only their active tenant's.
const visibleAssignments = computed(() => {
  const all = user.value?.assignments || [];
  return isPlatformAdmin.value ? all : all.filter((a) => a.tenantId === tenantStore.activeTenantId);
});

const tenantName = (id) => tenantOptions.value.find((t) => t.value === id)?.label || id;

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

// Roles assignable within a tenant (system + the tenant's custom roles).
const loadRoles = async (tenantId) => {
  roleOptions.value = [];
  if (!tenantId) return;
  loadingRoles.value = true;
  try {
    const roles = await roleApi.tenantRoles(tenantId);
    roleOptions.value = (roles || []).map((r) => ({ label: r.name, value: r.id }));
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loadingRoles.value = false;
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
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const save = async () => {
  saving.value = true;
  try {
    await userApi.update(userId, { firstName: firstName.value, lastName: lastName.value, phoneNumber: phoneNumber.value, email: email.value });
    notify.success("User updated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    saving.value = false;
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
const assign = reactive({ tenantId: null, roleId: null });

const onAssignTenantChange = (tenantId) => {
  assign.tenantId = tenantId;
  assign.roleId = null;
  loadRoles(assign.tenantId);
};

const openAssign = async () => {
  assign.roleId = null;
  if (isPlatformAdmin.value) {
    assign.tenantId = null;
    roleOptions.value = [];
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
  if (!assign.roleId) {
    notify.error("Select a role.");
    return;
  }
  assignSaving.value = true;
  try {
    await userApi.assignTenantRole(userId, { tenantId: assign.tenantId, roleId: assign.roleId });
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
  const ok = await confirm({
    title: "Remove assignment",
    message: `Remove ${a.roleName || a.role} on ${tenantName(a.tenantId)}?`,
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

onMounted(async () => {
  await loadTenants();
  await load();
});
</script>

<style scoped>
.user-card {
  border-radius: 12px;
}
</style>
