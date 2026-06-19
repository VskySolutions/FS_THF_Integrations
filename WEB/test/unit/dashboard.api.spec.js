import { beforeEach, describe, it, expect, vi } from "vitest";

// Stub the two axios instances so we can assert exactly which endpoint / verb / headers each
// dashboardApi method calls (mirrors customer.api.spec.js).
vi.mock("boot/axios", () => {
  const make = () => ({
    get: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    post: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    put: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    delete: vi.fn(() => Promise.resolve({ data: { data: {} } }))
  });
  return { http: make(), http2: make() };
});

import { dashboardApi } from "services/api";
import { http } from "boot/axios";

beforeEach(() => { vi.clearAllMocks(); });

describe("dashboardApi", () => {
  it("jobs / customers / users pass params and unwrap", async () => {
    await dashboardApi.jobs({ dateRange: "7d", tenantId: "t1" });
    await dashboardApi.customers({ dateRange: "30d" });
    await dashboardApi.users({ dateRange: "today" });
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/jobs", { params: { dateRange: "7d", tenantId: "t1" } });
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/customers", { params: { dateRange: "30d" } });
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/users", { params: { dateRange: "today" } });
  });

  it("health hits the health endpoint", async () => {
    await dashboardApi.health();
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/health");
  });

  it("platform sets the force-refresh header only when requested", async () => {
    await dashboardApi.platform({ dateRange: "7d" });
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/platform", { params: { dateRange: "7d" }, headers: undefined });
    await dashboardApi.platform({ dateRange: "7d" }, true);
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/platform", { params: { dateRange: "7d" }, headers: { "X-Dashboard-Force-Refresh": "1" } });
  });

  it("getLayout / saveLayout use the layout endpoint", async () => {
    await dashboardApi.getLayout();
    await dashboardApi.saveLayout({ widgetOrder: ["a"], hiddenWidgets: [], collapsedWidgets: ["b"] });
    expect(http.get).toHaveBeenCalledWith("/api/dashboard/layout");
    expect(http.put).toHaveBeenCalledWith("/api/dashboard/layout", { widgetOrder: ["a"], hiddenWidgets: [], collapsedWidgets: ["b"] });
  });
});
