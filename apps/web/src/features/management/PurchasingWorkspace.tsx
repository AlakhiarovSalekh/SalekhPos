"use client";

import { FormEvent, useEffect, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { listProducts, type Product } from "@/features/products/api";
import { changePurchaseStatus, createPurchaseOrder, getPurchaseOrders, getSuppliers } from "./api";
import type { PurchaseOrder, Supplier } from "./types";

type PurchasingData = { orders: PurchaseOrder[]; suppliers: Supplier[]; products: Product[] };

async function loadPurchasingData(organizationId: string, branchId: string, signal: AbortSignal): Promise<PurchasingData> {
  const [orders, suppliers] = await Promise.all([
    getPurchaseOrders(organizationId, branchId), getSuppliers(organizationId),
  ]);
  const products: Product[] = [];
  let after: string | null = null;
  for (let page = 0; page < 10; page++) {
    const result = await listProducts(organizationId, after, signal);
    products.push(...result.items.filter(item => item.isActive));
    if (!result.nextCursor) break;
    if (result.nextCursor === after) throw new Error("Repeated product cursor");
    after = result.nextCursor;
  }
  return { orders: [...orders.items], suppliers: [...suppliers.items.filter(item => item.isActive)], products };
}
export function PurchasingWorkspace() {
  const scope = useOperationsScope();
  const [orders, setOrders] = useState<PurchaseOrder[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [supplierId, setSupplierId] = useState("");
  const [productId, setProductId] = useState("");
  const [quantity, setQuantity] = useState("1");
  const [unitCost, setUnitCost] = useState("0");
  const [currency, setCurrency] = useState("GEL");
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!scope.organizationId || !scope.branchId) return;
    const controller = new AbortController();
    loadPurchasingData(scope.organizationId, scope.branchId, controller.signal).then(data => {
      if (controller.signal.aborted) return;
      setOrders(data.orders); setSuppliers(data.suppliers); setProducts(data.products); setError(null);
      setSupplierId(current => data.suppliers.some(item => item.id === current) ? current : (data.suppliers[0]?.id ?? ""));
      setProductId(current => data.products.some(item => item.id === current) ? current : (data.products[0]?.id ?? ""));
    }).catch(() => { if (!controller.signal.aborted) setError("Purchasing data could not be loaded."); });
    return () => controller.abort();
  }, [scope.branchId, scope.organizationId]);
  async function refresh() {
    if (!scope.organizationId || !scope.branchId) return;
    const data = await loadPurchasingData(scope.organizationId, scope.branchId, new AbortController().signal);
    setOrders(data.orders); setSuppliers(data.suppliers); setProducts(data.products);
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!supplierId || !productId || !scope.organizationId || !scope.branchId) return;
    setBusy(true); setError(null);
    try {
      await createPurchaseOrder(scope.organizationId, scope.branchId, {
        supplierId, currency: currency.trim().toUpperCase(), reference: reference.trim() || undefined,
        lines: [{ productId, quantity: Number(quantity), unitCost: Number(unitCost) }],
      });
      setReference("");
      await refresh();
    } catch { setError("Purchase order could not be created."); }
    finally { setBusy(false); }
  }

  async function transition(order: PurchaseOrder, action: "submit" | "approve" | "cancel") {
    if (!scope.organizationId || !scope.branchId) return;
    setBusy(true); setError(null);
    try { await changePurchaseStatus(scope.organizationId, scope.branchId, order, action); await refresh(); }
    catch { setError(`Purchase order could not be ${action}ed.`); }
    finally { setBusy(false); }
  }
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">PURCHASING</span><h1>Purchase orders</h1>
      <p>Create supplier orders and move them through the approval lifecycle.</p></div>
      <div className="metric-card"><strong>{orders.length}</strong><span>Orders loaded</span></div></div>
    <OperationsScopeSelector scope={scope} />
    {error ? <p className="error-banner">{error}</p> : null}
    <div className="split-grid">
      <form className="panel" onSubmit={submit}><h2>New draft</h2>
        <label>Supplier<select value={supplierId} onChange={event => setSupplierId(event.target.value)}>
          {suppliers.map(item => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>
        <label>Product<select value={productId} onChange={event => setProductId(event.target.value)}>
          {products.map(item => <option key={item.id} value={item.id}>{item.sku} · {item.name}</option>)}</select></label>
        <label>Quantity<input inputMode="decimal" value={quantity} onChange={event => setQuantity(event.target.value)} /></label>
        <label>Unit cost<input inputMode="decimal" value={unitCost} onChange={event => setUnitCost(event.target.value)} /></label>
        <label>Currency<input maxLength={3} value={currency} onChange={event => setCurrency(event.target.value.toUpperCase())} /></label>
        <label>Reference<input maxLength={120} value={reference} onChange={event => setReference(event.target.value)} /></label>
        <button disabled={busy || !scope.branchId || !supplierId || !productId}>{busy ? "Working…" : "Create draft"}</button>
      </form>
      <div className="panel"><h2>Orders</h2><div className="data-list">{orders.map(order =>
        <article key={order.id}><strong>{order.reference || order.id.slice(0, 8).toUpperCase()} · {order.status}</strong>
          <span>{order.total.toFixed(2)} {order.currency} · {order.lines.length} line(s)</span>
          <div className="inline-actions">
            {order.status === "draft" ? <button disabled={busy} onClick={() => void transition(order, "submit")}>Submit</button> : null}
            {order.status === "submitted" ? <button disabled={busy} onClick={() => void transition(order, "approve")}>Approve</button> : null}
            {order.status === "draft" || order.status === "submitted"
              ? <button disabled={busy} onClick={() => void transition(order, "cancel")}>Cancel</button> : null}
          </div></article>)}</div></div>
    </div>
  </section>;
}
