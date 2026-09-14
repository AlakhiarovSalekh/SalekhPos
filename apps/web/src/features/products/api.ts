import {
  ApiClient,
  ApiHttpError,
  ApiNetworkError,
  ApiProblemError,
  ApiRequestAbortedError,
  ApiResponseParseError,
  ApiResponseTooLargeError,
  organizationPath,
  isUuid,
} from "@salekhpos/packages-api-client";

export const PRODUCT_PAGE_SIZE = 25;
const ORGANIZATION_PAGE_SIZE = 100;

export type AccessibleOrganization = Readonly<{ id: string; name: string }>;
export type Product = Readonly<{
  id: string;
  sku: string;
  name: string;
  unitCode: string;
  barcode: string | null;
  isActive: boolean;
  version: number;
}>;
export type Page<T> = Readonly<{ items: readonly T[]; nextCursor: string | null }>;
export type ProductDraft = Readonly<{ sku: string; name: string; unitCode: string; barcode: string }>;
export type ProductCreateIntent = Readonly<{ idempotencyKey: string; fingerprint: string }>;

type BrowserClientOptions = { origin?: string; csrfToken?: string | null; fetch?: typeof fetch };
const noBearerToken = { getAccessToken: async () => null };

export function createBrowserApiClient(options: BrowserClientOptions = {}): ApiClient {
  const origin = options.origin ?? window.location.origin;
  const browserFetch = options.fetch ?? globalThis.fetch;
  return new ApiClient({
    baseUrl: origin,
    tokenProvider: noBearerToken,
    maxResponseBytes: 512 * 1024,
    fetch: async (input, init) => {
      const headers = new Headers(init?.headers);
      if (init?.method !== undefined && init.method !== "GET" && options.csrfToken) {
        headers.set("X-CSRF-Token", options.csrfToken);
      }
      return browserFetch(input, { ...init, headers, credentials: "same-origin", cache: "no-store" });
    },
  });
}

function objectWithKeys(value: unknown, keys: readonly string[]): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    && Object.keys(value).length === keys.length && keys.every((key) => key in value);
}

function validText(value: unknown, maximum: number): value is string {
  return typeof value === "string" && value.length > 0 && value.length <= maximum
    && value === value.trim() && !/[\u0000-\u001f\u007f]/u.test(value);
}

function parseCursor(value: unknown): string | null | undefined {
  if (value === null) return null;
  return typeof value === "string" && isUuid(value) ? value.toLowerCase() : undefined;
}

export function parseOrganizationPage(value: unknown): Page<AccessibleOrganization> {
  if (!objectWithKeys(value, ["items", "nextCursor"]) || !Array.isArray(value.items)
      || value.items.length > ORGANIZATION_PAGE_SIZE) throw new Error("Malformed organization response");
  const nextCursor = parseCursor(value.nextCursor);
  if (nextCursor === undefined) throw new Error("Malformed organization response");
  const seen = new Set<string>();
  const items = value.items.map((item) => {
    if (!objectWithKeys(item, ["id", "name"]) || typeof item.id !== "string"
        || !isUuid(item.id) || !validText(item.name, 200)) throw new Error("Malformed organization response");
    const id = item.id.toLowerCase();
    if (seen.has(id)) throw new Error("Malformed organization response");
    seen.add(id);
    return { id, name: item.name };
  });
  if (items.some((item, index) => index > 0 && items[index - 1]!.id >= item.id)
      || (nextCursor !== null && (items.length === 0 || items.at(-1)!.id !== nextCursor))) {
    throw new Error("Malformed organization response");
  }
  return { items, nextCursor };
}

export function parseProductPage(value: unknown): Page<Product> {
  if (!objectWithKeys(value, ["items", "nextCursor"]) || !Array.isArray(value.items)
      || value.items.length > PRODUCT_PAGE_SIZE) throw new Error("Malformed product response");
  const nextCursor = parseCursor(value.nextCursor);
  if (nextCursor === undefined) throw new Error("Malformed product response");
  const seen = new Set<string>();
  const items = value.items.map((item) => {
    if (!objectWithKeys(item, ["id", "sku", "name", "unitCode", "barcode", "isActive", "version"])
        || typeof item.id !== "string" || !isUuid(item.id) || !validText(item.sku, 64)
        || !validText(item.name, 200) || !validText(item.unitCode, 16)
        || !(item.barcode === null || (typeof item.barcode === "string" && /^[0-9]{4,64}$/u.test(item.barcode)))
        || typeof item.isActive !== "boolean" || !Number.isSafeInteger(item.version) || (item.version as number) < 1) {
      throw new Error("Malformed product response");
    }
    const id = item.id.toLowerCase();
    if (seen.has(id)) throw new Error("Malformed product response");
    seen.add(id);
    return { id, sku: item.sku, name: item.name, unitCode: item.unitCode,
      barcode: item.barcode, isActive: item.isActive, version: item.version as number };
  });
  if (items.some((item, index) => index > 0 && items[index - 1]!.id >= item.id)
      || (nextCursor !== null && (items.length === 0 || items.at(-1)!.id !== nextCursor))) {
    throw new Error("Malformed product response");
  }
  return { items, nextCursor };
}

