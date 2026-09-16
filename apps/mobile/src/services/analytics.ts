import { assertUuid, branchPath, organizationPath, type ApiClient } from "@salekhpos/packages-api-client";
import { AnalyticsContractError, parseAnalyticsOverview, parseSalesTrend, parseStoreComparison } from "@/api/analyticsContracts";

function required<T>(value:T|undefined):T{if(value===undefined)throw new AnalyticsContractError("response");return value}
function iso(value:string,label:string){const date=new Date(value);if(!Number.isFinite(date.getTime()))throw new TypeError(`${label} is invalid.`);return date.toISOString()}
function windowQuery(from:string,to:string){const f=iso(from,"From"),t=iso(to,"To");if(f>=t)throw new TypeError("Analytics window is invalid.");return {from:f,to:t};}

export function createAnalyticsService(client:ApiClient){return Object.freeze({
 async overview(org:string,branch:string,from:string,to:string,signal?:AbortSignal){const o=assertUuid(org,"organizationId"),b=assertUuid(branch,"branchId");return parseAnalyticsOverview(required(await client.get<unknown>(branchPath(o,b,"analytics","overview"),{query:windowQuery(from,to),...(signal?{signal}:{})})));},
 async trend(org:string,branch:string,from:string,to:string,signal?:AbortSignal){const o=assertUuid(org,"organizationId"),b=assertUuid(branch,"branchId");return parseSalesTrend(required(await client.get<unknown>(branchPath(o,b,"analytics","sales-trend"),{query:windowQuery(from,to),...(signal?{signal}:{})})));},
 async stores(org:string,from:string,to:string,signal?:AbortSignal){const o=assertUuid(org,"organizationId");return parseStoreComparison(required(await client.get<unknown>(organizationPath(o,"analytics","stores"),{query:windowQuery(from,to),...(signal?{signal}:{})})));},
 });}
export type AnalyticsService=ReturnType<typeof createAnalyticsService>;
