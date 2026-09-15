"use client";
import { FormEvent, useCallback, useEffect, useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { listProducts, type Product } from "@/features/products/api";
import { changeStockTransferStatus, createStockTransfer, getStockTransfers } from "@/features/management/api";
import type { StockTransfer } from "@/features/management/types";

async function loadProducts(organizationId:string, signal:AbortSignal):Promise<Product[]>{
  const items:Product[]=[];let after:string|null=null;
  for(let page=0;page<20;page++){const result=await listProducts(organizationId,after,signal);items.push(...result.items.filter(x=>x.isActive));if(!result.nextCursor)return items;if(result.nextCursor===after)throw new Error("Repeated product cursor");after=result.nextCursor;}
  throw new Error("Product catalog is too large for transfer editor.");
}

export function StockTransferWorkspace(){
  const scope=useOperationsScope();const [transfers,setTransfers]=useState<StockTransfer[]>([]);const [products,setProducts]=useState<Product[]>([]);
  const [destination,setDestination]=useState("");const [product,setProduct]=useState("");const [quantity,setQuantity]=useState("1");const [reference,setReference]=useState("");
  const [busy,setBusy]=useState(false);const [error,setError]=useState<string|null>(null);
  const ready=Boolean(scope.organizationId&&scope.branchId);
  const reload=useCallback(async(signal?:AbortSignal)=>{
    if(!scope.organizationId||!scope.branchId)return;
    const controller=signal?null:new AbortController();const actual=signal??controller!.signal;
    try{const [page,productRows]=await Promise.all([getStockTransfers(scope.organizationId,scope.branchId),loadProducts(scope.organizationId,actual)]);if(actual.aborted)return;setTransfers(page.items);setProducts(productRows);setDestination(current=>scope.branches.some(x=>x.id===current&&x.id!==scope.branchId)?current:(scope.branches.find(x=>x.id!==scope.branchId)?.id??""));setProduct(current=>productRows.some(x=>x.id===current)?current:(productRows[0]?.id??""));setError(null);}catch{if(!actual.aborted)setError("Stock transfers could not be loaded.");}
  },[scope.organizationId,scope.branchId,scope.branches]);
  useEffect(()=>{if(!ready)return;const controller=new AbortController();queueMicrotask(()=>{if(!controller.signal.aborted)void reload(controller.signal);});return()=>controller.abort();},[ready,reload]);

  async function submit(event:FormEvent){event.preventDefault();if(!ready||!destination||!product)return;const qty=Number(quantity);if(!Number.isFinite(qty)||qty<=0)return;setBusy(true);setError(null);try{await createStockTransfer(scope.organizationId,scope.branchId,{destinationBranchId:destination,reference:reference.trim()||undefined,lines:[{productId:product,quantity:qty}]});setReference("");await reload();}catch{setError("Stock transfer could not be created.");}finally{setBusy(false);}}
  async function transition(transfer:StockTransfer,action:"dispatch"|"receive"|"cancel"){setBusy(true);setError(null);try{const branch=action==="receive"?transfer.destinationBranchId:transfer.sourceBranchId;await changeStockTransferStatus(scope.organizationId,branch,transfer,action);await reload();}catch{setError(`Stock transfer ${action} failed.`);}finally{setBusy(false);}}

  return <section className="manager-content"><div className="manager-title"><div><span className="eyebrow">WAREHOUSING</span><h1>Stock transfers</h1><p>Move inventory between stores with dispatch and receiving evidence.</p></div><div className="metric-card"><strong>{transfers.length}</strong><span>Transfers loaded</span></div></div>
  <OperationsScopeSelector scope={scope}/>{error&&<p className="error-banner">{error}</p>}
  <div className="split-grid"><form className="panel" onSubmit={submit}><h2>New transfer</h2>
  <label>Destination<select value={destination} onChange={e=>setDestination(e.target.value)}><option value="" disabled>Select destination</option>{scope.branches.filter(x=>x.id!==scope.branchId).map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
  <label>Product<select value={product} onChange={e=>setProduct(e.target.value)}><option value="" disabled>Select product</option>{products.map(x=><option key={x.id} value={x.id}>{x.sku} · {x.name}</option>)}</select></label>
  <label>Quantity<input inputMode="decimal" value={quantity} onChange={e=>setQuantity(e.target.value)}/></label>
  <label>Reference<input maxLength={120} value={reference} onChange={e=>setReference(e.target.value)}/></label>
  <button disabled={busy||!ready||!destination||!product}>{busy?"Working…":"Create transfer"}</button></form>
  <div className="panel"><h2>Transfer lifecycle</h2><div className="data-list">{transfers.map(t=><article key={t.id}><strong>{t.reference||t.id.slice(0,8).toUpperCase()} · {t.status}</strong><span>{t.lines.length} line(s) · {t.sourceBranchId.slice(0,8)} → {t.destinationBranchId.slice(0,8)}</span><div className="inline-actions">{t.status==="draft"&&t.sourceBranchId===scope.branchId&&<><button disabled={busy} onClick={()=>void transition(t,"dispatch")}>Dispatch</button><button disabled={busy} onClick={()=>void transition(t,"cancel")}>Cancel</button></>}{t.status==="in_transit"&&t.destinationBranchId===scope.branchId&&<button disabled={busy} onClick={()=>void transition(t,"receive")}>Receive</button>}</div></article>)}</div></div></div></section>;
}
