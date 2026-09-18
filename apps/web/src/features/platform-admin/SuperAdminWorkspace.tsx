"use client";
import Link from"next/link";
import{useEffect,useRef,useState}from"react";
import{mutationIntent,type MutationIntent}from"@/features/operations/idempotency";
import{getPlatformAuthority,registerSuperAdmin,revokeSuperAdmin}from"./api";
import type{PlatformAuthority,SuperAdminRecord}from"./types";

export function SuperAdminWorkspace(){
  const[authority,setAuthority]=useState<PlatformAuthority|null>(null),[subject,setSubject]=useState(""),
    [reason,setReason]=useState("Administrative access change"),[adminId,setAdminId]=useState(""),
    [last,setLast]=useState<SuperAdminRecord|null>(null),[message,setMessage]=useState(""),[busy,setBusy]=useState(false),
    registerIntent=useRef<MutationIntent|null>(null),revokeIntent=useRef<MutationIntent|null>(null);
  useEffect(()=>{const controller=new AbortController();getPlatformAuthority(controller.signal)
    .then(v=>setAuthority(v)).catch(()=>setMessage("Platform authority could not be loaded."));return()=>controller.abort()},[]);
  async function register(){
    if(!authority?.isRoot)return;registerIntent.current=mutationIntent(["register-super-admin",subject.trim(),reason.trim()],registerIntent.current);
    setBusy(true);try{const value=await registerSuperAdmin(subject,reason,registerIntent.current.idempotencyKey);setLast(value);setAdminId(value.id);registerIntent.current=null;setMessage("")}
    catch{setMessage("Super Admin registration failed. Recent MFA and root authority are required.")}finally{setBusy(false)}
  }
  async function revoke(){
    if(!authority?.isRoot)return;revokeIntent.current=mutationIntent(["revoke-super-admin",adminId.trim(),reason.trim()],revokeIntent.current);
    setBusy(true);try{const value=await revokeSuperAdmin(adminId,reason,revokeIntent.current.idempotencyKey);setLast(value);revokeIntent.current=null;setMessage("")}
    catch{setMessage("Super Admin revocation failed. Verify the target and recent MFA.")}finally{setBusy(false)}
  }
  if(authority&&!authority.isSuperAdmin)return <main className="workspace"><section className="workspace-content"><h1>Platform access denied</h1><p>This account has no active platform authority.</p><Link href="/dashboard">Back to workspace</Link></section></main>;
  return <main className="manager-shell"><header className="manager-header"><Link className="brand" href="/dashboard">Salekh<span>Pos</span></Link><nav><Link href="/super-admin/overview">Authority</Link><Link href="/super-admin/system-health">System health</Link></nav></header>
    <section className="manager-content"><div className="manager-title"><div><span className="eyebrow">PLATFORM CONTROL</span><h1>Super Admin authority</h1><p>Root-only authority changes require a recent MFA session and server-side audit evidence.</p></div></div>
    {message?<p className="error-banner">{message}</p>:null}
    <div className="split-grid"><div className="panel"><h2>Current authority</h2>{authority?<><p><strong>{authority.isRoot?"Original Root":authority.isSuperAdmin?"Super Admin":"No authority"}</strong></p><p>Root: {authority.isRoot?"yes":"no"} · Super Admin: {authority.isSuperAdmin?"yes":"no"}</p></>:<p className="muted">Checking authority…</p>}<Link className="secondary-button" href="/super-admin/system-health">Open system health</Link></div>
    <div className="panel"><h2>Root authority operations</h2><label>Target subject<input value={subject} maxLength={256} onChange={e=>setSubject(e.target.value)}/></label><label>Reason<textarea value={reason} maxLength={1000} onChange={e=>setReason(e.target.value)}/></label><button disabled={busy||!authority?.isRoot||!subject.trim()} onClick={()=>void register()}>Register Super Admin</button><label>Admin UUID<input value={adminId} onChange={e=>setAdminId(e.target.value)}/></label><button disabled={busy||!authority?.isRoot||!adminId.trim()} onClick={()=>void revoke()}>Revoke Super Admin</button>{last?<article className="data-list-item"><strong>{last.subject} · {last.isActive?"active":"revoked"}</strong><span>{last.id}</span><span>{last.isRoot?"Original Root":"Super Admin"}</span></article>:null}</div></div></section></main>
}
