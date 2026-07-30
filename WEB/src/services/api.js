import { http, http2 } from "boot/axios";

// Shared API access points.
// `api`     → authenticated instance (Bearer token + tenant + correlation-id headers).
// `anonApi` → anonymous instance (login, refresh).
export const api = http;
export const anonApi = http2;

// Platform-wide stable error codes — mirrors EmsPortal.Shared.Contracts.ApiErrorCodes.
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
// Resource groups (mapped to the EMS Portal API controllers)
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
  archive: (id) => api.put(`/api/admin/tenants/${id}/archive`).then(unwrap)
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

// Per-tenant SMTP email accounts (WO-80/81). Reads require users.read; writes require email.manage.
// Super Admins target a tenant via the `tenantId` query (list/get/write) or create body; everyone
// else is auto-scoped to their active tenant. Passwords are write-only and never returned.
export const smtpAccountApi = {
  // params: { tenantId?, status? } — status is "active" | "inactive".
  list: (params) => api.get("/api/admin/smtp-accounts", { params }).then(envelope),
  get: (id, tenantId) => api.get(`/api/admin/smtp-accounts/${id}`, { params: { tenantId } }).then(unwrap),
  // payload: { tenantId?, accountName, host, port, encryptionType, authType, username?, password?, fromName, fromEmail }
  create: (payload) => api.post("/api/admin/smtp-accounts", payload).then(unwrap),
  // payload: same as create minus tenantId; omit password to preserve the existing one.
  update: (id, payload, tenantId) =>
    api.put(`/api/admin/smtp-accounts/${id}`, payload, { params: { tenantId } }).then(unwrap),
  remove: (id, tenantId) => api.delete(`/api/admin/smtp-accounts/${id}`, { params: { tenantId } }).then(envelope),
  activate: (id, tenantId) => api.put(`/api/admin/smtp-accounts/${id}/activate`, null, { params: { tenantId } }).then(unwrap),
  // body: { recipientEmail } → { success, sentAtUtc?, serverResponse?, errorCategory?, errorDetail? }
  test: (id, recipientEmail, tenantId) =>
    api.post(`/api/admin/smtp-accounts/${id}/test`, { recipientEmail }, { params: { tenantId } }).then(unwrap)
};

// Transactional email templates (WO email templates). Reads require users.read; writes require
// email.manage. Tenant Admins manage their tenant overrides; Super Admins manage the platform
// defaults (`global: true`) or any tenant (`tenantId`). `params` carries `{ tenantId?, global? }`.
export const emailTemplateApi = {
  list: (params) => api.get("/api/admin/email-templates", { params }).then(envelope),
  get: (key, params) => api.get(`/api/admin/email-templates/${key}`, { params }).then(unwrap),
  // payload: { subject, body }
  save: (key, payload, params) => api.put(`/api/admin/email-templates/${key}`, payload, { params }).then(unwrap),
  reset: (key, params) => api.delete(`/api/admin/email-templates/${key}`, { params }).then(unwrap),
  // payload: { subject?, body? } — renders the draft (or the effective template) with sample data.
  preview: (key, payload, params) => api.post(`/api/admin/email-templates/${key}/preview`, payload, { params }).then(unwrap)
};

// How an option list orders its items. Mirrors EmsPortal.Domain.Enums.OptionItemSortMode.
export const OptionItemSortMode = Object.freeze({
  AlphabeticalAsc: "AlphabeticalAsc",
  AlphabeticalDesc: "AlphabeticalDesc",
  Custom: "Custom"
});

// Tenant-configurable input value lists (e.g. Payment Terms). Reads require optionSets.read; writes
// require optionSets.manage. Standard (seeded) lists are returned read-only; only a tenant's own
// lists can be modified. Scoped to the caller's active tenant.
export const optionSetApi = {
  // params: { entityType? } — EntityType enum value.
  list: (params) => api.get("/api/option-sets", { params }).then(unwrap),
  get: (id) => api.get(`/api/option-sets/${id}`).then(unwrap),
  // Effective active values for a key: { entityType, key, parentItemId? }.
  resolve: (params) => api.get("/api/option-sets/resolve", { params }).then(unwrap),
  // payload: { entityType, key, name, parentSetId?, itemSortMode }
  create: (payload) => api.post("/api/option-sets", payload).then(unwrap),
  // payload: { name, itemSortMode, isActive }
  update: (id, payload) => api.put(`/api/option-sets/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/option-sets/${id}`).then(unwrap),
  // payload: { value, label, parentItemId?, isDefault, backgroundColor?, textColor?, metadataJson? }
  createItem: (setId, payload) => api.post(`/api/option-sets/${setId}/items`, payload).then(unwrap),
  // payload: { value, label, parentItemId?, isDefault, isActive, backgroundColor?, textColor?, metadataJson? }
  updateItem: (setId, itemId, payload) => api.put(`/api/option-sets/${setId}/items/${itemId}`, payload).then(unwrap),
  removeItem: (setId, itemId) => api.delete(`/api/option-sets/${setId}/items/${itemId}`).then(unwrap),
  // payload: itemIds in the desired order.
  reorderItems: (setId, itemIds) => api.put(`/api/option-sets/${setId}/items/reorder`, { itemIds }).then(unwrap)
};

