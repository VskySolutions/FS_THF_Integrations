import { beforeEach, describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";

// usePreferences: drive seenWidgets so we can assert the "New" badge.
const { prefsState } = vi.hoisted(() => ({ prefsState: { seenWidgets: [] } }));
vi.mock("composables/usePreferences", () => ({
  usePreferences: () => ({
    get: (key, fallback) => (key in prefsState ? prefsState[key] : fallback),
    set: (key, val) => { prefsState[key] = val; }
  })
}));

import DashboardCustomisePanel from "modules/dashboard/DashboardCustomisePanel.vue";
import { widgetsForRole } from "modules/dashboard/widgets/registry";

const passthrough = { template: "<div><slot /></div>" };
const stubs = {
  QDrawer: { props: ["modelValue"], template: "<div class='q-drawer'><slot /></div>" },
  QScrollArea: passthrough,
  QList: passthrough,
  QItem: passthrough,
  QItemSection: passthrough,
  QItemLabel: { template: "<div class='item-label'><slot /></div>" },
  QSeparator: true,
  QSpace: true,
  QIcon: true,
  QTooltip: true,
  QBadge: { props: ["label"], template: "<span class='q-badge'>{{ label }}</span>" },
  QToggle: {
    props: ["modelValue"],
    template: "<button class='q-toggle' :data-on='modelValue' @click=\"$emit('update:modelValue', !modelValue)\" />"
  },
  QBtn: {
    props: ["label"],
    template: "<button class='q-btn' :data-label='label'><slot /></button>"
  }
};

const makeLayout = (overrides = {}) => ({
  isHidden: vi.fn(() => false),
  toggleHidden: vi.fn(),
  resetToDefault: vi.fn(),
  ...overrides
});

const mountPanel = (props = {}) =>
  mount(DashboardCustomisePanel, {
    props: { modelValue: true, role: "common", layout: makeLayout(), ...props },
    global: { stubs }
  });

beforeEach(() => {
  vi.clearAllMocks();
  prefsState.seenWidgets = [];
});

describe("DashboardCustomisePanel", () => {
  it("lists every widget in widgetsForRole(role)", () => {
    const role = "tenantAdmin";
    const wrapper = mountPanel({ role });
    const expected = widgetsForRole(role);
    // One toggle per catalogue widget.
    expect(wrapper.findAll(".q-toggle")).toHaveLength(expected.length);
    for (const w of expected) {
      expect(wrapper.text()).toContain(w.title);
    }
  });

  it("toggling a widget off calls layout.toggleHidden with the widget key", async () => {
    const layout = makeLayout();
    const wrapper = mountPanel({ role: "common", layout });
    await wrapper.find(".q-toggle").trigger("click");

    const firstKey = widgetsForRole("common")[0].key;
    expect(layout.toggleHidden).toHaveBeenCalledWith(firstKey);
  });

  it("Reset to Default calls layout.resetToDefault", async () => {
    const layout = makeLayout();
    const wrapper = mountPanel({ layout });
    const reset = wrapper.findAll(".q-btn").find((b) => b.attributes("data-label") === "Reset to Default");
    expect(reset).toBeTruthy();
    await reset.trigger("click");
    expect(layout.resetToDefault).toHaveBeenCalledTimes(1);
  });

  it("shows the New badge for a widget key not in persisted seenWidgets", () => {
    // Mark everything except the first common widget as seen; open closed so markAllSeen doesn't fire.
    const all = widgetsForRole("common").map((w) => w.key);
    const firstKey = all[0];
    prefsState.seenWidgets = all.filter((k) => k !== firstKey);

    const wrapper = mountPanel({ role: "common", modelValue: false });
    const badges = wrapper.findAll(".q-badge").filter((b) => b.text() === "New");
    expect(badges).toHaveLength(1);
  });

  it("clears the New badges by marking all seen when the panel opens", async () => {
    prefsState.seenWidgets = [];
    // modelValue: true triggers the immediate watcher -> markAllSeen.
    const wrapper = mountPanel({ role: "common", modelValue: true });
    const badges = wrapper.findAll(".q-badge").filter((b) => b.text() === "New");
    expect(badges).toHaveLength(0);
    expect(prefsState.seenWidgets).toEqual(expect.arrayContaining(widgetsForRole("common").map((w) => w.key)));
  });
});
