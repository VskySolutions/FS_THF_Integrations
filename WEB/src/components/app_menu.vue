<template>
  <q-list class="app-menu q-py-sm">
    <q-item
      v-for="item in visibleItems"
      :key="item.label"
      v-ripple
      clickable
      :to="item.to"
      exact
      active-class="text-primary bg-blue-1"
    >
      <q-item-section avatar>
        <q-icon :name="item.icon" />
      </q-item-section>
      <q-item-section>{{ item.label }}</q-item-section>
    </q-item>
  </q-list>
</template>

<script setup>
import { computed } from "vue";
import { useTenantStore } from "stores/tenant";

const tenantStore = useTenantStore();

// `roles: null` → visible to everyone. Otherwise restricted to the active tenant role.
const items = [
  { label: "Home", icon: "o_home", to: "/", roles: null },
  { label: "Integration Jobs", icon: "o_sync", to: "/jobs", roles: null },
  { label: "Logs", icon: "o_description", to: "/logs", roles: ["TenantAdmin", "SuperAdmin"] },
  { label: "Mapping Config", icon: "o_swap_horiz", to: "/mappings", roles: ["TenantAdmin", "SuperAdmin"] },
  { label: "Users", icon: "o_group", to: "/users", roles: ["TenantAdmin", "SuperAdmin"] },
  { label: "Tenants", icon: "o_apartment", to: "/tenants", roles: ["SuperAdmin"] },
  { label: "Health", icon: "o_monitor_heart", to: "/health", roles: ["TenantAdmin", "SuperAdmin"] },
  { label: "Account", icon: "o_manage_accounts", to: "/account", roles: null }
];

const visibleItems = computed(() => {
  const role = tenantStore.activeRole;
  return items.filter((item) => !item.roles || (role && item.roles.includes(role)));
});
</script>
