import { describe, expect, it } from "vitest";
import { clearWorkspaceBranch, createWorkspaceState, selectWorkspaceBranch } from "../../src/state/workspaceState";

const organizationId = "11111111-1111-4111-8111-111111111111";
const branch = { id: "22222222-2222-4222-8222-222222222222", businessId: "33333333-3333-4333-8333-333333333333", regionId: null, code: "TBS", name: "Tbilisi", timeZoneId: "Asia/Tbilisi" } as const;

describe("workspace state", () => {
  it("keeps the authenticated organization immutable while selecting a permitted branch", () => {
    const selected = selectWorkspaceBranch(createWorkspaceState(organizationId), branch);
    expect(selected).toEqual({ organizationId, branch });
    expect(clearWorkspaceBranch(selected)).toEqual({ organizationId, branch: null });
  });

  it("rejects malformed tenant and branch identifiers", () => {
    expect(() => createWorkspaceState("tenant-a")).toThrow();
    expect(() => selectWorkspaceBranch(createWorkspaceState(organizationId), { ...branch, id: "branch-a" })).toThrow();
  });
});
