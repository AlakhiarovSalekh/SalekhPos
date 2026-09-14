"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { UUID_PATTERN } from "@/lib/boundedJson";
import { ApiError, formatDate, formatMoney, shortId } from "@/features/sales/api";
import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { ErrorPanel, LoadingPanel } from "@/features/sales/components/StatePanel";
import { getRefund, getReturn } from "../api";
import type { Refund, ReturnDetail } from "../types";

type State = { status: "loading" } | { status: "error"; error: ApiError } | { status: "ready"; item: ReturnDetail; refund: Refund | null };

export function ReturnDetailView({ organizationId, branchId, returnId }: { organizationId?: string; branchId?: string; returnId: string }) {
  const [state, setState] = useState<State>({ status: "loading" });
  const valid = Boolean(organizationId && branchId && UUID_PATTERN.test(organizationId) && UUID_PATTERN.test(branchId) && UUID_PATTERN.test(returnId));
  useEffect(() => {
    if (!valid) return;
    const controller = new AbortController();
    Promise.allSettled([getReturn(organizationId!, branchId!, returnId, controller.signal), getRefund(organizationId!, branchId!, returnId, controller.signal)])
      .then(([itemResult, refundResult]) => {
        if (itemResult.status === "rejected") throw itemResult.reason;
        setState({ status: "ready", item: itemResult.value, refund: refundResult.status === "fulfilled" ? refundResult.value : null });
      })
      .catch(error => { if (!controller.signal.aborted) setState({ status: "error", error: error instanceof ApiError ? error : new ApiError("unexpected", "Return details could not be loaded.", false) }); });
    return () => controller.abort();
  }, [branchId, organizationId, returnId, valid]);
  const query = organizationId && branchId ? `organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}` : "";
  return <ManagerShell organizationId={organizationId} branchId={branchId}><div className="manager-content"><Link className="back-link" href={query ? `/returns?${query}` : "/returns"}>← Back to returns</Link>
    {!valid && <ErrorPanel error={new ApiError("validation", "A valid organization, branch and return are required.", false)} />}
    {valid && state.status === "loading" && <LoadingPanel label="Loading return and refund evidence…" />}
    {state.status === "error" && <ErrorPanel error={state.error} retry={() => window.location.reload()} />}
    {state.status === "ready" && <><div className="detail-heading"><div><p className="eyebrow">RETURN #{shortId(state.item.id)}</p><h1>{formatMoney(state.item.amount, state.item.currency)}</h1><p>{formatDate(state.item.completedAt)}</p></div><span className="status-badge status-returned">Completed</span></div>
      <div className="detail-columns"><section className="data-card"><div className="card-heading"><div><h2>Return reason</h2><p>Immutable operator evidence</p></div></div><p className="padded reason-detail">{state.item.reason}</p><Link className="text-link padded-link" href={`/sales/${state.item.saleId}?${query}`}>Open original sale #{shortId(state.item.saleId)} →</Link></section>
      <section className="data-card"><div className="card-heading"><div><h2>Refund</h2><p>Payment module evidence</p></div></div>{state.refund ? <dl className="detail-list"><div><dt>Method</dt><dd>{state.refund.method}</dd></div><div><dt>Status</dt><dd>{state.refund.status}</dd></div><div><dt>Amount</dt><dd>{formatMoney(state.refund.amount, state.refund.currency)}</dd></div><div><dt>Payment</dt><dd className="mono">#{shortId(state.refund.paymentId)}</dd></div></dl> : <p className="muted padded">Refund evidence is restricted or unavailable.</p>}</section></div>
      <section className="data-card"><div className="card-heading"><div><h2>Returned lines</h2><p>Restocked quantities and allocated refund values</p></div></div><div className="table-scroll"><table><thead><tr><th>Line</th><th>Product</th><th>Quantity</th><th>Amount</th></tr></thead><tbody>{state.item.lines.map(line => <tr key={line.lineNumber}><td>{line.lineNumber}</td><td className="mono">#{shortId(line.productId)}</td><td>{line.quantity}</td><td><strong>{formatMoney(line.amount, state.item.currency)}</strong></td></tr>)}</tbody></table></div></section>
    </>}
  </div></ManagerShell>;
}
