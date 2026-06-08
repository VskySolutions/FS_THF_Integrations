import { beforeEach, describe, it, expect, vi } from "vitest";

vi.mock("quasar", () => {
  const jar = new Map();
  return {
    Cookies: {
      get: (k) => (jar.has(k) ? jar.get(k) : null),
      set: (k, v) => jar.set(k, v),
      remove: (k) => jar.delete(k),
      __jar: jar
    }
  };
});

import { usePreferences } from "composables/usePreferences";
import { Cookies } from "quasar";

beforeEach(() => Cookies.__jar.clear());

describe("usePreferences", () => {
  it("reads defaults, writes, merges, removes and clears", () => {
    const prefs = usePreferences("tenants");

    expect(prefs.get("pageSize", 20)).toBe(20);

    prefs.set("pageSize", 50);
    expect(prefs.get("pageSize")).toBe(50);

    prefs.merge({ sortBy: "name", descending: true });
    expect(prefs.get("sortBy")).toBe("name");
    expect(prefs.get("descending")).toBe(true);
    expect(prefs.get("pageSize")).toBe(50);

    prefs.remove("pageSize");
    expect(prefs.get("pageSize", 20)).toBe(20);

    prefs.clear();
    expect(prefs.all()).toEqual({});
  });

  it("isolates preferences by page key", () => {
    const a = usePreferences("users");
    const b = usePreferences("jobs");
    a.set("pageSize", 100);
    expect(b.get("pageSize", 20)).toBe(20);
  });
});
