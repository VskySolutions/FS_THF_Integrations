import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

const CATALOG = ["jobs.read", "jobs.trigger", "jobs.schedule", "tenants.read", "users.read", "roles.read"];

const { permissionGroupApi, roleApi, tenantState } = vi.hoisted(() => ({
  permissionGroupApi: {
    permissionCatalog: vi.fn(() => Promise.resolve(["jobs.read", "jobs.trigger", "jobs.schedule", "tenants.read", "users.read", "roles.read"])),
    get: vi.fn(() => Promise.resolve({})),
    create: vi.fn(() => Promise.resolve({})),
    update: vi.fn(() => Promise.resolve({}))
  },
  roleApi: { list: vi.fn(() => Promise.resolve([])), get: vi.fn(() => Promise.resolve({ permissions: [], effectivePermissions: [] })) },
  // Non-super-admin by default (component reads only `.value`).
  tenantState: {
    canChooseTenant: { value: false },
    activeTenantId: { value: "t1" },
    tenantOptions: { value: [] },
    loadingTenants: { value: false },
    loadTenants: vi.fn(() => Promise.resolve())
  }
}));

vi.mock("services/api", () => ({
  permissionGroupApi,
  roleApi,
  getApiErrorMessage: (e) => String(e),
  getApiErrorCode: () => null,
  ApiErrorCodes: {}
}));
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useTenantOptions", () => ({ useTenantOptions: () => tenantState }));

import PermissionGroupFormDrawer from "modules/permission-group/components/PermissionGroupFormDrawer.vue";

const AppFormDrawerStub = {
  props: ["modelValue", "title", "saving", "draft"],
  emits: ["submit", "cancel", "restore-draft"],
  template: "<div class='app-form-drawer'><slot /></div>"
};

const stubs = {
  AppFormDrawer: AppFormDrawerStub,
  AppSelect: true,
  AppTextField: { props: ["modelValue", "label"], template: "<input class='app-text-field' :data-label='label' />" },
  QForm: { template: "<form><slot /></form>", methods: { validate: () => Promise.resolve(true) } },
  QInput: { props: ["modelValue", "label"], template: "<input :data-label='label' />" },
  QSpace: true,
  QBadge: { template: "<span class='q-badge'><slot /></span>" },
  QIcon: true,
  QSpinner: true,
  QTooltip: true,
  QList: { template: "<div><slot /></div>" },
  QItem: { props: ["disable"], template: "<div class='q-item'><slot /></div>" },
  QItemSection: { template: "<div><slot /></div>" },
  QItemLabel: { template: "<div><slot /></div>" },
  QCheckbox: { props: ["modelValue", "val"], template: "<input type='checkbox' />" },
  // Expansion item: surface the category label so grouping is observable.
  QExpansionItem: { props: ["label", "caption"], template: "<div class='q-expansion' :data-label='label'><slot /></div>" }
};

const mountDrawer = (props = {}) => mount(PermissionGroupFormDrawer, {
  props: { modelValue: true, ...props },
  global: { stubs }
});

beforeEach(() => {
  vi.clearAllMocks();
  tenantState.canChooseTenant.value = false;
  permissionGroupApi.permissionCatalog.mockResolvedValue([...CATALOG]);
});

describe("PermissionGroupFormDrawer", () => {
  it("groups permission keys by category into expansion sections", async () => {
    const wrapper = mountDrawer();
    await flushPromises();

    const labels = wrapper.findAll(".q-expansion").map((n) => n.attributes("data-label"));
    // jobs.read/jobs.trigger → Jobs; jobs.schedule → Schedules; tenants → Tenants; users/roles → Users/Access.
    expect(labels).toContain("Jobs");
    expect(labels).toContain("Schedules");
    expect(labels).toContain("Tenants");
    expect(labels).toContain("Users");
    expect(labels).toContain("Access");
    // jobs.schedule belongs to Schedules, not Jobs.
    const jobs = wrapper.vm.visibleCategories.find((g) => g.category === "Jobs");
    expect(jobs.keys).toContain("jobs.read");
    expect(jobs.keys).not.toContain("jobs.schedule");
  });

  it("updates the real-time count badge as keys are selected", async () => {
    const wrapper = mountDrawer();
    await flushPromises();

    expect(wrapper.find("[data-test='key-count']").text()).toContain("0 selected");
    wrapper.vm.selectedKeys = ["jobs.read", "tenants.read"];
    await wrapper.vm.$nextTick();
    expect(wrapper.find("[data-test='key-count']").text()).toContain("2 selected");
  });

  it("pre-populates name, description and keys when a template is supplied", async () => {
    const template = { name: "Ops Bundle", description: "Operational keys", permissionKeys: ["jobs.read", "jobs.trigger"] };
    const wrapper = mountDrawer({ template });
    await flushPromises();

    expect(wrapper.vm.form.name).toBe("Ops Bundle");
    expect(wrapper.vm.form.description).toBe("Operational keys");
    expect(wrapper.vm.selectedKeys).toEqual(["jobs.read", "jobs.trigger"]);
  });
});
