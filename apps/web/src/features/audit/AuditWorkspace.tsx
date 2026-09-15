"use client";
import { useCallback,useEffect,useState } from "react";
import { OperationsScopeSelector } from "@/features/operations/OperationsScopeSelector";
import { useOperationsScope } from "@/features/operations/useOperationsScope";
import { getAuditEvents,verifyAuditChain } from "./api";
import type { AuditEvent,AuditIntegrity } from "./types";

export function AuditWorkspace(){
 const scope=useOperationsScope(); const [items,setItems]=useState<AuditEvent[]>([]),[action,setAction]=useState(""),[integrity,setIntegrity]=useState<AuditIntegrity|null>(null),[error,setError]=useState<string|null>(null),[loading,setLoading]=useState(false);
 const load=useCallback(async()=>{if(!scope.organizationId)return;setLoading(true);try{const input:{pageSize:number;action?:string;branchId?:string}={pageSize:100};if(action)input.action=action;if(scope.branchId)input.branchId=scope.branchId;const page=await getAuditEvents(scope.organizationId,input);setItems(page.items);setError(null);}catch{setError("Audit events could not be loaded.");}finally{setLoading(false)}},[action,scope.branchId,scope.organizationId]);
 useEffect(()=>{if(!scope.organizationId)return;queueMicrotask(()=>void load());},[load,scope.organizationId]);
 async function verify(){if(!scope.organizationId)return;setLoading(true);try{setIntegrity(await verifyAuditChain(scope.organizationId));setError(null);}catch{setError("Audit integrity verification failed.");}finally{setLoading(false)}}
 return <section className="manager-content"><div className="manager-title"><div><span className="eyebrow">AUDIT</span><h1>Audit trail</h1><p>Immutable tenant activity evidence with hash-chain integrity verification.</p></div><button onClick={()=>void verify()} disabled={loading||!scope.organizationId}>Verify chain</button></div>
 <OperationsScopeSelector scope={scope}/><div className="panel"><label>Action filter<input value={action} onChange={e=>setAction(e.target.value)} maxLength={120}/></label><button onClick={()=>void load()} disabled={loading||!scope.organizationId}>Refresh</button></div>
 {integrity?<p className={integrity.isValid?"muted":"error-banner"}>Integrity: {integrity.isValid?"valid":"INVALID"} · {integrity.verifiedEvents} event(s){integrity.lastSequence?` · through #${integrity.lastSequence}`:""}</p>:null}{error?<p className="error-banner">{error}</p>:null}
 <div className="data-list">{items.map(item=><article key={item.id}><strong>#{item.sequence} · {item.action}</strong><span>{item.actorSubject} · {item.outcome} · {new Date(item.occurredAt).toLocaleString()}</span><span>{item.targetType}{item.targetId?` · ${item.targetId}`:""}{item.branchId?` · branch ${item.branchId.slice(0,8)}`:""}</span><code>{item.eventHash}</code></article>)}</div>
 {!loading&&scope.organizationId&&items.length===0?<p className="muted">No audit events were returned.</p>:null}</section>;
}
