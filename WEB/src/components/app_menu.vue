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
          active-class="text-primary bg-teal-1"
          @click="onItem(item)"
        >
          <q-item-section avatar><q-icon :name="item.icon" size="20px" /></q-item-section>
          <q-item-section>{{ item.label }}</q-item-section>
          <q-tooltip v-if="mini" anchor="center right" self="center left">{{ item.label }}</q-tooltip>
        </q-item>
      </template>

      <!-- Collapsed, a group is a single icon: Quasar hides expansion content in a mini drawer, so
           opening one in place would be a dead click. It reopens the menu instead. -->
      <q-item
        v-else-if="mini"
        :key="`${section.key}-mini`"
        v-ripple
        dense
        clickable
        @click="emit('expand')"
      >
        <q-item-section avatar><q-icon :name="section.icon" size="20px" /></q-item-section>
        <q-item-section>{{ section.label }}</q-item-section>
        <q-tooltip anchor="center right" self="center left">{{ section.label }}</q-tooltip>
      </q-item>

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
          active-class="text-primary bg-teal-1"
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

defineProps({
  // The drawer is collapsed to its icon rail: labels are hidden by Quasar, and groups cannot open in
  // place, so they ask the layout to expand the menu instead.
  mini: { type: Boolean, default: false }
});
const emit = defineEmits(["expand"]);

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
    // REMS (Phase 15). Each item is gated by its own permission — never a role name — so a user sees only
    // the areas their roles grant. AC-ADM-019.5 / REQ-REMS-001.7. Approvals is the exception and is open
    // to everyone: anyone can be made an approver (the CSE, a commission recipient, or someone added on
    // the Approval tab), no permission governs it, and a role gate there would hide the page from a real
    // approver. Users with no tasks simply see an empty inbox.
    key: "rems",
    label: "REMS",
    icon: "o_business_center",
    // Three lists, one per role. The Admin Pool and EMS Inbox are gone: the initiator now fills the whole
    // request and sends the intake link themselves, so nothing ever waits in a pool to be picked up, and
    // what used to be two admin queues is one review queue.
    items: [
      { label: "My Requests", icon: "o_space_dashboard", to: "/rems/partner", permissions: [Permissions.RemsRequestsRead] },
      { label: "EMS Review", icon: "o_fact_check", to: "/rems/ems-review", permissions: [Permissions.RemsEngagementsManage] },
      { label: "Approvals", icon: "o_approval", to: "/rems/approvals", permissions: null }
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
      // Gated on MANAGE, not read: Partner and REMS Admin hold optionSets.read so their dropdowns resolve,
      // but the lists are configuration and only Super Admin / Tenant Admin maintain them.
      { label: "Option Sets", icon: "o_list_alt", to: "/option-sets", permissions: [Permissions.OptionSetsManage] },
      { label: "Tag Management", icon: "o_label", to: "/settings/tags", permissions: [Permissions.SettingsManage] },
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
      // The full list behind the bell's "View all". Named and ordered as the avatar menu has it, since
      // both lead to the same four pages and reading differently in each is what makes one look missing.
      { label: "My Notifications", icon: "o_notifications", to: { name: "uf_notifications" }, permissions: null },
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
