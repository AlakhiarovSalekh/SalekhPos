"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { listProducts, type Product } from "@/features/products/api";
import { changePurchaseStatus, createPurchaseOrder, getPurchaseOrders, getPurchaseReceipts, getPurchaseReceivingState, getSuppliers, receivePurchaseOrder } from "./api";
import type { PurchaseOrder, PurchaseReceipt, PurchaseReceivingState, Supplier } from "./types";

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
  const [receivingOrder, setReceivingOrder] = useState<PurchaseOrder | null>(null);
  const [receivingState, setReceivingState] = useState<PurchaseReceivingState | null>(null);
  const [receiptQuantities, setReceiptQuantities] = useState<Record<string, string>>({});
  const [receiptReference, setReceiptReference] = useState("");
  const [receivingBusy, setReceivingBusy] = useState(false);
  const [historyOrder, setHistoryOrder] = useState<PurchaseOrder | null>(null);
  const [receiptHistory, setReceiptHistory] = useState<PurchaseReceipt[]>([]);
  const [historyNextCursor, setHistoryNextCursor] = useState<string | null>(null);
  const [historyBusy, setHistoryBusy] = useState(false);
  const [loadedScopeKey, setLoadedScopeKey] = useState("");
  const scopeGeneration = useRef(0);

  const currentScopeKey = scope.organizationId && scope.branchId
    ? `${scope.organizationId}:${scope.branchId}`
    : "";
  const scopeReady = currentScopeKey !== "" && loadedScopeKey === currentScopeKey;
  const scopedOrders = scopeReady ? orders : [];
  const scopedSuppliers = scopeReady ? suppliers : [];
  const scopedProducts = scopeReady ? products : [];

  useEffect(() => {
    scopeGeneration.current += 1;
    if (!scope.organizationId || !scope.branchId) return;
    const controller = new AbortController();
    loadPurchasingData(scope.organizationId, scope.branchId, controller.signal).then(data => {
      if (controller.signal.aborted) return;
      setReceivingOrder(null);
      setReceivingState(null);
      setReceiptQuantities({});
      setReceiptReference("");
      setHistoryOrder(null);
      setReceiptHistory([]);
      setHistoryNextCursor(null);
      setBusy(false);
      setReceivingBusy(false);
      setHistoryBusy(false);
      setOrders(data.orders); setSuppliers(data.suppliers); setProducts(data.products); setError(null);
      setLoadedScopeKey(`${scope.organizationId}:${scope.branchId}`);
      setSupplierId(current => data.suppliers.some(item => item.id === current) ? current : (data.suppliers[0]?.id ?? ""));
      setProductId(current => data.products.some(item => item.id === current) ? current : (data.products[0]?.id ?? ""));
    }).catch(() => { if (!controller.signal.aborted) setError("Purchasing data could not be loaded."); });
    return () => { scopeGeneration.current += 1; controller.abort(); };
  }, [scope.branchId, scope.organizationId]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!scopeReady || !supplierId || !productId || !scope.organizationId || !scope.branchId) return;
    const generation = scopeGeneration.current;
    setBusy(true); setError(null);
    try {
      const created = await createPurchaseOrder(scope.organizationId, scope.branchId, {
        supplierId, currency: currency.trim().toUpperCase(), reference: reference.trim() || undefined,
        lines: [{ productId, quantity: Number(quantity), unitCost: Number(unitCost) }],
      });
      if (generation !== scopeGeneration.current) return;
      setOrders(current => [created, ...current.filter(order => order.id !== created.id)]);
      setReference("");
    } catch {
      if (generation === scopeGeneration.current) setError("Purchase order could not be created.");
    } finally {
      if (generation === scopeGeneration.current) setBusy(false);
    }
  }

  async function openReceiving(order: PurchaseOrder) {
    if (!scopeReady || !scope.organizationId || !scope.branchId
        || (order.status !== "approved" && order.status !== "partially_received")) return;
    const generation = scopeGeneration.current;
    setReceivingBusy(true); setError(null);
    try {
      const state = await getPurchaseReceivingState(scope.organizationId, scope.branchId, order.id);
      if (generation !== scopeGeneration.current) return;
      setReceivingState(state);
      setReceivingOrder({ ...order, status: state.status, version: state.version });
      setReceiptQuantities(Object.fromEntries(state.lines
        .filter(line => line.remainingQuantity > 0)
        .map(line => [line.productId, String(line.remainingQuantity)])));
      setReceiptReference("");
    } catch {
      if (generation === scopeGeneration.current)
        setError("Purchase receiving state could not be loaded.");
    } finally {
      if (generation === scopeGeneration.current) setReceivingBusy(false);
    }
  }

  async function submitReceipt() {
    if (!scopeReady || !scope.organizationId || !scope.branchId || !receivingOrder || !receivingState) return;
    const lines = receivingState.lines.map(line => ({
      productId: line.productId,
      quantity: Number(receiptQuantities[line.productId] ?? "0"),
    })).filter(line => Number.isFinite(line.quantity) && line.quantity > 0);
    if (!lines.length) { setError("Enter at least one positive receipt quantity."); return; }
    const generation = scopeGeneration.current;
    setReceivingBusy(true); setError(null);
    try {
      const result = await receivePurchaseOrder(scope.organizationId, scope.branchId, receivingOrder, {
        reference: receiptReference.trim() || undefined,
        receivedAt: new Date().toISOString(),
        lines,
      });
      if (generation !== scopeGeneration.current) return;
      setOrders(current => current.map(order => order.id === result.order.id ? result.order : order));
      if (historyOrder?.id === result.order.id) {
        setHistoryOrder(result.order);
        setReceiptHistory(current => [
          result.receipt,
          ...current.filter(item => item.id !== result.receipt.id),
        ]);
      }
      setReceivingOrder(null); setReceivingState(null); setReceiptQuantities({}); setReceiptReference("");
    } catch {
      if (generation === scopeGeneration.current)
        setError("Purchase receipt could not be recorded. Check remaining quantities and order state.");
    } finally {
      if (generation === scopeGeneration.current) setReceivingBusy(false);
    }
  }

  async function openReceiptHistory(order: PurchaseOrder) {
    if (!scopeReady || !scope.organizationId || !scope.branchId) return;
    const generation = scopeGeneration.current;
    setHistoryBusy(true); setError(null);
    setHistoryOrder(order);
    setReceiptHistory([]);
    setHistoryNextCursor(null);
    try {
      const page = await getPurchaseReceipts(scope.organizationId, scope.branchId, order.id, 25);
      if (generation !== scopeGeneration.current) return;
      setReceiptHistory(page.items);
      setHistoryNextCursor(page.nextCursor);
    } catch {
      if (generation === scopeGeneration.current) {
        setHistoryOrder(null);
        setReceiptHistory([]);
        setHistoryNextCursor(null);
        setError("Purchase receipt history could not be loaded.");
      }
    } finally {
      if (generation === scopeGeneration.current) setHistoryBusy(false);
    }
  }

  async function loadMoreReceipts() {
    if (!scopeReady || !scope.organizationId || !scope.branchId || !historyOrder || !historyNextCursor) return;
    const generation = scopeGeneration.current;
    setHistoryBusy(true); setError(null);
    try {
      const previousCursor = historyNextCursor;
      const page = await getPurchaseReceipts(
        scope.organizationId, scope.branchId, historyOrder.id, 25, previousCursor);
      if (generation !== scopeGeneration.current) return;
      if (page.nextCursor === previousCursor) throw new Error("Repeated receipt cursor");
      setReceiptHistory(current => {
        const seen = new Set(current.map(item => item.id));
        return [...current, ...page.items.filter(item => !seen.has(item.id))];
      });
      setHistoryNextCursor(page.nextCursor);
    } catch {
      if (generation === scopeGeneration.current)
        setError("More purchase receipts could not be loaded.");
    } finally {
      if (generation === scopeGeneration.current) setHistoryBusy(false);
    }
  }

  async function transition(order: PurchaseOrder, action: "submit" | "approve" | "cancel") {
    if (!scopeReady || !scope.organizationId || !scope.branchId) return;
    const generation = scopeGeneration.current;
    setBusy(true); setError(null);
    try {
      const changed = await changePurchaseStatus(
        scope.organizationId, scope.branchId, order, action);
      if (generation !== scopeGeneration.current) return;
      setOrders(current => current.map(item => item.id === changed.id ? changed : item));
    } catch {
      if (generation === scopeGeneration.current)
        setError(`Purchase order could not be ${action}ed.`);
    } finally {
      if (generation === scopeGeneration.current) setBusy(false);
    }
  }
  return <section className="manager-content">
    <div className="manager-title"><div><span className="eyebrow">PURCHASING</span><h1>Purchase orders</h1>
      <p>Create supplier orders and move them through the approval lifecycle.</p></div>
      <div className="metric-card"><strong>{scopedOrders.length}</strong><span>Orders loaded</span></div></div>
    <OperationsScopeSelector scope={scope} />
    {error ? <p className="error-banner">{error}</p> : null}
    <div className="split-grid">
      <form className="panel" onSubmit={submit}><h2>New draft</h2>
        <label>Supplier<select value={supplierId} onChange={event => setSupplierId(event.target.value)}>
          {scopedSuppliers.map(item => <option key={item.id} value={item.id}>{item.code} · {item.name}</option>)}</select></label>
        <label>Product<select value={productId} onChange={event => setProductId(event.target.value)}>
          {scopedProducts.map(item => <option key={item.id} value={item.id}>{item.sku} · {item.name}</option>)}</select></label>
        <label>Quantity<input inputMode="decimal" value={quantity} onChange={event => setQuantity(event.target.value)} /></label>
        <label>Unit cost<input inputMode="decimal" value={unitCost} onChange={event => setUnitCost(event.target.value)} /></label>
        <label>Currency<input maxLength={3} value={currency} onChange={event => setCurrency(event.target.value.toUpperCase())} /></label>
        <label>Reference<input maxLength={100} value={reference} onChange={event => setReference(event.target.value)} /></label>
        <button disabled={busy || !scopeReady || !supplierId || !productId}>{busy ? "Working…" : "Create draft"}</button>
      </form>
      <div className="panel"><h2>Orders</h2><div className="data-list">{scopedOrders.map(order =>
        <article key={order.id}><strong>{order.reference || order.id.slice(0, 8).toUpperCase()} · {order.status}</strong>
          <span>{order.total.toFixed(2)} {order.currency} · {order.lines.length} line(s)</span>
          <div className="inline-actions">
            {order.status === "draft" ? <button disabled={busy} onClick={() => void transition(order, "submit")}>Submit</button> : null}
            {order.status === "submitted" ? <button disabled={busy} onClick={() => void transition(order, "approve")}>Approve</button> : null}
            {order.status === "draft" || order.status === "submitted"
              ? <button disabled={busy} onClick={() => void transition(order, "cancel")}>Cancel</button> : null}
            {order.status === "approved" || order.status === "partially_received"
              ? <button disabled={busy || receivingBusy} onClick={() => void openReceiving(order)}>Receive goods</button> : null}
            <button disabled={busy || historyBusy} onClick={() => void openReceiptHistory(order)}>Receipts</button>
          </div></article>)}</div></div>
    </div>
    {scopeReady && historyOrder ? <div className="panel">
      <div className="manager-title"><div><span className="eyebrow">GOODS RECEIPTS</span>
        <h2>Receipt history</h2>
        <p>{historyOrder.reference || historyOrder.id.slice(0, 8).toUpperCase()} · {receiptHistory.length} receipt(s) loaded</p></div>
        <div className="inline-actions">
          {historyNextCursor ? <button disabled={historyBusy} onClick={() => void loadMoreReceipts()}>{historyBusy ? "Loading…" : "Load more"}</button> : null}
          <button disabled={historyBusy} onClick={() => { setHistoryOrder(null); setReceiptHistory([]); setHistoryNextCursor(null); }}>Close</button>
        </div>
      </div>
      {receiptHistory.length === 0 ? <p>No receipts have been recorded for this purchase order.</p> :
        <div className="data-list">{[...receiptHistory].sort((a,b)=>b.createdAt.localeCompare(a.createdAt)).map(receipt =>
          <article key={receipt.id}>
            <strong>{receipt.reference || receipt.id.slice(0, 8).toUpperCase()}</strong>
            <span>{new Date(receipt.receivedAt).toLocaleString()} · {receipt.lines.length} line(s) · received by {receipt.receivedBySubject}</span>
            {receipt.lines.map(line => {
              const product = scopedProducts.find(item => item.id === line.productId);
              return <span key={line.movementId}>{product ? `${product.sku} · ${product.name}` : line.productId} · {line.quantity}</span>;
            })}
          </article>)}</div>}
    </div> : null}
    {scopeReady && receivingOrder && receivingState ? <div className="panel">
      <div className="manager-title"><div><span className="eyebrow">GOODS RECEIVING</span>
        <h2>Receive purchase order</h2>
        <p>Record only quantities physically received. Inventory is updated atomically with this receipt.</p></div>
        <div className="metric-card"><strong>{receivingState.lines.filter(line => line.remainingQuantity > 0).length}</strong><span>Open lines</span></div>
      </div>
      <div className="data-list">{receivingState.lines.map(line => {
        const product = scopedProducts.find(item => item.id === line.productId);
        return <article key={line.productId}>
          <strong>{product ? `${product.sku} · ${product.name}` : line.productId}</strong>
          <span>Ordered {line.orderedQuantity} · received {line.receivedQuantity} · remaining {line.remainingQuantity}</span>
          <label>Receive now<input inputMode="decimal" disabled={line.remainingQuantity <= 0 || receivingBusy}
            value={receiptQuantities[line.productId] ?? ""}
            onChange={event => setReceiptQuantities(current => ({ ...current, [line.productId]: event.target.value }))}/></label>
        </article>;
      })}</div>
      <label>Receipt reference <span className="label-note">optional</span>
        <input maxLength={120} value={receiptReference} onChange={event => setReceiptReference(event.target.value)}/>
      </label>
      <div className="inline-actions">
        <button disabled={receivingBusy} onClick={() => void submitReceipt()}>{receivingBusy ? "Receiving…" : "Record receipt"}</button>
        <button disabled={receivingBusy} onClick={() => { setReceivingOrder(null); setReceivingState(null); }}>Cancel</button>
      </div>
    </div> : null}
  </section>;
}
