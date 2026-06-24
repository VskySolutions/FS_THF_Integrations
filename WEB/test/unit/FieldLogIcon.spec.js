import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";

// FieldModifiedLogDrawer (a child) pulls services/api + composables at module load; stub those.
vi.mock("services/api", () => ({ ufModifiedLogApi: { history: vi.fn() }, getApiErrorMessage: (e) => String(e) }));
vi.mock("composables/useNotify", () => ({ useNotify: () => ({ error: vi.fn() }) }));
vi.mock("composables/useDateFormat", () => ({ useDateFormat: () => ({ formatDateTime: (v) => v }) }));

import FieldLogIcon from "components/universal/FieldLogIcon.vue";

const stubs = {
  QBtn: { template: "<button class='q-btn'><slot /></button>" },
  QBadge: { props: ["label"], template: "<span class='q-badge'>{{ label }}<slot /></span>" },
  QTooltip: true,
  QIcon: true,
  FieldModifiedLogDrawer: true
};

const factory = (count) => mount(FieldLogIcon, {
  props: { entityType: 1, entityId: "e1", fieldName: "CreditLimit", fieldLabel: "Credit Limit", count },
  global: { stubs }
});

describe("FieldLogIcon", () => {
  it("renders nothing when there are no logged changes", () => {
    expect(factory(0).find(".uf-field-log-icon").exists()).toBe(false);
  });

  it("renders the icon (no count badge) for a small number of changes", () => {
    const wrapper = factory(3);
    expect(wrapper.find(".uf-field-log-icon").exists()).toBe(true);
    expect(wrapper.find(".q-badge").exists()).toBe(false);
  });

  it("shows a count badge when changes exceed five", () => {
    const wrapper = factory(6);
    expect(wrapper.find(".q-badge").exists()).toBe(true);
    expect(wrapper.find(".q-badge").text()).toBe("6");
  });
});
