import { beforeEach, describe, it, expect, vi } from "vitest";
import { ref } from "vue";
import { mount } from "@vue/test-utils";

// ---- Mocks (declared before importing the component under test) ----
vi.mock("quasar", () => ({ debounce: (fn) => fn }));

vi.mock("services/api", () => ({
  customerApi: { list: vi.fn(), remove: vi.fn() },
  getApiErrorMessage: (e) => String(e)
}));

// useListTable: return a controllable surface so we can drive rows / empty state.
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

vi.mock("composables/useColumnFilters", () => ({
  useColumnFilters: () => ({
    filters: {},
    filterableColumns: ref([]),
    filterChips: ref([]),
    removeFilter: vi.fn(),
    clearFilters: vi.fn()
  })
}));

const tenantState = {
  canChooseTenant: ref(false),
  tenantOptions: ref([]),
  loadingTenants: ref(false),
  loadTenants: vi.fn()
};
vi.mock("composables/useTenantOptions", () => ({ useTenantOptions: () => tenantState }));

vi.mock("stores/tenant", () => ({ useTenantStore: () => ({ activeTenantId: "t1" }) }));
vi.mock("composables/usePermissions", () => ({
  usePermissions: () => ({ has: () => true, hasAny: () => true }),
  Permissions: { CustomersDataEntry: "customers.dataEntry" }
}));
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: vi.fn() }) }));
vi.mock("composables/useDateFormat", () => ({ useDateFormat: () => ({ formatDateTime: (v) => v || "—" }) }));

import CustomersPage from "modules/customer/pages/index.vue";
import { customerStatusColor, customerStatusLabel } from "composables/useCustomerStatus";

const stubs = {
  AppListHeader: { template: "<div class='app-list-header'><slot name='actions' /></div>" },
  AppFilterDrawer: { template: "<div><slot /></div>" },
  AppColumnFilters: { template: "<div />" },
  AppSelect: { template: "<div class='app-select' />" },
  CustomerFormDrawer: { template: "<div class='customer-form-drawer' />" },
  // AppDataTable: expose rows + the status/no-data slots so we can assert on them.
  AppDataTable: {
    props: ["rows"],
    template: `
      <div class='app-data-table'>
        <div class='row-count'>{{ rows.length }}</div>
        <template v-if='rows.length'>
          <div v-for='r in rows' :key='r.id' class='data-row'>
            <slot name='body-cell-status' :value='r.status' :row='r' :props='{ row: r }' />
          </div>
        </template>
        <div v-else class='empty'><slot name='no-data' /></div>
      </div>`
  },
  QTd: { template: "<td><slot /></td>" },
  QBadge: { props: ["color"], template: "<span class='q-badge' :data-color='color'><slot /></span>" },
  QPage: { template: "<div><slot /></div>" },
  QBtn: true,
  QIcon: true
};

const mountPage = () => mount(CustomersPage, { global: { stubs } });

beforeEach(() => {
  vi.clearAllMocks();
  listState.rows.value = [];
  listState.totalRecords.value = 0;
  tenantState.canChooseTenant.value = false;
  tenantState.tenantOptions.value = [];
});

describe("CustomersPage", () => {
  it("renders a row per customer", () => {
    listState.rows.value = [
      { id: "c1", customerRequestNumber: "CR-1", companyName: "Acme", legalName: "Acme Inc", status: "Draft" },
      { id: "c2", customerRequestNumber: "CR-2", companyName: "Globex", legalName: "Globex LLC", status: "Synced" }
    ];
    const wrapper = mountPage();
    expect(wrapper.find(".row-count").text()).toBe("2");
    expect(wrapper.findAll(".data-row")).toHaveLength(2);
  });

  it("shows the empty-state prompt when there are no customers", () => {
    listState.rows.value = [];
    const wrapper = mountPage();
    expect(wrapper.find(".empty").exists()).toBe(true);
    expect(wrapper.text()).toContain("No customers yet");
  });

  it("renders the status badge with the mapped colour and label", () => {
    listState.rows.value = [{ id: "c1", customerRequestNumber: "CR-1", companyName: "Acme", legalName: "Acme Inc", status: "Synced" }];
    const wrapper = mountPage();
    const badge = wrapper.find(".data-row .q-badge");
    expect(badge.text()).toBe(customerStatusLabel("Synced"));
    expect(badge.attributes("data-color")).toBe(customerStatusColor("Synced"));
  });

  it("hides the Super-Admin tenant dropdown for non-super-admins", () => {
    tenantState.canChooseTenant.value = false;
    const wrapper = mountPage();
    expect(wrapper.find(".app-list-header .app-select").exists()).toBe(false);
  });

  it("shows the Super-Admin tenant dropdown when the user can choose a tenant", () => {
    tenantState.canChooseTenant.value = true;
    tenantState.tenantOptions.value = [{ label: "Acme", value: "t1" }];
    const wrapper = mountPage();
    expect(wrapper.find(".app-list-header .app-select").exists()).toBe(true);
  });
});
