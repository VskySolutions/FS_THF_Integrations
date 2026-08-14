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
  // rems.pool.read is gone with the Admin Pool — nothing waits in a shared pool to be picked up now that
  // the initiator fills the whole request and sends the intake link themselves.
  RemsFormsManage: "rems.forms.manage",
  RemsFormsSend: "rems.forms.send",
  RemsEmailLogRead: "rems.emailLog.read",
  RemsEngagementsManage: "rems.engagements.manage",
  RemsApprovalsSend: "rems.approvals.send"
  // No "approvals.act" key: deciding an approval task is authorised by owning the task, not by a
  // permission, so the Approvals inbox and task detail are open to every authenticated user and the
  // server returns only the caller's own tasks. Mirrors EmsPortal.Shared.Security.Permissions.
});

// Reactive permission checks for the active tenant. `has`/`hasAny` read the auth store's
// permissions (decoded from the JWT), so they stay reactive across tenant switches.
export function usePermissions () {
  const auth = useAuthStore();
  const has = (permission) => auth.hasPermission(permission);
  const hasAny = (permissions) => auth.hasAnyPermission(permissions);
  return { has, hasAny };
}
