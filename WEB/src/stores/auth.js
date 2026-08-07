import { defineStore } from "pinia";
import { LocalStorage } from "quasar";
import { authApi } from "services/api";
import { useTenantStore } from "stores/tenant";
import { decodeJwtPermissions } from "services/jwt";

// Refresh-token validity window (backend default: 7 days). Used to drive the
// session-expiry warning when the JWT does not carry a usable expiry.
const REFRESH_WINDOW_MS = 7 * 24 * 60 * 60 * 1000;

export const useAuthStore = defineStore("auth", {
  state: () => ({
    token: LocalStorage.getItem("token"),
    refreshToken: LocalStorage.getItem("refreshToken"),
    user: LocalStorage.getItem("user"),
    mustChangePassword: false,
    // Effective permissions for the active tenant, decoded from the JWT. Re-derived whenever
    // the token changes (login / refresh / tenant switch) so the UI gates stay in sync.
    permissions: decodeJwtPermissions(LocalStorage.getItem("token")),
    sessionExpiresAt: LocalStorage.getItem("sessionExpiresAt")
      ? new Date(LocalStorage.getItem("sessionExpiresAt"))
      : null
  }),

  getters: {
    isAuthenticated: (state) => !!state.token,
    // WO-123: the RBAC role names the user holds in the active tenant (multi-role).
    roles: () => useTenantStore().activeTenant?.roleNames || [],
    // Permission predicates for the active tenant.
    hasPermission: (state) => (permission) => state.permissions.includes(permission),
    hasAnyPermission: (state) => (permissions) =>
      Array.isArray(permissions) && permissions.some((p) => state.permissions.includes(p))
  },

  actions: {
    // AC-UI-001.2 / AC-UI-001.4
    async login (credentials) {
      const email = credentials.email ?? credentials.username;
      const resp = await authApi.login({ email, password: credentials.password });
      const data = resp?.data;
      if (!data?.accessToken) {
        return resp;
      }
      this._setTokens(data.accessToken, data.refreshToken);
      this.mustChangePassword = !!data.mustChangePassword;
      await this.loadProfile();
      return resp;
    },

    // GET /api/auth/profile → populate current user + tenant assignments.
    async loadProfile () {
      const profile = await authApi.profile();
      this.user = {
        userId: profile.userId,
        email: profile.email,
        displayName: profile.displayName,
        tenants: profile.tenants || []
      };
      LocalStorage.set("user", this.user);
      useTenantStore().setAssignments(profile.tenants || []);
      return profile;
    },

    // AC-UI-004.1: silent token refresh.
    async refresh () {
      if (!this.refreshToken) {
        throw new Error("No refresh token available.");
      }
      const resp = await authApi.refresh(this.refreshToken);
      const data = resp?.data;
      if (!data?.accessToken) {
        throw new Error("Token refresh failed.");
      }
      this._setTokens(data.accessToken, data.refreshToken);
      return data.accessToken;
    },

    // AC-UI-002.1 / AC-UI-002.3: clear local session even if the API call fails.
    async logout () {
      const rt = this.refreshToken;
      try {
        await authApi.logout(rt);
      } catch {
        // best-effort; clear locally regardless
      } finally {
        this.clearSession();
      }
    },

    // AC-UI-002.2
    async logoutAll () {
      try {
        await authApi.logoutAll();
      } catch {
        // best-effort
      } finally {
        this.clearSession();
      }
    },

    // Called on app mount to restore the session.
    async initialize () {
      if (!this.token) {
        return;
      }
      try {
        await this.loadProfile();
      } catch {
        this.clearSession();
      }
    },

    setUserInfo (payload) {
      this.user = { ...this.user, ...payload };
      LocalStorage.set("user", this.user);
    },

    _setTokens (accessToken, refreshToken) {
      this.token = accessToken;
      this.permissions = decodeJwtPermissions(accessToken);
      LocalStorage.set("token", accessToken);
      if (refreshToken) {
        this.refreshToken = refreshToken;
        LocalStorage.set("refreshToken", refreshToken);
      }
      this.sessionExpiresAt = new Date(Date.now() + REFRESH_WINDOW_MS);
      LocalStorage.set("sessionExpiresAt", this.sessionExpiresAt.toISOString());
    },

    // Clear local session without calling the API (safe to use inside interceptors).
    clearSession () {
      this.token = null;
      this.refreshToken = null;
      this.user = null;
      this.permissions = [];
      this.mustChangePassword = false;
      this.sessionExpiresAt = null;
      LocalStorage.clear();
      useTenantStore().clear();
    }
  }
});
