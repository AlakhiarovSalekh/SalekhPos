"use client";
import Link from"next/link";
import{useEffect,useState}from"react";
import type{PlatformHealth}from"./types";

async function probe(path:string,signal:AbortSignal):Promise<"healthy"|"unhealthy"|"unknown">{
  try{const response=await fetch(path,{cache:"no-store",credentials:"same-origin",signal});return response.ok?"healthy":"unhealthy"}catch(error){if(signal.aborted)throw error;return"unknown"}
}
export function SystemHealthWorkspace(){
  const[health,setHealth]=useState<PlatformHealth|null>(null);
  useEffect(()=>{const controller=new AbortController();Promise.all([probe("/health/live",controller.signal),probe("/health/ready",controller.signal)])
    .then(([live,ready])=>setHealth({live,ready,checkedAt:new Date().toISOString()})).catch(()=>{});return()=>controller.abort()},[]);
  return <main className="manager-shell"><header className="manager-header"><Link className="brand" href="/dashboard">Salekh<span>Pos</span></Link><nav><Link href="/super-admin/overview">Authority</Link><Link href="/super-admin/system-health">System health</Link></nav></header><section className="manager-content"><div className="manager-title"><div><span className="eyebrow">PLATFORM HEALTH</span><h1>Runtime readiness</h1><p>Live and ready probes reflect process health and critical authorized database dependencies.</p></div></div><div className="split-grid"><article className="metric-card"><strong>API liveness</strong><span>{health?.live??"checking"}</span></article><article className="metric-card"><strong>API readiness</strong><span>{health?.ready??"checking"}</span></article></div>{health?<p className="muted">Checked {new Date(health.checkedAt).toLocaleString()}</p>:null}</section></main>
}
