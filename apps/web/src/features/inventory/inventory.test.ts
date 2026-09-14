import { afterEach, describe, expect, it, vi } from "vitest";
import { createMovement, getStock, SafeApiError } from "./api";
import { isUncertainFailure, resolveMovementIntent } from "./idempotency";
import { parseAccess, parseStockPage, type MovementInput } from "./types";
import { validateMovement } from "./validation";

const input: MovementInput = { productId: "123e4567-e89b-12d3-a456-426614174000", kind: "receipt", quantity: "1.250000", reason: "Delivery", occurredAt: "2026-09-14T08:00:00.000Z" };
afterEach(() => vi.unstubAllGlobals());
describe("inventory contracts", () => {
  it("parses bounded stock and access responses", () => {
    expect(parseStockPage({ items: [{ productId: input.productId, sku: "SKU-1", name: "Tea", quantity: 2.5 }], nextCursor: null }).items[0].quantity).toBe("2.5");
    expect(parseAccess({ organizationId: input.productId, branches: [{ branchId: input.productId, code: "HQ", name: "Main", timeZoneId: "Asia/Tbilisi", canView: true, canAdjust: false }] }).branches[0].canAdjust).toBe(false);
    expect(() => parseStockPage({ items: [{ productId: "not-an-id", sku: "x", name: "x", quantity: 1 }], nextCursor: null })).toThrow("Invalid server response");
  });
  it("validates decimals, reasons and future timestamps", () => {
    const valid = validateMovement({ ...input, reason: " Delivery ", occurredAt: "2026-09-14T08:00" }, new Date("2026-09-14T08:01:00Z"));
    expect(valid.value?.reason).toBe("Delivery");
    expect(validateMovement({ ...input, quantity: "1.0000001", reason: "ok", occurredAt: "2026-09-14T08:00" }).errors.quantity).toBeTruthy();
    expect(validateMovement({ ...input, quantity: "100000000000000", reason: "ok", occurredAt: "2026-09-14T08:00" }).errors.quantity).toBeTruthy();
    expect(validateMovement({ ...input, reason: "bad\u0000", occurredAt: "2026-09-14T08:00" }).errors.reason).toBeTruthy();
    expect(validateMovement({ ...input, reason: "ok", occurredAt: "2099-09-14T08:07" }, new Date("2026-09-14T08:00:00Z")).errors.occurredAt).toBeTruthy();
  });
  it("preserves exact uncertain retries and rotates keys after edits", () => {
    const first = resolveMovementIntent(input, undefined, () => "first");
    expect(resolveMovementIntent(input, first, () => "second")).toBe(first);
    expect(resolveMovementIntent({ ...input, quantity: "2" }, first, () => "second").key).toBe("second");
    expect([null, 408, 429, 500, 503].every(isUncertainFailure)).toBe(true); expect(isUncertainFailure(409)).toBe(false);
  });
  it("constructs paged and CSRF-protected mutation requests", async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ items: [], nextCursor: null }), { status: 200 })).mockResolvedValueOnce(new Response(JSON.stringify({ id: input.productId, branchId: input.productId, ...input, recordedAt: input.occurredAt }), { status: 201 }));
    vi.stubGlobal("fetch", fetchMock); await getStock(input.productId, input.productId, input.productId); await createMovement(input.productId, input.productId, input, "operation-key", "csrf-token");
    expect(fetchMock.mock.calls[0][0]).toContain(`pageSize=50&after=${input.productId}`);
    expect(fetchMock.mock.calls[1][1]).toMatchObject({ method: "POST", credentials: "same-origin", headers: { "Content-Type": "application/json", "Idempotency-Key": "operation-key", "X-CSRF-Token": "csrf-token" } });
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body)).quantity).toBe("1.250000");
  });
  it("maps server errors without exposing untrusted details", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({ code: "service_unavailable", detail: "secret database host" }), { status: 503 })));
    await expect(getStock(input.productId, input.productId)).rejects.toEqual(expect.objectContaining<Partial<SafeApiError>>({ status: 503, message: "Inventory is temporarily unavailable. You can safely retry." }));
  });
});
