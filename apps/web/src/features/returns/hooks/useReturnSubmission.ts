"use client";

import { useCallback, useState } from "react";
import { ApiError, getCsrfToken } from "@/features/sales/api";
import { submitReturn } from "../api";
import { beginIntent, uncertainIntent, type ReturnIntent } from "../returnIntent";
import type { ReturnRequest } from "../types";

export function useReturnSubmission(organizationId: string, branchId: string) {
  const [intent, setIntent] = useState<ReturnIntent>({ state: "idle" });

  const submit = useCallback(async (request: ReturnRequest) => {
    const active = beginIntent(intent, request, () => crypto.randomUUID());
    if (active.state !== "submitting") return;
    setIntent(active);
    try {
      const csrf = await getCsrfToken();
      const result = await submitReturn(organizationId, branchId, active.request, active.operationId, csrf);
      setIntent({ state: "succeeded", result });
    } catch (error) {
      if (error instanceof ApiError && error.retryable) setIntent(uncertainIntent(active, error.message));
      else setIntent({ state: "failed", message: error instanceof ApiError ? error.message : "The return could not be completed." });
    }
  }, [branchId, intent, organizationId]);

  const reset = useCallback(() => setIntent({ state: "idle" }), []);
  return { intent, submit, reset };
}
