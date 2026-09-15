import { randomUUID } from "expo-crypto";
import { ApiHttpError, assertUuid, branchPath, organizationPath, type ApiClient } from "@salekhpos/packages-api-client";
import {
  OperationsContractError, parseCashMovements, parseClosedShift, parseClosedShiftPage,
  parsePaymentEventPage, parsePrice, parseRegister, parseRegisterPage, parseResolvedPrice, parseShift,
  type CashMovementSummary, type ClosedShiftSummary, type CursorPage, type PaymentCursorPage,
  type PriceSummary, type RegisterSummary, type ResolvedPriceSummary, type ShiftSummary,
} from "@/api/operationsContracts";

export type RegisterInput = Readonly<{ code: string; name: string }>;
export type PriceInput = Readonly<{
  productId: string;
  branchId: string | null;
  amount: number;
  currency: string;
  taxMode: "inclusive" | "exclusive";
  taxRate: number;
  validFrom: string;
  validUntil: string | null;
}>;

function requireResponse<T>(value: T | undefined): T {
  if (value === undefined) throw new OperationsContractError("response");
  return value;
}

function pageSize(value: number): number {
  if (!Number.isInteger(value) || value < 1 || value > 100) throw new TypeError("Page size is invalid.");
  return value;
}
function registerInput(input: RegisterInput): RegisterInput {
  const code = input.code.trim();
  const name = input.name.trim();
  if (!/^[A-Z0-9_-]{1,32}$/u.test(code)) throw new TypeError("Register code must use 1-32 uppercase letters, numbers, dashes or underscores.");
  if (name.length < 1 || name.length > 100 || /[\u0000-\u001f\u007f]/u.test(name)) throw new TypeError("Register name is invalid.");
  return Object.freeze({ code, name });
}

function priceInput(input: PriceInput): PriceInput {
  const productId = assertUuid(input.productId, "productId");
  const branchId = input.branchId === null ? null : assertUuid(input.branchId, "branchId");
  if (!(input.amount > 0) || !Number.isFinite(input.amount) || !Number.isSafeInteger(input.amount * 1_000_000)) throw new TypeError("Price amount is invalid.");
  if (!/^[A-Z]{3}$/u.test(input.currency)) throw new TypeError("Currency is invalid.");
  if (input.taxMode !== "inclusive" && input.taxMode !== "exclusive") throw new TypeError("Tax mode is invalid.");
  if (input.taxRate < 0 || input.taxRate > 100 || !Number.isSafeInteger(input.taxRate * 10_000)) throw new TypeError("Tax rate is invalid.");
  if (!input.validFrom.endsWith("Z") || !Number.isFinite(Date.parse(input.validFrom))) throw new TypeError("Valid-from must be UTC.");
  if (input.validUntil !== null && (!input.validUntil.endsWith("Z") || !Number.isFinite(Date.parse(input.validUntil))
      || Date.parse(input.validUntil) <= Date.parse(input.validFrom))) throw new TypeError("Valid-until is invalid.");
  return Object.freeze({ ...input, productId, branchId });
}

