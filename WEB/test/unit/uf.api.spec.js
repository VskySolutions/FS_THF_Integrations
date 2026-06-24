import { beforeEach, describe, it, expect, vi } from "vitest";

// Stub the two axios instances so we can assert exactly which endpoint/verb each UF api method calls.
vi.mock("boot/axios", () => {
  const make = () => ({
    get: vi.fn(() => Promise.resolve({ data: { data: {}, meta: {} } })),
    post: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    put: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    patch: vi.fn(() => Promise.resolve({ data: { data: {} } })),
    delete: vi.fn(() => Promise.resolve({ data: { data: {} } }))
  });
  return { http: make(), http2: make() };
});

import {
  ufNotesApi, ufTagsApi, ufReminderApi, ufNotificationApi, ufPinApi,
  ufChecklistApi, ufStickyNoteApi, ufDeletedApi, ufModifiedLogApi, EntityType
} from "services/api";
import { http } from "boot/axios";

beforeEach(() => vi.clearAllMocks());

describe("universal features api", () => {
  it("notes list passes entity scope params", async () => {
    await ufNotesApi.list({ entityType: 1, entityId: "e1", page: 1, limit: 20 });
    expect(http.get).toHaveBeenCalledWith("/api/uf/notes", { params: { entityType: 1, entityId: "e1", page: 1, limit: 20 } });
  });

  it("notes create posts the body", async () => {
    await ufNotesApi.create({ entityType: 1, entityId: "e1", body: "hi" });
    expect(http.post).toHaveBeenCalledWith("/api/uf/notes", { entityType: 1, entityId: "e1", body: "hi" });
  });

  it("tags apply posts to entity-tags", async () => {
    await ufTagsApi.apply({ entityType: 1, entityId: "e1", tagId: "t1" });
    expect(http.post).toHaveBeenCalledWith("/api/uf/entity-tags", { entityType: 1, entityId: "e1", tagId: "t1" });
  });

  it("reminder create posts to reminders", async () => {
    await ufReminderApi.create({ entityType: 1, entityId: "e1", dueAtUtc: "2026-01-01T00:00:00Z" });
    expect(http.post).toHaveBeenCalledWith("/api/uf/reminders", { entityType: 1, entityId: "e1", dueAtUtc: "2026-01-01T00:00:00Z" });
  });

  it("notification markRead hits the read endpoint", async () => {
    await ufNotificationApi.markRead("n1");
    expect(http.put).toHaveBeenCalledWith("/api/notifications/n1/read");
  });

  it("pin create posts to pins", async () => {
    await ufPinApi.create({ entityType: EntityType.CustomerRequest, entityId: "e1" });
    expect(http.post).toHaveBeenCalledWith("/api/uf/pins", { entityType: 1, entityId: "e1" });
  });

  it("checklist toggleItem patches the item", async () => {
    await ufChecklistApi.toggleItem("c1", "i1", true);
    expect(http.patch).toHaveBeenCalledWith("/api/uf/checklists/c1/items/i1", { isCompleted: true });
  });

  it("sticky note saveState puts position payload", async () => {
    const payload = { x: 1, y: 2, width: 3, height: 4, isMinimised: false, zIndex: 5 };
    await ufStickyNoteApi.saveState("s1", payload);
    expect(http.put).toHaveBeenCalledWith("/api/uf/sticky-note-states/s1", payload);
  });

  it("hard delete sends the payload in the request body", async () => {
    await ufDeletedApi.hardDelete({ entityType: 1, entityId: "e1", confirmationToken: "CUS-1" });
    expect(http.delete).toHaveBeenCalledWith("/api/uf/hard-delete", { data: { entityType: 1, entityId: "e1", confirmationToken: "CUS-1" } });
  });

  it("modified log icon-counts passes entity scope", async () => {
    await ufModifiedLogApi.iconCounts(1, "e1");
    expect(http.get).toHaveBeenCalledWith("/api/uf/modified-log/icon-counts", { params: { entityType: 1, entityId: "e1" } });
  });
});
