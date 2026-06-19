import { beforeEach, afterEach, describe, it, expect, vi } from "vitest";

// quasar.debounce: a real setTimeout-based debounce so we can drive the 500ms flush with fake timers.
vi.mock("quasar", () => ({
  debounce: (fn, wait) => {
    let t = null;
    const wrapped = (...args) => {
      if (t) clearTimeout(t);
      t = setTimeout(() => { t = null; fn(...args); }, wait);
    };
    return wrapped;
  }
}));

const { dashboardApi } = vi.hoisted(() => ({
  dashboardApi: { getLayout: vi.fn(), saveLayout: vi.fn() }
}));
vi.mock("services/api", () => ({
  dashboardApi,
  getApiErrorMessage: (e, fallback) => (e?.message || fallback || String(e))
}));

const { confirmFn } = vi.hoisted(() => ({ confirmFn: vi.fn() }));
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: confirmFn }) }));

const { notify } = vi.hoisted(() => ({ notify: { success: vi.fn(), error: vi.fn() } }));
vi.mock("composables/useNotify", () => ({ useNotify: () => notify }));

import { useDashboardLayout } from "composables/useDashboardLayout";
import { defaultLayoutForRole } from "modules/dashboard/widgets/registry";

beforeEach(() => {
  vi.clearAllMocks();
  dashboardApi.getLayout.mockResolvedValue({ widgetOrder: [], hiddenWidgets: [], collapsedWidgets: [] });
  dashboardApi.saveLayout.mockResolvedValue({});
});

afterEach(() => {
  vi.useRealTimers();
});

describe("useDashboardLayout", () => {
  it("loadLayout() calls getLayout and populates state from the response", async () => {
    dashboardApi.getLayout.mockResolvedValue({
      widgetOrder: ["jobKpiCards", "flowBreakdown"],
      hiddenWidgets: ["flowBreakdown"],
      collapsedWidgets: ["jobKpiCards"]
    });
    const layout = useDashboardLayout("common");
    await layout.loadLayout();

    expect(dashboardApi.getLayout).toHaveBeenCalledTimes(1);
    expect(layout.widgetOrder.value).toEqual(["jobKpiCards", "flowBreakdown"]);
    expect(layout.hiddenWidgets.value).toEqual(["flowBreakdown"]);
    expect(layout.collapsedWidgets.value).toEqual(["jobKpiCards"]);
  });

  it("loadLayout() falls back to defaultLayoutForRole when widgetOrder is empty", async () => {
    dashboardApi.getLayout.mockResolvedValue({ widgetOrder: [], hiddenWidgets: [], collapsedWidgets: [] });
    const layout = useDashboardLayout("tenantAdmin");
    await layout.loadLayout();

    expect(layout.widgetOrder.value).toEqual(defaultLayoutForRole("tenantAdmin"));
  });

  it("loadLayout() falls back to the default layout when getLayout rejects", async () => {
    dashboardApi.getLayout.mockRejectedValue(new Error("boom"));
    const layout = useDashboardLayout("superAdmin");
    await layout.loadLayout();

    expect(layout.widgetOrder.value).toEqual(defaultLayoutForRole("superAdmin"));
    expect(layout.hiddenWidgets.value).toEqual([]);
    expect(layout.collapsedWidgets.value).toEqual([]);
  });

  it("isHidden(key) / isCollapsed(key) reflect the loaded state", async () => {
    dashboardApi.getLayout.mockResolvedValue({
      widgetOrder: ["jobKpiCards", "flowBreakdown"],
      hiddenWidgets: ["flowBreakdown"],
      collapsedWidgets: ["jobKpiCards"]
    });
    const layout = useDashboardLayout("common");
    await layout.loadLayout();

    expect(layout.isHidden("flowBreakdown")).toBe(true);
    expect(layout.isHidden("jobKpiCards")).toBe(false);
    expect(layout.isCollapsed("jobKpiCards")).toBe(true);
    expect(layout.isCollapsed("flowBreakdown")).toBe(false);
  });

  it("saveLayout() debounces and calls dashboardApi.saveLayout after 500ms", async () => {
    vi.useFakeTimers();
    const layout = useDashboardLayout("common");
    layout.widgetOrder.value = ["jobKpiCards"];

    layout.saveLayout();
    layout.saveLayout();
    layout.saveLayout();
    // Nothing should fire before the debounce window elapses.
    expect(dashboardApi.saveLayout).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(500);

    expect(dashboardApi.saveLayout).toHaveBeenCalledTimes(1);
    expect(dashboardApi.saveLayout).toHaveBeenCalledWith({
      widgetOrder: ["jobKpiCards"],
      hiddenWidgets: [],
      collapsedWidgets: []
    });
  });

  it("toggleHidden() updates hiddenWidgets and persists after the debounce", async () => {
    vi.useFakeTimers();
    const layout = useDashboardLayout("common");

    layout.toggleHidden("jobKpiCards");
    expect(layout.isHidden("jobKpiCards")).toBe(true);

    await vi.advanceTimersByTimeAsync(500);
    expect(dashboardApi.saveLayout).toHaveBeenCalledTimes(1);

    layout.toggleHidden("jobKpiCards");
    expect(layout.isHidden("jobKpiCards")).toBe(false);
  });

  it("resetToDefault() does nothing when the confirm is declined", async () => {
    confirmFn.mockResolvedValue(false);
    const layout = useDashboardLayout("common");
    layout.widgetOrder.value = ["custom"];

    await layout.resetToDefault();

    expect(confirmFn).toHaveBeenCalledTimes(1);
    expect(layout.widgetOrder.value).toEqual(["custom"]);
    expect(dashboardApi.saveLayout).not.toHaveBeenCalled();
  });

  it("resetToDefault() resets order + clears hidden/collapsed and saves when confirmed", async () => {
    vi.useFakeTimers();
    confirmFn.mockResolvedValue(true);
    const layout = useDashboardLayout("tenantAdmin");
    layout.hiddenWidgets.value = ["flowBreakdown"];
    layout.collapsedWidgets.value = ["jobKpiCards"];

    await layout.resetToDefault();

    expect(layout.widgetOrder.value).toEqual(defaultLayoutForRole("tenantAdmin"));
    expect(layout.hiddenWidgets.value).toEqual([]);
    expect(layout.collapsedWidgets.value).toEqual([]);
    expect(notify.success).toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(500);
    expect(dashboardApi.saveLayout).toHaveBeenCalledTimes(1);
  });

  it("visibleWidgets excludes hidden widgets and respects the role tier", async () => {
    dashboardApi.getLayout.mockResolvedValue({
      widgetOrder: ["jobKpiCards", "flowBreakdown", "tenantKpiCards"],
      hiddenWidgets: ["flowBreakdown"],
      collapsedWidgets: []
    });
    const layout = useDashboardLayout("common");
    await layout.loadLayout();

    const keys = layout.visibleWidgets.value.map((w) => w.key);
    expect(keys).toContain("jobKpiCards");
    expect(keys).not.toContain("flowBreakdown"); // hidden
    expect(keys).not.toContain("tenantKpiCards"); // not allowed for common role
  });
});
