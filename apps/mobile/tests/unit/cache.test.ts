import { describe, expect, it } from "vitest";
import { createMemoryReadThroughCache } from "../../src/offline/cache";

describe("offline-ready read cache boundary", () => {
  it("isolates organization and branch data and removes only the requested scope", async () => {
    const cache = createMemoryReadThroughCache();
    const organization = { organizationId: "org-a" };
    const branchA = { organizationId: "org-a", branchId: "branch-a" };
    const branchB = { organizationId: "org-a", branchId: "branch-b" };
    await cache.write(organization, "products", ["p"], 1);
    await cache.write(branchA, "stock", ["a"], 2);
    await cache.write(branchB, "stock", ["b"], 3);
    expect(await cache.read(branchA, "stock")).toEqual({ value: ["a"], storedAt: 2 });
    await cache.removeScope(branchA);
    expect(await cache.read(branchA, "stock")).toBeNull();
    expect(await cache.read(branchB, "stock")).not.toBeNull();
    expect(await cache.read(organization, "products")).not.toBeNull();
  });
});
