import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock the axios boot module so importing services/api gives us spy-able http instances. The
// factory is inlined inside vi.mock because the call is hoisted above any top-level variables.
vi.mock("boot/axios", () => {
  const instance = {
    get: vi.fn(() => Promise.resolve({ data: { data: null } })),
    post: vi.fn(() => Promise.resolve({ data: { data: null } })),
    put: vi.fn(() => Promise.resolve({ data: { data: null } })),
    delete: vi.fn(() => Promise.resolve({ data: { data: null } }))
  };
  return { http: instance, http2: instance };
});

import { permissionGroupApi, roleApi } from "services/api";
import { http } from "boot/axios";

describe("permissionGroupApi", () => {
  beforeEach(() => {
    http.get.mockClear();
    http.post.mockClear();
    http.put.mockClear();
    http.delete.mockClear();
  });

  it("list GETs /api/admin/permission-groups with params and returns the envelope", async () => {
    http.get.mockResolvedValueOnce({ data: { data: [{ id: "1" }], meta: { totalRecords: 1 } } });
    const params = { page: 1, limit: 20, search: "x", category: "Jobs" };
    const res = await permissionGroupApi.list(params);
    expect(http.get).toHaveBeenCalledWith("/api/admin/permission-groups", { params });
    expect(res.data).toEqual([{ id: "1" }]);
    expect(res.meta.totalRecords).toBe(1);
  });

  it("get unwraps data", async () => {
    http.get.mockResolvedValueOnce({ data: { data: { id: "g1", name: "Group" } } });
    const res = await permissionGroupApi.get("g1");
    expect(http.get).toHaveBeenCalledWith("/api/admin/permission-groups/g1");
    expect(res).toEqual({ id: "g1", name: "Group" });
  });

  it("create POSTs the payload and unwraps", async () => {
    const payload = { tenantId: "t1", name: "G", permissionKeys: ["jobs.read"] };
    await permissionGroupApi.create(payload);
    expect(http.post).toHaveBeenCalledWith("/api/admin/permission-groups", payload);
  });

  it("update PUTs to the id route", async () => {
    const payload = { name: "G2", permissionKeys: [] };
    await permissionGroupApi.update("g1", payload);
    expect(http.put).toHaveBeenCalledWith("/api/admin/permission-groups/g1", payload);
  });

  it("setStatus PUTs { isActive } to the status route", async () => {
    await permissionGroupApi.setStatus("g1", false);
    expect(http.put).toHaveBeenCalledWith("/api/admin/permission-groups/g1/status", { isActive: false });
  });

  it("remove DELETEs the id route", async () => {
    await permissionGroupApi.remove("g1");
    expect(http.delete).toHaveBeenCalledWith("/api/admin/permission-groups/g1");
  });

  it("templates GETs the templates route", async () => {
    await permissionGroupApi.templates();
    expect(http.get).toHaveBeenCalledWith("/api/admin/permission-groups/templates");
  });

  it("createTemplate POSTs to the templates route", async () => {
    const payload = { name: "T", permissionKeys: [] };
    await permissionGroupApi.createTemplate(payload);
    expect(http.post).toHaveBeenCalledWith("/api/admin/permission-groups/templates", payload);
  });

  it("permissionCatalog GETs /api/admin/permissions", async () => {
    http.get.mockResolvedValueOnce({ data: { data: ["jobs.read"] } });
    const res = await permissionGroupApi.permissionCatalog();
    expect(http.get).toHaveBeenCalledWith("/api/admin/permissions");
    expect(res).toEqual(["jobs.read"]);
  });
});

describe("roleApi composition", () => {
  beforeEach(() => {
    http.get.mockClear();
    http.post.mockClear();
    http.delete.mockClear();
  });

  it("getGroups GETs /api/admin/roles/{id}/groups", async () => {
    await roleApi.getGroups("r1");
    expect(http.get).toHaveBeenCalledWith("/api/admin/roles/r1/groups");
  });

  it("assignGroups POSTs { groupIds }", async () => {
    await roleApi.assignGroups("r1", ["g1", "g2"]);
    expect(http.post).toHaveBeenCalledWith("/api/admin/roles/r1/groups", { groupIds: ["g1", "g2"] });
  });

  it("removeGroup DELETEs the group route", async () => {
    await roleApi.removeGroup("r1", "g1");
    expect(http.delete).toHaveBeenCalledWith("/api/admin/roles/r1/groups/g1");
  });

  it("previewPermissions GETs the preview route", async () => {
    await roleApi.previewPermissions("r1");
    expect(http.get).toHaveBeenCalledWith("/api/admin/roles/r1/permissions/preview");
  });
});
