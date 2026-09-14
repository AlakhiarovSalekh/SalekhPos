import type { SessionAuthorization } from "@/security/session";

export type MobileRoute = "/dashboard" | "/organizations" | "/stores" | "/products" | "/inventory" | "/scanner";

export type NavigationItem = Readonly<{
  id: string;
  labelKey: "navigation.dashboard" | "navigation.organizations" | "navigation.stores" | "navigation.products" | "navigation.inventory" | "navigation.scanner";
  route: MobileRoute;
  requiredPermissions?: readonly string[];
  allowedRoles?: readonly string[];
}>;

export const mobileNavigationItems: readonly NavigationItem[] = Object.freeze([
  Object.freeze({
    id: "dashboard",
    labelKey: "navigation.dashboard" as const,
    route: "/dashboard" as const,
  }),
  Object.freeze({
    id: "organizations",
    labelKey: "navigation.organizations" as const,
    route: "/organizations" as const,
    requiredPermissions: ["branches.view"],
  }),
  Object.freeze({
    id: "stores",
    labelKey: "navigation.stores" as const,
    route: "/stores" as const,
    requiredPermissions: ["branches.view"],
  }),
  Object.freeze({
    id: "products",
    labelKey: "navigation.products" as const,
    route: "/products" as const,
    requiredPermissions: ["products.view"],
  }),
  Object.freeze({
    id: "inventory",
    labelKey: "navigation.inventory" as const,
    route: "/inventory" as const,
    requiredPermissions: ["inventory.view"],
  }),
  Object.freeze({
    id: "scanner",
    labelKey: "navigation.scanner" as const,
    route: "/scanner" as const,
    requiredPermissions: ["products.view"],
  }),
]);

export function canAccessNavigationItem(
  item: NavigationItem,
  authorization: SessionAuthorization,
): boolean {
  const permissionSet = new Set(authorization.permissions);
  const roleSet = new Set(authorization.roles);
  const hasPermissions =
    item.requiredPermissions === undefined ||
    item.requiredPermissions.every((permission) => permissionSet.has(permission));
  const hasRole =
    item.allowedRoles === undefined || item.allowedRoles.some((role) => roleSet.has(role));
  return hasPermissions && hasRole;
}

export function getVisibleNavigationItems(
  items: readonly NavigationItem[],
  authorization: SessionAuthorization,
): readonly NavigationItem[] {
  return items.filter((item) => canAccessNavigationItem(item, authorization));
}
