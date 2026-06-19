import { beforeEach, describe, it, expect, vi } from "vitest";
import { ref, defineComponent, h } from "vue";
import { mount, flushPromises } from "@vue/test-utils";

const { dashboardApi } = vi.hoisted(() => ({
  dashboardApi: { jobs: vi.fn(), health: vi.fn() }
}));
vi.mock("services/api", () => ({
  dashboardApi,
  getApiErrorMessage: (e) => (e?.message || String(e))
}));

import { useJobsDashboard, useHealthDashboard } from "composables/useDashboardData";

// Mount a composable inside a throwaway component so onMounted / watch fire as they do in the app.
// Returns the composable's reactive surface for assertions.
const withComposable = (factory) => {
  let exposed;
  const Host = defineComponent({
    setup () {
      exposed = factory();
      return () => h("div");
    }
  });
  const wrapper = mount(Host);
  return { wrapper, get: () => exposed };
};

beforeEach(() => {
  vi.clearAllMocks();
  dashboardApi.jobs.mockResolvedValue({
    kpis: { total: 10, completed: 8, failed: 1, pending: 1 },
    successRate: 80,
    volumeChart: [{ date: "2026-01-01", completed: 5 }],
    flowBreakdown: [{ flowName: "A", total: 5 }],
    failedJobs: [{ id: "j1" }],
    retryQueueCount: 2,
    retryQueueNextRunUtc: "2026-01-02T00:00:00Z"
  });
  dashboardApi.health.mockResolvedValue({ status: "Healthy", components: [], allOperational: true });
});

describe("useJobsDashboard", () => {
  it("calls dashboardApi.jobs on mount and exposes kpis / volumeChart etc.", async () => {
    const dateRange = ref("7d");
    const { get } = withComposable(() => useJobsDashboard(dateRange));
    await flushPromises();

    expect(dashboardApi.jobs).toHaveBeenCalledTimes(1);
    expect(dashboardApi.jobs).toHaveBeenCalledWith({ dateRange: "7d" });

    const c = get();
    expect(c.kpis.value).toEqual({ total: 10, completed: 8, failed: 1, pending: 1 });
    expect(c.successRate.value).toBe(80);
    expect(c.volumeChart.value).toHaveLength(1);
    expect(c.flowBreakdown.value).toHaveLength(1);
    expect(c.failedJobs.value).toHaveLength(1);
    expect(c.retryQueueCount.value).toBe(2);
    expect(c.retryQueueNextRunUtc.value).toBe("2026-01-02T00:00:00Z");
  });

  it("passes tenantId only when supplied", async () => {
    const dateRange = ref("30d");
    const tenantId = ref("t1");
    withComposable(() => useJobsDashboard(dateRange, tenantId));
    await flushPromises();
    expect(dashboardApi.jobs).toHaveBeenCalledWith({ dateRange: "30d", tenantId: "t1" });
  });

  it("re-fetches when the dateRange ref changes", async () => {
    const dateRange = ref("7d");
    withComposable(() => useJobsDashboard(dateRange));
    await flushPromises();
    expect(dashboardApi.jobs).toHaveBeenCalledTimes(1);

    dateRange.value = "30d";
    await flushPromises();

    expect(dashboardApi.jobs).toHaveBeenCalledTimes(2);
    expect(dashboardApi.jobs).toHaveBeenLastCalledWith({ dateRange: "30d" });
  });

  it("sets error (and does NOT throw) on API failure, and clears loading", async () => {
    dashboardApi.jobs.mockRejectedValue(new Error("jobs down"));
    const dateRange = ref("7d");
    const { get } = withComposable(() => useJobsDashboard(dateRange));
    await flushPromises();

    const c = get();
    expect(c.error.value).toBe("jobs down");
    expect(c.loading.value).toBe(false);
    expect(c.kpis.value).toBeNull();
  });

  it("clears loading after a successful response", async () => {
    const dateRange = ref("7d");
    const { get } = withComposable(() => useJobsDashboard(dateRange));
    await flushPromises();
    expect(get().loading.value).toBe(false);
  });

  it("a jobs failure does not affect a separately-instantiated health composable", async () => {
    dashboardApi.jobs.mockRejectedValue(new Error("jobs down"));
    const dateRange = ref("7d");
    let jobs, health;
    const Host = defineComponent({
      setup () {
        jobs = useJobsDashboard(dateRange);
        health = useHealthDashboard();
        return () => h("div");
      }
    });
    mount(Host);
    await flushPromises();

    expect(jobs.error.value).toBe("jobs down");
    expect(health.error.value).toBeNull();
    expect(health.status.value).toBe("Healthy");
    expect(health.allOperational.value).toBe(true);
  });
});
