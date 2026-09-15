"use client";

import { useEffect, useMemo, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { getOperationalReport } from "./api";
import type { OperationalReport } from "./types";

export function ReportingWorkspace() {
  const scope = useOperationsScope();
  const [report, setReport] = useState<OperationalReport | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [days, setDays] = useState(7);
  const range = useMemo(() => {
    const to = new Date();
    const from = new Date(to.getTime() - days * 86_400_000);
    return { from: from.toISOString(), to: to.toISOString() };
  }, [days]);

  useEffect(() => {
    if (!scope.organizationId || !scope.branchId) return;
    let live = true;
    getOperationalReport(scope.organizationId, scope.branchId, range.from, range.to).then(result => {
      if (!live) return;
      setReport(result); setError(null);
    }).catch(() => { if (live) setError("Report could not be loaded."); });
    return () => { live = false; };
  }, [scope.branchId, scope.organizationId, range.from, range.to]);

  const money = (value: number) => report?.currency ? `${value.toFixed(2)} ${report.currency}` : value.toFixed(2);
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">REPORTING</span><h1>Operational summary</h1>
      <p>Branch performance across sales, returns, purchasing and active shifts.</p></div>
      <label className="metric-card">Window<select value={days} onChange={event => setDays(Number(event.target.value))}>
        <option value={1}>24 hours</option><option value={7}>7 days</option><option value={30}>30 days</option>
      </select></label></div>
    <OperationsScopeSelector scope={scope} />
    {error ? <p className="error-banner">{error}</p> : null}
    {report ? <div className="metric-grid">
      <article><strong>{report.salesCount}</strong><span>Sales · {money(report.salesGross)}</span></article>
      <article><strong>{money(report.netSales)}</strong><span>Net sales</span></article>
      <article><strong>{report.returnCount}</strong><span>Returns · {money(report.returnsTotal)}</span></article>
      <article><strong>{report.purchaseOrderCount}</strong><span>Purchase orders · {money(report.purchaseOrderTotal)}</span></article>
      <article><strong>{report.openShiftCount}</strong><span>Open shifts</span></article>
    </div> : <p className="muted">Choose a branch to load the operational report.</p>}
  </section>;
}