export function validateProductDraft(draft: ProductDraft): string | null {
  if (!/^[A-Z0-9][A-Z0-9_.-]{0,63}$/u.test(draft.sku)) return "Use 1–64 uppercase letters, numbers, dots, dashes, or underscores for SKU.";
  if (!validText(draft.name, 200)) return "Enter a product name between 1 and 200 characters.";
  if (!/^[A-Z0-9][A-Z0-9_-]{0,15}$/u.test(draft.unitCode)) return "Use 1–16 uppercase letters, numbers, dashes, or underscores for unit.";
  if (draft.barcode !== "" && !/^[0-9]{4,64}$/u.test(draft.barcode)) return "Barcode must contain 4–64 digits, or be left blank.";
  return null;
}

export function getProductCreateIntent(organizationId: string, draft: ProductDraft,
  previous: ProductCreateIntent | null, createId: () => string = () => crypto.randomUUID()): ProductCreateIntent {
  if (!isUuid(organizationId)) throw new TypeError("The organization is invalid.");
  const fingerprint = JSON.stringify([organizationId.toLowerCase(), draft.sku, draft.name, draft.unitCode, draft.barcode]);
  if (previous?.fingerprint === fingerprint) return previous;
  const idempotencyKey = createId();
  if (!isUuid(idempotencyKey)) throw new TypeError("The operation identifier is invalid.");
  return { idempotencyKey: idempotencyKey.toLowerCase(), fingerprint };
}

export async function loadCsrfToken(signal: AbortSignal): Promise<string> {
  const value = await createBrowserApiClient().get<unknown>("/auth/session", { signal });
  if (!objectWithKeys(value, ["configured", "authenticated", "name", "csrfToken"])
      || value.configured !== true || value.authenticated !== true
      || typeof value.csrfToken !== "string" || value.csrfToken.length < 16 || value.csrfToken.length > 4096
      || /[\u0000-\u001f\u007f]/u.test(value.csrfToken)) throw new Error("Session unavailable");
  return value.csrfToken;
}

export async function listOrganizations(after: string | null, signal: AbortSignal): Promise<Page<AccessibleOrganization>> {
  const value = await createBrowserApiClient().get<unknown>("/api/v1/access/organizations", {
    query: { pageSize: ORGANIZATION_PAGE_SIZE, after }, signal,
  });
  return parseOrganizationPage(value);
}

export async function listProducts(organizationId: string, after: string | null,
  signal: AbortSignal): Promise<Page<Product>> {
  const value = await createBrowserApiClient().get<unknown>(organizationPath(organizationId, "products"), {
    query: { pageSize: PRODUCT_PAGE_SIZE, after }, signal,
  });
  return parseProductPage(value);
}

export async function createProduct(organizationId: string, draft: ProductDraft,
  csrfToken: string, idempotencyKey: string, signal: AbortSignal): Promise<Product> {
  const validationError = validateProductDraft(draft);
  if (validationError) throw new TypeError(validationError);
  const value = await createBrowserApiClient({ csrfToken }).post<unknown, unknown>(
    organizationPath(organizationId, "products"), {
      body: { sku: draft.sku, name: draft.name, unitCode: draft.unitCode,
        barcode: draft.barcode === "" ? null : draft.barcode },
      idempotencyKey, signal,
    });
  return parseProductPage({ items: [value], nextCursor: null }).items[0]!;
}

export function productErrorMessage(error: unknown): string {
  if (error instanceof ApiRequestAbortedError) return "";
  if (error instanceof TypeError) return error.message;
  if (error instanceof ApiProblemError || error instanceof ApiHttpError) {
    if (error.status === 401) return "Your session has ended. Please sign in again.";
    if (error.status === 403) return "You do not have permission to manage products for this organization.";
    if (error.status === 409) return "That SKU or barcode is already in use, or the request conflicts with current data.";
    if (error.status === 503) return "Product service is temporarily unavailable. Please try again.";
  }
  if (error instanceof ApiNetworkError) return "We could not reach the service. Check your connection and try again.";
  if (error instanceof ApiResponseParseError || error instanceof ApiResponseTooLargeError)
    return "The service returned an unexpected response. Please try again.";
  return "We could not complete the request. Please try again.";
}
