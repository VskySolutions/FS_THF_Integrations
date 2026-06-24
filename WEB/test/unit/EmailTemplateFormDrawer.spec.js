import { beforeEach, describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";

const { emailTemplateApi } = vi.hoisted(() => ({
  emailTemplateApi: { save: vi.fn(), preview: vi.fn() }
}));
vi.mock("services/api", () => ({ emailTemplateApi, getApiErrorMessage: (e) => String(e) }));

const notify = { success: vi.fn(), error: vi.fn() };
vi.mock("composables/useNotify", () => ({ useNotify: () => notify }));

import EmailTemplateFormDrawer from "modules/email-template/components/EmailTemplateFormDrawer.vue";

const stubs = {
  AppFormDrawer: {
    emits: ["submit"],
    template: "<div class='app-form-drawer'><slot /><button class='do-submit' @click=\"$emit('submit', {})\" /></div>"
  },
  QForm: { template: "<form><slot /></form>", methods: { validate: () => Promise.resolve(true) } },
  AppTextField: { props: ["modelValue", "label", "type"], template: "<input class='app-text-field' :data-label='label' />" },
  QChip: { emits: ["click"], template: "<button class='chip' @click=\"$emit('click')\"><slot /></button>" },
  QBtn: { props: ["label"], emits: ["click"], template: "<button class='q-btn' :data-label='label' @click=\"$emit('click')\"><slot /></button>" },
  EmailTemplatePreviewDialog: { props: ["modelValue"], template: "<div class='preview-dialog' :data-open='modelValue' />" }
};

const template = {
  key: "UserInvitation",
  displayName: "User Invitation",
  description: "Invite a user",
  subject: "Welcome",
  body: "Hello",
  placeholders: ["FullName", "Email"]
};

const mountDrawer = (scopeParams = { global: true }) =>
  mount(EmailTemplateFormDrawer, { props: { modelValue: true, template, scopeParams }, global: { stubs } });

beforeEach(() => vi.clearAllMocks());

describe("EmailTemplateFormDrawer", () => {
  it("initialises the form from the template descriptor", () => {
    const wrapper = mountDrawer();
    expect(wrapper.vm.form.subject).toBe("Welcome");
    expect(wrapper.vm.form.body).toBe("Hello");
  });

  it("inserts a placeholder token into the body when a chip is clicked", async () => {
    const wrapper = mountDrawer();
    await wrapper.findAll(".chip")[0].trigger("click"); // FullName
    expect(wrapper.vm.form.body).toBe("Hello{{FullName}}");
  });

  it("saves with the template key, draft and scope params, then emits saved", async () => {
    emailTemplateApi.save.mockResolvedValue({});
    const wrapper = mountDrawer({ tenantId: "t1" });
    await wrapper.find(".do-submit").trigger("click");
    await flushPromises();

    expect(emailTemplateApi.save).toHaveBeenCalledWith("UserInvitation", { subject: "Welcome", body: "Hello" }, { tenantId: "t1" });
    expect(wrapper.emitted("saved")).toBeTruthy();
  });

  it("previews the draft and opens the preview dialog", async () => {
    emailTemplateApi.preview.mockResolvedValue({ subject: "Welcome", body: "<p>Hello</p>" });
    const wrapper = mountDrawer();
    const previewBtn = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Preview");
    await previewBtn.trigger("click");
    await flushPromises();

    expect(emailTemplateApi.preview).toHaveBeenCalledWith("UserInvitation", { subject: "Welcome", body: "Hello" }, { global: true });
    expect(wrapper.find(".preview-dialog").attributes("data-open")).toBe("true");
  });
});
