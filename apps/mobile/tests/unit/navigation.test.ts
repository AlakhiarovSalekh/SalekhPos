import { describe, expect, it } from "vitest";

import {
  canAccessNavigationItem,
  getVisibleNavigationItems,
  type NavigationItem,
} from "../../src/navigation/model";

const authorization = {
  roles: ["server-role"],
  permissions: ["server.permission.one", "server.permission.two"],
} as const;

describe("role-aware navigation", () => {
  it("uses only roles and permissions supplied by the session", () => {
    const items: readonly NavigationItem[] = [
      { id: "open", labelKey: "navigation.dashboard", route: "/dashboard" },
      {
        id: "permitted",
        labelKey: "navigation.scanner",
        route: "/scanner",
        requiredPermissions: ["server.permission.one"],
      },
      {
        id: "denied",
        labelKey: "navigation.scanner",
        route: "/scanner",
        requiredPermissions: ["server.permission.missing"],
      },
    ];

    expect(getVisibleNavigationItems(items, authorization).map((item) => item.id)).toEqual([
      "open",
      "permitted",
    ]);
  });

  it("requires all declared permissions and at least one declared role", () => {
    expect(
      canAccessNavigationItem(
        {
          id: "controlled",
          labelKey: "navigation.scanner",
          route: "/scanner",
          requiredPermissions: ["server.permission.one", "server.permission.two"],
          allowedRoles: ["another-role", "server-role"],
        },
        authorization,
      ),
    ).toBe(true);
  });
});
