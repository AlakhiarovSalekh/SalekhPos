import { createContext, useCallback, useContext, useEffect, useMemo, useState, type PropsWithChildren } from "react";

import type { BranchSummary } from "@/api/contracts";
import { useSession } from "@/state/SessionContext";
import { clearWorkspaceBranch, createWorkspaceState, selectWorkspaceBranch, type WorkspaceState } from "@/state/workspaceState";

export { clearWorkspaceBranch, createWorkspaceState, selectWorkspaceBranch } from "@/state/workspaceState";
export type { WorkspaceState } from "@/state/workspaceState";

type WorkspaceContextValue = Readonly<{
  workspace: WorkspaceState;
  selectBranch: (branch: BranchSummary) => void;
  clearBranch: () => void;
}>;

const WorkspaceContext = createContext<WorkspaceContextValue | null>(null);

export function WorkspaceProvider({ children }: PropsWithChildren) {
  const { session } = useSession();
  if (session === null) throw new Error("WorkspaceProvider requires an authenticated session.");
  const [workspace, setWorkspace] = useState(() => createWorkspaceState(session.organizationId));
  useEffect(() => setWorkspace(createWorkspaceState(session.organizationId)), [session.organizationId, session.subject]);
  const selectBranch = useCallback((branch: BranchSummary) => setWorkspace((current) => selectWorkspaceBranch(current, branch)), []);
  const clearBranch = useCallback(() => setWorkspace((current) => clearWorkspaceBranch(current)), []);
  const value = useMemo(() => ({ workspace, selectBranch, clearBranch }), [workspace, selectBranch, clearBranch]);
  return <WorkspaceContext.Provider value={value}>{children}</WorkspaceContext.Provider>;
}

export function useWorkspace(): WorkspaceContextValue {
  const value = useContext(WorkspaceContext);
  if (value === null) throw new Error("useWorkspace must be used within WorkspaceProvider.");
  return value;
}
