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

export const userApi = {
  list: (params) => api.get("/api/admin/users", { params }).then(envelope),
  get: (id) => api.get(`/api/admin/users/${id}`).then(unwrap),
  create: (payload) => api.post("/api/admin/users", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/admin/users/${id}`, payload).then(unwrap),
  setStatus: (id, isActive) => api.put(`/api/admin/users/${id}/status`, { isActive }).then(unwrap),
  // Admin password reset (REQ-ADM-013) — returns a new temporary password.
  resetPassword: (id) => api.post(`/api/admin/users/${id}/reset-password`).then(unwrap),
  // payload: { tenantId, role?, roleId? } — roleId (RBAC) takes precedence over the legacy role enum.
  assignTenantRole: (id, payload) =>
    api.post(`/api/admin/users/${id}/tenant-assignments`, payload).then(unwrap),
  removeTenantRole: (id, tenantId) =>
    api.delete(`/api/admin/users/${id}/tenant-assignments/${tenantId}`).then(envelope)
};

export const roleApi = {
  list: () => api.get("/api/admin/roles").then(unwrap),
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
    api.delete(`/api/admin/tenants/${tenantId}/roles/${roleId}`).then(envelope)
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

export const mappingApi = {
  list: (tenantId, params) => api.get(`/api/admin/tenants/${tenantId}/mappings`, { params }).then(envelope),
  create: (tenantId, payload) => api.post(`/api/admin/tenants/${tenantId}/mappings`, payload).then(unwrap),
  update: (tenantId, mappingId, payload) =>
    api.put(`/api/admin/tenants/${tenantId}/mappings/${mappingId}`, payload).then(unwrap),
  remove: (tenantId, mappingId) =>
    api.delete(`/api/admin/tenants/${tenantId}/mappings/${mappingId}`).then(envelope)
};

export const jobApi = {
  importExpenses: () => api.post("/api/concur/expenses/import").then(envelope),
  importInvoices: () => api.post("/api/concur/invoices/import").then(envelope),
  importPayments: () => api.post("/api/concur/payments/import").then(envelope),
  list: (params) => api.get("/api/admin/jobs", { params }).then(envelope),
  retry: (jobId) => api.post(`/api/admin/retry/${jobId}`).then(unwrap),
  retries: (params) => api.get("/api/admin/retries", { params }).then(envelope)
};

export const logApi = {
  list: (params) => api.get("/api/admin/logs", { params }).then(envelope)
};

export const adminApi = {
  health: () => api.get("/api/admin/health").then(unwrap)
};
