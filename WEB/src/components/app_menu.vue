<template>
  <q-list class="app-menu q-py-xs">
    <template v-for="(section, index) in visibleSections" :key="section.key">
      <q-separator v-if="index > 0" class="q-my-xs" />
      <q-item-label v-if="section.label" header class="app-menu__header text-primary text-weight-bold">
        {{ section.label }}
      </q-item-label>
      <q-item
        v-for="item in section.items"
        :key="item.label"
        v-ripple
        dense
        clickable
        :to="item.to"
        :exact="item.exact"
        active-class="text-primary bg-blue-1"
      >
        <q-item-section avatar>
          <q-icon :name="item.icon" size="20px" />
        </q-item-section>
        <q-item-section>{{ item.label }}</q-item-section>
      </q-item>
    </template>
  </q-list>
</template>

<script setup>
import { computed } from "vue";
import { useAuthStore } from "stores/auth";
import { Permissions } from "composables/usePermissions";

const authStore = useAuthStore();

// Ordered by application flow: overview → set up → configure → operate → personal.
// `permissions: null` → visible to every authenticated user; otherwise visible when the active
// tenant grants any one of the listed permissions.
const sections = [
  {
    key: "overview",
    label: null,
    items: [
      { label: "Dashboard", icon: "o_dashboard", to: "/dashboard", permissions: null }
    ]
  },
  {
    key: "administration",
    label: "Administration",
    items: [
      { label: "Tenants", icon: "o_apartment", to: "/tenants", permissions: [Permissions.TenantsWrite] },
      { label: "Person", icon: "o_badge", to: "/persons", permissions: [Permissions.PersonsRead] },
      { label: "Customers", icon: "o_groups", to: "/customers", permissions: null }
    ]
  },
  {
    key: "configuration",
    label: "Configuration",
    items: [
      { label: "Mapping Config", icon: "o_swap_horiz", to: "/mappings", permissions: [Permissions.MappingsRead] }
    ]
  },
  {
    key: "operations",
    label: "Operations",
    items: [
      { label: "Integration Jobs", icon: "o_sync", to: "/jobs", permissions: [Permissions.JobsRead] },
      { label: "Logs", icon: "o_description", to: "/logs", permissions: [Permissions.LogsRead] },
      { label: "Health", icon: "o_monitor_heart", to: "/health", permissions: [Permissions.HealthRead] }
    ]
  },
  {
    key: "access-management",
    label: "Access Management",
    items: [
      { label: "Permission Groups", icon: "o_workspaces", to: "/permission-groups", permissions: [Permissions.GroupsManage] },
      { label: "Roles", icon: "o_admin_panel_settings", to: "/roles", permissions: [Permissions.RolesWrite] },
      { label: "Users", icon: "o_group", to: "/users", permissions: [Permissions.UsersRead] },
      { label: "User Groups", icon: "o_groups", to: "/user-groups", permissions: [Permissions.UsersGroupManagement] }
    ]
  },
  {
    // Tenant-wide settings and universal features (email, and future cross-cutting settings).
    key: "settings",
    label: "Settings",
    items: [
      { label: "Email Accounts", icon: "o_mail", to: "/smtp-accounts", permissions: [Permissions.EmailManage] },
      { label: "Email Templates", icon: "o_drafts", to: "/email-templates", permissions: [Permissions.EmailManage] },
      { label: "Tag Management", icon: "o_label", to: "/settings/tags", permissions: [Permissions.SettingsManage] },
      { label: "Saved Views", icon: "o_view_list", to: "/settings/saved-views", permissions: [Permissions.SettingsManage] },
      { label: "Sticky Notes", icon: "o_sticky_note_2", to: "/settings/sticky-notes", permissions: [Permissions.SettingsManage] },
      { label: "Modified Log", icon: "o_manage_history", to: "/settings/modified-log-config", permissions: [Permissions.SettingsManage] },
      { label: "Deleted Records", icon: "o_restore_from_trash", to: "/settings/retention", permissions: [Permissions.RecordsAdminDelete] }
    ]
  },
  {
    key: "account",
    label: "Account",
    items: [
      { label: "My Account", icon: "o_manage_accounts", to: "/account", permissions: null }
    ]
  }
];

const canSee = (permissions) => !permissions || authStore.hasAnyPermission(permissions);

const visibleSections = computed(() =>
  sections
    .map((section) => ({ ...section, items: section.items.filter((item) => canSee(item.permissions)) }))
    .filter((section) => section.items.length));
</script>

<style scoped>
/* Compact spacing — the drawer holds many items. */
.app-menu :deep(.q-item) {
  min-height: 34px;
}
/* Tighten the icon gutter so icon + label sit close together. */
.app-menu :deep(.q-item__section--avatar) {
  min-width: 32px;
  padding-right: 8px;
}
/* Slim, uppercase section headers in the theme colour — made to stand out. */
.app-menu__header {
  min-height: auto;
  padding: 10px 16px 2px;
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  line-height: 1.2;
  opacity: 1;
}
</style>
