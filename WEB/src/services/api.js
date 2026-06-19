import { http, http2 } from "boot/axios";

// Shared API access points.
// `api`     → authenticated instance (Bearer token + tenant + correlation-id headers).
// `anonApi` → anonymous instance (login, refresh).
export const api = http;
export const anonApi = http2;

// Platform-wide stable error codes — mirrors IntegrationHub.Shared.Contracts.ApiErrorCodes.
export const ApiErrorCodes = Object.freeze({
  ValidationFailed: "VALIDATION_FAILED",
  Unauthorized: "UNAUTHORIZED",
  Forbidden: "FORBIDDEN",
  NotFound: "NOT_FOUND",
  DuplicateIdentifier: "DUPLICATE_IDENTIFIER",
  DuplicateGroupName: "DUPLICATE_GROUP_NAME",
  PermissionCeilingExceeded: "PERMISSION_CEILING_EXCEEDED",
  TenantInactive: "TENANT_INACTIVE",
  TenantNotFound: "TENANT_NOT_FOUND",
  TenantArchived: "TENANT_ARCHIVED",
  ActiveJobsExist: "ACTIVE_JOBS_EXIST",
  JobNotFound: "JOB_NOT_FOUND",
  CredentialsNotConfigured: "CREDENTIALS_NOT_CONFIGURED",
  InternalError: "INTERNAL_ERROR"
});

/**
 * @typedef {Object} PaginatedMeta
 * @property {number} page
 * @property {number} limit
 * @property {number} totalRecords
 */
/**
 * @template T
 * @typedef {Object} ApiResponse
 * @property {boolean} success
 * @property {string} message
 * @property {T} [data]
 * @property {PaginatedMeta} [meta]
 */

/** Extract a caller-safe message from an Axios error. */
export function getApiErrorMessage (error, fallback = "Something went wrong. Please try again.") {
  return (
    error?.response?.data?.error?.details ||
    error?.response?.data?.message ||
    (typeof error?.response?.data === "string" ? error.response.data : null) ||
    error?.message ||
    fallback
  );
}

/** Extract the stable machine-readable error code, if present. */
export function getApiErrorCode (error) {
  return error?.response?.data?.error?.code || null;
}

// Unwrap the standard ApiResponse envelope to its `data` (and attach `meta` for lists).
const unwrap = (response) => response?.data?.data;
const envelope = (response) => response?.data;

// ---------------------------------------------------------------------------
// Resource groups (mapped to the IntegrationHub API controllers)
// ---------------------------------------------------------------------------

export const authApi = {
  login: (credentials) => anonApi.post("/api/auth/login", credentials).then(envelope),
  refresh: (refreshToken) => anonApi.post("/api/auth/refresh", { refreshToken }).then(envelope),
  logout: (refreshToken) => api.post("/api/auth/logout", { refreshToken }).then(envelope),
  logoutAll: () => api.post("/api/auth/logout-all").then(envelope),
  switchTenant: (tenantId) => api.post("/api/auth/switch-tenant", { tenantId }).then(envelope),
  profile: () => api.get("/api/auth/profile").then(unwrap),
  changePassword: (currentPassword, newPassword) =>
    api.put("/api/users/me/change-password", { currentPassword, newPassword }).then(envelope),
  updateMe: (displayName) => api.put("/api/users/me", { displayName }).then(unwrap)
};

export const tenantApi = {
  list: (params) => api.get("/api/admin/tenants", { params }).then(envelope),
  get: (id) => api.get(`/api/admin/tenants/${id}`).then(unwrap),
  create: (payload) => api.post("/api/admin/tenants", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/admin/tenants/${id}`, payload).then(unwrap),
  setStatus: (id, isActive) => api.put(`/api/admin/tenants/${id}/status`, { isActive }).then(unwrap),
  archive: (id) => api.put(`/api/admin/tenants/${id}/archive`).then(unwrap),
  setConcurConfig: (id, payload) => api.put(`/api/admin/tenants/${id}/concur-config`, payload).then(envelope),
  setMaconomyConfig: (id, payload) => api.put(`/api/admin/tenants/${id}/maconomy-config`, payload).then(envelope),
  clearConcurConfig: (id) => api.delete(`/api/admin/tenants/${id}/concur-config`).then(envelope),
  clearMaconomyConfig: (id) => api.delete(`/api/admin/tenants/${id}/maconomy-config`).then(envelope),
  testConcurConfig: (id) => api.post(`/api/admin/tenants/${id}/concur-config/test`).then(unwrap),
  testMaconomyConfig: (id) => api.post(`/api/admin/tenants/${id}/maconomy-config/test`).then(unwrap)
};

export const personApi = {
  list: (params) => api.get("/api/admin/persons", { params }).then(envelope),
  get: (id) => api.get(`/api/admin/persons/${id}`).then(unwrap),
  create: (payload) => api.post("/api/admin/persons", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/admin/persons/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/admin/persons/${id}`).then(envelope),
  // Lightweight options for the user-create Person dropdown (each carries isUser).
  selectable: () => api.get("/api/admin/persons/selectable").then(unwrap)
};

