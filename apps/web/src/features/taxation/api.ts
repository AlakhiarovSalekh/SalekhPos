import { getCsrfToken, requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseTaxCalculation, parseTaxProfile, parseTaxProfilePage, parseTaxRate, parseTaxRates } from "./parsers";
import type { TaxCalculation, TaxProfile, TaxProfilePage, TaxRate } from "./types";

const root = (organizationId: string) => `/bff/api/v1/organizations/${uuid(organizationId,"organization")}/tax-profiles`;
async function mutation<T>(url:string, body:unknown, parser:(value:unknown)=>T, idempotent=false):Promise<T>{
  const csrf=await getCsrfToken(); const headers:Record<string,string>={"content-type":"application/json","X-CSRF-TOKEN":csrf};
  if(idempotent)headers["Idempotency-Key"]=crypto.randomUUID();
  return requestJson(url,parser,{method:"POST",headers,body:JSON.stringify(body)});
}
export function getTaxProfiles(organizationId:string,signal?:AbortSignal):Promise<TaxProfilePage>{
  return requestJson(`${root(organizationId)}?pageSize=100`,parseTaxProfilePage,signal?{signal}:undefined);
}
export function createTaxProfile(organizationId:string,input:{code:string;name:string;countryCode:string;pricesIncludeTax:boolean}):Promise<TaxProfile>{
  return mutation(root(organizationId),input,parseTaxProfile,true);
}
export function getTaxRates(organizationId:string,profileId:string,signal?:AbortSignal):Promise<readonly TaxRate[]>{
  return requestJson(`${root(organizationId)}/${uuid(profileId,"tax profile")}/rates`,parseTaxRates,signal?{signal}:undefined);
}
export function createTaxRate(organizationId:string,profileId:string,input:{branchId?:string;categoryCode:string;ratePercent:number;effectiveFrom:string;effectiveUntil?:string}):Promise<TaxRate>{
  const body={...input,branchId:input.branchId?uuid(input.branchId,"branch"):null,effectiveUntil:input.effectiveUntil||null};
  return mutation(`${root(organizationId)}/${uuid(profileId,"tax profile")}/rates`,body,parseTaxRate,true);
}
export function calculateTax(organizationId:string,input:{profileId:string;branchId:string;categoryCode:string;amount:number;at:string}):Promise<TaxCalculation>{
  return mutation(`${root(organizationId)}/calculate`,{...input,profileId:uuid(input.profileId,"tax profile"),branchId:uuid(input.branchId,"branch")},parseTaxCalculation);
}