// ---------------------------------------------------------------------------
// Universal Features (Phase 14/15). Attach to any entity via (entityType, entityId).
// EntityType enum: Tenant=3, User=4, UserGroup=5.
// ---------------------------------------------------------------------------

export const EntityType = Object.freeze({
  Tenant: 3,
  User: 4,
  UserGroup: 5
});

// Notes (@mention-aware annotations).
export const ufNotesApi = {
  list: (params) => api.get("/api/uf/notes", { params }).then(envelope),
  create: (payload) => api.post("/api/uf/notes", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/uf/notes/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/uf/notes/${id}`).then(envelope),
  // Tenant users for the @mention autocomplete.
  mentionCandidates: (search) => api.get("/api/uf/mention-candidates", { params: { search } }).then(unwrap)
};

// Tags (admin CRUD + entity application).
export const ufTagsApi = {
  list: (search) => api.get("/api/admin/tags", { params: { search } }).then(unwrap),
  // Read-only picker list available to any tenant user (for applying tags).
  picker: (search) => api.get("/api/uf/tags", { params: { search } }).then(unwrap),
  create: (payload) => api.post("/api/admin/tags", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/admin/tags/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/admin/tags/${id}`).then(envelope),
  entityTags: (entityType, entityId) => api.get("/api/uf/entity-tags", { params: { entityType, entityId } }).then(unwrap),
  apply: (payload) => api.post("/api/uf/entity-tags", payload).then(unwrap),
  removeApplication: (id) => api.delete(`/api/uf/entity-tags/${id}`).then(envelope)
};

// Attachments.
export const ufAttachmentsApi = {
  list: (entityType, entityId) => api.get("/api/uf/attachments", { params: { entityType, entityId } }).then(unwrap),
  upload: (entityType, entityId, file) => {
    const form = new FormData();
    form.append("file", file);
    form.append("entityType", entityType);
    form.append("entityId", entityId);
    return api.post("/api/uf/attachments", form, { headers: { "Content-Type": "multipart/form-data" } }).then(unwrap);
  },
  download: (id) => api.get(`/api/uf/attachments/${id}/download`, { responseType: "blob" }).then((r) => r?.data),
  remove: (id) => api.delete(`/api/uf/attachments/${id}`).then(envelope)
};

// Activity timeline (read-only).
export const ufActivityApi = {
  list: (params) => api.get("/api/uf/activity", { params }).then(envelope)
};

// Reminders (personal).
export const ufReminderApi = {
  list: (params) => api.get("/api/uf/reminders", { params }).then(envelope),
  create: (payload) => api.post("/api/uf/reminders", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/uf/reminders/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/uf/reminders/${id}`).then(envelope)
};

// Notification centre + preferences.
export const ufNotificationApi = {
  list: (params) => api.get("/api/notifications", { params }).then(envelope),
  unreadCount: () => api.get("/api/notifications/unread-count").then(unwrap),
  markRead: (id) => api.put(`/api/notifications/${id}/read`).then(envelope),
  markAllRead: () => api.put("/api/notifications/read-all").then(envelope),
  getPreferences: () => api.get("/api/notifications/preferences").then(unwrap),
  updatePreferences: (preferences) => api.put("/api/notifications/preferences", { preferences }).then(envelope),
  // Mention inbox.
  mentions: (params) => api.get("/api/uf/mentions", { params }).then(envelope),
  markMentionRead: (id) => api.put(`/api/uf/mentions/${id}/read`).then(envelope)
};

// Pins (bookmarks).
export const ufPinApi = {
  list: (params) => api.get("/api/uf/pins", { params }).then(envelope),
  create: (payload) => api.post("/api/uf/pins", payload).then(unwrap),
  remove: (id) => api.delete(`/api/uf/pins/${id}`).then(envelope)
};

// Colour codes (row tinting).
export const ufColourApi = {
  batch: (entityType, entityIds) => api.get("/api/uf/colour-codes", { params: { entityType, entityIds }, paramsSerializer: { indexes: null } }).then(unwrap),
  upsert: (payload) => api.put("/api/uf/colour-codes", payload).then(unwrap)
};

// PDF export (binary stream).
export const ufPdfApi = {
  export: (payload) => api.post("/api/uf/pdf-export", payload, { responseType: "blob" }).then((r) => r?.data)
};

// Saved views.
export const ufSavedViewApi = {
  list: (listPage) => api.get("/api/uf/saved-views", { params: { listPage } }).then(unwrap),
  shared: () => api.get("/api/uf/saved-views/shared").then(unwrap),
  create: (payload) => api.post("/api/uf/saved-views", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/uf/saved-views/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/uf/saved-views/${id}`).then(envelope)
};

