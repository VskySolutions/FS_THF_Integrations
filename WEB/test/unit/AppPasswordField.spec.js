import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";

import AppPasswordField from "components/common/AppPasswordField.vue";

const stubs = {
  // Surface the resolved `type` and render the #append slot so the eye toggle is clickable.
  AppTextField: {
    props: ["modelValue", "type", "label", "hint", "placeholder"],
    template: "<div class='atf' :data-type='type'><slot name='append' /></div>"
  },
  QIcon: { emits: ["click"], template: "<i class='eye' @click=\"$emit('click')\"><slot /></i>" },
  QTooltip: true
};

describe("AppPasswordField", () => {
  it("masks by default and toggles visibility when the eye icon is clicked", async () => {
    const wrapper = mount(AppPasswordField, { props: { modelValue: "secret", label: "Password" }, global: { stubs } });

    expect(wrapper.find(".atf").attributes("data-type")).toBe("password");

    await wrapper.find(".eye").trigger("click");
    expect(wrapper.find(".atf").attributes("data-type")).toBe("text");

    await wrapper.find(".eye").trigger("click");
    expect(wrapper.find(".atf").attributes("data-type")).toBe("password");
  });
});
