"use client";

import { useEffect, useState } from "react";
import { ApiError, getSales } from "../api";
import type { SalePage } from "../types";

type State = { key: string; status: "loading" } | { key: string; status: "ready"; page: SalePage } | { key: string; status: "error"; error: ApiError };

export function useSalesPage(organizationId: string, branchId: string, after?: string) {
  const key = `${organizationId}:${branchId}:${after ?? ""}`;
  const [state, setState] = useState<State>({ key, status: "loading" });
  useEffect(() => {
    const controller = new AbortController();
    getSales(organizationId, branchId, after, controller.signal)
      .then(page => setState({ key, status: "ready", page }))
      .catch(error => { if (!controller.signal.aborted) setState({ key, status: "error", error: error instanceof ApiError ? error : new ApiError("unexpected", "Sales could not be loaded.", false) }); });
    return () => controller.abort();
  }, [after, branchId, key, organizationId]);
  return state.key === key ? state : { key, status: "loading" as const };
}
