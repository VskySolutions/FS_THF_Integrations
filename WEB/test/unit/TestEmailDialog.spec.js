import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";

const { smtpAccountApi } = vi.hoisted(() => ({ smtpAccountApi: { test: vi.fn() } }));
vi.mock("services/api", () => ({ smtpAccountApi, getApiErrorMessage: (e) => String(e) }));

const notify = { success: vi.fn(), error: vi.fn() };
vi.mock("composables/useNotify", () => ({ useNotify: () => notify }));
vi.mock("composables/useDateFormat", () => ({ useDateFormat: () => ({ formatDateTime: (v) => v || "—" }) }));
vi.mock("composables/useSmtpOptions", () => ({
  useSmtpOptions: () => ({
    errorCategoryLabel: (v) => ({ AuthenticationFailure: "Authentication Failed" }[v] || "Send Failed")
  })
}));

import TestEmailDialog from "modules/smtp/components/TestEmailDialog.vue";

const stubs = {
  QDialog: { props: ["modelValue"], template: "<div v-if='modelValue' class='q-dialog'><slot /></div>" },
  QCard: { template: "<div><slot /></div>" },
  QCardSection: { template: "<div><slot /></div>" },
  QCardActions: { template: "<div><slot /></div>" },
  QSeparator: true,
  QSpace: true,
  QIcon: true,
  QForm: { template: "<form><slot /></form>", methods: { validate: () => Promise.resolve(true) } },
  AppTextField: { props: ["modelValue"], template: "<input class='app-text-field' />" },
  QBanner: { template: "<div class='q-banner'><slot name='avatar' /><slot /></div>" },
  QExpansionItem: { props: ["label"], template: "<div class='q-expansion-item'><slot /></div>" },
  QBtn: {
    props: ["label", "loading", "disable"],
    template: "<button class='q-btn' :data-label='label' :data-loading='loading' :data-disable='disable' @click=\"$emit('click')\"><slot /></button>"
  },
  QSpinner: true
};

const account = { id: "a1", accountName: "Primary", host: "smtp.example.com", port: 587 };

const mountDialog = () => mount(TestEmailDialog, {
  props: { modelValue: true, account, tenantId: null },
  global: { stubs }
});

const sendButton = (wrapper) => wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Send Test");

beforeEach(() => {
  vi.clearAllMocks();
});

describe("TestEmailDialog", () => {
  it("disables the Send button while the request is in flight", async () => {
    let resolveTest;
    smtpAccountApi.test.mockReturnValue(new Promise((r) => { resolveTest = r; }));
    const wrapper = mountDialog();
    wrapper.vm.recipient = "to@example.com";

    await sendButton(wrapper).trigger("click");
    await nextTick();

    expect(sendButton(wrapper).attributes("data-loading")).toBe("true");
    expect(sendButton(wrapper).attributes("data-disable")).toBe("true");

    resolveTest({ success: true, sentAtUtc: "2026-06-24T10:00:00Z", serverResponse: "250 OK" });
    await flushPromises();
  });

  it("shows a positive banner with the server response on success", async () => {
    smtpAccountApi.test.mockResolvedValue({ success: true, sentAtUtc: "2026-06-24T10:00:00Z", serverResponse: "250 OK" });
    const wrapper = mountDialog();
    wrapper.vm.recipient = "to@example.com";

    await sendButton(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.find(".bg-positive").exists()).toBe(true);
    expect(wrapper.text()).toContain("250 OK");
  });

  it("shows a negative banner with the error category and raw detail on failure", async () => {
    smtpAccountApi.test.mockResolvedValue({
      success: false, errorCategory: "AuthenticationFailure", errorDetail: "535 5.7.8 Authentication failed"
    });
    const wrapper = mountDialog();
    wrapper.vm.recipient = "to@example.com";

    await sendButton(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.find(".bg-negative").exists()).toBe(true);
    expect(wrapper.text()).toContain("Authentication Failed");
    expect(wrapper.find(".q-expansion-item").text()).toContain("535 5.7.8 Authentication failed");
  });

  it("does not auto-close the dialog after a result", async () => {
    smtpAccountApi.test.mockResolvedValue({ success: true, sentAtUtc: "2026-06-24T10:00:00Z", serverResponse: "250 OK" });
    const wrapper = mountDialog();
    wrapper.vm.recipient = "to@example.com";

    await sendButton(wrapper).trigger("click");
    await flushPromises();

    // The component never emits a close after a result — the user dismisses manually.
    const closeEvents = (wrapper.emitted("update:modelValue") || []).filter((e) => e[0] === false);
    expect(closeEvents).toHaveLength(0);
  });
});