export const userApi = {
  list: (params) => api.get("/api/admin/users", { params }).then(envelope),
  get: (id) => api.get(`/api/admin/users/${id}`).then(unwrap),
  // payload: { personId, email?, phoneNumber?, countryCode?, tenantId, roleId } — promotes a Person to a login account.
  create: (payload) => api.post("/api/admin/users", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/admin/users/${id}`, payload).then(unwrap),
  setStatus: (id, isActive) => api.put(`/api/admin/users/${id}/status`, { isActive }).then(unwrap),
  // Admin password reset (REQ-ADM-013) — returns a new temporary password.
  resetPassword: (id) => api.post(`/api/admin/users/${id}/reset-password`).then(unwrap),
  // payload: { tenantId, role?, roleId? } — roleId (RBAC) takes precedence over the legacy role enum.
  assignTenantRole: (id, payload) =>
    api.post(`/api/admin/users/${id}/tenant-assignments`, payload).then(unwrap),
  removeTenantRole: (id, tenantId) =>
    api.delete(`/api/admin/users/${id}/tenant-assignments/${tenantId}`).then(envelope),
  // Replace the user's group memberships with the given set of group ids.
  setGroups: (id, groupIds) => api.put(`/api/admin/users/${id}/groups`, { groupIds }).then(unwrap)
};

// Tenant-scoped user groups (segmentation/tagging, independent of RBAC roles).
export const userGroupApi = {
  list: (search) => api.get("/api/admin/user-groups", { params: { search } }).then(unwrap),
  // payload: { name, description? } → created (or existing) group
  create: (payload) => api.post("/api/admin/user-groups", payload).then(unwrap),
  remove: (id) => api.delete(`/api/admin/user-groups/${id}`).then(envelope),
  // ---- Members ----
  members: (id) => api.get(`/api/admin/user-groups/${id}/members`).then(unwrap),
  addMembers: (id, userIds) => api.post(`/api/admin/user-groups/${id}/members`, { userIds }).then(unwrap),
  removeMember: (id, userId) => api.delete(`/api/admin/user-groups/${id}/members/${userId}`).then(envelope)
};

export const roleApi = {
  list: (params) => api.get("/api/admin/roles", { params }).then(unwrap),
  get: (id) => api.get(`/api/admin/roles/${id}`).then(unwrap),
  create: (payload) => api.post("/api/admin/roles", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/admin/roles/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/admin/roles/${id}`).then(envelope),
  // Full permission catalogue, for the role permission picker.
  permissions: () => api.get("/api/admin/permissions").then(unwrap),
  // Roles assignable within a tenant (system roles + the tenant's custom roles) — drives user role pickers.
  tenantRoles: (tenantId) => api.get(`/api/admin/tenants/${tenantId}/roles`).then(unwrap),
  // Tenant ids a role is currently available to (for the role↔tenant availability editor).
  roleTenants: (id) => api.get(`/api/admin/roles/${id}/tenants`).then(unwrap),
  assignToTenant: (tenantId, roleId) =>
    api.post(`/api/admin/tenants/${tenantId}/roles`, { roleId }).then(unwrap),
  unassignFromTenant: (tenantId, roleId) =>
    api.delete(`/api/admin/tenants/${tenantId}/roles/${roleId}`).then(envelope),
  // ---- Role ↔ Permission Group composition (WO-70) ----
  // The role's assigned groups + the role's effective permission set.
  getGroups: (roleId) => api.get(`/api/admin/roles/${roleId}/groups`).then(unwrap),
  assignGroups: (roleId, groupIds) => api.post(`/api/admin/roles/${roleId}/groups`, { groupIds }).then(unwrap),
  removeGroup: (roleId, groupId) => api.delete(`/api/admin/roles/${roleId}/groups/${groupId}`).then(unwrap),
  // Union of effective permissions for a role → { permissions, sources }.
  previewPermissions: (roleId) => api.get(`/api/admin/roles/${roleId}/permissions/preview`).then(unwrap)
};

