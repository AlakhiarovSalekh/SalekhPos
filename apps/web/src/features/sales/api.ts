import { boundedJson, exactKeys, object, text, uuid } from "@/lib/boundedJson";
import { parseBranchPage, parsePayment, parseSaleDetail, parseSalePage, parseSaleVoid } from "./parsers";
import type { BranchPage, Payment, SaleDetail, SalePage, SaleVoid } from "./types";

export type ApiFailureKind = "authentication" | "permission" | "not-found" | "validation" | "conflict" | "temporary" | "unexpected" | "uncertain";

export class ApiError extends Error {
  constructor(public readonly kind: ApiFailureKind, message: string, public readonly retryable: boolean, public readonly code?: string) {
    super(message);
  }
}

function scopePath(organizationId: string, branchId?: string): string {
  const organization = uuid(organizationId, "organization id");
  return branchId ? `/bff/api/v1/organizations/${organization}/branches/${uuid(branchId, "branch id")}` : `/bff/api/v1/organizations/${organization}`;
}

async function failure(response: Response): Promise<ApiError> {
  let code: string | undefined;
  try {
    const value = object(await boundedJson(response, 16_384), "problem");
    if ("code" in value) code = text(value.code, "error code", 100, 1);
  } catch { /* Never expose an untrusted server body. */ }
  if (response.status === 401) return new ApiError("authentication", "Your session has ended. Sign in again.", false, code);
  if (response.status === 403) return new ApiError("permission", "You do not have permission for this operation.", false, code);
  if (response.status === 404) return new ApiError("not-found", "The requested record is unavailable.", false, code);
  if (response.status === 400 || response.status === 422) return new ApiError("validation", "The request was not accepted. Review the entered values.", false, code);
  if (response.status === 409) return new ApiError("conflict", "The record changed or the operation conflicts with current state.", false, code);
  if (response.status === 429 || response.status >= 500) return new ApiError("temporary", "The service is temporarily unavailable. Retry safely.", true, code);
  return new ApiError("unexpected", "The request could not be completed.", false, code);
}

export async function requestJson<T>(url: string, parser: (value: unknown) => T, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, { cache: "no-store", credentials: "same-origin", ...init });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") throw error;
    throw new ApiError("uncertain", "The connection ended before the result was known. Retry the same operation.", true);
  }
  if (!response.ok) throw await failure(response);
  try { return parser(await boundedJson(response)); }
  catch (error) {
    if (error instanceof ApiError) throw error;
    throw new ApiError("unexpected", "The server returned an invalid response.", false);
  }
}

export function getBranches(organizationId: string, signal?: AbortSignal): Promise<BranchPage> {
  return requestJson(`${scopePath(organizationId)}/branches?pageSize=100`, parseBranchPage, { signal });
}

export async function getAllBranches(organizationId: string, signal?: AbortSignal): Promise<BranchPage["items"]> {
  const items: BranchPage["items"] = [];
  let after: string | undefined;
  for (let pageNumber = 0; pageNumber < 10; pageNumber++) {
    const query = new URLSearchParams({ pageSize: "100" });
    if (after) query.set("after", after);
    const page = await requestJson(`${scopePath(organizationId)}/branches?${query}`, parseBranchPage, { signal });
    items.push(...page.items);
    if (!page.nextCursor) return items;
    if (page.nextCursor === after) throw new ApiError("unexpected", "The server returned a repeated branch cursor.", false);
    after = page.nextCursor;
  }
  throw new ApiError("unexpected", "This organization has too many branches to select safely in one view.", false);
}

export function getSales(organizationId: string, branchId: string, after?: string, signal?: AbortSignal): Promise<SalePage> {
  const query = new URLSearchParams({ pageSize: "12" });
  if (after) query.set("after", uuid(after, "sale cursor"));
  return requestJson(`${scopePath(organizationId, branchId)}/sales?${query}`, parseSalePage, { signal });
}

export function getSale(organizationId: string, branchId: string, saleId: string, signal?: AbortSignal): Promise<SaleDetail> {
  return requestJson(`${scopePath(organizationId, branchId)}/sales/${uuid(saleId, "sale id")}`, parseSaleDetail, { signal });
}

export async function getPayment(organizationId: string, branchId: string, saleId: string, signal?: AbortSignal): Promise<Payment | null> {
  try { return await requestJson(`${scopePath(organizationId, branchId)}/sales/${uuid(saleId, "sale id")}/payment`, parsePayment, { signal }); }
  catch (error) { if (error instanceof ApiError && error.kind === "not-found") return null; throw error; }
}

export async function getSaleVoid(organizationId: string, branchId: string, saleId: string, signal?: AbortSignal): Promise<SaleVoid | null> {
  try { return await requestJson(`${scopePath(organizationId, branchId)}/sales/${uuid(saleId, "sale id")}/void`, parseSaleVoid, { signal }); }
  catch (error) { if (error instanceof ApiError && error.kind === "not-found") return null; throw error; }
}

export async function getCsrfToken(signal?: AbortSignal): Promise<string> {
  return requestJson("/auth/session", value => {
    const x = object(value, "session");
    exactKeys(x, ["configured", "authenticated", "name", "csrfToken"]);
    if (x.configured !== true || x.authenticated !== true) throw new ApiError("authentication", "Your session has ended. Sign in again.", false);
    return text(x.csrfToken, "CSRF token", 4096, 16);
  }, { signal });
}

export function formatMoney(amount: number, currency: string): string {
  try { return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount); }
  catch { return `${amount.toFixed(2)} ${currency}`; }
}

export function formatDate(value: string, timeZone?: string): string {
  try { return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short", timeZone }).format(new Date(value)); }
  catch { return new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)); }
}

export function shortId(value: string): string { return value.slice(0, 8).toUpperCase(); }
