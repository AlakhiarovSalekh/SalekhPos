import{getCsrfToken,requestJson}from"@/features/sales/api";
import{uuid}from"@/lib/boundedJson";
import{parsePlatformAuthority,parseSuperAdminRecord}from"./parsers";
import type{PlatformAuthority,SuperAdminRecord}from"./types";

const root="/bff/api/v1/platform";
export function getPlatformAuthority(signal?:AbortSignal):Promise<PlatformAuthority>{
  return requestJson(root+"/authority",parsePlatformAuthority,signal?{signal}:undefined)
}
async function headers(){return{"content-type":"application/json","X-CSRF-TOKEN":await getCsrfToken()}}
export async function registerSuperAdmin(subject:string,reason:string,operationId:string):Promise<SuperAdminRecord>{
  const s=subject.trim(),r=reason.trim();if(!s||s.length>256||!r||r.length>1000)throw new TypeError("Invalid platform operation.");
  return requestJson(root+"/super-admins",parseSuperAdminRecord,{method:"POST",headers:await headers(),
    body:JSON.stringify({operationId:uuid(operationId,"operationId"),subject:s,reason:r})})
}
export async function revokeSuperAdmin(adminId:string,reason:string,operationId:string):Promise<SuperAdminRecord>{
  const r=reason.trim();if(!r||r.length>1000)throw new TypeError("Invalid platform operation.");
  return requestJson(root+"/super-admins/"+uuid(adminId,"adminId")+"/revoke",parseSuperAdminRecord,{method:"POST",headers:await headers(),
    body:JSON.stringify({operationId:uuid(operationId,"operationId"),reason:r})})
}
