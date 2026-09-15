import { getCsrfToken, requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseCustomer, parseCustomerPage, parseEmployee, parseEmployeePage, parseOperationalReport, parsePurchaseOrder, parsePurchaseOrderPage, parseSupplier, parseSupplierPage, parseStockTransfer, parseStockTransferPage, parsePromotion, parsePromotionPage, parsePromotionEvaluation, parseLoyaltyAccount, parseLoyaltyAccountPage, parseLoyaltyPointsResult } from "./parsers";
import type { Customer, Employee, OperationalReport, Page, PurchaseOrder, Supplier, StockTransfer, Promotion, PromotionEvaluation, LoyaltyAccount, LoyaltyPointsResult } from "./types";

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
export const getStockTransfers=(o:string,b:string):Promise<Page<StockTransfer>>=>requestJson(`${branch(o,b)}/stock-transfers?pageSize=100`,parseStockTransferPage);
export const createStockTransfer=(o:string,b:string,input:{destinationBranchId:string;reference?:string;lines:{productId:string;quantity:number}[]})=>mutation(`${branch(o,b)}/stock-transfers`,input,parseStockTransfer,true);
export const changeStockTransferStatus=(o:string,b:string,transfer:StockTransfer,action:"dispatch"|"receive"|"cancel")=>mutation(`${branch(o,b)}/stock-transfers/${uuid(transfer.id,"transfer")}/${action}`,{expectedVersion:transfer.version},parseStockTransfer);
export const getPromotions=(o:string,b:string):Promise<Page<Promotion>>=>requestJson(`${branch(o,b)}/promotions?pageSize=100`,parsePromotionPage);
export const createPromotion=(o:string,b:string,input:{code:string;name:string;branchId?:string;discountKind:"percentage"|"fixed";value:number;currency?:string;minimumSubtotal:number;startsAt:string;endsAt?:string})=>mutation(`${branch(o,b)}/promotions`,input,parsePromotion,true);
export const deactivatePromotion=(o:string,b:string,promotion:Promotion)=>mutation(`${branch(o,b)}/promotions/${uuid(promotion.id,"promotion")}/deactivate`,{expectedVersion:promotion.version},parsePromotion);
export const evaluatePromotions=(o:string,b:string,subtotal:number,currency:string,at:string):Promise<PromotionEvaluation>=>{const q=new URLSearchParams({subtotal:String(subtotal),currency,at});return requestJson(`${branch(o,b)}/promotions/evaluate?${q}`,parsePromotionEvaluation);};
export const getLoyaltyAccounts=(o:string):Promise<Page<LoyaltyAccount>>=>requestJson(`${org(o)}/loyalty/accounts?pageSize=100`,parseLoyaltyAccountPage);
export const openLoyaltyAccount=(o:string,customerId:string)=>mutation(`${org(o)}/loyalty/accounts`,{customerId:uuid(customerId,"customer")},parseLoyaltyAccount,true);
export const changeLoyaltyPoints=(o:string,accountId:string,action:"earn"|"redeem",points:number,reason:string):Promise<LoyaltyPointsResult>=>mutation(`${org(o)}/loyalty/accounts/${uuid(accountId,"loyalty account")}/${action}`,{points,reason},parseLoyaltyPointsResult,true);
