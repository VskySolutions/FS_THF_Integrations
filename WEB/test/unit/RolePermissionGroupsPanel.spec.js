import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

const { roleApi, permissionGroupApi, confirmMock } = vi.hoisted(() => ({
  roleApi: { getGroups: vi.fn(), assignGroups: vi.fn(), removeGroup: vi.fn(), previewPermissions: vi.fn() },
  permissionGroupApi: { list: vi.fn() },
  confirmMock: vi.fn(() => Promise.resolve(true))
}));

vi.mock("services/api", () => ({ roleApi, permissionGroupApi, getApiErrorMessage: (e) => String(e) }));
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: confirmMock }) }));

import RolePermissionGroupsPanel from "modules/permission-group/components/RolePermissionGroupsPanel.vue";

const passthrough = { template: "<div><slot /></div>" };
const stubs = {
  AppSelect: { props: ["modelValue"], template: "<div class='app-select' />" },
  QSpace: true,
  QBtn: { props: ["label"], emits: ["click"], template: "<button class='q-btn' :data-label='label' @click='$emit(\"click\")'><slot /></button>" },
  QBadge: { template: "<span class='q-badge'><slot /></span>" },
  QList: passthrough,
  QItem: passthrough,
  QItemSection: passthrough,
  QItemLabel: { template: "<div><slot /></div>" },
  QIcon: true,
  QTooltip: true,
  QSpinner: true,
  QDialog: { props: ["modelValue"], template: "<div v-if='modelValue' class='q-dialog'><slot /></div>" },
  QCard: passthrough,
  QCardSection: passthrough,
  QCardActions: passthrough,
  QSeparator: true
};

const mountPanel = () => mount(RolePermissionGroupsPanel, { props: { roleId: "r1" }, global: { stubs } });

beforeEach(() => {
  vi.clearAllMocks();
  confirmMock.mockResolvedValue(true);
  roleApi.getGroups.mockResolvedValue({
    groups: [{ id: "g1", name: "Ops", permissionCount: 2, isActive: true }],
    effectivePermissions: ["jobs.read", "jobs.trigger"]
  });
});

describe("RolePermissionGroupsPanel", () => {
  it("shows the union of effective permissions by category in the preview dialog", async () => {
    roleApi.previewPermissions.mockResolvedValue({ permissions: ["jobs.read", "tenants.read", "users.read"], sources: [] });
    const wrapper = mountPanel();
    await flushPromises();

    await wrapper.vm.openPreview();
    await flushPromises();

    const categories = wrapper.vm.previewGroups.map((g) => g.category);
    expect(categories).toContain("Jobs");
    expect(categories).toContain("Tenants");
    expect(categories).toContain("Users");
    // The preview dialog renders one section per category.
    expect(wrapper.findAll("[data-test='preview-category']").length).toBe(categories.length);
  });

  it("requires confirmation before removing a group", async () => {
    roleApi.removeGroup.mockResolvedValue({ effectivePermissions: [] });
    const wrapper = mountPanel();
    await flushPromises();

    await wrapper.vm.removeGroup({ id: "g1", name: "Ops" });
    await flushPromises();

    expect(confirmMock).toHaveBeenCalled();
    expect(roleApi.removeGroup).toHaveBeenCalledWith("r1", "g1");
  });

  it("does NOT remove when confirmation is declined", async () => {
    confirmMock.mockResolvedValue(false);
    const wrapper = mountPanel();
    await flushPromises();

    await wrapper.vm.removeGroup({ id: "g1", name: "Ops" });
    await flushPromises();

    expect(roleApi.removeGroup).not.toHaveBeenCalled();
  });

  it("updates the effective-permission display when a group is added", async () => {
    const afterAdd = {
      groups: [
        { id: "g1", name: "Ops", permissionCount: 2, isActive: true },
        { id: "g2", name: "Audit", permissionCount: 1, isActive: true }
      ],
      effectivePermissions: ["jobs.read", "jobs.trigger", "logs.read"]
    };
    roleApi.assignGroups.mockResolvedValue(afterAdd);
    roleApi.getGroups.mockResolvedValue(afterAdd); // post-assign reload reflects the new set
    const wrapper = mountPanel();
    await flushPromises();

    wrapper.vm.selectedToAdd = ["g2"];
    await wrapper.vm.confirmAdd();
    await flushPromises();

    expect(roleApi.assignGroups).toHaveBeenCalledWith("r1", ["g2"]);
    expect(wrapper.vm.previewKeys).toContain("logs.read");
    expect(wrapper.vm.previewGroups.map((g) => g.category)).toContain("System"); // logs.* → System
  });
});
