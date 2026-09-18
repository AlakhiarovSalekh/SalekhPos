import { describe, expect, it } from "vitest";
import { canUseInventoryMovement, hasPermission, permissions } from "../../src/permissions/policy";

describe("mobile permission policy", () => {
  it("uses exact server permission names", () => {
    const authorization = { roles: ["Owner"], permissions: [permissions.inventoryView, permissions.inventoryAdjust, permissions.salesView, permissions.notificationsManage] };
    expect(hasPermission(authorization, permissions.inventoryView)).toBe(true);
    expect(canUseInventoryMovement(authorization)).toBe(true);
    expect(hasPermission(authorization, permissions.salesView)).toBe(true);
    expect(permissions.salesView).toBe("sales.view");
    expect(permissions.notificationsManage).toBe("notifications.manage");
    expect(hasPermission(authorization, permissions.notificationsManage)).toBe(true);
  });

  it("does not infer permissions from role names or related grants", () => {
    expect(canUseInventoryMovement({ roles: ["Owner"], permissions: [permissions.inventoryView] })).toBe(false);
    expect(hasPermission({ roles: [], permissions: ["inventory.receive"] }, permissions.inventoryAdjust)).toBe(false);
  });
});
