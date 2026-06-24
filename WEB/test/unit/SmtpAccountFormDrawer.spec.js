import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

const { smtpAccountApi } = vi.hoisted(() => ({
  smtpAccountApi: { get: vi.fn(), create: vi.fn(), update: vi.fn() }
}));
vi.mock("services/api", () => ({
  smtpAccountApi,
  getApiErrorMessage: (e) => String(e),
  getApiErrorCode: (e) => e?.code,
  ApiErrorCodes: { DuplicateIdentifier: "DUPLICATE_IDENTIFIER" }
}));

const notify = { success: vi.fn(), error: vi.fn() };
vi.mock("composables/useNotify", () => ({ useNotify: () => notify }));
vi.mock("composables/useTenantOptions", () => ({
  useTenantOptions: () => ({
    canChooseTenant: { value: false },
    activeTenantId: { value: "t1" },
    tenantOptions: { value: [] },
    loadingTenants: { value: false },
    loadTenants: vi.fn()
  })
}));

import SmtpAccountFormDrawer from "modules/smtp/components/SmtpAccountFormDrawer.vue";

const clearDraft = vi.fn();
const stubs = {
  AppFormDrawer: {
    emits: ["submit"],
    template: "<div class='app-form-drawer'><slot /><button class='do-submit' @click=\"$emit('submit', { clearDraft })\" /></div>",
    setup () { return { clearDraft }; }
  },
  AppSelect: true,
  QForm: { template: "<form><slot /></form>", methods: { validate: () => Promise.resolve(true) } },
  AppTextField: {
    props: ["modelValue", "label", "hint", "error", "errorMessage", "type"],
    template: "<input class='app-text-field' :data-label='label' :data-hint='hint' :data-type='type' :data-error-message='errorMessage' />"
  }
};

const fieldByLabel = (wrapper, label) =>
  wrapper.findAll(".app-text-field").find((n) => n.attributes("data-label") === label);

beforeEach(() => {
  vi.clearAllMocks();
});

describe("SmtpAccountFormDrawer", () => {
  it("leaves the password blank and shows the keep-existing hint when editing", async () => {
    smtpAccountApi.get.mockResolvedValue({
      id: "a1", tenantId: "t1", accountName: "Primary", host: "smtp.example.com", port: 587,
      encryptionType: "StartTls", authType: "Plain", username: "user", fromName: "Acme", fromEmail: "noreply@acme.com"
    });
    const wrapper = mount(SmtpAccountFormDrawer, {
      props: { modelValue: true, accountId: "a1" },
      global: { stubs }
    });
    await flushPromises();

    expect(wrapper.vm.form.password).toBe("");
    const passwordField = fieldByLabel(wrapper, "Password");
    expect(passwordField).toBeTruthy();
    expect(passwordField.attributes("data-type")).toBe("password");
    expect(passwordField.attributes("data-hint")).toBe("Leave blank to keep existing password");
  });

  it("shows no keep-existing hint when creating", async () => {
    const wrapper = mount(SmtpAccountFormDrawer, {
      props: { modelValue: true, accountId: null },
      global: { stubs }
    });
    await flushPromises();
    expect(smtpAccountApi.get).not.toHaveBeenCalled();
    expect(fieldByLabel(wrapper, "Password").attributes("data-hint")).toBe("");
  });

  it("shows an inline account-name error on a duplicate name", async () => {
    smtpAccountApi.create.mockRejectedValue({ code: "DUPLICATE_IDENTIFIER" });
    const wrapper = mount(SmtpAccountFormDrawer, {
      props: { modelValue: true, accountId: null },
      global: { stubs }
    });
    await flushPromises();

    Object.assign(wrapper.vm.form, {
      accountName: "Primary", host: "smtp.example.com", port: 587, fromName: "Acme", fromEmail: "noreply@acme.com"
    });

    await wrapper.find(".do-submit").trigger("click");
    await flushPromises();

    expect(smtpAccountApi.create).toHaveBeenCalledTimes(1);
    expect(fieldByLabel(wrapper, "Account Name *").attributes("data-error-message"))
      .toBe("An account with this name already exists.");
    expect(notify.error).not.toHaveBeenCalled();
  });

  it("creates the account and emits saved on success", async () => {
    smtpAccountApi.create.mockResolvedValue({ id: "a9" });
    const wrapper = mount(SmtpAccountFormDrawer, {
      props: { modelValue: true, accountId: null },
      global: { stubs }
    });
    await flushPromises();

    Object.assign(wrapper.vm.form, {
      accountName: "Primary", host: "smtp.example.com", port: 587, fromName: "Acme", fromEmail: "noreply@acme.com"
    });

    await wrapper.find(".do-submit").trigger("click");
    await flushPromises();

    expect(smtpAccountApi.create).toHaveBeenCalledTimes(1);
    expect(notify.success).toHaveBeenCalled();
    expect(wrapper.emitted("saved")).toBeTruthy();
  });
});