// Checklists.
export const ufChecklistApi = {
  list: (entityType, entityId) => api.get("/api/uf/checklists", { params: { entityType, entityId } }).then(unwrap),
  create: (payload) => api.post("/api/uf/checklists", payload).then(unwrap),
  addItem: (id, text) => api.post(`/api/uf/checklists/${id}/items`, { text }).then(unwrap),
  toggleItem: (id, itemId, isCompleted) => api.patch(`/api/uf/checklists/${id}/items/${itemId}`, { isCompleted }).then(unwrap),
  editItem: (id, itemId, text) => api.put(`/api/uf/checklists/${id}/items/${itemId}`, { text }).then(unwrap),
  reorder: (id, itemIds) => api.put(`/api/uf/checklists/${id}/reorder`, { itemIds }).then(unwrap),
  removeItem: (id, itemId) => api.delete(`/api/uf/checklists/${id}/items/${itemId}`).then(envelope),
  remove: (id) => api.delete(`/api/uf/checklists/${id}`).then(envelope)
};

// Sticky notes (personal + tenant broadcast) and per-user layout state.
export const ufStickyNoteApi = {
  list: (scope) => api.get("/api/uf/sticky-notes", { params: { scope } }).then(unwrap),
  create: (payload) => api.post("/api/uf/sticky-notes", payload).then(unwrap),
  update: (id, payload) => api.put(`/api/uf/sticky-notes/${id}`, payload).then(unwrap),
  remove: (id) => api.delete(`/api/uf/sticky-notes/${id}`).then(envelope),
  dismiss: (id) => api.post(`/api/uf/sticky-notes/${id}/dismiss`).then(envelope),
  saveState: (noteId, payload) => api.put(`/api/uf/sticky-note-states/${noteId}`, payload).then(envelope),
  adminList: () => api.get("/api/admin/sticky-notes").then(unwrap)
};

// Deleted records management.
export const ufDeletedApi = {
  list: (params) => api.get("/api/uf/deleted", { params }).then(envelope),
  restore: (payload) => api.post("/api/uf/restore", payload).then(envelope),
  restoreBulk: (payload) => api.post("/api/uf/restore/bulk", payload).then(unwrap),
  hardDelete: (payload) => api.delete("/api/uf/hard-delete", { data: payload }).then(envelope),
  hardDeleteBulk: (payload) => api.delete("/api/uf/hard-delete/bulk", { data: payload }).then(envelope),
  getRetention: (tenantId) => api.get("/api/admin/retention-config", { params: { tenantId } }).then(unwrap),
  updateRetention: (retentionDays, tenantId) => api.put("/api/admin/retention-config", { retentionDays }, { params: { tenantId } }).then(unwrap),
  overdue: (tenantId) => api.get("/api/admin/retention-overdue", { params: { tenantId } }).then(unwrap)
};

// Modified log (field change history).
export const ufModifiedLogApi = {
  history: (params) => api.get("/api/uf/modified-log", { params }).then(envelope),
  iconCounts: (entityType, entityId) => api.get("/api/uf/modified-log/icon-counts", { params: { entityType, entityId } }).then(unwrap),
  config: (entityType) => api.get("/api/admin/modified-log-config", { params: { entityType } }).then(unwrap),
  toggleConfig: (fieldKey, isEnabled) => api.patch(`/api/admin/modified-log-config/${fieldKey}`, { isEnabled }).then(unwrap)
};

// Dashboard (WO-73). Role-aware analytics endpoints + per-user layout persistence. Tenant-scoped
// endpoints auto-scope to the active tenant; Super Admins may target a tenant via `tenantId`.
// `params` carries `{ dateRange, tenantId? }`. All responses use the standard ApiResponse envelope.
export const dashboardApi = {
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
