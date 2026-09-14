import { parseAccess, parseMovement, parseSession, parseStockPage, type MovementInput } from "./types";

export class SafeApiError extends Error {
  constructor(public readonly status: number | null, message: string, public readonly code: string | null = null) { super(message); }
}
const safeMessages: Record<number, string> = { 400: "Check the information and try again.", 401: "Your session has ended. Sign in again.", 403: "You do not have permission for this action.", 404: "The requested inventory context is unavailable.", 409: "This operation conflicts with the current inventory state.", 429: "Too many requests. Wait a moment and retry.", 503: "Inventory is temporarily unavailable. You can safely retry." };

async function request(path: string, init?: RequestInit): Promise<unknown> {
  let response: Response;
  try { response = await fetch(path, { ...init, cache: "no-store", credentials: "same-origin" }); }
  catch { throw new SafeApiError(null, "We could not confirm whether the request completed. Retry safely."); }
  if (!response.ok) {
    let code: string | null = null;
    try { const body = await response.json() as { code?: unknown }; if (typeof body.code === "string") code = body.code; } catch { /* response is intentionally not exposed */ }
    throw new SafeApiError(response.status, safeMessages[response.status] ?? "Inventory could not be loaded. Try again.", code);
  }
  try { return await response.json(); } catch { throw new SafeApiError(response.status, "The server returned an invalid response."); }
}
const base = (organizationId: string) => `/bff/v1/organizations/${encodeURIComponent(organizationId)}/inventory`;
export async function getSession() { return parseSession(await request("/auth/session")); }
export async function getAccess(organizationId: string) { return parseAccess(await request(`${base(organizationId)}/access`)); }
export async function getStock(organizationId: string, branchId: string, after?: string) {
  const query = new URLSearchParams({ pageSize: "50" }); if (after) query.set("after", after);
  return parseStockPage(await request(`${base(organizationId)}/branches/${encodeURIComponent(branchId)}/stock?${query}`));
}
export async function createMovement(organizationId: string, branchId: string, input: MovementInput, key: string, csrfToken: string) {
  return parseMovement(await request(`${base(organizationId)}/branches/${encodeURIComponent(branchId)}/movements`, { method: "POST", headers: { "Content-Type": "application/json", "Idempotency-Key": key, "X-CSRF-Token": csrfToken }, body: JSON.stringify(input) }));
}
