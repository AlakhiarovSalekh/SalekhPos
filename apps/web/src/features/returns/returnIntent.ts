import type { ReturnDetail, ReturnRequest } from "./types";

export type ReturnIntent =
  | { state: "idle" }
  | { state: "submitting" | "uncertain"; operationId: string; request: ReturnRequest; message?: string }
  | { state: "failed"; message: string }
  | { state: "succeeded"; result: ReturnDetail };

export function beginIntent(current: ReturnIntent, request: ReturnRequest, createId: () => string): ReturnIntent {
  if (current.state === "submitting" || current.state === "uncertain") {
    return { state: "submitting", operationId: current.operationId, request: current.request };
  }
  return { state: "submitting", operationId: createId(), request };
}

export function uncertainIntent(current: ReturnIntent, message: string): ReturnIntent {
  if (current.state !== "submitting") throw new Error("No return intent is in progress");
  return { ...current, state: "uncertain", message };
}
