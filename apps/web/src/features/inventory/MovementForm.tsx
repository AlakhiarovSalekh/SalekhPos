"use client";

import { useState } from "react";
import { createMovement, SafeApiError } from "./api";
import { isUncertainFailure, resolveMovementIntent, type MovementIntent } from "./idempotency";
import type { InventoryBranch, MovementKind, StockItem } from "./types";
import { validateMovement, type MovementDraft } from "./validation";

const localNow = () => { const date = new Date(); return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16); };
const initial = (): MovementDraft => ({ productId: "", kind: "receipt", quantity: "", reason: "", occurredAt: localNow() });

export function MovementForm({ organizationId, branch, products, csrfToken, onCreated }: {
  organizationId: string; branch: InventoryBranch; products: StockItem[]; csrfToken: string; onCreated: () => Promise<void>;
}) {
  const [draft, setDraft] = useState(initial);
  const [errors, setErrors] = useState<Partial<Record<keyof MovementDraft, string>>>({});
  const [intent, setIntent] = useState<MovementIntent>();
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<{ kind: "error" | "success" | "uncertain"; text: string }>();
  const update = <K extends keyof MovementDraft>(key: K, value: MovementDraft[K]) => setDraft(current => ({ ...current, [key]: value }));
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); const validated = validateMovement(draft); setErrors(validated.errors); if (!validated.value) return;
    const nextIntent = resolveMovementIntent(validated.value, intent); setIntent(nextIntent); setSubmitting(true); setMessage(undefined);
    try {
      await createMovement(organizationId, branch.branchId, validated.value, nextIntent.key, csrfToken);
      setIntent(undefined); setDraft(initial()); setMessage({ kind: "success", text: "Movement recorded. Stock has been refreshed." }); await onCreated();
    } catch (error) {
      const api = error instanceof SafeApiError ? error : new SafeApiError(null, "We could not confirm whether the request completed. Retry safely.");
      const uncertain = isUncertainFailure(api.status); if (!uncertain) setIntent(undefined);
      setMessage({ kind: uncertain ? "uncertain" : "error", text: api.message + (uncertain ? " The same idempotency key will be reused while the form is unchanged." : "") });
    } finally { setSubmitting(false); }
  };
  if (!branch.canAdjust) return <div className="state-card"><strong>View-only access</strong><p>You can review stock for this branch, but inventory adjustments are not assigned to your account.</p></div>;
  return <form className="movement-form" onSubmit={submit} noValidate>
    <div className="section-heading"><div><p className="eyebrow">NEW MOVEMENT</p><h2>Update stock</h2></div><span className="permission-chip">inventory.adjust</span></div>
    <fieldset disabled={submitting}>
      <legend className="sr-only">Movement type</legend>
      <div className="segmented" aria-label="Movement type">{(["receipt", "adjustment_in", "adjustment_out"] as MovementKind[]).map(kind => <button key={kind} type="button" aria-pressed={draft.kind === kind} onClick={() => update("kind", kind)}>{kind === "receipt" ? "Receipt" : kind === "adjustment_in" ? "Adjust in" : "Adjust out"}</button>)}</div>
      <label>Product<select value={draft.productId} onChange={e => update("productId", e.target.value)} aria-invalid={Boolean(errors.productId)}><option value="">Select a loaded product</option>{products.map(item => <option key={item.productId} value={item.productId}>{item.sku} — {item.name}</option>)}</select>{errors.productId && <span className="field-error">{errors.productId}</span>}</label>
      <div className="form-grid"><label>Quantity<input inputMode="decimal" value={draft.quantity} onChange={e => update("quantity", e.target.value)} placeholder="0.000000" aria-invalid={Boolean(errors.quantity)} />{errors.quantity && <span className="field-error">{errors.quantity}</span>}</label><label>Occurred at<input type="datetime-local" step="1" value={draft.occurredAt} onChange={e => update("occurredAt", e.target.value)} aria-invalid={Boolean(errors.occurredAt)} />{errors.occurredAt && <span className="field-error">{errors.occurredAt}</span>}</label></div>
      <label>Reason <span className="label-note">optional, 200 characters</span><textarea value={draft.reason} maxLength={200} onChange={e => update("reason", e.target.value)} aria-invalid={Boolean(errors.reason)} />{errors.reason && <span className="field-error">{errors.reason}</span>}</label>
      {message && <div role={message.kind === "success" ? "status" : "alert"} className={`form-message ${message.kind}`}>{message.text}</div>}
      <button className="button compact" type="submit">{submitting ? "Recording…" : intent ? "Retry movement" : "Record movement"}</button>
    </fieldset>
  </form>;
}
