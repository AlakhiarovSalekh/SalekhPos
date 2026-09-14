"use client";

import { useMemo, useState } from "react";
import type { StockItem } from "./types";

export function StockTable({ items, hasMore, loadingMore, onLoadMore }: { items: StockItem[]; hasMore: boolean; loadingMore: boolean; onLoadMore: () => void }) {
  const [query, setQuery] = useState(""); const [level, setLevel] = useState("all");
  const visible = useMemo(() => items.filter(item => {
    const matches = !query || `${item.sku} ${item.name}`.toLocaleLowerCase().includes(query.toLocaleLowerCase());
    const quantity = Number(item.quantity); const matchesLevel = level === "all" || (level === "positive" && quantity > 0) || (level === "zero" && quantity === 0) || (level === "negative" && quantity < 0);
    return matches && matchesLevel;
  }), [items, query, level]);
  return <section className="stock-panel" aria-labelledby="stock-title">
    <div className="section-heading"><div><p className="eyebrow">CURRENT LEDGER BALANCE</p><h2 id="stock-title">Stock</h2></div><span className="count">{items.length} loaded</span></div>
    <div className="filters"><label>Search loaded stock<input type="search" value={query} onChange={e => setQuery(e.target.value)} placeholder="SKU or product name" /></label><label>Loaded balance<select value={level} onChange={e => setLevel(e.target.value)}><option value="all">All levels</option><option value="positive">Positive</option><option value="zero">Zero</option><option value="negative">Negative</option></select></label></div>
    <p className="filter-note">Search and balance filters apply only to the {items.length} products loaded from the server.</p>
    {!items.length ? <div className="empty-state"><h3>No active products</h3><p>This branch has no active catalog products to show.</p></div> : !visible.length ? <div className="empty-state"><h3>No loaded matches</h3><p>Clear the search or load more products.</p></div> : <div className="table-wrap"><table><thead><tr><th scope="col">Product</th><th scope="col">SKU</th><th scope="col" className="numeric">On hand</th></tr></thead><tbody>{visible.map(item => <tr key={item.productId}><td><strong>{item.name}</strong><small>{item.productId}</small></td><td>{item.sku}</td><td className={`numeric quantity ${Number(item.quantity) < 0 ? "negative" : ""}`}>{item.quantity}</td></tr>)}</tbody></table></div>}
    {hasMore && <button className="secondary-button" type="button" onClick={onLoadMore} disabled={loadingMore}>{loadingMore ? "Loading…" : "Load next 50"}</button>}
  </section>;
}
