"use client";

import { useMemo, useState } from "react";
import { formatMoney, shortId } from "@/features/sales/api";
import type { SaleDetail } from "@/features/sales/types";
import { validateReturnRequest } from "../api";
import { useReturnSubmission } from "../hooks/useReturnSubmission";

export function ReturnForm({ organizationId, branchId, sale, remaining }: { organizationId: string; branchId: string; sale: SaleDetail; remaining: ReadonlyMap<string, number> }) {
  const [reason, setReason] = useState("");
  const [quantities, setQuantities] = useState<Record<string, string>>({});
  const [errors, setErrors] = useState<string[]>([]);
  const { intent, submit, reset } = useReturnSubmission(organizationId, branchId);
  const availableLines = useMemo(() => sale.lines.filter(line => (remaining.get(line.productId) ?? 0) > 0), [remaining, sale.lines]);
  const locked = intent.state === "submitting" || intent.state === "uncertain" || intent.state === "succeeded";

  const build = () => ({ saleId: sale.id, reason: reason.trim(), lines: availableLines.flatMap(line => {
    const source = quantities[line.productId]?.trim();
    if (!source) return [];
    return [{ productId: line.productId, quantity: Number(source) }];
  }) });

  const onSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    const request = build();
    const validation = validateReturnRequest(request, remaining);
    setErrors(validation);
    if (validation.length === 0) void submit(request);
  };

  if (intent.state === "succeeded") return <div className="success-panel" role="status"><h3>Return completed</h3><p>Refund evidence for return <strong>#{shortId(intent.result.id)}</strong> was recorded for {formatMoney(intent.result.amount, intent.result.currency)}.</p><a className="button compact" href={`/returns/${intent.result.id}?organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}`}>Open return</a></div>;
  if (availableLines.length === 0) return <div className="notice"><h3>Fully returned</h3><p>No sold quantity remains eligible for another return.</p></div>;

  return <form className="return-form" onSubmit={onSubmit} noValidate>
    <div><h3>Initiate return and cash refund</h3><p>Only quantities remaining on the immutable sale are accepted. Completion is atomic on the server.</p></div>
    <label>Reason<textarea value={reason} onChange={event => setReason(event.target.value)} minLength={3} maxLength={500} disabled={locked} required /></label>
    <fieldset disabled={locked}><legend>Quantities to return</legend>{availableLines.map(line => <label className="quantity-row" key={line.productId}><span><strong>Product #{shortId(line.productId)}</strong><small>Sold {line.quantity} · remaining {remaining.get(line.productId)} · {formatMoney(line.unitAmount, line.currency)} each</small></span><input aria-label={`Return quantity for product ${line.productId}`} inputMode="decimal" value={quantities[line.productId] ?? ""} onChange={event => setQuantities(current => ({ ...current, [line.productId]: event.target.value }))} placeholder="0" /></label>)}</fieldset>
    {errors.length > 0 && <ul className="validation-list" role="alert">{errors.map(error => <li key={error}>{error}</li>)}</ul>}
    {intent.state === "failed" && <div className="inline-error" role="alert"><p>{intent.message}</p><button type="button" className="text-button" onClick={reset}>Edit and try again</button></div>}
    {intent.state === "uncertain" && <div className="warning-panel" role="alert"><h4>Outcome not confirmed</h4><p>{intent.message} The original idempotency key and payload are retained.</p><button className="button compact" type="button" onClick={() => void submit(intent.request)}>Retry same request</button></div>}
    <button className="button compact" type="submit" disabled={locked}>{intent.state === "submitting" ? "Submitting safely…" : "Complete return"}</button>
  </form>;
}
