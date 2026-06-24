import { describe, it, expect, vi } from "vitest";

// useEntityMeta only needs the EntityType enum from services/api.
vi.mock("services/api", () => ({
  EntityType: { CustomerRequest: 1, IntegrationJob: 2, Tenant: 3, User: 4, UserGroup: 5 }
}));

import { useEntityMeta } from "composables/uf/useEntityMeta";

describe("useEntityMeta", () => {
  const { labelFor, iconFor, routeFor } = useEntityMeta();

  it("labels known entity types", () => {
    expect(labelFor(1)).toBe("Customer Request");
    expect(labelFor(4)).toBe("User");
  });

  it("resolves the customer detail permalink with the id param", () => {
    expect(routeFor(1, "c1")).toEqual({ name: "customer_detail", params: { id: "c1" } });
  });

  it("falls back gracefully for an unknown type", () => {
    expect(labelFor(999)).toBe("Record");
    expect(iconFor(999)).toBe("o_description");
  });
});
