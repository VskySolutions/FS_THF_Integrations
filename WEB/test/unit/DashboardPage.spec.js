import { beforeEach, describe, it, expect, vi } from "vitest";
import { ref, computed } from "vue";
import { mount, flushPromises } from "@vue/test-utils";

// ---- Permissions: drive the role the page resolves. ----
const { hasFn } = vi.hoisted(() => ({ hasFn: vi.fn() }));
vi.mock("composables/usePermissions", () => ({
  usePermissions: () => ({ has: hasFn }),
  Permissions: { TenantsWrite: "tenants.write", UsersRead: "users.read", TenantsRead: "tenants.read" }
}));

// ---- Layout composable: a controllable fake (visibleWidgets reflects the resolved role). ----
import { defaultLayoutForRole, WIDGETS_BY_KEY } from "modules/dashboard/widgets/registry";

const { layoutState } = vi.hoisted(() => ({ layoutState: {} }));
vi.mock("composables/useDashboardLayout", () => ({
  useDashboardLayout: (role) => {
    // Build visible widgets from the registry default for the resolved role, but swap each widget's
    // component for a trivial async stub so the page never resolves the real (heavy) widget SFCs.
    const keys = defaultLayoutForRole(role);
    const widgets = keys.map((key) => ({
      key,
      ...WIDGETS_BY_KEY[key],
      component: () => Promise.resolve({ template: "<div class='widget-stub'></div>" })
    }));
    Object.assign(layoutState, {
      role,
      widgetOrder: ref(keys),
      visibleWidgets: computed(() => widgets),
      isCollapsed: () => false,
      setCollapsed: vi.fn(),
      loadLayout: vi.fn().mockResolvedValue(undefined)
    });
    return layoutState;
  }
}));

// ---- Data composables: minimal valid surfaces, all resolved. ----
const makeSource = (extra = {}) => ({
  loading: ref(false),
  error: ref(null),
  refresh: vi.fn(),
  kpis: ref({}),
  successRate: ref(0),
  volumeChart: ref([]),
  flowBreakdown: ref([]),
  failedJobs: ref([]),
  retryQueueCount: ref(0),
  retryQueueNextRunUtc: ref(null),
  status: ref("Healthy"),
  components: ref([]),
  allOperational: ref(true),
  funnel: ref([]),
  ageing: ref([]),
  syncHealth: ref(null),
  activityFeed: ref([]),
  topSubmitters: ref([]),
  submissionTrend: ref([]),
  roleDistribution: ref([]),
  tenantKpis: ref(null),
  crossTenantJobs: ref([]),
  tenantHealth: ref([]),
  growth: ref([]),
  onboarding: ref([]),
  systemAlerts: ref([]),
  userAnalytics: ref(null),
  customer: ref(null),
  ...extra
});
vi.mock("composables/useDashboardData", () => ({
  useJobsDashboard: () => makeSource(),
  useHealthDashboard: () => makeSource(),
  useCustomerDashboard: () => makeSource(),
  useUserDashboard: () => makeSource(),
  usePlatformDashboard: () => makeSource()
}));

vi.mock("composables/usePreferences", () => ({
  usePreferences: () => ({ get: (_k, fb) => fb, set: vi.fn() })
}));
vi.mock("composables/useNotify", () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() })
}));

import DashboardPage from "modules/dashboard/pages/DashboardPage.vue";

const passthrough = { template: "<div><slot /></div>" };
const stubs = {
  AppBreadcrumbs: true,
  // The customise panel: expose its visibility via modelValue so we can assert it opened.
  DashboardCustomisePanel: {
    props: ["modelValue", "role", "layout"],
    template: "<div class='customise-panel' :data-open='modelValue' />"
  },
  QPage: passthrough,
  QBtn: {
    props: ["label"],
    template: "<button class='q-btn' :data-label='label'><slot /></button>"
  },
  QBtnDropdown: passthrough,
  QList: passthrough,
  QItem: passthrough,
  QItemSection: passthrough,
  QIcon: true,
  QSpace: true,
  QBanner: { template: "<div class='q-banner'><slot /></div>" },
  QTooltip: true
};

const mountPage = async () => {
  const wrapper = mount(DashboardPage, {
    global: { stubs, directives: { "close-popup": {} } }
  });
  await flushPromises();
  return wrapper;
};

const visibleKeys = (wrapper) => layoutState.visibleWidgets.value.map((w) => w.key);

beforeEach(() => {
  vi.clearAllMocks();
  hasFn.mockReturnValue(false);
});

describe("DashboardPage", () => {
  it("renders the Common widget set for an Operator (no admin permissions)", async () => {
    hasFn.mockReturnValue(false);
    const wrapper = await mountPage();
    expect(layoutState.role).toBe("common");
    expect(visibleKeys(wrapper)).toEqual(defaultLayoutForRole("common"));
    // The async widget stubs mount.
    expect(wrapper.findAll(".widget-stub").length).toBe(defaultLayoutForRole("common").length);
  });

  it("renders the Tenant Admin set when the user has users.read + tenants.read", async () => {
    hasFn.mockImplementation((p) => p === "users.read" || p === "tenants.read");
    const wrapper = await mountPage();
    expect(layoutState.role).toBe("tenantAdmin");
    expect(visibleKeys(wrapper)).toEqual(defaultLayoutForRole("tenantAdmin"));
  });

  it("renders the Super Admin set when the user has tenants.write", async () => {
    hasFn.mockImplementation((p) => p === "tenants.write");
    const wrapper = await mountPage();
    expect(layoutState.role).toBe("superAdmin");
    expect(visibleKeys(wrapper)).toEqual(defaultLayoutForRole("superAdmin"));
  });

  it("calls layout.loadLayout on mount", async () => {
    await mountPage();
    expect(layoutState.loadLayout).toHaveBeenCalledTimes(1);
  });

  it("clicking Customise opens the DashboardCustomisePanel", async () => {
    const wrapper = await mountPage();
    expect(wrapper.find(".customise-panel").attributes("data-open")).toBe("false");

    const customise = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Customise");
    expect(customise).toBeTruthy();
    await customise.trigger("click");

    expect(wrapper.find(".customise-panel").attributes("data-open")).toBe("true");
  });
});
