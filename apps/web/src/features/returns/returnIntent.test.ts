import { describe, expect, it } from "vitest";
import { beginIntent, uncertainIntent } from "./returnIntent";
import type { ReturnRequest } from "./types";

const request: ReturnRequest = { saleId: "sale", reason: "Customer request", lines: [{ productId: "product", quantity: 1 }] };

describe("return idempotency intent", () => {
  it("retains the exact key and payload after an uncertain attempt", () => {
    const first = beginIntent({ state: "idle" }, request, () => "operation-1");
    const uncertain = uncertainIntent(first, "Network ended");
    const retry = beginIntent(uncertain, { ...request, reason: "changed" }, () => "operation-2");
    expect(retry).toMatchObject({ operationId: "operation-1", request });
  });
});
