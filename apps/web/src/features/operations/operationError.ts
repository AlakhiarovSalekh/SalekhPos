import { ApiError } from "@/features/sales/api";

export function operationErrorMessage(error: unknown): string {
  if (error instanceof DOMException && error.name === "AbortError") return "";
  if (error instanceof TypeError) return error.message;
  if (error instanceof ApiError) {
    switch (error.kind) {
      case "authentication": return "Your session ended. Sign in again.";
      case "permission": return "Your role does not allow this operation.";
      case "not-found": return "The selected record is no longer available.";
      case "validation": return "The request was rejected. Review the entered values.";
      case "conflict": return "The operation conflicts with current store state. Refresh and retry.";
      case "temporary": return "The service is temporarily unavailable. Retry with the same operation.";
      case "uncertain": return "The connection ended before the result was known. Retry safely.";
      default: return "The operation could not be completed.";
    }
  }
  return "The operation could not be completed.";
}
