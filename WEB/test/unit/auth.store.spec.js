import { setActivePinia, createPinia } from "pinia";
import { beforeEach, describe, it, expect, vi } from "vitest";

vi.mock("quasar", () => {
  const store = new Map();
  return {
    LocalStorage: {
      getItem: (k) => (store.has(k) ? store.get(k) : null),
      set: (k, v) => store.set(k, v),
      remove: (k) => store.delete(k),
      clear: () => store.clear()
    }
  };
});

vi.mock("services/api", () => ({
  authApi: {
    login: vi.fn(),
    profile: vi.fn(),
    refresh: vi.fn(),
    logout: vi.fn(),
    logoutAll: vi.fn()
  }
}));

import { authApi } from "services/api";
import { useAuthStore } from "stores/auth";
import { useTenantStore } from "stores/tenant";
import { LocalStorage } from "quasar";

beforeEach(() => {
  setActivePinia(createPinia());
  LocalStorage.clear();
  vi.clearAllMocks();
});

describe("AuthStore", () => {
  it("logs in: stores tokens, mustChangePassword, profile and tenant assignments", async () => {
    authApi.login.mockResolvedValue({ data: { accessToken: "AT", refreshToken: "RT", mustChangePassword: true } });
    authApi.profile.mockResolvedValue({
      userId: "u1",
      email: "a@b.com",
      displayName: "Ann",
      tenants: [{ tenantId: "t1", identifier: "acme", name: "Acme", role: "SuperAdmin" }]
    });

    const auth = useAuthStore();
    await auth.login({ email: "a@b.com", password: "x" });

    expect(auth.token).toBe("AT");
    expect(auth.refreshToken).toBe("RT");
    expect(auth.mustChangePassword).toBe(true);
    expect(auth.isAuthenticated).toBe(true);
    expect(auth.user.displayName).toBe("Ann");
    expect(auth.sessionExpiresAt).toBeInstanceOf(Date);
    expect(useTenantStore().activeTenantId).toBe("t1");
  });

  it("refresh updates the access token; throws without a refresh token", async () => {
    const auth = useAuthStore();
    auth.refreshToken = "RT";
    authApi.refresh.mockResolvedValue({ data: { accessToken: "AT2", refreshToken: "RT2" } });

    await expect(auth.refresh()).resolves.toBe("AT2");
    expect(auth.token).toBe("AT2");

    auth.refreshToken = null;
    await expect(auth.refresh()).rejects.toThrow();
  });

  it("logout clears the session even when the API call fails", async () => {
    const auth = useAuthStore();
    auth.token = "AT";
    auth.refreshToken = "RT";
    authApi.logout.mockRejectedValue(new Error("network"));

    await auth.logout();

    expect(auth.token).toBeNull();
    expect(auth.isAuthenticated).toBe(false);
  });

  it("clearSession resets all state", () => {
    const auth = useAuthStore();
    auth.token = "AT";
    auth.user = { email: "x" };
    auth.clearSession();
    expect(auth.token).toBeNull();
    expect(auth.user).toBeNull();
    expect(auth.refreshToken).toBeNull();
  });
});
