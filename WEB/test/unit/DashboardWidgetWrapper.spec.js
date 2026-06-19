import { beforeEach, describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";

const { hasFn } = vi.hoisted(() => ({ hasFn: vi.fn() }));
vi.mock("composables/usePermissions", () => ({ usePermissions: () => ({ has: hasFn }) }));

import DashboardWidgetWrapper from "components/dashboard/DashboardWidgetWrapper.vue";

// Lightweight Quasar stubs that preserve the structure the template relies on (slots / props).
const passthrough = { template: "<div><slot /></div>" };
const stubs = {
  QCard: passthrough,
  QCardSection: passthrough,
  QSeparator: true,
  QSpace: true,
  QIcon: true,
  QTooltip: true,
  QSkeleton: { template: "<div class='q-skeleton' />" },
  QChip: { props: ["label"], template: "<span class='q-chip'>{{ label }}<slot /></span>" },
  QBanner: { template: "<div class='q-banner'><slot /><slot name='action' /></div>" },
  QBtn: {
    props: ["label", "icon", "to"],
    template: "<button class='q-btn' :data-label='label' :data-icon='icon'><slot /></button>"
  }
};

const mountWrapper = (props = {}, slots = {}) =>
  mount(DashboardWidgetWrapper, { props: { title: "W", ...props }, slots, global: { stubs } });

beforeEach(() => {
  vi.clearAllMocks();
  hasFn.mockReturnValue(true);
});

describe("DashboardWidgetWrapper", () => {
  it("shows the skeleton while loading", () => {
    const wrapper = mountWrapper({ loading: true }, { default: "<div class='content'>data</div>" });
    expect(wrapper.find(".q-skeleton").exists()).toBe(true);
    expect(wrapper.find(".content").exists()).toBe(false);
  });

  it("shows the error banner with a Retry button and emits retry on click", async () => {
    const wrapper = mountWrapper({ error: "Failed to load" });
    const banner = wrapper.find(".q-banner");
    expect(banner.exists()).toBe(true);
    expect(banner.text()).toContain("Failed to load");

    const retry = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Retry");
    expect(retry).toBeTruthy();
    await retry.trigger("click");
    expect(wrapper.emitted("retry")).toHaveLength(1);
  });

  it("renders the default slot when not loading and no error", () => {
    const wrapper = mountWrapper({}, { default: "<div class='content'>data</div>" });
    expect(wrapper.find(".content").exists()).toBe(true);
    expect(wrapper.find(".q-skeleton").exists()).toBe(false);
    expect(wrapper.find(".q-banner").exists()).toBe(false);
  });

  it("collapse toggle emits update:collapsed with the negated value", async () => {
    const wrapper = mountWrapper({ collapsed: false });
    const collapseBtn = wrapper.findAll(".q-btn").find((b) => b.attributes("data-icon") === "o_expand_less");
    expect(collapseBtn).toBeTruthy();
    await collapseBtn.trigger("click");
    expect(wrapper.emitted("update:collapsed")[0]).toEqual([true]);
  });

  it("shows the compact alert indicator when collapsed AND alert", () => {
    const wrapper = mountWrapper({ collapsed: true, alert: true });
    const chip = wrapper.find(".q-chip");
    expect(chip.exists()).toBe(true);
    expect(chip.text()).toContain("Attention");
  });

  it("does not show the alert chip when collapsed but no alert", () => {
    const wrapper = mountWrapper({ collapsed: true, alert: false });
    expect(wrapper.find(".q-chip").exists()).toBe(false);
  });

  it("hides the nav action button when actionPermission is set and the user lacks it", () => {
    hasFn.mockReturnValue(false);
    const wrapper = mountWrapper({
      actionLabel: "View Jobs",
      actionRoute: "/jobs",
      actionPermission: "jobs.read"
    });
    const action = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "View Jobs");
    expect(action).toBeUndefined();
  });

  it("shows the nav action button when the user holds the required permission", () => {
    hasFn.mockReturnValue(true);
    const wrapper = mountWrapper({
      actionLabel: "View Jobs",
      actionRoute: "/jobs",
      actionPermission: "jobs.read"
    });
    const action = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "View Jobs");
    expect(action).toBeTruthy();
    expect(hasFn).toHaveBeenCalledWith("jobs.read");
  });

  it("shows the nav action button (ungated) when no actionPermission is required", () => {
    const wrapper = mountWrapper({ actionLabel: "View Jobs", actionRoute: "/jobs" });
    const action = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "View Jobs");
    expect(action).toBeTruthy();
  });
});
