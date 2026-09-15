import { randomUUID } from "expo-crypto";
import { assertUuid, branchPath, organizationPath, type ApiClient } from "@salekhpos/packages-api-client";
import { ManagementContractError, parseCustomer, parseCustomerPage, parseEmployee, parseEmployeePage, parseOperationalReport, parsePurchaseOrder, parsePurchaseOrderPage, parseSupplier, parseSupplierPage, type PurchaseOrderSummary } from "@/api/managementContracts";

function required<T>(value:T|undefined):T{if(value===undefined)throw new ManagementContractError("response");return value}
function pageSize(value:number){if(!Number.isInteger(value)||value<1||value>100)throw new TypeError("Page size is invalid.");return value}
function clean(value:string,max:number,label:string){const v=value.trim();if(!v||v.length>max||/[\u0000-\u001f\u007f]/u.test(v))throw new TypeError(`${label} is invalid.`);return v}
function code(value:string){const v=value.trim().toUpperCase();if(!/^[A-Z0-9_-]{1,32}$/u.test(v))throw new TypeError("Code is invalid.");return v}
function nullable(value:string|undefined,max:number){if(!value?.trim())return null;return clean(value,max,"Value")}
export function createManagerBusiness(client:ApiClient){return Object.freeze({
 async listCustomers(organizationId:string,size=100,signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId");const v=await client.get<unknown>(organizationPath(o,"customers"),{query:{pageSize:pageSize(size)},...(signal?{signal}:{})});return parseCustomerPage(required(v));},
 async createCustomer(organizationId:string,input:{code:string;displayName:string;email?:string;phone?:string},signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId");const body={code:code(input.code),displayName:clean(input.displayName,200,"Name"),email:nullable(input.email,254),phone:nullable(input.phone,32)};return parseCustomer(required(await client.post<unknown>(organizationPath(o,"customers"),{body,idempotencyKey:randomUUID(),...(signal?{signal}:{})})));},
 async listSuppliers(organizationId:string,size=100,signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId");const v=await client.get<unknown>(organizationPath(o,"suppliers"),{query:{pageSize:pageSize(size)},...(signal?{signal}:{})});return parseSupplierPage(required(v));},
 async createSupplier(organizationId:string,input:{code:string;name:string;taxId?:string;email?:string;phone?:string},signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId");const body={code:code(input.code),name:clean(input.name,200,"Name"),taxId:nullable(input.taxId,64),email:nullable(input.email,254),phone:nullable(input.phone,32)};return parseSupplier(required(await client.post<unknown>(organizationPath(o,"suppliers"),{body,idempotencyKey:randomUUID(),...(signal?{signal}:{})})));},
 async listEmployees(organizationId:string,branchId:string,size=100,signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId"),b=assertUuid(branchId,"branchId");const v=await client.get<unknown>(branchPath(o,b,"employees"),{query:{pageSize:pageSize(size)},...(signal?{signal}:{})});const r=parseEmployeePage(required(v));if(r.items.some(x=>x.branchId!==b))throw new ManagementContractError("employees");return r;},
 async createEmployee(organizationId:string,branchId:string,input:{code:string;displayName:string;email?:string;phone?:string;jobTitle:string},signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId"),b=assertUuid(branchId,"branchId");const body={code:code(input.code),displayName:clean(input.displayName,200,"Name"),email:nullable(input.email,254),phone:nullable(input.phone,32),jobTitle:clean(input.jobTitle,120,"Job title")};const r=parseEmployee(required(await client.post<unknown>(branchPath(o,b,"employees"),{body,idempotencyKey:randomUUID(),...(signal?{signal}:{})})));if(r.branchId!==b)throw new ManagementContractError("employee");return r;},
 async listPurchaseOrders(organizationId:string,branchId:string,size=100,signal?:AbortSignal){const o=assertUuid(organizationId,"organizationId"),b=assertUuid(branchId,"branchId");const v=await client.get<unknown>(branchPath(o,b,"purchase-orders"),{query:{pageSize:pageSize(size)},...(signal?{signal}:{})});const r=parsePurchaseOrderPage(required(v));if(r.items.some(x=>x.branchId!==b))throw new ManagementContractError("purchaseOrders");return r;},
 async createPurchaseOrder(organizationId:string,branchId:string,input:{supplierId:string;currency:string;reference?:string;lines:readonly {productId:string;quantity:number;unitCost:number}[]},signal?:AbortSignal){
  const o=assertUuid(organizationId,"organizationId"),b=assertUuid(branchId,"branchId"),supplierId=assertUuid(input.supplierId,"supplierId");
  if(!/^[A-Z]{3}$/u.test(input.currency)||input.lines.length<1||input.lines.length>500)throw new TypeError("Purchase order is invalid.");
  const lines=input.lines.map(x=>({productId:assertUuid(x.productId,"productId"),quantity:x.quantity,unitCost:x.unitCost}));
  if(lines.some(x=>!Number.isFinite(x.quantity)||x.quantity<=0||!Number.isFinite(x.unitCost)||x.unitCost<0))throw new TypeError("Purchase order line is invalid.");
  const body={supplierId,currency:input.currency,reference:input.reference?.trim()||null,lines};
  const r=parsePurchaseOrder(required(await client.post<unknown>(branchPath(o,b,"purchase-orders"),{body,idempotencyKey:randomUUID(),...(signal?{signal}:{})})));
  if(r.branchId!==b||r.supplierId!==supplierId)throw new ManagementContractError("purchaseOrder");return r;
 },
 async changePurchaseOrderStatus(organizationId:string,branchId:string,order:PurchaseOrderSummary,action:"submit"|"approve"|"cancel",signal?:AbortSignal){
  const o=assertUuid(organizationId,"organizationId"),b=assertUuid(branchId,"branchId"),id=assertUuid(order.id,"orderId");
  const r=parsePurchaseOrder(required(await client.post<unknown>(branchPath(o,b,"purchase-orders",id,action),{body:{expectedVersion:order.version},...(signal?{signal}:{})})));
  if(r.id!==id||r.branchId!==b)throw new ManagementContractError("purchaseOrder");return r;
 },
 async readOperationalReport(organizationId:string,branchId:string,from:string,to:string,signal?:AbortSignal){
  const o=assertUuid(organizationId,"organizationId"),b=assertUuid(branchId,"branchId");
  if(!Number.isFinite(Date.parse(from))||!Number.isFinite(Date.parse(to))||Date.parse(from)>=Date.parse(to))throw new TypeError("Report window is invalid.");
  const v=await client.get<unknown>(branchPath(o,b,"reports","operational-summary"),{query:{from,to},...(signal?{signal}:{})});const r=parseOperationalReport(required(v));
  if(r.organizationId!==o||r.branchId!==b)throw new ManagementContractError("report");return r;
 }
 });}
export type ManagerBusiness=ReturnType<typeof createManagerBusiness>;
