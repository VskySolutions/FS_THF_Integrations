import { beforeEach, describe, it, expect, vi } from "vitest";
import { ref, reactive } from "vue";
import { mount } from "@vue/test-utils";

vi.mock("quasar", () => ({ debounce: (fn) => fn }));

const { smtpAccountApi } = vi.hoisted(() => ({
  smtpAccountApi: { list: vi.fn(), remove: vi.fn(), activate: vi.fn(), test: vi.fn() }
}));
vi.mock("services/api", () => ({ smtpAccountApi, getApiErrorMessage: (e) => String(e) }));

const listState = {
  rows: ref([]),
  loading: ref(false),
  totalRecords: ref(0),
  selected: ref([]),
  search: ref(""),
  filterOpen: ref(false),
  pagination: ref({ page: 1, rowsPerPage: 20 }),
  load: vi.fn(),
  onRequest: vi.fn()
};
vi.mock("composables/useListTable", () => ({ useListTable: () => listState }));

const columnFilterState = {
  filters: reactive({ status: null }),
  filterableColumns: ref([]),
  filterChips: ref([]),
  removeFilter: vi.fn(),
  clearFilters: vi.fn()
};
vi.mock("composables/useColumnFilters", () => ({ useColumnFilters: () => columnFilterState }));

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
vi.mock("composables/useDateFormat", () => ({ useDateFormat: () => ({ formatDateTime: (v) => v || "—" }) }));
vi.mock("stores/tenant", () => ({ useTenantStore: () => ({ activeTenantId: "t1" }) }));

import SmtpAccountsPage from "modules/smtp/pages/index.vue";

const stubs = {
  AppListHeader: { props: ["filterCount"], template: "<div class='app-list-header' :data-filter-count='filterCount'><slot name='actions' /></div>" },
  AppFilterDrawer: { template: "<div><slot /></div>" },
  AppColumnFilters: { template: "<div />" },
  AppSelect: { template: "<div class='app-select' />" },
  SmtpAccountFormDrawer: { template: "<div class='smtp-form-drawer' />" },
  TestEmailDialog: { template: "<div class='test-email-dialog' />" },
  QBanner: { template: "<div class='q-banner'><slot /></div>" },
  QPage: { template: "<div><slot /></div>" },
  QBadge: { props: ["color"], template: "<span class='q-badge' :data-color='color'><slot /></span>" },
  QBtn: true,
  QIcon: true,
  AppDataTable: {
    props: ["rows"],
    template: `
      <div class='app-data-table'>
        <div v-for='r in rows' :key='r.id' class='data-row'>
          <slot name='body-cell-status' :value='r.isActive' :row='r' :props='{ row: r }' />
        </div>
        <div v-if='!rows.length' class='empty'><slot name='no-data' /></div>
      </div>`
  },
  QTd: { template: "<td><slot /></td>" }
};

const mountPage = () => mount(SmtpAccountsPage, { global: { stubs } });

beforeEach(() => {
  vi.clearAllMocks();
  listState.rows.value = [];
  columnFilterState.filters.status = null;
  columnFilterState.filterChips.value = [];
  tenantState.canChooseTenant.value = false;
});

describe("SmtpAccountsPage", () => {
  it("renders a positive badge for the active account row", () => {
    listState.rows.value = [
      { id: "a1", accountName: "Primary", isActive: true },
      { id: "a2", accountName: "Backup", isActive: false }
    ];
    const wrapper = mountPage();
    const badges = wrapper.findAll(".data-row .q-badge");
    expect(badges[0].attributes("data-color")).toBe("positive");
    expect(badges[0].text()).toBe("Active");
    expect(badges[1].attributes("data-color")).toBe("grey");
  });

  it("shows the no-active-account warning banner when no row is active", () => {
    listState.rows.value = [{ id: "a1", accountName: "Backup", isActive: false }];
    const wrapper = mountPage();
    expect(wrapper.find(".q-banner").exists()).toBe(true);
    expect(wrapper.text()).toContain("No active email account");
  });

  it("hides the warning banner when an active account exists", () => {
    listState.rows.value = [{ id: "a1", accountName: "Primary", isActive: true }];
    const wrapper = mountPage();
    expect(wrapper.find(".q-banner").exists()).toBe(false);
  });

  it("shows the empty state when there are no accounts", () => {
    listState.rows.value = [];
    const wrapper = mountPage();
    expect(wrapper.find(".empty").exists()).toBe(true);
    expect(wrapper.text()).toContain("No email accounts configured");
  });

  it("blocks deleting the active account with an error toast and no API call", async () => {
    const wrapper = mountPage();
    await wrapper.vm.deleteAccount({ id: "a1", accountName: "Primary", isActive: true });
    expect(notify.error).toHaveBeenCalled();
    expect(smtpAccountApi.remove).not.toHaveBeenCalled();
    expect(confirmFn).not.toHaveBeenCalled();
  });

  it("set-as-active asks for confirmation then calls activate", async () => {
    confirmFn.mockResolvedValue(true);
    smtpAccountApi.activate.mockResolvedValue({ activatedId: "a2" });
    const wrapper = mountPage();
    await wrapper.vm.setActive({ id: "a2", accountName: "Backup", isActive: false });
    expect(confirmFn).toHaveBeenCalledTimes(1);
    expect(smtpAccountApi.activate).toHaveBeenCalledWith("a2", undefined);
  });

  it("set-as-active is a no-op for an already active row", async () => {
    const wrapper = mountPage();
    await wrapper.vm.setActive({ id: "a1", accountName: "Primary", isActive: true });
    expect(confirmFn).not.toHaveBeenCalled();
    expect(smtpAccountApi.activate).not.toHaveBeenCalled();
  });

  it("surfaces active filter chips in the list header count", () => {
    columnFilterState.filterChips.value = [{ key: "status", label: "Status: Active" }];
    const wrapper = mountPage();
    expect(wrapper.find(".app-list-header").attributes("data-filter-count")).toBe("1");
  });
});
