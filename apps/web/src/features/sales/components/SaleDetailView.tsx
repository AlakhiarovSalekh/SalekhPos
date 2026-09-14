"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";
import { getAllSaleReturns } from "@/features/returns/api";
import type { ReturnDetail } from "@/features/returns/types";
import { ReturnForm } from "@/features/returns/components/ReturnForm";
import { UUID_PATTERN } from "@/lib/boundedJson";
import { ApiError, formatDate, formatMoney, getPayment, getSale, getSaleVoid, shortId } from "../api";
import { deriveSaleStatus } from "../status";
import type { Payment, SaleDetail, SaleVoid } from "../types";
import { ManagerShell } from "./ManagerShell";
import { SaleStatusBadge } from "./SaleStatusBadge";
import { ErrorPanel, LoadingPanel } from "./StatePanel";

type DetailState = { status: "loading" } | { status: "error"; error: ApiError } | { status: "ready"; sale: SaleDetail; payment: Payment | null; returns: ReturnDetail[] | null; saleVoid: SaleVoid | null | undefined };

export function SaleDetailView({ organizationId, branchId, saleId }: { organizationId?: string; branchId?: string; saleId: string }) {
  const valid = Boolean(organizationId && branchId && UUID_PATTERN.test(organizationId) && UUID_PATTERN.test(branchId) && UUID_PATTERN.test(saleId));
  const [state, setState] = useState<DetailState>({ status: "loading" });
  useEffect(() => {
    if (!valid) return;
    const controller = new AbortController();
    (async () => {
      try {
        const sale = await getSale(organizationId!, branchId!, saleId, controller.signal);
        const [paymentResult, returnsResult, voidResult] = await Promise.allSettled([getPayment(organizationId!, branchId!, saleId, controller.signal), getAllSaleReturns(organizationId!, branchId!, saleId, controller.signal), getSaleVoid(organizationId!, branchId!, saleId, controller.signal)]);
        const payment = paymentResult.status === "fulfilled" ? paymentResult.value : null;
        const returns = returnsResult.status === "fulfilled" ? returnsResult.value : returnsResult.reason instanceof ApiError && returnsResult.reason.kind === "permission" ? null : null;
        const saleVoid = voidResult.status === "fulfilled" ? voidResult.value : voidResult.reason instanceof ApiError && voidResult.reason.kind === "permission" ? undefined : undefined;
        if (!controller.signal.aborted) setState({ status: "ready", sale, payment, returns, saleVoid });
      } catch (error) { if (!controller.signal.aborted) setState({ status: "error", error: error instanceof ApiError ? error : new ApiError("unexpected", "Sale details could not be loaded.", false) }); }
    })();
    return () => controller.abort();
  }, [branchId, organizationId, saleId, valid]);

  return <ManagerShell organizationId={organizationId} branchId={branchId}>
    <div className="manager-content">
      <Link className="back-link" href={organizationId && branchId ? `/sales?organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}` : "/sales"}>← Back to sales</Link>
      {!valid && <ErrorPanel error={new ApiError("validation", "A valid organization, branch and sale are required.", false)} />}
      {valid && state.status === "loading" && <LoadingPanel label="Loading sale, payment and reversal evidence…" />}
      {state.status === "error" && <ErrorPanel error={state.error} retry={() => window.location.reload()} />}
      {state.status === "ready" && <SaleDetailContent organizationId={organizationId!} branchId={branchId!} state={state} />}
    </div>
  </ManagerShell>;
}