// Permission Groups (WO-70): the RBAC composition layer (Permission Keys → Groups → Roles → Users).
// Tenant-scoped with a Super Admin `tenantId` override (query on list, body on create); everyone
// else is auto-scoped server-side. Mutations require `groups.manage`.
export const permissionGroupApi = {
  list: (params) => api.get("/api/admin/permission-groups", { params }).then(envelope),
  get: (id) => api.get(`/api/admin/permission-groups/${id}`).then(unwrap),
  // payload: { tenantId?, name, description?, permissionKeys[] }
  create: (payload) => api.post("/api/admin/permission-groups", payload).then(unwrap),
  // payload: { name, description?, permissionKeys[] }
  update: (id, payload) => api.put(`/api/admin/permission-groups/${id}`, payload).then(unwrap),
  setStatus: (id, isActive) => api.put(`/api/admin/permission-groups/${id}/status`, { isActive }).then(unwrap),
  remove: (id) => api.delete(`/api/admin/permission-groups/${id}`).then(envelope),
  // ---- Templates ----
  templates: () => api.get("/api/admin/permission-groups/templates").then(unwrap),
  createTemplate: (payload) => api.post("/api/admin/permission-groups/templates", payload).then(unwrap),
  // Full permission key catalogue (string[]) — drives the key picker.
  permissionCatalog: () => api.get("/api/admin/permissions").then(unwrap)
};

export const profileApi = {
  // Current user's person profile.
  getMine: () => api.get("/api/users/me/profile").then(unwrap),
  updateMine: (payload) => api.put("/api/users/me/profile", payload).then(unwrap),
  // Admin: any user's profile.
  getForUser: (userId) => api.get(`/api/admin/users/${userId}/profile`).then(unwrap),
  updateForUser: (userId, payload) => api.put(`/api/admin/users/${userId}/profile`, payload).then(unwrap)
};

export const mediaApi = {
  // Uploads a file (multipart) and returns the stored media (incl. publicUrl).
  upload: (file, mediaCategory = "Profile") => {
    const form = new FormData();
    form.append("file", file);
    form.append("mediaCategory", mediaCategory);
    return api.post("/api/media", form, { headers: { "Content-Type": "multipart/form-data" } }).then(unwrap);
  },
  // Absolute URL for a media public path (the API serves public media anonymously).
  absoluteUrl: (publicUrl) => (publicUrl ? `${process.env.API_BASE_URL || ""}${publicUrl}` : null)
};

// Field mappings scoped per tenant + flow (e.g. ExpenseImport). One field set per flow.
export const flowMappingApi = {
  list: (tenantId) => api.get(`/api/admin/tenants/${tenantId}/flow-mappings`).then(unwrap),
  get: (tenantId, interfaceName) => api.get(`/api/admin/tenants/${tenantId}/flow-mappings/${interfaceName}`).then(unwrap),
  save: (tenantId, interfaceName, fields) => api.put(`/api/admin/tenants/${tenantId}/flow-mappings/${interfaceName}`, { fields }).then(unwrap),
  clear: (tenantId, interfaceName) => api.delete(`/api/admin/tenants/${tenantId}/flow-mappings/${interfaceName}`).then(envelope)
};

export const jobApi = {
  // The flow's field mappings are resolved server-side from the tenant + flow rules.
  // tenantId is optional: Super Admins target a tenant; others run for their active tenant.
  importExpenses: (tenantId) => api.post("/api/concur/expenses/import", null, { params: tenantId ? { tenantId } : undefined }).then(envelope),
  importInvoices: (tenantId) => api.post("/api/concur/invoices/import", null, { params: tenantId ? { tenantId } : undefined }).then(envelope),
  importPayments: (tenantId) => api.post("/api/concur/payments/import", null, { params: tenantId ? { tenantId } : undefined }).then(envelope),
  list: (params) => api.get("/api/admin/jobs", { params }).then(envelope),
  retry: (jobId) => api.post(`/api/admin/retry/${jobId}`).then(unwrap),
  retries: (params) => api.get("/api/admin/retries", { params }).then(envelope),
  remove: (jobId) => api.delete(`/api/admin/jobs/${jobId}`).then(envelope)
};

export const logApi = {
  list: (params) => api.get("/api/admin/logs", { params }).then(envelope)
};

