import { requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseAccountingJournalPage, parseAccountingSummary } from "./parsers";
import type { AccountingJournalPage, AccountingSummary } from "./types";

function windowQuery(from: string, to: string): URLSearchParams {
  const fromDate = new Date(from);
  const toDate = new Date(to);
  if (!Number.isFinite(fromDate.getTime()) || !Number.isFinite(toDate.getTime()) || fromDate >= toDate)
    throw new TypeError("Accounting window is invalid.");
  return new URLSearchParams({ from: fromDate.toISOString(), to: toDate.toISOString() });
}

export function getAccountingSummary(organizationId: string, branchId: string,
  from: string, to: string, signal?: AbortSignal): Promise<AccountingSummary> {
  const org = uuid(organizationId, "organization");
  const branch = uuid(branchId, "branch");
  return requestJson(
    `/bff/api/v1/organizations/${org}/branches/${branch}/accounting/summary?${windowQuery(from, to)}`,
    parseAccountingSummary,
    signal ? { signal } : undefined);
}

export function getAccountingJournal(organizationId: string, branchId: string,
  from: string, to: string, pageSize = 50, cursor?: string | null,
  signal?: AbortSignal): Promise<AccountingJournalPage> {
  if (!Number.isInteger(pageSize) || pageSize < 1 || pageSize > 100)
    throw new TypeError("Accounting page size is invalid.");
  const org = uuid(organizationId, "organization");
  const branch = uuid(branchId, "branch");
  const query = windowQuery(from, to);
  query.set("pageSize", String(pageSize));
  if (cursor) query.set("cursor", cursor);
  return requestJson(
    `/bff/api/v1/organizations/${org}/branches/${branch}/accounting/journal?${query}`,
    parseAccountingJournalPage,
    signal ? { signal } : undefined);
}
