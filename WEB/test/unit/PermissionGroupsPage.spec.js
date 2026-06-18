import { beforeEach, describe, it, expect, vi } from "vitest";
import { ref } from "vue";
import { mount } from "@vue/test-utils";

vi.mock("quasar", () => ({ debounce: (fn) => fn }));

vi.mock("services/api", () => ({
  permissionGroupApi: { list: vi.fn(), templates: vi.fn(() => Promise.resolve([])), setStatus: vi.fn(), remove: vi.fn() },
  getApiErrorMessage: (e) => String(e)
}));

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
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: vi.fn() }) }));

import PermissionGroupsPage from "modules/permission-group/pages/index.vue";

const stubs = {
  AppListHeader: { template: "<div class='app-list-header'><slot name='actions' /></div>" },
  AppFilterDrawer: { template: "<div><slot /></div>" },
  AppColumnFilters: { template: "<div />" },
  AppSelect: { props: ["label"], template: "<div class='app-select' :data-label='label' />" },
  PermissionGroupFormDrawer: { template: "<div class='pg-form-drawer' />" },
  AppDataTable: {
    props: ["rows"],
    template: `
      <div class='app-data-table'>
        <div class='row-count'>{{ rows.length }}</div>
        <template v-if='rows.length'>
          <div v-for='r in rows' :key='r.id' class='data-row'>
            <slot name='body-cell-status' :value='r.isActive' :row='r' :props='{ row: r }' />
          </div>
        </template>
        <div v-else class='empty'><slot name='no-data' /></div>
      </div>`
  },
  QTd: { template: "<td><slot /></td>" },
  QBadge: { props: ["color"], template: "<span class='q-badge' :data-color='color'><slot /></span>" },
  QToggle: true,
  QPage: { template: "<div><slot /></div>" },
  QDialog: { props: ["modelValue"], template: "<div v-if='modelValue'><slot /></div>" },
  QCard: { template: "<div><slot /></div>" },
  QCardSection: { template: "<div><slot /></div>" },
  QCardActions: { template: "<div><slot /></div>" },
  QSeparator: true,
  QList: { template: "<div><slot /></div>" },
  QItem: { template: "<div><slot /></div>" },
  QItemSection: { template: "<div><slot /></div>" },
  QItemLabel: { template: "<div><slot /></div>" },
  QMenu: { template: "<div><slot /></div>" },
  QBtn: true,
  QIcon: true,
  QTooltip: true
};

const mountPage = () => mount(PermissionGroupsPage, { global: { stubs } });

beforeEach(() => {
  vi.clearAllMocks();
  listState.rows.value = [];
  listState.totalRecords.value = 0;
  tenantState.canChooseTenant.value = false;
  tenantState.tenantOptions.value = [];
});

describe("PermissionGroupsPage", () => {
  it("renders a row per permission group", () => {
    listState.rows.value = [
      { id: "g1", name: "Ops", description: "ops", permissionCount: 3, rolesUsingCount: 2, isActive: true },
      { id: "g2", name: "ReadOnly", description: "", permissionCount: 1, rolesUsingCount: 0, isActive: false }
    ];
    const wrapper = mountPage();
    expect(wrapper.find(".row-count").text()).toBe("2");
    expect(wrapper.findAll(".data-row")).toHaveLength(2);
  });

  it("shows the empty-state prompt when there are no groups", () => {
    listState.rows.value = [];
    const wrapper = mountPage();
    expect(wrapper.find(".empty").exists()).toBe(true);
    expect(wrapper.text()).toContain("No permission groups yet");
  });

  it("renders the status badge reflecting isActive", () => {
    listState.rows.value = [
      { id: "g1", name: "Ops", description: "ops", permissionCount: 3, rolesUsingCount: 2, isActive: true },
      { id: "g2", name: "ReadOnly", description: "", permissionCount: 1, rolesUsingCount: 0, isActive: false }
    ];
    const wrapper = mountPage();
    const badges = wrapper.findAll(".data-row .q-badge");
    expect(badges[0].text()).toBe("Active");
    expect(badges[0].attributes("data-color")).toBe("positive");
    expect(badges[1].text()).toBe("Inactive");
    expect(badges[1].attributes("data-color")).toBe("grey");
  });

  it("hides the Super-Admin tenant dropdown for non-super-admins", () => {
    tenantState.canChooseTenant.value = false;
    const wrapper = mountPage();
    expect(wrapper.find(".app-list-header .app-select[data-label='Tenant']").exists()).toBe(false);
  });

  it("shows the Super-Admin tenant dropdown when the user can choose a tenant", () => {
    tenantState.canChooseTenant.value = true;
    tenantState.tenantOptions.value = [{ label: "Acme", value: "t1" }];
    const wrapper = mountPage();
    expect(wrapper.find(".app-list-header .app-select[data-label='Tenant']").exists()).toBe(true);
  });
});
