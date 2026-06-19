import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

// Hoisted so the values exist before the (hoisted) vi.mock factories reference them. The component
// only reads `.value` off these tenant fields, so plain { value } objects stand in for refs.
const { customerApi, tenantState } = vi.hoisted(() => ({
  customerApi: { create: vi.fn(), submit: vi.fn() },
  tenantState: {
    canChooseTenant: { value: false },
    activeTenantId: { value: "t1" },
    tenantOptions: { value: [] },
    loadingTenants: { value: false },
    loadTenants: vi.fn()
  }
}));

vi.mock("services/api", () => ({ customerApi, getApiErrorMessage: (e) => String(e) }));
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ success: vi.fn(), error: vi.fn() }) }));
vi.mock("composables/useTenantOptions", () => ({ useTenantOptions: () => tenantState }));

import CustomerFormDrawer from "modules/customer/components/CustomerFormDrawer.vue";

// AppFormDrawer stub: exposes clearDraft, re-emits submit, renders the body + footer-actions slots.
const clearDraft = vi.fn();
const AppFormDrawerStub = {
  emits: ["submit", "cancel", "restore-draft"],
  template: "<div class='app-form-drawer'><slot /><slot name='footer-actions' /><button class='do-submit' @click='$emit(\"submit\", { clearDraft })' /></div>",
  setup () { return { clearDraft }; }
};

const stubs = {
  AppFormDrawer: AppFormDrawerStub,
  AppSelect: true,
  // q-form always validates true so submit/draft logic runs.
  QForm: { template: "<form><slot /></form>", methods: { validate: () => Promise.resolve(true) } },
  AppTextField: { props: ["modelValue", "label"], template: "<input class='app-text-field' :data-label='label' />" },
  QSeparator: true,
  QBtn: { props: ["label"], emits: ["click"], template: "<button type='button' class='q-btn' :data-label='label' @click='$emit(\"click\")'><slot /></button>" },
  QDialog: { props: ["modelValue"], template: "<div v-if='modelValue' class='q-dialog'><slot /></div>" },
  QCard: { template: "<div><slot /></div>" },
  QCardSection: { template: "<div><slot /></div>" },
  QCardActions: { template: "<div><slot /></div>" },
  QList: { template: "<div><slot /></div>" },
  QItem: { template: "<div><slot /></div>" },
  QItemSection: { template: "<div><slot /></div>" },
  QItemLabel: { template: "<div><slot /></div>" },
  QIcon: true
};

const fillValid = (wrapper) => {
  // Bypass field validation by writing a complete form straight onto the component instance.
  Object.assign(wrapper.vm.form, {
    legalName: "Acme Inc",
    companyName: "Acme",
    emailAddress: "a@b.com",
    country: "United States",
    addressLine1: "1 St"
  });
};

const mountDrawer = () => mount(CustomerFormDrawer, {
  props: { modelValue: true },
  global: { stubs }
});

beforeEach(() => {
  vi.clearAllMocks();
  tenantState.canChooseTenant.value = false;
});

describe("CustomerFormDrawer", () => {
  it("Save as Draft calls customerApi.create and does NOT submit", async () => {
    customerApi.create.mockResolvedValue({ customerId: "c1" });
    const wrapper = mountDrawer();
    fillValid(wrapper);

    const draftBtn = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Save as Draft");
    await draftBtn.trigger("click");
    await flushPromises();

    expect(customerApi.create).toHaveBeenCalledTimes(1);
    expect(customerApi.submit).not.toHaveBeenCalled();
    expect(wrapper.emitted("saved")).toBeTruthy();
  });

  it("Submit for Approval creates then submits, finishing when no duplicates", async () => {
    customerApi.create.mockResolvedValue({ customerId: "c1" });
    customerApi.submit.mockResolvedValue({ submitted: true, status: "Submitted", duplicates: [] });
    const wrapper = mountDrawer();
    fillValid(wrapper);

    await wrapper.find(".do-submit").trigger("click");
    await flushPromises();

    expect(customerApi.create).toHaveBeenCalledTimes(1);
    expect(customerApi.submit).toHaveBeenCalledWith("c1", false);
    expect(wrapper.emitted("saved")).toBeTruthy();
  });

  it("shows the duplicate dialog when submit returns submitted === false", async () => {
    customerApi.create.mockResolvedValue({ customerId: "c1" });
    customerApi.submit.mockResolvedValue({
      submitted: false,
      duplicates: [{ id: "d1", companyName: "Acme", matchedFields: ["companyName"], customerRequestNumber: "CR-9" }]
    });
    const wrapper = mountDrawer();
    fillValid(wrapper);

    await wrapper.find(".do-submit").trigger("click");
    await flushPromises();

    expect(wrapper.find(".q-dialog").exists()).toBe(true);
    expect(wrapper.text()).toContain("Acme");
    expect(wrapper.emitted("saved")).toBeFalsy();
  });

  it("Proceed in the duplicate dialog re-submits with duplicateAcknowledged true", async () => {
    customerApi.create.mockResolvedValue({ customerId: "c1" });
    customerApi.submit
      .mockResolvedValueOnce({ submitted: false, duplicates: [{ id: "d1", companyName: "Acme", matchedFields: ["companyName"], customerRequestNumber: "CR-9" }] })
      .mockResolvedValueOnce({ submitted: true, status: "Submitted", duplicates: [] });
    const wrapper = mountDrawer();
    fillValid(wrapper);

    await wrapper.find(".do-submit").trigger("click");
    await flushPromises();

    const proceed = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Proceed");
    await proceed.trigger("click");
    await flushPromises();

    expect(customerApi.submit).toHaveBeenNthCalledWith(2, "c1", true);
    expect(wrapper.emitted("saved")).toBeTruthy();
  });
});
