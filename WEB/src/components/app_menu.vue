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
import { useTenantStore } from "stores/tenant";

const tenantStore = useTenantStore();

const TENANT_ADMIN = ["TenantAdmin", "SuperAdmin"];

// Ordered by application flow: overview → set up → configure → operate → personal.
// `roles: null` → visible to everyone; otherwise restricted to the active tenant role.
const sections = [
  {
    key: "overview",
    label: null,
    items: [
      { label: "Dashboard", icon: "o_dashboard", to: "/", exact: true, roles: null }
    ]
  },
  {
    key: "administration",
    label: "Administration",
    items: [
      { label: "Tenants", icon: "o_apartment", to: "/tenants", roles: ["SuperAdmin"] },
      { label: "Users", icon: "o_group", to: "/users", roles: TENANT_ADMIN }
    ]
  },
  {
    key: "configuration",
    label: "Configuration",
    items: [
      { label: "Mapping Config", icon: "o_swap_horiz", to: "/mappings", roles: TENANT_ADMIN }
    ]
  },
  {
    key: "operations",
    label: "Operations",
    items: [
      { label: "Integration Jobs", icon: "o_sync", to: "/jobs", roles: null },
      { label: "Logs", icon: "o_description", to: "/logs", roles: TENANT_ADMIN },
      { label: "Health", icon: "o_monitor_heart", to: "/health", roles: TENANT_ADMIN }
    ]
  },
  {
    key: "account",
    label: "Account",
    items: [
      { label: "My Account", icon: "o_manage_accounts", to: "/account", roles: null }
    ]
  }
];

const canSee = (roles) => {
  if (!roles) return true;
  const role = tenantStore.activeRole;
  return !!role && roles.includes(role);
};

const visibleSections = computed(() =>
  sections
    .map((section) => ({ ...section, items: section.items.filter((item) => canSee(item.roles)) }))
    .filter((section) => section.items.length));
</script>
