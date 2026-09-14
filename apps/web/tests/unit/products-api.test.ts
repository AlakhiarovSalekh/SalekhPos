import assert from "node:assert/strict";
import test from "node:test";
import { ApiProblemError } from "@salekhpos/packages-api-client";
import {
  createBrowserApiClient,
  getProductCreateIntent,
  parseOrganizationPage,
  parseProductPage,
  productErrorMessage,
  validateProductDraft,
} from "../../src/features/products/api.js";

const organizationId = "11111111-1111-1111-1111-111111111111";
const productId = "22222222-2222-2222-2222-222222222222";

test("organization and product parsers accept only bounded exact contracts", () => {
  assert.deepEqual(parseOrganizationPage({ items: [{ id: organizationId, name: "Corner Shop" }], nextCursor: null }), {
    items: [{ id: organizationId, name: "Corner Shop" }], nextCursor: null,
  });
  assert.equal(parseProductPage({ items: [{ id: productId, sku: "MILK-1", name: "Milk", unitCode: "EA", barcode: "1234", isActive: true, version: 1 }], nextCursor: null }).items[0]?.name, "Milk");
  assert.throws(() => parseOrganizationPage({ items: [], nextCursor: null, internal: "secret" }));
  assert.throws(() => parseProductPage({ items: [{ id: productId, sku: "MILK-1", name: "Milk", unitCode: "EA", barcode: null, isActive: true, version: 0 }], nextCursor: null }));
});

test("browser client sends cookies and antiforgery token without bearer storage", async () => {
  let captured: RequestInit | undefined;
  const client = createBrowserApiClient({
    origin: "https://pos.example",
    csrfToken: "csrf-value",
    fetch: async (_input, init) => {
      captured = init;
      return new Response(JSON.stringify({ ok: true }), { status: 200, headers: { "content-type": "application/json" } });
    },
  });
  await client.post("/api/v1/test", { body: { value: 1 }, idempotencyKey: productId });
  assert.equal(captured?.credentials, "same-origin");
  assert.equal(new Headers(captured?.headers).get("X-CSRF-Token"), "csrf-value");
  assert.equal(new Headers(captured?.headers).has("Authorization"), false);
});

test("product create retries keep one idempotency key until organization or payload changes", () => {
  const draft = { sku: "COFFEE-1", name: "Coffee", unitCode: "EA", barcode: "" };
  const first = getProductCreateIntent(organizationId, draft, null, () => productId);
  const retry = getProductCreateIntent(organizationId, draft, first, () => "33333333-3333-3333-3333-333333333333");
  assert.equal(retry.idempotencyKey, productId);
  const changed = getProductCreateIntent(organizationId, { ...draft, name: "Coffee beans" }, retry,
    () => "33333333-3333-3333-3333-333333333333");
  assert.equal(changed.idempotencyKey, "33333333-3333-3333-3333-333333333333");
});

test("product validation is bounded and raw backend messages are not displayed", () => {
  assert.equal(validateProductDraft({ sku: "COFFEE-1", name: "Coffee", unitCode: "EA", barcode: "" }), null);
  assert.match(validateProductDraft({ sku: "bad sku", name: "Coffee", unitCode: "EA", barcode: "" }) ?? "", /SKU/u);
  const message = productErrorMessage(new ApiProblemError(503, "service_unavailable", "database host secret"));
  assert.equal(message, "Product service is temporarily unavailable. Please try again.");
  assert.doesNotMatch(message, /database host secret/u);
});
