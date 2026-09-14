import { assertUuid } from "@salekhpos/packages-api-client";
import type { BranchSummary } from "@/api/contracts";

export type WorkspaceState = Readonly<{
  organizationId: string;
  branch: BranchSummary | null;
}>;

export function createWorkspaceState(organizationId: string): WorkspaceState {
  return Object.freeze({ organizationId: assertUuid(organizationId, "organizationId"), branch: null });
}

export function selectWorkspaceBranch(state: WorkspaceState, branch: BranchSummary): WorkspaceState {
  assertUuid(branch.id, "branchId");
  return Object.freeze({ organizationId: state.organizationId, branch });
}

export function clearWorkspaceBranch(state: WorkspaceState): WorkspaceState {
  return Object.freeze({ organizationId: state.organizationId, branch: null });
}
