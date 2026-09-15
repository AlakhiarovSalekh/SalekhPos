import { uuid } from "@/lib/boundedJson";
import { ApiError, getCsrfToken, requestJson } from "@/features/sales/api";
import {
  parseCashMovements, parseClosedShift, parseClosedShiftPage, parsePaymentEventPage,
  parsePrice, parseRegister, parseRegisterPage, parseResolvedPrice, parseShift,
} from "./parsers";
import type {
  CashMovement, ClosedShift, ClosedShiftPage, PaymentEventPage, Price, PriceDraft,
  Register, RegisterDraft, RegisterPage, ResolvedPrice, Shift,
} from "./types";
import { validatePriceDraft, validateRegisterDraft } from "./validation";

function organizationPath(organizationId: string): string {
  return `/bff/api/v1/organizations/${uuid(organizationId, "organization id")}`;
}

function branchPath(organizationId: string, branchId: string): string {
  return `${organizationPath(organizationId)}/branches/${uuid(branchId, "branch id")}`;
}

function mutationHeaders(csrfToken: string, idempotencyKey: string): HeadersInit {
  return { "Content-Type": "application/json", "X-CSRF-Token": csrfToken, "Idempotency-Key": uuid(idempotencyKey, "idempotency key") };
}
export function getRegisters(organizationId: string, branchId: string, after?: string,
  signal?: AbortSignal): Promise<RegisterPage> {
  const query = new URLSearchParams({ pageSize: "100" });
  if (after) query.set("after", uuid(after, "register cursor"));
  return requestJson(`${branchPath(organizationId, branchId)}/registers?${query}`, parseRegisterPage, { signal });
}

export async function getAllRegisters(organizationId: string, branchId: string,
  signal?: AbortSignal): Promise<readonly Register[]> {
  const items: Register[] = [];
  let after: string | undefined;
  for (let page = 0; page < 20; page++) {
    const result = await getRegisters(organizationId, branchId, after, signal);
    items.push(...result.items);
    if (!result.nextCursor) return items;
    if (result.nextCursor === after) throw new ApiError("unexpected", "The server returned a repeated register cursor.", false);
    after = result.nextCursor;
  }
  throw new ApiError("unexpected", "This branch has too many registers to load safely in one view.", false);
}

export async function createRegister(organizationId: string, branchId: string, draft: RegisterDraft,
  idempotencyKey: string, signal?: AbortSignal): Promise<Register> {
  const validation = validateRegisterDraft(draft);
  if (validation) throw new TypeError(validation);
  const csrfToken = await getCsrfToken(signal);
  return requestJson(`${branchPath(organizationId, branchId)}/registers`, parseRegister, {
    method: "POST", signal, headers: mutationHeaders(csrfToken, idempotencyKey), body: JSON.stringify(draft),
  });
}

export async function schedulePrice(organizationId: string, draft: PriceDraft, idempotencyKey: string,
  signal?: AbortSignal): Promise<Price> {
  const validation = validatePriceDraft(draft);
  if (validation) throw new TypeError(validation);
  const csrfToken = await getCsrfToken(signal);
  return requestJson(`${organizationPath(organizationId)}/pricing/prices`, parsePrice, {
    method: "POST", signal, headers: mutationHeaders(csrfToken, idempotencyKey), body: JSON.stringify(draft),
  });
}

export function resolvePrice(organizationId: string, branchId: string, productId: string,
  at: string, signal?: AbortSignal): Promise<ResolvedPrice> {
  const query = new URLSearchParams({ branchId: uuid(branchId, "branch id"), productId: uuid(productId, "product id"), at });
  return requestJson(`${organizationPath(organizationId)}/pricing/resolve?${query}`, parseResolvedPrice, { signal });
}

export async function getOpenShift(organizationId: string, branchId: string, registerId: string,
  signal?: AbortSignal): Promise<Shift | null> {
  const query = new URLSearchParams({ registerId: uuid(registerId, "register id") });
  try { return await requestJson(`${branchPath(organizationId, branchId)}/shifts/open?${query}`, parseShift, { signal }); }
  catch (error) { if (error instanceof ApiError && error.kind === "not-found") return null; throw error; }
}

export function getCashMovements(organizationId: string, branchId: string, shiftId: string,
  signal?: AbortSignal): Promise<readonly CashMovement[]> {
  return requestJson(`${branchPath(organizationId, branchId)}/shifts/${uuid(shiftId, "shift id")}/cash-movements`,
    parseCashMovements, { signal });
}

export function getClosedShifts(organizationId: string, branchId: string, after?: string,
  signal?: AbortSignal): Promise<ClosedShiftPage> {
  const query = new URLSearchParams({ pageSize: "25" });
  if (after) query.set("after", uuid(after, "shift cursor"));
  return requestJson(`${branchPath(organizationId, branchId)}/shifts/closed?${query}`, parseClosedShiftPage, { signal });
}

export async function getClosedShift(organizationId: string, branchId: string, shiftId: string,
  signal?: AbortSignal): Promise<ClosedShift | null> {
  try {
    return await requestJson(`${branchPath(organizationId, branchId)}/shifts/${uuid(shiftId, "shift id")}`,
      parseClosedShift, { signal });
  } catch (error) { if (error instanceof ApiError && error.kind === "not-found") return null; throw error; }
}

export function getPaymentEvents(organizationId: string, branchId: string, after?: string,
  signal?: AbortSignal): Promise<PaymentEventPage> {
  const query = new URLSearchParams({ pageSize: "50" });
  if (after) query.set("after", after);
  return requestJson(`${branchPath(organizationId, branchId)}/payment-events?${query}`, parsePaymentEventPage, { signal });
}

export function newOperationId(): string {
  return crypto.randomUUID();
}
