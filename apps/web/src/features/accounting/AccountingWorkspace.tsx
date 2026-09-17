"use client";
import { useEffect, useMemo, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { getAccountingJournal, getAccountingSummary } from "./api";
import type { AccountingJournalItem, AccountingSummary } from "./types";

export function AccountingWorkspace() {
  const scope = useOperationsScope();
  const [days, setDays] = useState(30);
  const [summary, setSummary] = useState<AccountingSummary | null>(null);
  const [journal, setJournal] = useState<readonly AccountingJournalItem[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
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
      getAccountingSummary(scope.organizationId, scope.branchId, range.from, range.to, controller.signal),
      getAccountingJournal(scope.organizationId, scope.branchId, range.from, range.to, 50, null, controller.signal),
    ]).then(([nextSummary, nextJournal]) => {
      if (controller.signal.aborted) return;
      setSummary(nextSummary);
      setJournal(nextJournal.items);
      setNextCursor(nextJournal.nextCursor);
      setError(null);
    }).catch(() => {
      if (!controller.signal.aborted) setError("Accounting data could not be loaded.");
    });
    return () => controller.abort();
  }, [scope.organizationId, scope.branchId, range.from, range.to]);

  async function loadMore() {
    if (!scope.organizationId || !scope.branchId || !nextCursor || loadingMore) return;
    setLoadingMore(true);
    try {
      const page = await getAccountingJournal(scope.organizationId, scope.branchId,
        range.from, range.to, 50, nextCursor);
      setJournal(current => [...current, ...page.items]);
      setNextCursor(page.nextCursor);
    } catch {
      setError("The next accounting journal page could not be loaded.");
    } finally {
      setLoadingMore(false);
    }
  }

  const money = (value: number, currency: string | null) =>
    currency ? `${value.toFixed(2)} ${currency}` : value.toFixed(2);
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">ACCOUNTING</span>
      <h1>Accounting control center</h1>
      <p>Provider-neutral sales, tax, cash, purchase commitments and reconciliation evidence.</p></div>
      <label className="metric-card">Window<select value={days}
        onChange={event => setDays(Number(event.target.value))}>
        <option value={1}>24 hours</option><option value={7}>7 days</option>
        <option value={30}>30 days</option><option value={90}>90 days</option>
      </select></label></div>
    <OperationsScopeSelector scope={scope} />
    {error ? <p className="error-banner">{error}</p> : null}
    {summary ? <div className="metric-grid">
      <article><strong>{money(summary.netReceipts, summary.currency)}</strong><span>Net receipts</span></article>
      <article><strong>{money(summary.salesGross, summary.currency)}</strong><span>Gross sales</span></article>
      <article><strong>{money(summary.salesTax, summary.currency)}</strong><span>Tax captured</span></article>
      <article><strong>{money(summary.refunds, summary.currency)}</strong><span>Refunds / voids</span></article>
      <article><strong>{money(summary.cashIn - summary.cashOut, summary.currency)}</strong><span>Net cash movement</span></article>
      <article><strong>{summary.approvedPurchaseOrders}</strong><span>Approved purchase orders</span></article>
      <article><strong>{money(summary.purchaseCommitments, summary.currency)}</strong><span>Purchase commitments</span></article>
      <article><strong>{money(summary.shiftVariance, summary.currency)}</strong><span>Closed-shift variance</span></article>
    </div> : <p className="muted">Choose a branch to load accounting evidence.</p>}
    <h2>Source journal</h2>
    <div className="table-shell"><table><thead><tr><th>Time</th><th>Kind</th><th>Gross</th>
      <th>Net</th><th>Tax</th><th>Cash effect</th><th>Source</th></tr></thead><tbody>
      {journal.map(item => <tr key={`${item.kind}:${item.sourceId}`}>
        <td>{new Date(item.occurredAt).toLocaleString()}</td><td>{item.kind.replaceAll("_", " ")}</td>
        <td>{money(item.grossAmount, item.currency)}</td>
        <td>{item.netAmount === null ? "—" : money(item.netAmount, item.currency)}</td>
        <td>{item.taxAmount === null ? "—" : money(item.taxAmount, item.currency)}</td>
        <td>{money(item.cashEffect, item.currency)}</td><td><code>{item.sourceId}</code></td>
      </tr>)}
    </tbody></table></div>
    {nextCursor ? <button className="secondary-button" disabled={loadingMore}
      onClick={() => void loadMore()}>{loadingMore ? "Loading…" : "Load more journal entries"}</button> : null}
  </section>;
}
