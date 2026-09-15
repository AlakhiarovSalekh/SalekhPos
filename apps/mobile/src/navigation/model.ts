import type { SessionAuthorization } from "@/security/session";

export type MobileRoute =
  | "/dashboard" | "/organizations" | "/stores" | "/products" | "/inventory" | "/scanner"
  | "/pricing" | "/registers" | "/reconciliation" | "/customers" | "/suppliers" | "/employees" | "/purchasing" | "/reports";

export type NavigationLabelKey =
  | "navigation.dashboard" | "navigation.organizations" | "navigation.stores"
  | "navigation.products" | "navigation.inventory" | "navigation.scanner"
  | "navigation.pricing" | "navigation.registers" | "navigation.reconciliation" | "navigation.customers" | "navigation.suppliers" | "navigation.employees" | "navigation.purchasing" | "navigation.reports";

export type NavigationItem = Readonly<{
  id: string;
  labelKey: NavigationLabelKey;
  route: MobileRoute;
  requiredPermissions?: readonly string[];
  requiredAnyPermissions?: readonly string[];
  allowedRoles?: readonly string[];
}>;

export const mobileNavigationItems: readonly NavigationItem[] = Object.freeze([
  Object.freeze({ id: "dashboard", labelKey: "navigation.dashboard" as const, route: "/dashboard" as const }),
  Object.freeze({ id: "organizations", labelKey: "navigation.organizations" as const, route: "/organizations" as const,
    requiredPermissions: ["branches.view"] }),
  Object.freeze({ id: "stores", labelKey: "navigation.stores" as const, route: "/stores" as const,
    requiredPermissions: ["branches.view"] }),
  Object.freeze({ id: "products", labelKey: "navigation.products" as const, route: "/products" as const,
    requiredPermissions: ["products.view"] }),
  Object.freeze({ id: "inventory", labelKey: "navigation.inventory" as const, route: "/inventory" as const,
    requiredPermissions: ["inventory.view"] }),
  Object.freeze({ id: "scanner", labelKey: "navigation.scanner" as const, route: "/scanner" as const,
    requiredPermissions: ["products.view"] }),
  Object.freeze({ id: "pricing", labelKey: "navigation.pricing" as const, route: "/pricing" as const,
    requiredAnyPermissions: ["pricing.view", "pricing.manage"] }),
  Object.freeze({ id: "registers", labelKey: "navigation.registers" as const, route: "/registers" as const,
    requiredAnyPermissions: ["stores.view", "stores.manage"] }),
  Object.freeze({ id: "reconciliation", labelKey: "navigation.reconciliation" as const, route: "/reconciliation" as const,
    requiredPermissions: ["shifts.view", "payments.view"] }),
  Object.freeze({ id: "customers", labelKey: "navigation.customers" as const, route: "/customers" as const, requiredPermissions: ["customers.view"] }),
  Object.freeze({ id: "suppliers", labelKey: "navigation.suppliers" as const, route: "/suppliers" as const, requiredPermissions: ["suppliers.view"] }),
  Object.freeze({ id: "employees", labelKey: "navigation.employees" as const, route: "/employees" as const, requiredPermissions: ["employees.view"] }),
  Object.freeze({ id: "purchasing", labelKey: "navigation.purchasing" as const, route: "/purchasing" as const, requiredPermissions: ["purchase_orders.view"] }),
  Object.freeze({ id: "reports", labelKey: "navigation.reports" as const, route: "/reports" as const, requiredPermissions: ["reports.view"] }),
]);

export function canAccessNavigationItem(
  item: NavigationItem,
  authorization: SessionAuthorization,
): boolean {
  const permissionSet = new Set(authorization.permissions);
  const roleSet = new Set(authorization.roles);
  const hasPermissions = item.requiredPermissions === undefined
    || item.requiredPermissions.every(permission => permissionSet.has(permission));
  const hasAnyPermission = item.requiredAnyPermissions === undefined
    || item.requiredAnyPermissions.some(permission => permissionSet.has(permission));
  const hasRole = item.allowedRoles === undefined || item.allowedRoles.some(role => roleSet.has(role));
  return hasPermissions && hasAnyPermission && hasRole;
}

export function getVisibleNavigationItems(
  items: readonly NavigationItem[],
  authorization: SessionAuthorization,
): readonly NavigationItem[] {
  return items.filter(item => canAccessNavigationItem(item, authorization));
}
