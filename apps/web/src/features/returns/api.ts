import { exactKeys, object, text, uuid } from "@/lib/boundedJson";
import { ApiError, requestJson } from "@/features/sales/api";
import { parseRefund, parseReturnDetail, parseReturnPage } from "./parsers";
import type { Refund, ReturnDetail, ReturnPage, ReturnRequest } from "./types";

const scope = (organizationId: string, branchId: string) => `/bff/api/v1/organizations/${uuid(organizationId, "organization id")}/branches/${uuid(branchId, "branch id")}`;

export function getReturns(organizationId: string, branchId: string, after?: string, signal?: AbortSignal): Promise<ReturnPage> {
  const query = new URLSearchParams({ pageSize: "20" });
  if (after) query.set("after", uuid(after, "return cursor"));
  return requestJson(`${scope(organizationId, branchId)}/returns?${query}`, parseReturnPage, { signal });
}

export function getReturn(organizationId: string, branchId: string, returnId: string, signal?: AbortSignal): Promise<ReturnDetail> {
  return requestJson(`${scope(organizationId, branchId)}/returns/${uuid(returnId, "return id")}`, parseReturnDetail, { signal });
}

export async function getRefund(organizationId: string, branchId: string, returnId: string, signal?: AbortSignal): Promise<Refund | null> {
  try { return await requestJson(`${scope(organizationId, branchId)}/returns/${uuid(returnId, "return id")}/refund`, parseRefund, { signal }); }
  catch (error) { if (error instanceof ApiError && error.kind === "not-found") return null; throw error; }
}

export async function getAllSaleReturns(organizationId: string, branchId: string, saleId: string, signal?: AbortSignal): Promise<ReturnDetail[]> {
  const summaries = await getSaleReturnSummaries(organizationId, branchId, saleId, signal);
  const details: ReturnDetail[] = [];
  for (let index = 0; index < summaries.length; index += 8) {
    details.push(...await Promise.all(summaries.slice(index, index + 8).map(item => getReturn(organizationId, branchId, item.id, signal))));
  }
  return details;
}

export async function getSaleReturnSummaries(organizationId: string, branchId: string, saleId: string, signal?: AbortSignal): Promise<ReturnPage["items"]> {
  const summaries: ReturnPage["items"] = [];
  let after: string | undefined;
  for (let pageNumber = 0; pageNumber < 10; pageNumber++) {
    const query = new URLSearchParams({ pageSize: "100" });
    if (after) query.set("after", after);
    const page = await requestJson(`${scope(organizationId, branchId)}/sales/${uuid(saleId, "sale id")}/returns?${query}`, parseReturnPage, { signal });
    summaries.push(...page.items);
    if (!page.nextCursor) return summaries;
    if (page.nextCursor === after) throw new ApiError("unexpected", "The server returned a repeated return cursor.", false);
    after = page.nextCursor;
  }
  throw new ApiError("unexpected", "Return history is too large to validate safely in this view.", false);
}

export function validateReturnRequest(request: ReturnRequest, remaining: ReadonlyMap<string, number>): string[] {
  const errors: string[] = [];
  const reason = request.reason.trim();
  if (reason.length < 3 || reason.length > 500 || [...reason].some(char => char < " ")) errors.push("Reason must contain 3 to 500 printable characters.");
  if (request.lines.length < 1 || request.lines.length > 500) errors.push("Select at least one sale line.");
  const seen = new Set<string>();
  for (const line of request.lines) {
    if (seen.has(line.productId)) errors.push("Each product may appear only once.");
    seen.add(line.productId);
    const available = remaining.get(line.productId);
    if (available === undefined || !Number.isFinite(line.quantity) || line.quantity <= 0 || line.quantity > available || Math.round(line.quantity * 1_000_000) / 1_000_000 !== line.quantity) {
      errors.push("Return quantities must be positive, use at most six decimals, and not exceed the remaining sold quantity.");
    }
  }
  return [...new Set(errors)];
}

export async function submitReturn(organizationId: string, branchId: string, request: ReturnRequest,
  operationId: string, csrfToken: string, signal?: AbortSignal): Promise<ReturnDetail> {
  return requestJson(`${scope(organizationId, branchId)}/returns`, parseReturnDetail, {
    method: "POST",
    signal,
    headers: { "Content-Type": "application/json", "Idempotency-Key": uuid(operationId, "operation id"), "X-CSRF-Token": text(csrfToken, "CSRF token", 4096, 16) },
    body: JSON.stringify(request)
  });
}

export function parseSessionForTests(value: unknown): string {
  const x = object(value, "session");
  exactKeys(x, ["configured", "authenticated", "name", "csrfToken"]);
  return text(x.csrfToken, "CSRF token", 4096, 16);
}
