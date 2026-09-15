"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { listProducts, type Product } from "@/features/products/api";
import { formatDate, formatMoney } from "@/features/sales/api";
import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { mutationIntent, type MutationIntent } from "./idempotency";
import { operationErrorMessage } from "./operationError";
import { OperationsScopeSelector } from "./OperationsScopeSelector";
import { resolvePrice, schedulePrice } from "./api";
import type { Price, PriceDraft, ResolvedPrice } from "./types";
import { useOperationsScope } from "./useOperationsScope";

async function loadProducts(organizationId: string, signal: AbortSignal): Promise<readonly Product[]> {
  const items: Product[] = [];
  let after: string | null = null;
  for (let page = 0; page < 40; page++) {
    const result = await listProducts(organizationId, after, signal);
    items.push(...result.items.filter(item => item.isActive));
    if (!result.nextCursor) return items;
    if (result.nextCursor === after) throw new Error("Repeated product cursor");
    after = result.nextCursor;
  }
  throw new Error("Too many products for a single price editor.");
}
function localIso(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) throw new TypeError("Enter a valid date and time.");
  return date.toISOString();
}

export function PricingWorkspace() {
  const scope = useOperationsScope();
  const [products, setProducts] = useState<readonly Product[]>([]);
  const [productId, setProductId] = useState("");
  const [scopeBranch, setScopeBranch] = useState("");
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("USD");
  const [taxMode, setTaxMode] = useState<"inclusive" | "exclusive">("exclusive");
  const [taxRate, setTaxRate] = useState("0");
  const [validFrom, setValidFrom] = useState("");
  const [validUntil, setValidUntil] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState<Price | null>(null);
  const [resolved, setResolved] = useState<ResolvedPrice | null>(null);
  const intent = useRef<MutationIntent | null>(null);
  useEffect(() => {
    if (!scope.organizationId) return;
    const controller = new AbortController();
    loadProducts(scope.organizationId, controller.signal).then(items => {
      if (controller.signal.aborted) return;
      setProducts(items);
      setProductId(current => items.some(item => item.id === current) ? current : (items[0]?.id ?? ""));
      setSaved(null); setResolved(null); setError(null);
    }).catch(reason => { if (!controller.signal.aborted) setError(operationErrorMessage(reason)); });
    return () => controller.abort();
  }, [scope.organizationId]);

  const effectiveScopeBranch = scope.branches.some(branch => branch.id === scopeBranch) ? scopeBranch : "";

  const branchName = useMemo(() => scope.branches.find(item => item.id === scope.branchId)?.name, [scope.branches, scope.branchId]);

  async function submit(event: React.FormEvent) {
    event.preventDefault(); setError(null); setSaved(null); setSaving(true);
    try {
      const draft: PriceDraft = {
        productId, branchId: effectiveScopeBranch || null, amount: Number(amount), currency: currency.toUpperCase(),
        taxMode, taxRate: Number(taxRate), validFrom: localIso(validFrom), validUntil: validUntil ? localIso(validUntil) : null,
      };
      intent.current = mutationIntent([scope.organizationId, draft], intent.current);
      const result = await schedulePrice(scope.organizationId, draft, intent.current.idempotencyKey);
      setSaved(result); intent.current = null;
    } catch (reason) { setError(operationErrorMessage(reason)); }
    finally { setSaving(false); }
  }

  async function resolveCurrent() {
    if (!scope.organizationId || !scope.branchId || !productId) return;
    setError(null); setResolved(null);
    try { setResolved(await resolvePrice(scope.organizationId, scope.branchId, productId, new Date().toISOString())); }
    catch (reason) { setError(operationErrorMessage(reason)); }
  }

  return <ManagerShell organizationId={scope.organizationId || undefined} branchId={scope.branchId || undefined} branchName={branchName}>
    <section className="manager-content operations-page">
      <div className="manager-title"><div><p className="eyebrow">PRICING CONTROL</p><h1>Price schedules<span className="accent">.</span></h1>
        <p className="description">Create organization-wide or branch-specific prices without weakening terminal security boundaries.</p></div></div>
      <OperationsScopeSelector scope={scope} />
      <div className="operations-grid">
        <form className="operations-card" onSubmit={submit}>
          <h2>Schedule price</h2>
          <label><span>Product</span><select value={productId} onChange={event => { setProductId(event.target.value); intent.current = null; }} required>
            <option value="" disabled>Select a product</option>{products.map(product => <option key={product.id} value={product.id}>{product.sku} — {product.name}</option>)}
          </select></label>
          <label><span>Price scope</span><select value={effectiveScopeBranch} onChange={event => { setScopeBranch(event.target.value); intent.current = null; }}>
            <option value="">All branches</option>{scope.branches.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
          </select></label>
          <div className="operations-form-row"><label><span>Amount</span><input inputMode="decimal" value={amount} onChange={event => { setAmount(event.target.value); intent.current = null; }} required /></label>
            <label><span>Currency</span><input value={currency} maxLength={3} onChange={event => { setCurrency(event.target.value.toUpperCase()); intent.current = null; }} required /></label></div>
          <div className="operations-form-row"><label><span>Tax mode</span><select value={taxMode} onChange={event => { setTaxMode(event.target.value as "inclusive" | "exclusive"); intent.current = null; }}><option value="exclusive">Exclusive</option><option value="inclusive">Inclusive</option></select></label>
            <label><span>Tax rate %</span><input inputMode="decimal" value={taxRate} onChange={event => { setTaxRate(event.target.value); intent.current = null; }} required /></label></div>
          <label><span>Valid from</span><input type="datetime-local" value={validFrom} onChange={event => { setValidFrom(event.target.value); intent.current = null; }} required /></label>
          <label><span>Valid until (optional)</span><input type="datetime-local" value={validUntil} onChange={event => { setValidUntil(event.target.value); intent.current = null; }} /></label>
          <button className="button compact" disabled={saving || !scope.organizationId || !productId}>{saving ? "Saving…" : "Schedule price"}</button>
        </form>
        <aside className="operations-card operations-summary">
          <h2>Live resolution</h2><p>Resolve the effective price for the selected product and branch at the current UTC time.</p>
          <button className="secondary-button" type="button" disabled={!scope.branchId || !productId} onClick={resolveCurrent}>Resolve current price</button>
          {resolved ? <dl><div><dt>Amount</dt><dd>{formatMoney(resolved.amount, resolved.currency)}</dd></div>
            <div><dt>Tax</dt><dd>{resolved.taxRate}% {resolved.taxMode}</dd></div><div><dt>Valid from</dt><dd>{formatDate(resolved.validFrom)}</dd></div>
            <div><dt>Scope</dt><dd>{resolved.branchId ? "Branch" : "Organization"}</dd></div></dl> : <p className="quiet">No resolved price loaded.</p>}
          {saved ? <div className="operations-success" role="status"><strong>Price scheduled</strong><span>{formatMoney(saved.amount, saved.currency)} from {formatDate(saved.validFrom)}</span></div> : null}
          {error ? <p role="alert" className="operations-error">{error}</p> : null}
        </aside>
      </div>
    </section>
  </ManagerShell>;
}
