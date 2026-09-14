import type { SessionAuthorization } from "@/security/session";

export const permissions = Object.freeze({
  branchesView: "branches.view",
  productsView: "products.view",
  inventoryView: "inventory.view",
  inventoryAdjust: "inventory.adjust",
} as const);

export function hasPermission(authorization: SessionAuthorization, permission: string): boolean {
  return authorization.permissions.includes(permission);
}

export function canUseInventoryMovement(authorization: SessionAuthorization): boolean {
  return hasPermission(authorization, permissions.inventoryAdjust);
}