export function createManagerOperations(client: ApiClient) {
  return Object.freeze({
    async listRegisters(organizationId: string, branchId: string, size = 50, after?: string,
      signal?: AbortSignal): Promise<CursorPage<RegisterSummary>> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const query: Record<string, string | number> = { pageSize: pageSize(size) };
      if (after) query.after = assertUuid(after, "after");
      const value = await client.get<unknown>(branchPath(organization, branch, "registers"), { query, ...(signal ? { signal } : {}) });
      const result = parseRegisterPage(requireResponse(value));
      if (result.items.some(item => item.branchId !== branch)) throw new OperationsContractError("registers");
      return result;
    },
    async createRegister(organizationId: string, branchId: string, input: RegisterInput,
      operationId = randomUUID(), signal?: AbortSignal): Promise<RegisterSummary> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const safe = registerInput(input);
      const value = await client.post<unknown>(branchPath(organization, branch, "registers"), {
        body: safe,
        idempotencyKey: assertUuid(operationId, "operationId"),
        ...(signal ? { signal } : {}),
      });
      const result = parseRegister(requireResponse(value));
      if (result.branchId !== branch || result.code !== safe.code || result.name !== safe.name) throw new OperationsContractError("register");
      return result;
    },

    async schedulePrice(organizationId: string, input: PriceInput, operationId = randomUUID(),
      signal?: AbortSignal): Promise<PriceSummary> {
      const organization = assertUuid(organizationId, "organizationId");
      const safe = priceInput(input);
      const value = await client.post<unknown>(organizationPath(organization, "pricing", "prices"), {
        body: safe,
        idempotencyKey: assertUuid(operationId, "operationId"),
        ...(signal ? { signal } : {}),
      });
      const result = parsePrice(requireResponse(value));
      if (result.productId !== safe.productId || result.branchId !== safe.branchId || result.amount !== safe.amount
          || result.currency !== safe.currency || result.taxMode !== safe.taxMode || result.taxRate !== safe.taxRate) {
        throw new OperationsContractError("price");
      }
      return result;
    },

    async resolvePrice(organizationId: string, branchId: string, productId: string, at: string,
      signal?: AbortSignal): Promise<ResolvedPriceSummary | null> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const product = assertUuid(productId, "productId");
      if (!at.endsWith("Z") || !Number.isFinite(Date.parse(at))) throw new TypeError("Price timestamp must be UTC.");
      try {
        const value = await client.get<unknown>(organizationPath(organization, "pricing", "resolve"), {
          query: { branchId: branch, productId: product, at }, ...(signal ? { signal } : {}),
        });
        const result = parseResolvedPrice(requireResponse(value));
        const atMs = Date.parse(at);
        if (result.productId !== product || (result.branchId !== null && result.branchId !== branch)
            || Date.parse(result.validFrom) > atMs || (result.validUntil !== null && Date.parse(result.validUntil) <= atMs)) {
          throw new OperationsContractError("resolvedPrice");
        }
        return result;
      } catch (error) { if (error instanceof ApiHttpError && error.status === 404) return null; throw error; }
    },
    async readOpenShift(organizationId: string, branchId: string, registerId: string,
      signal?: AbortSignal): Promise<ShiftSummary | null> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const register = assertUuid(registerId, "registerId");
      try {
        const value = await client.get<unknown>(branchPath(organization, branch, "shifts", "open"), {
          query: { registerId: register }, ...(signal ? { signal } : {}),
        });
        const result = parseShift(requireResponse(value));
        if (result.branchId !== branch || result.registerId !== register) throw new OperationsContractError("shift");
        return result;
      } catch (error) { if (error instanceof ApiHttpError && error.status === 404) return null; throw error; }
    },

    async listCashMovements(organizationId: string, branchId: string, shiftId: string,
      signal?: AbortSignal): Promise<readonly CashMovementSummary[]> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const shift = assertUuid(shiftId, "shiftId");
      const value = await client.get<unknown>(branchPath(organization, branch, "shifts", shift, "cash-movements"), signal ? { signal } : {});
      const items = parseCashMovements(requireResponse(value));
      if (items.some(item => item.shiftId !== shift)) throw new OperationsContractError("movements");
      return items;
    },

    async listClosedShifts(organizationId: string, branchId: string, size = 25, after?: string,
      signal?: AbortSignal): Promise<CursorPage<ClosedShiftSummary>> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const query: Record<string, string | number> = { pageSize: pageSize(size) };
      if (after) query.after = assertUuid(after, "after");
      const value = await client.get<unknown>(branchPath(organization, branch, "shifts", "closed"), { query, ...(signal ? { signal } : {}) });
      const result = parseClosedShiftPage(requireResponse(value));
      if (result.items.some(item => item.branchId !== branch)) throw new OperationsContractError("closedShifts");
      return result;
    },
    async readClosedShift(organizationId: string, branchId: string, shiftId: string,
      signal?: AbortSignal): Promise<ClosedShiftSummary | null> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const shift = assertUuid(shiftId, "shiftId");
      try {
        const value = await client.get<unknown>(branchPath(organization, branch, "shifts", shift), signal ? { signal } : {});
        const result = parseClosedShift(requireResponse(value));
        if (result.branchId !== branch || result.id !== shift) throw new OperationsContractError("closedShift");
        return result;
      } catch (error) { if (error instanceof ApiHttpError && error.status === 404) return null; throw error; }
    },

    async listPaymentEvents(organizationId: string, branchId: string, size = 50, after?: string,
      signal?: AbortSignal): Promise<PaymentCursorPage> {
      const organization = assertUuid(organizationId, "organizationId");
      const branch = assertUuid(branchId, "branchId");
      const query: Record<string, string | number> = { pageSize: pageSize(size) };
      if (after) {
        if (after.length > 256 || /[\u0000-\u001f\u007f]/u.test(after)) throw new TypeError("Payment cursor is invalid.");
        query.after = after;
      }
      const value = await client.get<unknown>(branchPath(organization, branch, "payment-events"), { query, ...(signal ? { signal } : {}) });
      const result = parsePaymentEventPage(requireResponse(value));
      if (result.items.some(item => item.branchId !== branch)) throw new OperationsContractError("paymentEvents");
      return result;
    },
  });
}

export type ManagerOperations = ReturnType<typeof createManagerOperations>;
