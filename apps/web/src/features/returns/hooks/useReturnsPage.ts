"use client";

import { useEffect, useState } from "react";
import { ApiError } from "@/features/sales/api";
import { getReturns } from "../api";
import type { ReturnPage } from "../types";

type State = { key: string; status: "loading" } | { key: string; status: "ready"; page: ReturnPage } | { key: string; status: "error"; error: ApiError };

export function useReturnsPage(organizationId: string, branchId: string, after?: string) {
  const key = `${organizationId}:${branchId}:${after ?? ""}`;
  const [state, setState] = useState<State>({ key, status: "loading" });
  useEffect(() => {
    const controller = new AbortController();
    getReturns(organizationId, branchId, after, controller.signal)
      .then(page => setState({ key, status: "ready", page }))
      .catch(error => { if (!controller.signal.aborted) setState({ key, status: "error", error: error instanceof ApiError ? error : new ApiError("unexpected", "Returns could not be loaded.", false) }); });
    return () => controller.abort();
  }, [after, branchId, key, organizationId]);
  return state.key === key ? state : { key, status: "loading" as const };
}
