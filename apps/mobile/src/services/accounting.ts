import { assertUuid, branchPath, type ApiClient } from "@salekhpos/packages-api-client";
import { AccountingContractError, parseAccountingJournalPage, parseAccountingSummary } from "@/api/accountingContracts";

function required<T>(value:T|undefined):T{if(value===undefined)throw new AccountingContractError("response");return value}
function iso(value:string,label:string){const date=new Date(value);if(!Number.isFinite(date.getTime()))throw new TypeError(`${label} is invalid.`);return date.toISOString()}
function windowQuery(from:string,to:string){const f=iso(from,"From"),t=iso(to,"To");if(f>=t)throw new TypeError("Accounting window is invalid.");return {from:f,to:t};}

export function createAccountingService(client:ApiClient){return Object.freeze({
 async summary(org:string,branch:string,from:string,to:string,signal?:AbortSignal){const o=assertUuid(org,"organizationId"),b=assertUuid(branch,"branchId");return parseAccountingSummary(required(await client.get<unknown>(branchPath(o,b,"accounting","summary"),{query:windowQuery(from,to),...(signal?{signal}:{})})));},
 async journal(org:string,branch:string,from:string,to:string,pageSize=50,cursor?:string|null,signal?:AbortSignal){if(!Number.isInteger(pageSize)||pageSize<1||pageSize>100)throw new TypeError("Accounting page size is invalid.");const o=assertUuid(org,"organizationId"),b=assertUuid(branch,"branchId");const query:Record<string,string|number|boolean|undefined|null>={...windowQuery(from,to),pageSize,...(cursor?{cursor}:{})};return parseAccountingJournalPage(required(await client.get<unknown>(branchPath(o,b,"accounting","journal"),{query,...(signal?{signal}:{})})));},
 });}
export type AccountingService=ReturnType<typeof createAccountingService>;
