"use client";

import { useEffect, useMemo, useState } from "react";
import { listOrganizations, type AccessibleOrganization } from "@/features/products/api";
import { getAllBranches } from "@/features/sales/api";
import type { Branch } from "@/features/sales/types";

export type OperationsScope = Readonly<{
  organizations: readonly AccessibleOrganization[];
  organizationId: string;
  branches: readonly Branch[];
  branchId: string;
  loading: boolean;
  error: string | null;
  setOrganizationId: (value: string) => void;
  setBranchId: (value: string) => void;
}>;

async function allOrganizations(signal: AbortSignal): Promise<readonly AccessibleOrganization[]> {
  const items: AccessibleOrganization[] = [];
  let after: string | null = null;
  for (let page = 0; page < 20; page++) {
    const result = await listOrganizations(after, signal);
    items.push(...result.items);
    if (!result.nextCursor) return items;
    if (result.nextCursor === after) throw new Error("Repeated organization cursor");
    after = result.nextCursor;
  }
  throw new Error("Too many organizations for a single selector");
}

export function useOperationsScope(): OperationsScope {
  const [organizations, setOrganizations] = useState<readonly AccessibleOrganization[]>([]);
  const [organizationId, setOrganizationIdState] = useState("");
  const [branches, setBranches] = useState<readonly Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [organizationsLoaded, setOrganizationsLoaded] = useState(false);
  const [branchesFor, setBranchesFor] = useState("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    allOrganizations(controller.signal).then(items => {
      if (controller.signal.aborted) return;
      setOrganizations(items);
      setOrganizationIdState(current => items.some(item => item.id === current) ? current : (items[0]?.id ?? ""));
      setOrganizationsLoaded(true);
    }).catch(() => { if (!controller.signal.aborted) { setError("Organizations could not be loaded."); setOrganizationsLoaded(true); } });
    return () => controller.abort();
  }, []);
  useEffect(() => {
    if (!organizationId) return;
    const controller = new AbortController();
    getAllBranches(organizationId, controller.signal).then(items => {
      if (controller.signal.aborted) return;
      setBranches(items);
      setBranchId(current => items.some(item => item.id === current) ? current : (items[0]?.id ?? ""));
      setBranchesFor(organizationId);
    }).catch(() => { if (!controller.signal.aborted) { setError("Branches could not be loaded."); setBranchesFor(organizationId); } });
    return () => controller.abort();
  }, [organizationId]);

  const setOrganizationId = (value: string) => {
    setOrganizationIdState(value);
    setBranches([]);
    setBranchId("");
    setBranchesFor("");
    setError(null);
  };
  const loading = !organizationsLoaded || Boolean(organizationId && branchesFor !== organizationId);
  return useMemo(() => ({
    organizations, organizationId, branches, branchId, loading, error,
    setOrganizationId, setBranchId,
  }), [organizations, organizationId, branches, branchId, loading, error]);
}
