import { beforeEach, describe, it, expect, vi } from "vitest";

// boot/axios is the only real dependency of services/api; stub the two axios instances so we can
// assert exactly which endpoint / verb each customerApi method calls. The factory is inlined inside
// vi.mock because the call is hoisted above any top-level variables.
vi.mock("boot/axios", () => {
  const make = () => ({
    get: vi.fn(() => Promise.resolve({ data: { data: {}, meta: {} } })),
    post: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    put: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    delete: vi.fn(() => Promise.resolve({ data: { data: {} } }))
  });
  return { http: make(), http2: make() };
});

import { customerApi } from "services/api";
import { http } from "boot/axios";

beforeEach(() => { vi.clearAllMocks(); });

describe("customerApi", () => {
  it("list passes the tenant/search/status params and unwraps the envelope", async () => {
    await customerApi.list({ tenantId: "t1", search: "acme", status: "Draft", page: 1, limit: 20 });
    expect(http.get).toHaveBeenCalledWith("/api/customers", { params: { tenantId: "t1", search: "acme", status: "Draft", page: 1, limit: 20 } });
  });

  it("get / update / remove hit the id-scoped endpoints", async () => {
    await customerApi.get("c1");
    await customerApi.update("c1", { legalName: "X" });
    await customerApi.remove("c1");
    expect(http.get).toHaveBeenCalledWith("/api/customers/c1");
    expect(http.put).toHaveBeenCalledWith("/api/customers/c1", { legalName: "X" });
    expect(http.delete).toHaveBeenCalledWith("/api/customers/c1");
  });

  it("submit sends the duplicateAcknowledged flag", async () => {
    await customerApi.submit("c1", true);
    expect(http.post).toHaveBeenCalledWith("/api/customers/c1/submit", { duplicateAcknowledged: true });
  });

  it("approve wraps step2 + duplicateAcknowledged in the body", async () => {
    await customerApi.approve("c1", { taxNumber: "T" }, true);
    expect(http.post).toHaveBeenCalledWith("/api/customers/c1/approve", { step2: { taxNumber: "T" }, duplicateAcknowledged: true });
  });

  it("revert-to-reviewer and return send the expected bodies", async () => {
    await customerApi.revertToReviewer("c1", "needs another look");
    await customerApi.returnForCorrections("c1", "fix it", ["legalName"]);
    expect(http.post).toHaveBeenCalledWith("/api/customers/c1/revert-to-reviewer", { notes: "needs another look" });
    expect(http.post).toHaveBeenCalledWith("/api/customers/c1/return", { notes: "fix it", fields: ["legalName"] });
  });

  it("uploadDocument posts multipart form-data with the file field", async () => {
    const file = new File(["x"], "a.pdf", { type: "application/pdf" });
    await customerApi.uploadDocument("c1", file);
    const call = http.post.mock.calls.find((c) => c[0] === "/api/customers/c1/documents");
    expect(call).toBeTruthy();
    expect(call[1]).toBeInstanceOf(FormData);
    expect(call[1].get("file")).toBe(file);
    expect(call[2]).toEqual({ headers: { "Content-Type": "multipart/form-data" } });
  });

  it("downloadDocument requests a blob response", async () => {
    await customerApi.downloadDocument("c1", "d1");
    expect(http.get).toHaveBeenCalledWith("/api/customers/c1/documents/d1/download", { responseType: "blob" });
  });
});