export const scheduleApi = {
  // Per-tenant import schedules. tenantId is optional (Super Admin targets a tenant; others are
  // scoped to their active tenant). update payload: { cronExpression, isActive }.
  list: (tenantId) => api.get("/api/admin/job-schedules", { params: { tenantId } }).then(unwrap),
  update: (jobName, payload, tenantId) =>
    api.put(`/api/admin/job-schedules/${jobName}`, payload, { params: { tenantId } }).then(unwrap)
};

// Customer onboarding & approval workflow (WO-65). Super Admins target a tenant via the `tenantId`
// query param on list and `tenantId` in the create body; everyone else is auto-scoped server-side.
export const customerApi = {
  list: (params) => api.get("/api/customers", { params }).then(envelope),
  get: (id) => api.get(`/api/customers/${id}`).then(unwrap),
  // payload: step1 fields { tenantId?, legalName, companyName, contactPerson?, emailAddress, ... }
  create: (payload) => api.post("/api/customers", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/customers/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/customers/${id}`).then(unwrap),
  // body: { duplicateAcknowledged } → { submitted, customerRequestNumber, status, duplicates[] }
  submit: (id, duplicateAcknowledged = false) =>
    api.post(`/api/customers/${id}/submit`, { duplicateAcknowledged }).then(unwrap),
  // body: enrichment fields → { customerId, status }
  enrich: (id, payload) => api.post(`/api/customers/${id}/enrich`, payload).then(unwrap),
  sendForApproval: (id) => api.post(`/api/customers/${id}/send-for-approval`).then(unwrap),
  // body: step2 fields → { customerId }
  saveStep2: (id, payload) => api.post(`/api/customers/${id}/step2`, payload).then(unwrap),
  // body: { step2: {...}, duplicateAcknowledged } → { approved, status, duplicates[] }
  approve: (id, step2, duplicateAcknowledged = false) =>
    api.post(`/api/customers/${id}/approve`, { step2, duplicateAcknowledged }).then(unwrap),
  // Approver sends an awaiting-approval request back to the reviewer. body: { notes? }
  revertToReviewer: (id, notes = null) => api.post(`/api/customers/${id}/revert-to-reviewer`, { notes }).then(unwrap),
  // Reviewer returns a request under review to data entry. body: { notes, fields[] } → { customerId, status }
  returnForCorrections: (id, notes, fields = []) =>
    api.post(`/api/customers/${id}/return`, { notes, fields }).then(unwrap),
  retrySync: (id) => api.post(`/api/customers/${id}/retry-sync`).then(unwrap),
  reopen: (id) => api.post(`/api/customers/${id}/reopen`).then(unwrap),
  // ---- Documents ----
  listDocuments: (id) => api.get(`/api/customers/${id}/documents`).then(unwrap),
  uploadDocument: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return api.post(`/api/customers/${id}/documents`, form, {
      headers: { "Content-Type": "multipart/form-data" }
    }).then(unwrap);
  },
  downloadDocument: (id, documentId) =>
    api.get(`/api/customers/${id}/documents/${documentId}/download`, { responseType: "blob" }).then((r) => r?.data),
  removeDocument: (id, documentId) =>
    api.delete(`/api/customers/${id}/documents/${documentId}`).then(unwrap)
};

export const adminApi = {
  health: () => api.get("/api/admin/health").then(unwrap)
};

// Dashboard (WO-73). Role-aware analytics endpoints + per-user layout persistence. Tenant-scoped
// endpoints auto-scope to the active tenant; Super Admins may target a tenant via `tenantId`.
// `params` carries `{ dateRange, tenantId? }`. All responses use the standard ApiResponse envelope.
export const dashboardApi = {
  jobs: (params) => api.get("/api/dashboard/jobs", { params }).then(unwrap),
  health: () => api.get("/api/dashboard/health").then(unwrap),
  customers: (params) => api.get("/api/dashboard/customers", { params }).then(unwrap),
  users: (params) => api.get("/api/dashboard/users", { params }).then(unwrap),
  // Super Admin platform overview. `forceRefresh` bypasses the server cache via a request header.
  platform: (params, forceRefresh = false) =>
    api.get("/api/dashboard/platform", {
      params,
      headers: forceRefresh ? { "X-Dashboard-Force-Refresh": "1" } : undefined
    }).then(unwrap),
  // Per-user widget layout (order / hidden / collapsed).
  getLayout: () => api.get("/api/dashboard/layout").then(unwrap),
  // payload: { widgetOrder, hiddenWidgets, collapsedWidgets }
  saveLayout: (payload) => api.put("/api/dashboard/layout", payload).then(unwrap)
};
