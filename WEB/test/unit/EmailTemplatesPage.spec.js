import { beforeEach, describe, it, expect, vi } from "vitest";
import { ref } from "vue";
import { mount } from "@vue/test-utils";

const { emailTemplateApi } = vi.hoisted(() => ({
  emailTemplateApi: { list: vi.fn(), reset: vi.fn(), preview: vi.fn() }
}));
vi.mock("services/api", () => ({ emailTemplateApi, getApiErrorMessage: (e) => String(e) }));

const listState = {
  rows: ref([]),
  loading: ref(false),
  totalRecords: ref(0),
  pagination: ref({ page: 1, rowsPerPage: 20 }),
  load: vi.fn(),
  onRequest: vi.fn()
};
vi.mock("composables/useListTable", () => ({ useListTable: () => listState }));

const tenantState = {
  canChooseTenant: ref(false),
  tenantOptions: ref([]),
  loadingTenants: ref(false),
  loadTenants: vi.fn()
};
vi.mock("composables/useTenantOptions", () => ({ useTenantOptions: () => tenantState }));

const confirmFn = vi.fn();
const notify = { success: vi.fn(), error: vi.fn() };
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: confirmFn }) }));
vi.mock("composables/useNotify", () => ({ useNotify: () => notify }));

import EmailTemplatesPage from "modules/email-template/pages/index.vue";

const stubs = {
  AppListHeader: { template: "<div class='app-list-header'><slot name='actions' /></div>" },
  AppSelect: { template: "<div class='app-select' />" },
  EmailTemplateFormDrawer: { template: "<div class='form-drawer' />" },
  EmailTemplatePreviewDialog: { props: ["modelValue"], template: "<div class='preview-dialog' :data-open='modelValue' />" },
  QBanner: { template: "<div class='q-banner'><slot /></div>" },
  QPage: { template: "<div><slot /></div>" },
  QBadge: { props: ["color"], template: "<span class='q-badge' :data-color='color'><slot /></span>" },
  QBtn: true,
  QIcon: true,
  AppDataTable: {
    props: ["rows"],
    template: `
      <div class='app-data-table'>
        <div v-for='r in rows' :key='r.key' class='data-row'>
          <slot name='body-cell-status' :row='r' :props='{ row: r }' />
        </div>
      </div>`
  },
  QTd: { template: "<td><slot /></td>" }
};

const mountPage = () => mount(EmailTemplatesPage, { global: { stubs } });

beforeEach(() => {
  vi.clearAllMocks();
  listState.rows.value = [];
  tenantState.canChooseTenant.value = false;
});

describe("EmailTemplatesPage", () => {
  it("renders a Custom/Default badge per template row", () => {
    listState.rows.value = [
      { key: "UserInvitation", displayName: "User Invitation", subject: "Hi", isOverridden: true },
      { key: "Welcome", displayName: "Welcome", subject: "Hello", isOverridden: false }
    ];
    const wrapper = mountPage();
    const badges = wrapper.findAll(".data-row .q-badge");
    expect(badges[0].text()).toBe("Custom");
    expect(badges[0].attributes("data-color")).toBe("positive");
    expect(badges[1].text()).toBe("Default");
  });

  it("reset asks for confirmation then calls the API", async () => {
    confirmFn.mockResolvedValue(true);
    emailTemplateApi.reset.mockResolvedValue({});
    const wrapper = mountPage();
    await wrapper.vm.resetRow({ key: "Welcome", displayName: "Welcome", isOverridden: true });
    expect(confirmFn).toHaveBeenCalledTimes(1);
    expect(emailTemplateApi.reset).toHaveBeenCalledWith("Welcome", {});
  });

  it("preview fetches the rendered template and opens the dialog", async () => {
    emailTemplateApi.preview.mockResolvedValue({ subject: "Hi", body: "<p>Hi</p>" });
    const wrapper = mountPage();
    await wrapper.vm.previewRow({ key: "UserInvitation" });
    expect(emailTemplateApi.preview).toHaveBeenCalledWith("UserInvitation", {}, {});
    expect(wrapper.vm.previewOpen).toBe(true);
  });

  it("super admin can reset any template (global scope), tenant admin only overridden ones", () => {
    // Tenant admin (cannot choose tenant): reset only when overridden.
    let wrapper = mountPage();
    expect(wrapper.vm.canReset({ isOverridden: false })).toBe(false);
    expect(wrapper.vm.canReset({ isOverridden: true })).toBe(true);

    // Super admin defaults to the global scope: reset always available.
    tenantState.canChooseTenant.value = true;
    wrapper = mountPage();
    expect(wrapper.vm.isGlobalScope).toBe(true);
    expect(wrapper.vm.canReset({ isOverridden: false })).toBe(true);
    expect(wrapper.vm.scopeParams).toEqual({ global: true });
  });
});
