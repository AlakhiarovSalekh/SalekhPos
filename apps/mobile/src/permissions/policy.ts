import type { SessionAuthorization } from "@/security/session";

export const permissions = Object.freeze({
  branchesView: "branches.view",
  productsView: "products.view",
  inventoryView: "inventory.view",
  inventoryAdjust: "inventory.adjust",
  pricingView: "pricing.view",
  pricingManage: "pricing.manage",
  storesView: "stores.view",
  storesManage: "stores.manage",
  shiftsView: "shifts.view",
  paymentsView: "payments.view",
} as const);

export function hasPermission(authorization: SessionAuthorization, permission: string): boolean {
  return authorization.permissions.includes(permission);
}

export function hasAnyPermission(authorization: SessionAuthorization, required: readonly string[]): boolean {
  return required.some(permission => hasPermission(authorization, permission));
}

export function canUseInventoryMovement(authorization: SessionAuthorization): boolean {
  return hasPermission(authorization, permissions.inventoryAdjust);
}

export function canManagePricing(authorization: SessionAuthorization): boolean {
  return hasPermission(authorization, permissions.pricingManage);
}

export function canManageRegisters(authorization: SessionAuthorization): boolean {
  return hasPermission(authorization, permissions.storesManage);
}
