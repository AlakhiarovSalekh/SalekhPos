import { describe, expect, it } from "vitest";
import { ApiHttpError, ApiNetworkError, ApiProblemError, ApiRequestAbortedError, ApiResponseTooLargeError } from "@salekhpos/packages-api-client";
import { ContractParseError } from "../../src/api/contracts";
import { mapSafeError } from "../../src/services/safeError";

describe("safe error mapping", () => {
  it.each([
    [new ApiRequestAbortedError(), "cancelled", false],
    [new ApiNetworkError(), "offline", true],
    [new ApiHttpError(401), "unauthenticated", false],
    [new ApiProblemError(403, "secret-internal-code", "hidden"), "forbidden", false],
    [new ApiHttpError(404), "not_found", false],
    [new ApiHttpError(409), "conflict", false],
    [new ApiHttpError(503), "unavailable", true],
    [new ApiResponseTooLargeError(200, 10), "unsafe_response", true],
    [new ContractParseError("token"), "unsafe_response", true],
  ] as const)("maps %o without exposing raw details", (error, code, retryable) => {
    expect(mapSafeError(error)).toEqual({ code, retryable });
  });

  it("maps unknown exceptions to a non-retryable generic failure", () => {
    expect(mapSafeError(new Error("database password"))).toEqual({ code: "unavailable", retryable: false });
  });
});
