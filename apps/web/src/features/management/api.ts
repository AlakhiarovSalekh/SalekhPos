import { getCsrfToken, requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseCustomer, parseCustomerPage, parseEmployee, parseEmployeePage, parseOperationalReport, parsePurchaseOrder, parsePurchaseOrderPage, parseSupplier, parseSupplierPage } from "./parsers";
import type { Customer, Employee, OperationalReport, Page, PurchaseOrder, Supplier } from "./types";

const org=(id:string)=>`/bff/api/v1/organizations/${uuid(id,"organization")}`;
const branch=(o:string,b:string)=>`${org(o)}/branches/${uuid(b,"branch")}`;
async function mutation<T>(url:string,body:unknown,parser:(v:unknown)=>T,idempotent=false):Promise<T>{ const csrf=await getCsrfToken(); const headers:Record<string,string>={"content-type":"application/json","X-CSRF-TOKEN":csrf}; if(idempotent) headers["Idempotency-Key"]=crypto.randomUUID(); return requestJson(url,parser,{method:"POST",headers,body:JSON.stringify(body)}); }
export const getCustomers=(o:string):Promise<Page<Customer>>=>requestJson(`${org(o)}/customers?pageSize=100`,parseCustomerPage);
export const createCustomer=(o:string,input:{code:string;displayName:string;email?:string;phone?:string})=>mutation(`${org(o)}/customers`,input,parseCustomer,true);
export const getSuppliers=(o:string):Promise<Page<Supplier>>=>requestJson(`${org(o)}/suppliers?pageSize=100`,parseSupplierPage);
export const createSupplier=(o:string,input:{code:string;name:string;taxId?:string;email?:string;phone?:string})=>mutation(`${org(o)}/suppliers`,input,parseSupplier,true);
export const getEmployees=(o:string,b:string):Promise<Page<Employee>>=>requestJson(`${branch(o,b)}/employees?pageSize=100`,parseEmployeePage);
export const createEmployee=(o:string,b:string,input:{code:string;displayName:string;email?:string;phone?:string;jobTitle:string})=>mutation(`${branch(o,b)}/employees`,input,parseEmployee,true);
export const getPurchaseOrders=(o:string,b:string):Promise<Page<PurchaseOrder>>=>requestJson(`${branch(o,b)}/purchase-orders?pageSize=100`,parsePurchaseOrderPage);
export const createPurchaseOrder=(o:string,b:string,input:{supplierId:string;currency:string;reference?:string;lines:{productId:string;quantity:number;unitCost:number}[]})=>mutation(`${branch(o,b)}/purchase-orders`,input,parsePurchaseOrder,true);
export const changePurchaseStatus=(o:string,b:string,order:PurchaseOrder,action:"submit"|"approve"|"cancel")=>mutation(`${branch(o,b)}/purchase-orders/${uuid(order.id,"order")}/${action}`,{expectedVersion:order.version},parsePurchaseOrder);
export const getOperationalReport=(o:string,b:string,from:string,to:string):Promise<OperationalReport>=>{ const q=new URLSearchParams({from,to}); return requestJson(`${branch(o,b)}/reports/operational-summary?${q}`,parseOperationalReport); };
