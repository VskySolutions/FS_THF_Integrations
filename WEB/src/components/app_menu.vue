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
      { label: "Tenants", icon: "o_apartment", to: "/tenants", permissions: [Permissions.TenantsWrite] }
    ]
  },
  {
    key: "access-management",
    label: "Access Management",
    items: [
      { label: "Permission Groups", icon: "o_workspaces", to: "/permission-groups", permissions: [Permissions.GroupsManage] },
      { label: "Person", icon: "o_badge", to: "/persons", permissions: [Permissions.PersonsRead] },
      { label: "Roles", icon: "o_admin_panel_settings", to: "/roles", permissions: [Permissions.RolesWrite] },
      { label: "Users", icon: "o_group", to: "/users", permissions: [Permissions.UsersRead] },
      { label: "User Groups", icon: "o_groups", to: "/user-groups", permissions: [Permissions.UsersGroupManagement] }
    ]
  },
  {
    // Tenant-wide settings and universal features (email, and future cross-cutting settings).
    key: "settings",
    label: "Tenant Settings",
    items: [
      { label: "Email Accounts", icon: "o_mail", to: "/smtp-accounts", permissions: [Permissions.EmailManage] },
      { label: "Email Templates", icon: "o_drafts", to: "/email-templates", permissions: [Permissions.EmailManage] },
      { label: "Option Sets", icon: "o_list_alt", to: "/option-sets", permissions: [Permissions.OptionSetsRead] },
      { label: "Tag Management", icon: "o_label", to: "/settings/tags", permissions: [Permissions.SettingsManage] },
      { label: "Saved Views", icon: "o_view_list", to: "/settings/saved-views", permissions: [Permissions.SettingsManage] },
      { label: "Sticky Notes", icon: "o_sticky_note_2", to: "/settings/sticky-notes", permissions: [Permissions.SettingsManage] },
      { label: "Modified Log", icon: "o_manage_history", to: "/settings/modified-log-config", permissions: [Permissions.SettingsManage] },
      { label: "Deleted Records", icon: "o_restore_from_trash", to: "/settings/retention", permissions: [Permissions.RecordsAdminDelete] }
    ]
  },
  {
    // REMS (Phase 15). Each item is gated by its own permission — never a role name — so a user sees
    // only the areas their roles grant (e.g. an Approver-only user holds just rems.approvals.act and
    // therefore sees only "Approvals", never the Partner/Admin items). AC-ADM-019.5 / REQ-REMS-001.7.
    key: "rems",
    label: "REMS",
    items: [
      { label: "Partner Dashboard", icon: "o_space_dashboard", to: "/rems/partner", permissions: [Permissions.RemsRequestsRead] },
      { label: "Admin Pool", icon: "o_inbox", to: "/rems/admin-pool", permissions: [Permissions.RemsPoolRead] },
      { label: "EMS Inbox", icon: "o_move_to_inbox", to: "/rems/ems-inbox", permissions: [Permissions.RemsFormsManage] },
      { label: "Client Forms", icon: "o_dynamic_form", to: "/rems/client-forms", permissions: [Permissions.RemsEngagementsManage] },
      { label: "Approvals", icon: "o_approval", to: "/rems/approvals", permissions: [Permissions.RemsApprovalsAct] }
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
