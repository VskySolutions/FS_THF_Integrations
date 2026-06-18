import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

const { permissionGroupApi, routerPush } = vi.hoisted(() => ({
  permissionGroupApi: { get: vi.fn(), setStatus: vi.fn(), remove: vi.fn() },
  routerPush: vi.fn()
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "g1" } }),
  useRouter: () => ({ push: routerPush })
}));
vi.mock("services/api", () => ({ permissionGroupApi, getApiErrorMessage: (e) => String(e) }));
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: vi.fn() }) }));
vi.mock("composables/useDateFormat", () => ({ useDateFormat: () => ({ formatDateTime: (v) => v || "—" }) }));

import PermissionGroupDetailPage from "modules/permission-group/pages/detail.vue";

const passthrough = { template: "<div><slot /></div>" };
const stubs = {
  AppDetailHeader: { template: "<div><slot name='actions' /></div>" },
  PermissionGroupFormDrawer: { template: "<div class='pg-form-drawer' />" },
  QPage: passthrough,
  QCard: passthrough,
  QCardSection: passthrough,
  QSeparator: true,
  QSpinner: true,
  QSpace: true,
  QBanner: { template: "<div class='q-banner'><slot /></div>" },
  QBadge: { template: "<span class='q-badge'><slot /></span>" },
  QList: passthrough,
  QItem: { props: { clickable: { type: Boolean, default: false } }, emits: ["click"], template: "<div class='q-item' :data-clickable='clickable ? \"1\" : \"0\"' @click='$emit(\"click\")'><slot /></div>" },
  QItemSection: passthrough,
  QItemLabel: { template: "<div class='item-label'><slot /></div>" },
  QBtn: { props: ["icon"], emits: ["click"], template: "<button class='q-btn' :data-icon='icon' @click='$emit(\"click\")'><slot /></button>" },
  QIcon: true,
  QTooltip: true
};

const detailFixture = (overrides = {}) => ({
  id: "g1",
  name: "Ops Bundle",
  description: "Ops keys",
  isActive: true,
  permissionKeys: ["jobs.read", "tenants.read"],
  rolesUsing: [{ roleId: "r1", roleName: "Operator" }, { roleId: "r2", roleName: "Auditor" }],
  auditTrail: [
    { action: "Created", performedBy: "alice", performedOnUtc: "2026-01-01T10:00:00Z", details: "init" },
    { action: "Updated", performedBy: "bob", performedOnUtc: "2026-06-01T10:00:00Z", details: "edit" }
  ],
  ...overrides
});

const mountDetail = async (detail) => {
  permissionGroupApi.get.mockResolvedValue(detail);
  const wrapper = mount(PermissionGroupDetailPage, { global: { stubs } });
  await flushPromises();
  return wrapper;
};

beforeEach(() => vi.clearAllMocks());

describe("PermissionGroupDetailPage", () => {
  it("shows the warning banner when the group is inactive", async () => {
    const wrapper = await mountDetail(detailFixture({ isActive: false }));
    const banner = wrapper.find("[data-test='inactive-banner']");
    expect(banner.exists()).toBe(true);
    expect(banner.text()).toContain("contributes zero permissions");
  });

  it("does not show the warning banner when active", async () => {
    const wrapper = await mountDetail(detailFixture({ isActive: true }));
    expect(wrapper.find("[data-test='inactive-banner']").exists()).toBe(false);
  });

  it("orders audit trail entries newest-first", async () => {
    const wrapper = await mountDetail(detailFixture());
    expect(wrapper.vm.auditTrail.map((e) => e.action)).toEqual(["Updated", "Created"]);
    // The first rendered audit label is the newest entry.
    const labels = wrapper.findAll(".item-label").map((n) => n.text());
    expect(labels.indexOf("Updated")).toBeLessThan(labels.indexOf("Created"));
  });

  it("renders 'Roles Using' entries as clickable items that navigate", async () => {
    const wrapper = await mountDetail(detailFixture());
    expect(wrapper.text()).toContain("Operator");
    expect(wrapper.text()).toContain("Auditor");
    const clickable = wrapper.findAll(".q-item").filter((i) => i.attributes("data-clickable") === "1");
    expect(clickable.length).toBeGreaterThanOrEqual(2);
    await clickable[0].trigger("click");
    expect(routerPush).toHaveBeenCalledWith({ name: "roles" });
  });
});
