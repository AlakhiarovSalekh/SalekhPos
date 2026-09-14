"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { UUID_PATTERN } from "@/lib/boundedJson";
import { ApiError, getAllBranches } from "../api";
import type { Branch } from "../types";

export function ScopeSelector({ view, organizationId = "", branchId = "" }: { view: "sales" | "returns"; organizationId?: string; branchId?: string }) {
  const router = useRouter();
  const [organization, setOrganization] = useState(organizationId);
  const [branch, setBranch] = useState(branchId);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [state, setState] = useState<"idle" | "loading" | "ready" | "error" | "denied">(UUID_PATTERN.test(organizationId) ? "loading" : "idle");

  useEffect(() => {
    if (!UUID_PATTERN.test(organization) || organization === "00000000-0000-0000-0000-000000000000") return;
    const controller = new AbortController();
    getAllBranches(organization, controller.signal).then(items => {
      setBranches(items);
      setState("ready");
      setBranch(current => current && !items.some(item => item.id === current) ? "" : current);
    }).catch(error => { if (!controller.signal.aborted) setState(error instanceof ApiError && error.kind === "permission" ? "denied" : "error"); });
    return () => controller.abort();
  }, [organization]);

  const valid = UUID_PATTERN.test(organization) && UUID_PATTERN.test(branch) && branches.some(item => item.id === branch);
  return <section className="scope-panel" aria-labelledby="scope-title">
    <div><p className="eyebrow">DATA SCOPE</p><h2 id="scope-title">Organization and branch</h2><p>Server permissions still determine which branch data is visible.</p></div>
    <div className="scope-fields">
      <label>Organization ID<input value={organization} onChange={event => { const value = event.target.value.trim(); setOrganization(value); setBranch(""); setBranches([]); setState(UUID_PATTERN.test(value) ? "loading" : "idle"); }} placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" autoComplete="off" /></label>
      <label>Branch<select value={branch} onChange={event => setBranch(event.target.value)} disabled={state !== "ready"}>
        <option value="">{state === "loading" ? "Loading permitted branches…" : "Select a branch"}</option>
        {branches.map(item => <option value={item.id} key={item.id}>{item.code} — {item.name}</option>)}
      </select></label>
      <button className="button compact" type="button" disabled={!valid} onClick={() => router.push(`/${view}?organization=${encodeURIComponent(organization)}&branch=${encodeURIComponent(branch)}`)}>Open {view}</button>
    </div>
    {state === "denied" && <p className="inline-error" role="alert">This account cannot discover branches in that organization.</p>}
    {state === "error" && <p className="inline-error" role="alert">Branch context could not be loaded. Check the ID and try again.</p>}
  </section>;
}
