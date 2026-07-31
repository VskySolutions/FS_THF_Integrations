<template>
  <q-list class="app-menu q-py-xs">
    <template v-for="section in visibleSections" :key="section.key">
      <!-- Ungrouped items (no label, e.g. Dashboard) render flat at the top. -->
      <template v-if="!section.label">
        <q-item
          v-for="item in section.items"
          :key="item.label"
          v-ripple
          dense
          clickable
          :to="item.to"
          :exact="item.exact"
          active-class="text-primary bg-blue-1"
          @click="onItem(item)"
        >
          <q-item-section avatar><q-icon :name="item.icon" size="20px" /></q-item-section>
          <q-item-section>{{ item.label }}</q-item-section>
        </q-item>
      </template>

      <!-- Labelled sections are collapsible groups. Open by default; collapse state is remembered
           while the drawer stays mounted. The group that contains the active route stays open. -->
      <q-expansion-item
        v-else
        dense
        :icon="section.icon"
        :label="section.label"
        :model-value="isOpen(section)"
        header-class="app-menu__group text-primary text-weight-bold"
        @update:model-value="(v) => setOpen(section.key, v)"
      >
        <q-item
          v-for="item in section.items"
          :key="item.label"
          v-ripple
          dense
          clickable
          :to="item.to"
          :exact="item.exact"
          active-class="text-primary bg-blue-1"
          class="app-menu__nested"
          @click="onItem(item)"
        >
          <q-item-section avatar><q-icon :name="item.icon" size="20px" /></q-item-section>
          <q-item-section>{{ item.label }}</q-item-section>
        </q-item>
      </q-expansion-item>
    </template>
  </q-list>
</template>

<script setup>
import { computed, reactive } from "vue";
import { LocalStorage } from "quasar";
import { useRouter } from "vue-router";
import { useAuthStore } from "stores/auth";
import { Permissions } from "composables/usePermissions";

const authStore = useAuthStore();
const router = useRouter();

// Menu items with an `action` (e.g. Logout) run a handler instead of navigating.
const onItem = async (item) => {
  if (item.action === "logout") {
    await authStore.logout();
    router.replace({ name: "login" });
  } else if (item.action === "logoutAll") {
    await authStore.logoutAll();
    router.replace({ name: "login" });
  }
};

// Ordered by application flow: overview → set up → configure → operate → personal.
// `permissions: null` → visible to every authenticated user; otherwise visible when the active
// tenant grants any one of the listed permissions. `icon` labels the collapsible group header.
const sections = [
  {
    key: "overview",
    label: null,
    items: [
      { label: "Dashboard", icon: "o_dashboard", to: "/dashboard", permissions: null }
    ]
  },
  {
    // REMS (Phase 15). Each item is gated by its own permission — never a role name — so a user sees
    // only the areas their roles grant (e.g. an Approver-only user holds just rems.approvals.act and
    // therefore sees only "Approvals", never the Partner/Admin items). AC-ADM-019.5 / REQ-REMS-001.7.
    key: "rems",
    label: "REMS",
    icon: "o_business_center",
    items: [
      { label: "Partner Dashboard", icon: "o_space_dashboard", to: "/rems/partner", permissions: [Permissions.RemsRequestsRead] },
      { label: "Admin Pool", icon: "o_inbox", to: "/rems/admin-pool", permissions: [Permissions.RemsPoolRead] },
      { label: "EMS Inbox", icon: "o_move_to_inbox", to: "/rems/ems-inbox", permissions: [Permissions.RemsFormsManage] },
      { label: "Client Forms", icon: "o_dynamic_form", to: "/rems/client-forms", permissions: [Permissions.RemsEngagementsManage] },
      { label: "Approvals", icon: "o_approval", to: "/rems/approvals", permissions: [Permissions.RemsApprovalsAct] }
    ]
  },
  {
    key: "access-management",
    label: "Access Management",
    icon: "o_lock",
    items: [
      { label: "Permission Groups", icon: "o_workspaces", to: "/permission-groups", permissions: [Permissions.GroupsManage] },
      { label: "Person", icon: "o_badge", to: "/persons", permissions: [Permissions.PersonsRead] },
      { label: "Roles", icon: "o_admin_panel_settings", to: "/roles", permissions: [Permissions.RolesWrite] },
      { label: "Users", icon: "o_group", to: "/users", permissions: [Permissions.UsersRead] },
      { label: "User Groups", icon: "o_groups", to: "/user-groups", permissions: [Permissions.UsersGroupManagement] }
    ]
  },
  {
    key: "administration",
    label: "Administration",
    icon: "o_corporate_fare",
    items: [
      { label: "Tenants", icon: "o_apartment", to: "/tenants", permissions: [Permissions.TenantsWrite] }
    ]
  },
  {
    // Tenant-wide settings and universal features (email, and future cross-cutting settings).
    key: "settings",
    label: "Tenant Settings",
    icon: "o_settings",
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
    key: "account",
    label: "Account",
    icon: "o_account_circle",
    items: [
      { label: "My Account", icon: "o_manage_accounts", to: "/account", permissions: null },
      { label: "Profile", icon: "o_person", to: { name: "profile" }, permissions: null },
      { label: "Change Password", icon: "o_lock", to: { name: "change_password" }, permissions: null },
      { label: "My Mentions", icon: "o_alternate_email", to: { name: "uf_mentions" }, permissions: null },
      { label: "My Pinned", icon: "o_push_pin", to: { name: "uf_pinned" }, permissions: null },
      { label: "Notification Preferences", icon: "o_tune", to: { name: "uf_notification_preferences" }, permissions: null },
      { label: "Logout", icon: "o_logout", action: "logout", permissions: null },
      { label: "Logout all devices", icon: "o_devices", action: "logoutAll", permissions: null }
    ]
  }
];

const canSee = (permissions) => !permissions || authStore.hasAnyPermission(permissions);

const visibleSections = computed(() =>
  sections
    .map((section) => ({ ...section, items: section.items.filter((item) => canSee(item.permissions)) }))
    .filter((section) => section.items.length));

// Per-group collapse state, persisted to LocalStorage so the user's expand/collapse choices survive a
// page refresh. The stored object holds only the collapsed groups ({ [sectionKey]: true }).
const STORAGE_KEY = "appMenuCollapsed";
const collapsed = reactive(LocalStorage.getItem(STORAGE_KEY) || {});
const isOpen = (section) => collapsed[section.key] !== true;
const setOpen = (key, open) => {
  if (open) {
    delete collapsed[key];
  } else {
    collapsed[key] = true;
  }
  LocalStorage.set(STORAGE_KEY, { ...collapsed });
};
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
/* Slim, uppercase collapsible group headers in the theme colour. */
.app-menu :deep(.app-menu__group) {
  min-height: 38px;
  padding: 4px 12px;
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.app-menu :deep(.app-menu__group .q-item__section--avatar) {
  min-width: 30px;
  padding-right: 6px;
}
/* Indent the items within a group so the hierarchy reads clearly. */
.app-menu__nested {
  padding-left: 20px;
}
</style>
