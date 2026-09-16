"use client";
import { useEffect, useMemo, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { getAnalyticsOverview, getSalesTrend, getStoreComparison } from "./api";
import type { AnalyticsOverview, SalesTrend, StoreComparison } from "./types";

export function AnalyticsWorkspace() {
  const scope = useOperationsScope();
  const [days, setDays] = useState(30);
  const [overview, setOverview] = useState<AnalyticsOverview | null>(null);
  const [trend, setTrend] = useState<SalesTrend | null>(null);
  const [stores, setStores] = useState<StoreComparison | null>(null);
  const [error, setError] = useState<string | null>(null);
  const range = useMemo(() => {
    const to = new Date();
    const from = new Date(to.getTime() - days * 86_400_000);
    return { from: from.toISOString(), to: to.toISOString() };
  }, [days]);

  useEffect(() => {
    if (!scope.organizationId || !scope.branchId) return;
    const controller = new AbortController();
    Promise.all([
      getAnalyticsOverview(scope.organizationId, scope.branchId, range.from, range.to, controller.signal),
      getSalesTrend(scope.organizationId, scope.branchId, range.from, range.to, controller.signal),
    ]).then(([nextOverview, nextTrend]) => {
      if (controller.signal.aborted) return;
      setOverview(nextOverview); setTrend(nextTrend); setError(null);
    }).catch(() => { if (!controller.signal.aborted) setError("Analytics could not be loaded."); });

    getStoreComparison(scope.organizationId, range.from, range.to, controller.signal)
      .then(result => { if (!controller.signal.aborted) setStores(result); })
      .catch(() => { if (!controller.signal.aborted) setStores(null); });
    return () => controller.abort();
  }, [scope.organizationId, scope.branchId, range.from, range.to]);

  const money = (value: number, currency: string | null) =>
    currency ? `${value.toFixed(2)} ${currency}` : value.toFixed(2);
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">ANALYTICS</span><h1>Business analytics</h1>
      <p>Revenue, sales activity, stock health and store comparison from transactional ledgers.</p></div>
      <label className="metric-card">Window<select value={days} onChange={event => setDays(Number(event.target.value))}>
        <option value={1}>24 hours</option><option value={7}>7 days</option><option value={30}>30 days</option><option value={90}>90 days</option>
      </select></label></div>
    <OperationsScopeSelector scope={scope} />
    {error ? <p className="error-banner">{error}</p> : null}
    {overview ? <>
      <div className="metric-grid">
        <article><strong>{money(overview.netRevenue, overview.currency)}</strong><span>Net revenue</span></article>
        <article><strong>{overview.completedSales}</strong><span>Completed sales</span></article>
        <article><strong>{money(overview.averageTicket, overview.currency)}</strong><span>Average ticket</span></article>
        <article><strong>{overview.completedReturns}</strong><span>Returns · {money(overview.refunds, overview.currency)}</span></article>
        <article><strong>{overview.distinctProductsSold}</strong><span>Distinct products sold</span></article>
        <article><strong>{overview.positiveStockProducts}</strong><span>Products with positive stock</span></article>
        <article><strong>{overview.zeroStockProducts}</strong><span>Products at zero stock</span></article>
        <article><strong>{overview.negativeStockProducts}</strong><span>Products with negative stock</span></article>
      </div>
      <h2>Daily sales trend</h2>
      <div className="table-shell"><table><thead><tr><th>Day</th><th>Sales</th><th>Gross</th><th>Refunds</th><th>Net</th></tr></thead><tbody>
        {(trend?.points ?? []).map(point => <tr key={point.bucketStart}><td>{new Date(point.bucketStart).toLocaleDateString()}</td>
          <td>{point.completedSales}</td><td>{money(point.grossSales, point.currency)}</td><td>{money(point.refunds, point.currency)}</td>
          <td>{money(point.netRevenue, point.currency)}</td></tr>)}
      </tbody></table></div>
    </> : <p className="muted">Choose a branch to load analytics.</p>}
    <h2>Store comparison</h2>
    {stores ? <div className="table-shell"><table><thead><tr><th>Branch</th><th>Sales</th><th>Gross</th><th>Refunds</th><th>Net</th><th>Average ticket</th></tr></thead><tbody>
      {stores.items.map(item => <tr key={item.branchId}><td>{item.branchId}</td><td>{item.completedSales}</td>
        <td>{money(item.grossSales, item.currency)}</td><td>{money(item.refunds, item.currency)}</td>
        <td>{money(item.netRevenue, item.currency)}</td><td>{money(item.averageTicket, item.currency)}</td></tr>)}
    </tbody></table></div> : <p className="muted">Store comparison requires organization-wide reports permission.</p>}
  </section>;
}
