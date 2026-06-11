<template>
  <q-list class="app-menu q-py-sm">
    <template v-for="(section, index) in visibleSections" :key="section.key">
      <q-separator v-if="index > 0" class="q-my-sm" />
      <q-item-label v-if="section.label" header class="text-grey-7 text-weight-medium">
        {{ section.label }}
      </q-item-label>
      <q-item
        v-for="item in section.items"
        :key="item.label"
        v-ripple
        clickable
        :to="item.to"
        :exact="item.exact"
        active-class="text-primary bg-blue-1"
      >
        <q-item-section avatar>
          <q-icon :name="item.icon" />
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
      { label: "Dashboard", icon: "o_dashboard", to: "/", exact: true, permissions: null }
    ]
  },
  {
    key: "administration",
    label: "Administration",
    items: [
      { label: "Tenants", icon: "o_apartment", to: "/tenants", permissions: [Permissions.TenantsWrite] },
      { label: "Users", icon: "o_group", to: "/users", permissions: [Permissions.UsersRead] },
      { label: "Roles", icon: "o_admin_panel_settings", to: "/roles", permissions: [Permissions.RolesWrite] }
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
