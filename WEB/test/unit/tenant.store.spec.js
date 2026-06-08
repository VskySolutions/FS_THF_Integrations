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
  authApi: { switchTenant: vi.fn(), login: vi.fn(), profile: vi.fn(), refresh: vi.fn(), logout: vi.fn(), logoutAll: vi.fn() }
}));

import { authApi } from "services/api";
import { useTenantStore } from "stores/tenant";
import { LocalStorage } from "quasar";

beforeEach(() => {
  setActivePinia(createPinia());
  LocalStorage.clear();
  vi.clearAllMocks();
});

const twoTenants = [
  { tenantId: "t1", name: "Acme", role: "Operator" },
  { tenantId: "t2", name: "Globex", role: "TenantAdmin" }
];

describe("TenantStore", () => {
  it("setAssignments selects the first tenant as active", () => {
    const t = useTenantStore();
    t.setAssignments(twoTenants);
    expect(t.activeTenantId).toBe("t1");
    expect(t.hasMultipleTenants).toBe(true);
    expect(t.activeRole).toBe("Operator");
    expect(t.activeTenant.name).toBe("Acme");
  });

  it("switchTenant success updates active tenant and swaps the token", async () => {
    authApi.switchTenant.mockResolvedValue({ data: { accessToken: "AT-scoped" } });
    const t = useTenantStore();
    t.setAssignments(twoTenants);

    const ok = await t.switchTenant("t2");

    expect(ok).toBe(true);
    expect(t.activeTenantId).toBe("t2");
    expect(authApi.switchTenant).toHaveBeenCalledWith("t2");
  });

  it("switchTenant aborts and retains the tenant when the unsaved-form guard declines", async () => {
    const t = useTenantStore();
    t.setAssignments(twoTenants);
    t.setUnsavedForm(true);

    const ok = await t.switchTenant("t2", { confirm: async () => false });

    expect(ok).toBe(false);
    expect(t.activeTenantId).toBe("t1");
    expect(authApi.switchTenant).not.toHaveBeenCalled();
  });

  it("tracks the hasUnsavedForm flag", () => {
    const t = useTenantStore();
    expect(t.hasUnsavedForm).toBe(false);
    t.setUnsavedForm(true);
    expect(t.hasUnsavedForm).toBe(true);
    t.setUnsavedForm(false);
    expect(t.hasUnsavedForm).toBe(false);
  });
});
