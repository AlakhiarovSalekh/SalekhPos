export type Customer = { id:string; code:string; displayName:string; email:string|null; phone:string|null; isActive:boolean; version:number; createdAt:string; updatedAt:string };
export type Supplier = { id:string; code:string; name:string; taxId:string|null; email:string|null; phone:string|null; isActive:boolean; version:number; createdAt:string; updatedAt:string };
export type Employee = { id:string; branchId:string; code:string; displayName:string; email:string|null; phone:string|null; jobTitle:string; isActive:boolean; version:number; createdAt:string; updatedAt:string };
export type PurchaseLine = { productId:string; quantity:number; unitCost:number; lineTotal:number };
export type PurchaseOrder = { id:string; branchId:string; supplierId:string; status:"draft"|"submitted"|"approved"|"cancelled"; currency:string; reference:string|null; total:number; version:number; createdAt:string; updatedAt:string; lines:PurchaseLine[] };
export type OperationalReport = { organizationId:string; branchId:string; from:string; to:string; currency:string|null; salesCount:number; salesGross:number; returnCount:number; returnsTotal:number; netSales:number; purchaseOrderCount:number; purchaseOrderTotal:number; openShiftCount:number };
export type Page<T> = { items:T[]; nextCursor:string|null };
