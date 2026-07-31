<template>
  <q-page padding>
    <app-detail-header
      :items="[
        { label: 'Permission Groups', to: { name: 'permission_groups' } },
        { label: detail?.name || 'Permission Group' }
      ]"
      :back-to="{ name: 'permission_groups' }"
    >
      <template #actions>
        <q-badge v-if="detail" :color="detail.isActive ? 'positive' : 'grey'" class="q-mr-sm">
          {{ detail.isActive ? "Active" : "Inactive" }}
        </q-badge>
        <q-badge v-if="detail" :color="detail.isFull ? 'negative' : 'grey-7'" class="q-mr-sm" data-test="members-badge">
          <q-icon v-if="detail.isFull" name="o_block" size="12px" class="q-mr-xs" />
          {{ detail.currentUsage }}<template v-if="detail.capacityLimit != null"> / {{ detail.capacityLimit }}</template> members
        </q-badge>
        <q-btn v-if="detail" flat round dense color="primary" icon="o_edit" @click="openEdit">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="detail" flat round dense color="primary" :icon="detail.isActive ? 'o_toggle_off' : 'o_toggle_on'" @click="toggleStatus">
          <q-tooltip>{{ detail.isActive ? "Deactivate" : "Activate" }}</q-tooltip>
        </q-btn>
        <q-btn v-if="detail" flat round dense color="negative" icon="o_delete" @click="removeGroup">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </app-detail-header>

    <div v-if="loading" class="row flex-center q-pa-xl"><q-spinner color="primary" size="40px" /></div>

    <template v-else-if="detail">
      <q-banner v-if="!detail.isActive" dense rounded class="bg-orange-1 text-orange-9 q-mb-md" data-test="inactive-banner">
        <template #avatar><q-icon name="o_warning" color="warning" /></template>
        This group is inactive and contributes zero permissions to all roles.
      </q-banner>

      <!-- Permission Keys -->
      <q-card flat bordered class="pg-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">
          Permission Keys
          <q-badge color="primary" class="q-ml-sm">{{ (detail.permissionKeys || []).length }}</q-badge>
        </q-card-section>
        <q-separator />
        <q-card-section>
          <div v-if="!detail.permissionKeys || !detail.permissionKeys.length" class="text-grey-6">No permission keys.</div>
          <div v-for="group in keyGroups" :key="group.category" class="q-mb-md">
            <div class="section-subhead q-mb-xs">{{ group.category }}</div>
            <div class="row q-gutter-xs">
              <q-badge v-for="key in group.keys" :key="key" color="blue-1" text-color="primary" class="pg-key">
                {{ humanizeKey(key) }}
                <q-tooltip>{{ key }}</q-tooltip>
              </q-badge>
            </div>
          </div>
        </q-card-section>
      </q-card>

      <!-- Roles Using This Group -->
      <q-card flat bordered class="pg-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Roles Using This Group</q-card-section>
        <q-separator />
        <q-list separator>
          <q-item
            v-for="role in detail.rolesUsing" :key="role.roleId" clickable
            @click="goToRole(role)"
          >
            <q-item-section avatar><q-icon name="o_admin_panel_settings" color="grey-7" /></q-item-section>
            <q-item-section>{{ role.roleName }}</q-item-section>
            <q-item-section side><q-icon name="o_chevron_right" color="grey-6" /></q-item-section>
          </q-item>
          <q-item v-if="!detail.rolesUsing || !detail.rolesUsing.length">
            <q-item-section class="text-grey-6">No roles use this group yet.</q-item-section>
          </q-item>
        </q-list>
      </q-card>

      <!-- Audit Trail -->
      <q-card flat bordered class="pg-card q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Audit Trail</q-card-section>
        <q-separator />
        <q-list separator>
          <q-item v-for="(entry, i) in auditTrail" :key="i">
            <q-item-section avatar><q-icon name="o_history" color="grey-7" /></q-item-section>
            <q-item-section>
              <q-item-label>{{ entry.action }}</q-item-label>
              <q-item-label caption>
                {{ entry.performedBy || "System" }} · {{ fmt.formatDateTime(entry.performedOnUtc) }}
                <template v-if="entry.details"> — {{ entry.details }}</template>
              </q-item-label>
            </q-item-section>
          </q-item>
          <q-item v-if="!auditTrail.length">
            <q-item-section class="text-grey-6">No activity yet.</q-item-section>
          </q-item>
        </q-list>
      </q-card>
    </template>

    <permission-group-form-drawer v-model="formOpen" :group-id="groupId" @saved="onSaved" />
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { permissionGroupApi, getApiErrorMessage } from "services/api";
import { useNotify } from "composables/useNotify";
import { useConfirm } from "composables/useConfirm";
import { useDateFormat } from "composables/useDateFormat";
import { humanizeKey, groupKeysByCategory } from "composables/usePermissionCategories";
import AppDetailHeader from "components/common/AppDetailHeader.vue";
import PermissionGroupFormDrawer from "modules/permission-group/components/PermissionGroupFormDrawer.vue";

const route = useRoute();
const router = useRouter();
const notify = useNotify();
const { confirm } = useConfirm();
const fmt = useDateFormat();

const groupId = route.params.id;
const loading = ref(true);
const detail = ref(null);

const keyGroups = computed(() => groupKeysByCategory(detail.value?.permissionKeys || []));

// Audit entries newest-first.
const auditTrail = computed(() => {
  const list = [...(detail.value?.auditTrail || [])];
  return list.sort((a, b) => new Date(b.performedOnUtc) - new Date(a.performedOnUtc));
});

const load = async () => {
  loading.value = true;
  try {
    detail.value = await permissionGroupApi.get(groupId);
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  } finally {
    loading.value = false;
  }
};

const goToRole = () => {
  // Roles are managed on the central role page; navigate there for editing.
  router.push({ name: "roles" });
};

// ---- Edit ----
const formOpen = ref(false);
const openEdit = () => { formOpen.value = true; };
const onSaved = () => { formOpen.value = false; load(); };

// ---- Activate / Deactivate ----
const toggleStatus = async () => {
  try {
    await permissionGroupApi.setStatus(groupId, !detail.value.isActive);
    notify.success(detail.value.isActive ? "Group deactivated." : "Group activated.");
    load();
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

// ---- Delete ----
const removeGroup = async () => {
  const ok = await confirm({
    title: "Delete permission group",
    message: `Delete "${detail.value.name}"? Roles using it will lose its permissions. This cannot be undone.`,
    confirmLabel: "Delete",
    type: "danger"
  });
  if (!ok) return;
  try {
    await permissionGroupApi.remove(groupId);
    notify.success("Permission group deleted.");
    router.push({ name: "permission_groups" });
  } catch (err) {
    notify.error(getApiErrorMessage(err));
  }
};

onMounted(load);
</script>

<style scoped>
.pg-card {
  border-radius: 12px;
}
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
