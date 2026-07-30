import { useAuthStore } from "stores/auth";

// Permission catalogue keys (mirror EmsPortal.Shared.Security.Permissions). Centralised so
// components reference a constant rather than scattering magic strings.
export const Permissions = Object.freeze({
  TenantsRead: "tenants.read",
  TenantsWrite: "tenants.write",
  TenantsArchive: "tenants.archive",
  PersonsRead: "persons.read",
  PersonsWrite: "persons.write",
  PersonsDelete: "persons.delete",
  UsersRead: "users.read",
  UsersWrite: "users.write",
  UsersResetPassword: "users.reset_password",
  UsersGroupManagement: "users.groupManagement",
  RolesRead: "roles.read",
  RolesWrite: "roles.write",
  RolesAssign: "roles.assign",
  GroupsManage: "groups.manage",
  EmailManage: "email.manage",
  // Universal Features (Phase 14/15).
  SettingsManage: "settings.manage",
  RecordsAdminDelete: "records.adminDelete",
  // Option Sets (tenant-configurable input value lists).
  OptionSetsRead: "optionSets.read",
  OptionSetsManage: "optionSets.manage",
  // REMS (Phase 15) — role-aware navigation is gated per permission, not per role name.
  RemsRequestsRead: "rems.requests.read",
  RemsRequestsCreate: "rems.requests.create",
  RemsRequestsUpdate: "rems.requests.update",
  RemsRequestsDelete: "rems.requests.delete",
  RemsRequestsAssign: "rems.requests.assign",
  RemsPoolRead: "rems.pool.read",
  RemsFormsManage: "rems.forms.manage",
  RemsEmailLogRead: "rems.emailLog.read",
  RemsEngagementsManage: "rems.engagements.manage",
  RemsApprovalsSend: "rems.approvals.send",
  RemsApprovalsAct: "rems.approvals.act"
});

// Reactive permission checks for the active tenant. `has`/`hasAny` read the auth store's
// permissions (decoded from the JWT), so they stay reactive across tenant switches.
export function usePermissions () {
  const auth = useAuthStore();
  const has = (permission) => auth.hasPermission(permission);
  const hasAny = (permissions) => auth.hasAnyPermission(permissions);
  return { has, hasAny };
}