function SaleDetailContent({ organizationId, branchId, state }: { organizationId: string; branchId: string; state: Extract<DetailState, { status: "ready" }> }) {
  const { sale, payment, returns, saleVoid } = state;
  const status = deriveSaleStatus(sale, returns, saleVoid);
  const remaining = useMemo(() => {
    const values = new Map(sale.lines.map(line => [line.productId, line.quantity]));
    for (const completedReturn of returns ?? []) for (const line of completedReturn.lines) values.set(line.productId, Math.max(0, (values.get(line.productId) ?? 0) - line.quantity));
    return values;
  }, [returns, sale.lines]);
  return <>
    <div className="detail-heading"><div><p className="eyebrow">SALE #{shortId(sale.id)}</p><h1>{formatMoney(sale.grandTotal, sale.currency)}</h1><p>{formatDate(sale.completedAt)}</p></div><SaleStatusBadge status={status} /></div>
    <section className="metric-grid" aria-label="Sale totals"><div><span>Net</span><strong>{formatMoney(sale.netTotal, sale.currency)}</strong></div><div><span>Tax</span><strong>{formatMoney(sale.taxTotal, sale.currency)}</strong></div><div><span>Cash received</span><strong>{formatMoney(sale.cashReceived, sale.currency)}</strong></div><div><span>Change</span><strong>{formatMoney(sale.changeDue, sale.currency)}</strong></div></section>
    <section className="data-card"><div className="card-heading"><div><h2>Line snapshots</h2><p>Applied prices and tax rules are historical sale evidence.</p></div></div><div className="table-scroll"><table><thead><tr><th>Product</th><th>Quantity</th><th>Unit price</th><th>Tax</th><th>Gross</th></tr></thead><tbody>{sale.lines.map(line => <tr key={line.lineNumber}><td><span className="mono">#{shortId(line.productId)}</span><small>Price #{shortId(line.priceId)}</small></td><td>{line.quantity}</td><td>{formatMoney(line.unitAmount, line.currency)}</td><td>{line.taxMode} · {line.taxRate}%<small>{formatMoney(line.taxAmount, line.currency)}</small></td><td><strong>{formatMoney(line.grossAmount, line.currency)}</strong></td></tr>)}</tbody></table></div></section>
    <div className="detail-columns"><section className="data-card"><div className="card-heading"><div><h2>Payment</h2><p>Captured payment evidence</p></div></div>{payment ? <dl className="detail-list"><div><dt>Method</dt><dd>{payment.method}</dd></div><div><dt>Status</dt><dd>{payment.status}</dd></div><div><dt>Amount</dt><dd>{formatMoney(payment.amount, payment.currency)}</dd></div><div><dt>Tendered</dt><dd>{formatMoney(payment.tendered, payment.currency)}</dd></div></dl> : <p className="muted padded">Payment evidence is restricted or unavailable.</p>}</section>
      <section className="data-card"><div className="card-heading"><div><h2>Assignment</h2><p>Durable operating context</p></div></div><dl className="detail-list"><div><dt>Branch</dt><dd className="mono">{shortId(sale.branchId)}</dd></div><div><dt>Register</dt><dd className="mono">{sale.registerId ? shortId(sale.registerId) : "Legacy"}</dd></div><div><dt>Shift</dt><dd className="mono">{sale.shiftId ? shortId(sale.shiftId) : "Legacy"}</dd></div></dl></section></div>
    {saleVoid && <section className="warning-panel"><h2>Sale voided</h2><p>{saleVoid.reason} · {formatDate(saleVoid.voidedAt)}</p></section>}
    {returns && returns.length > 0 && <section className="data-card"><div className="card-heading"><div><h2>Returns</h2><p>{returns.length} immutable return records</p></div></div><ul className="record-list">{returns.map(item => <li key={item.id}><Link href={`/returns/${item.id}?organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}`}>Return #{shortId(item.id)}</Link><span>{formatMoney(item.amount, item.currency)}</span><small>{item.reason}</small></li>)}</ul></section>}
    {returns === null ? <section className="notice"><h2>Return permission required</h2><p>This account cannot view or initiate returns for the selected branch.</p></section> : !saleVoid && <ReturnForm organizationId={organizationId} branchId={branchId} sale={sale} remaining={remaining} />}
  </>;
}
