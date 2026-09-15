import { requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseAuditIntegrity, parseAuditPage } from "./parsers";
import type { AuditEventPage, AuditIntegrity } from "./types";

const root=(organizationId:string)=>`/bff/api/v1/organizations/${uuid(organizationId,"organization")}/audit-events`;
export function getAuditEvents(organizationId:string,input:{pageSize?:number;afterSequence?:number;action?:string;branchId?:string}={}):Promise<AuditEventPage>{
  const q=new URLSearchParams(); q.set("pageSize",String(input.pageSize??50));
  if(input.afterSequence) q.set("afterSequence",String(input.afterSequence));
  if(input.action?.trim()) q.set("action",input.action.trim());
  if(input.branchId) q.set("branchId",uuid(input.branchId,"branch"));
  return requestJson(`${root(organizationId)}?${q}`,parseAuditPage);
}
export function verifyAuditChain(organizationId:string,fromSequence?:number,limit=1000):Promise<AuditIntegrity>{
  const q=new URLSearchParams({limit:String(limit)}); if(fromSequence) q.set("fromSequence",String(fromSequence));
  return requestJson(`${root(organizationId)}/verify?${q}`,parseAuditIntegrity);
}
