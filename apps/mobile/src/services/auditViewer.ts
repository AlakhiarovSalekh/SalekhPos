import { assertUuid, organizationPath, type ApiClient } from "@salekhpos/packages-api-client";
import { parseAuditIntegrity, parseAuditPage } from "@/api/auditContracts";
export function createAuditViewer(client:ApiClient){return Object.freeze({
 async list(organizationId:string,input:{pageSize?:number;afterSequence?:number;action?:string;branchId?:string}={},signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId");const query:Record<string,string|number>={pageSize:input.pageSize??100};if(input.afterSequence)query.afterSequence=input.afterSequence;if(input.action?.trim())query.action=input.action.trim();if(input.branchId)query.branchId=assertUuid(input.branchId,"branchId");return parseAuditPage(await client.get<unknown>(organizationPath(o,"audit-events"),{query,...(signal?{signal}:{})}));},
 async verify(organizationId:string,limit=1000,signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId");return parseAuditIntegrity(await client.get<unknown>(organizationPath(o,"audit-events","verify"),{query:{limit},...(signal?{signal}:{})}));}
});}
export type AuditViewer=ReturnType<typeof createAuditViewer>;
