import { describe, it, expect, vi } from "vitest";

vi.mock("quasar", () => ({ LocalStorage: { getItem: () => null } }));

import { getApiErrorMessage, getApiErrorCode, ApiErrorCodes } from "services/api";

describe("api helpers", () => {
  it("mirrors the backend error codes", () => {
    expect(ApiErrorCodes.ValidationFailed).toBe("VALIDATION_FAILED");
    expect(ApiErrorCodes.ActiveJobsExist).toBe("ACTIVE_JOBS_EXIST");
    expect(ApiErrorCodes.CredentialsNotConfigured).toBe("CREDENTIALS_NOT_CONFIGURED");
  });

  it("getApiErrorMessage prefers error.details, then message, then fallback", () => {
    expect(getApiErrorMessage({ response: { data: { error: { details: "D" }, message: "M" } } })).toBe("D");
    expect(getApiErrorMessage({ response: { data: { message: "M" } } })).toBe("M");
    expect(getApiErrorMessage({ message: "axios" })).toBe("axios");
    expect(getApiErrorMessage({}, "fallback")).toBe("fallback");
  });

  it("getApiErrorCode returns the code or null", () => {
    expect(getApiErrorCode({ response: { data: { error: { code: "X" } } } })).toBe("X");
    expect(getApiErrorCode({})).toBeNull();
  });
});
