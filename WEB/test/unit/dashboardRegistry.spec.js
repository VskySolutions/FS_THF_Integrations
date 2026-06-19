import { describe, it, expect } from "vitest";
import { WIDGETS, defaultLayoutForRole, widgetsForRole, WIDGETS_BY_KEY } from "modules/dashboard/widgets/registry";

describe("dashboard widget registry", () => {
  it("has the expected widget counts per role", () => {
    const byRole = (r) => WIDGETS.filter((w) => w.role === r).length;
    expect(byRole("common")).toBe(7);
    expect(byRole("tenantAdmin")).toBe(8);
    expect(byRole("superAdmin")).toBe(11);
    expect(WIDGETS.length).toBe(26);
  });

  it("every entry has the required shape and a lazy component", () => {
    const roles = ["common", "tenantAdmin", "superAdmin"];
    const categories = ["jobs", "health", "customers", "users", "platform"];
    for (const w of WIDGETS) {
      expect(typeof w.key).toBe("string");
      expect(typeof w.title).toBe("string");
      expect(typeof w.description).toBe("string");
      expect(roles).toContain(w.role);
      expect(categories).toContain(w.category);
      expect(typeof w.component).toBe("function");
    }
  });

  it("widget keys are unique and indexed by WIDGETS_BY_KEY", () => {
    const keys = WIDGETS.map((w) => w.key);
    expect(new Set(keys).size).toBe(keys.length);
    expect(Object.keys(WIDGETS_BY_KEY).sort()).toEqual([...keys].sort());
  });

  it("defaultLayoutForRole is cumulative across roles", () => {
    expect(defaultLayoutForRole("common")).toHaveLength(7);
    expect(defaultLayoutForRole("tenantAdmin")).toHaveLength(15);
    expect(defaultLayoutForRole("superAdmin")).toHaveLength(26);
    // tenantAdmin layout starts with the common keys.
    expect(defaultLayoutForRole("tenantAdmin").slice(0, 7)).toEqual(defaultLayoutForRole("common"));
  });

  it("widgetsForRole respects the visibility tiers", () => {
    expect(widgetsForRole("common").every((w) => w.role === "common")).toBe(true);
    expect(widgetsForRole("tenantAdmin").length).toBe(15);
    expect(widgetsForRole("superAdmin").length).toBe(26);
  });
});
