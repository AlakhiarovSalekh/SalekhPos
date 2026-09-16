import { requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseAnalyticsOverview, parseSalesTrend, parseStoreComparison } from "./parsers";
import type { AnalyticsOverview, SalesTrend, StoreComparison } from "./types";

function windowQuery(from: string, to: string): string {
  const fromDate = new Date(from); const toDate = new Date(to);
  if (!Number.isFinite(fromDate.getTime()) || !Number.isFinite(toDate.getTime()) || fromDate >= toDate)
    throw new TypeError("Analytics window is invalid.");
  return new URLSearchParams({ from: fromDate.toISOString(), to: toDate.toISOString() }).toString();
}

export function getAnalyticsOverview(organizationId: string, branchId: string,
  from: string, to: string, signal?: AbortSignal): Promise<AnalyticsOverview> {
  const org = uuid(organizationId, "organization"); const branch = uuid(branchId, "branch");
  return requestJson(`/bff/api/v1/organizations/${org}/branches/${branch}/analytics/overview?${windowQuery(from, to)}`,
    parseAnalyticsOverview, signal ? { signal } : undefined);
}
export function getSalesTrend(organizationId: string, branchId: string,
  from: string, to: string, signal?: AbortSignal): Promise<SalesTrend> {
  const org = uuid(organizationId, "organization"); const branch = uuid(branchId, "branch");
  return requestJson(`/bff/api/v1/organizations/${org}/branches/${branch}/analytics/sales-trend?${windowQuery(from, to)}`,
    parseSalesTrend, signal ? { signal } : undefined);
}

export function getStoreComparison(organizationId: string,
  from: string, to: string, signal?: AbortSignal): Promise<StoreComparison> {
  const org = uuid(organizationId, "organization");
  return requestJson(`/bff/api/v1/organizations/${org}/analytics/stores?${windowQuery(from, to)}`,
    parseStoreComparison, signal ? { signal } : undefined);
}
