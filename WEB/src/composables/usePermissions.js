import { useAuthStore } from "stores/auth";

// Permission catalogue keys (mirror IntegrationHub.Shared.Security.Permissions). Centralised so
// components reference a constant rather than scattering magic strings.
export const Permissions = Object.freeze({
  TenantsRead: "tenants.read",
  TenantsWrite: "tenants.write",
  TenantsArchive: "tenants.archive",
  TenantsCredentials: "tenants.credentials",
  UsersRead: "users.read",
  UsersWrite: "users.write",
  UsersResetPassword: "users.reset_password",
  RolesRead: "roles.read",
  RolesWrite: "roles.write",
  RolesAssign: "roles.assign",
  MappingsRead: "mappings.read",
  MappingsWrite: "mappings.write",
  JobsRead: "jobs.read",
  JobsTrigger: "jobs.trigger",
  JobsRetry: "jobs.retry",
  LogsRead: "logs.read",
  HealthRead: "health.read"
});

// Reactive permission checks for the active tenant. `has`/`hasAny` read the auth store's
// permissions (decoded from the JWT), so they stay reactive across tenant switches.
export function usePermissions () {
  const auth = useAuthStore();
  const has = (permission) => auth.hasPermission(permission);
  const hasAny = (permissions) => auth.hasAnyPermission(permissions);
  return { has, hasAny };
}
