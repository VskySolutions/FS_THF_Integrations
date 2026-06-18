import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

vi.mock("vue-router", () => ({ useRoute: () => ({ params: { id: "c1" } }) }));

const { customerApi } = vi.hoisted(() => ({ customerApi: { get: vi.fn() } }));
vi.mock("services/api", () => ({
  customerApi,
  getApiErrorMessage: (e) => String(e),
  getApiErrorCode: () => null,
  ApiErrorCodes: { ValidationFailed: "VALIDATION_FAILED" }
}));

vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useConfirm", () => ({ useConfirm: () => ({ confirm: vi.fn() }) }));
vi.mock("composables/useDateFormat", () => ({ useDateFormat: () => ({ formatDateTime: (v) => v || "—" }) }));

import CustomerDetailPage from "modules/customer/pages/detail.vue";

const noActions = {
  canEdit: false,
  canDelete: false,
  canSubmit: false,
  canEnrich: false,
  canSendForApproval: false,
  canViewStep2: false,
  canEditStep2: false,
  canApprove: false,
  canReject: false,
  canReturn: false,
  canRetrySync: false,
  canReopen: false
};

const baseDetail = (overrides = {}) => ({
  id: "c1",
  customerRequestNumber: "CR-1001",
  companyName: "Acme",
  legalName: "Acme Inc",
  status: "UnderReview",
  step2: null,
  maconomyCustomerNumber: null,
  auditTrail: [],
  documents: [],
  actions: { ...noActions },
  ...overrides
});

// Render real q-cards/badges/chips minimally; pass through slots so v-if logic is observable.
const passthrough = { template: "<div><slot /></div>" };
const stubs = {
  AppDetailHeader: { template: "<div><slot name='actions' /></div>" },
  AppTextField: { props: ["modelValue", "label"], template: "<div class='app-text-field' :data-label='label' />" },
  AppSelect: true,
  QPage: passthrough,
  QCard: { props: ["class"], template: "<div class='q-card'><slot /></div>" },
  QCardSection: passthrough,
  QSeparator: true,
  QSpinner: true,
  QSpace: true,
  QBanner: { template: "<div class='q-banner'><slot /></div>" },
  QChip: { template: "<span class='q-chip'><slot /></span>" },
  QBadge: { template: "<span class='q-badge'><slot /></span>" },
  QTimeline: passthrough,
  QTimelineEntry: { props: ["title"], template: "<div class='timeline-entry'>{{ title }}</div>" },
  QList: passthrough,
  QItem: passthrough,
  QItemSection: passthrough,
  QItemLabel: { template: "<div class='item-label'><slot /></div>" },
  QFile: true,
  QBtn: { props: ["label"], template: "<button class='q-btn' :data-label='label'><slot /></button>" },
  QIcon: true,
  QTooltip: true,
  QDialog: { props: ["modelValue"], template: "<div v-if='modelValue'><slot /></div>" },
  QForm: passthrough,
  QInput: true,
  QCardActions: passthrough
};

const mountDetail = async (detail) => {
  customerApi.get.mockResolvedValue(detail);
  const wrapper = mount(CustomerDetailPage, { global: { stubs } });
  await flushPromises();
  return wrapper;
};

beforeEach(() => vi.clearAllMocks());

describe("CustomerDetailPage", () => {
  it("hides the Maconomy Fields card when step2 is null", async () => {
    const wrapper = await mountDetail(baseDetail({ step2: null }));
    expect(wrapper.text()).not.toContain("Maconomy Fields");
  });

  it("shows the Maconomy Fields card when step2 is present", async () => {
    const wrapper = await mountDetail(baseDetail({
      step2: { taxNumber: "T1", registrationNumber: "R1", businessUnit: "BU", currency: "USD", paymentTerms: "Net30" },
      actions: { ...noActions, canViewStep2: true }
    }));
    expect(wrapper.text()).toContain("Maconomy Fields");
  });

  it("omits the Approve button without actions.canApprove", async () => {
    const wrapper = await mountDetail(baseDetail({ actions: { ...noActions, canApprove: false } }));
    const approve = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Approve");
    expect(approve).toBeUndefined();
  });

  it("renders the Approve button when actions.canApprove is true", async () => {
    const wrapper = await mountDetail(baseDetail({ status: "PendingApproval", actions: { ...noActions, canApprove: true } }));
    const approve = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Approve");
    expect(approve).toBeTruthy();
  });

  it("shows the Maconomy Customer Number prominently when Synced", async () => {
    const wrapper = await mountDetail(baseDetail({ status: "Synced", maconomyCustomerNumber: "MC-9000" }));
    expect(wrapper.text()).toContain("MC-9000");
  });

  it("renders the audit trail entries in the returned order", async () => {
    const wrapper = await mountDetail(baseDetail({
      auditTrail: [
        { id: "a1", actionType: "Created", performedBy: "Ann", performedOnUtc: "2026-01-01", notes: "" },
        { id: "a2", actionType: "Submitted", performedBy: "Ben", performedOnUtc: "2026-01-02", notes: "" },
        { id: "a3", actionType: "Approved", performedBy: "Cara", performedOnUtc: "2026-01-03", notes: "" }
      ]
    }));
    const labels = wrapper.findAll(".item-label").map((n) => n.text());
    const order = ["Created", "Submitted", "Approved"].map((t) => labels.findIndex((l) => l === t));
    expect(order[0]).toBeLessThan(order[1]);
    expect(order[1]).toBeLessThan(order[2]);
  });
});
