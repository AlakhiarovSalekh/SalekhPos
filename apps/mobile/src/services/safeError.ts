import {
  ApiHttpError,
  ApiNetworkError,
  ApiProblemError,
  ApiRequestAbortedError,
  ApiResponseParseError,
  ApiResponseTooLargeError,
} from "@salekhpos/packages-api-client";
import { ContractParseError } from "@/api/contracts";

export type SafeErrorCode = "cancelled" | "offline" | "unauthenticated" | "forbidden" | "not_found" | "conflict" | "invalid" | "unsafe_response" | "unavailable";
export type SafeAppError = Readonly<{ code: SafeErrorCode; retryable: boolean }>;

export function mapSafeError(error: unknown): SafeAppError {
  if (error instanceof ApiRequestAbortedError) return { code: "cancelled", retryable: false };
  if (error instanceof ApiNetworkError) return { code: "offline", retryable: true };
  if (error instanceof ContractParseError || error instanceof ApiResponseParseError || error instanceof ApiResponseTooLargeError) return { code: "unsafe_response", retryable: true };
  if (error instanceof ApiProblemError || error instanceof ApiHttpError) {
    if (error.status === 401) return { code: "unauthenticated", retryable: false };
    if (error.status === 403) return { code: "forbidden", retryable: false };
    if (error.status === 404) return { code: "not_found", retryable: false };
    if (error.status === 409) return { code: "conflict", retryable: false };
    if (error.status === 400 || error.status === 422) return { code: "invalid", retryable: false };
    return { code: "unavailable", retryable: error.status >= 500 || error.status === 429 };
  }
  return { code: "unavailable", retryable: false };
}
