import {
  ApiClientConfigurationError,
  ApiHttpError,
  ApiNetworkError,
  ApiProblemError,
  ApiRequestAbortedError,
  ApiResponseParseError,
  ApiResponseTooLargeError,
} from "./errors.js";
import type { BearerTokenProvider } from "./token-provider.js";
import { validateBearerToken } from "./token-provider.js";
import { assertRelativeApiPath, normalizeApiBaseUrl } from "./validation.js";

export type QueryPrimitive = string | number | boolean;
export type QueryValue = QueryPrimitive | readonly QueryPrimitive[] | null | undefined;
export type ApiQuery = Readonly<Record<string, QueryValue>>;

export interface ApiClientOptions {
  baseUrl: string;
  tokenProvider: BearerTokenProvider;
  fetch?: typeof fetch;
  maxResponseBytes?: number;
}

export interface ApiRequestOptions {
  query?: ApiQuery;
  idempotencyKey?: string;
  signal?: AbortSignal;
}

export interface ApiRequestWithBodyOptions<TBody> extends ApiRequestOptions {
  body: TBody;
}

const DEFAULT_MAX_RESPONSE_BYTES = 1_048_576;

function encodeQueryComponent(value: string): string {
  return encodeURIComponent(value).replace(/[!'()*]/gu, (character) =>
    `%${character.charCodeAt(0).toString(16).toUpperCase()}`,
  );
}

function queryPrimitiveToString(value: QueryPrimitive): string {
  if (typeof value === "number" && !Number.isFinite(value)) {
    throw new ApiClientConfigurationError("Query numbers must be finite.");
  }

  return String(value);
}

export function encodeQuery(query: ApiQuery | undefined): string {
  if (query === undefined) {
    return "";
  }

  const parts: string[] = [];
  for (const key of Object.keys(query).sort()) {
    const value = query[key];
    if (value === null || value === undefined) {
      continue;
    }

    const encodedKey = encodeQueryComponent(key);
    const values = Array.isArray(value) ? value : [value];
    for (const item of values) {
      parts.push(`${encodedKey}=${encodeQueryComponent(queryPrimitiveToString(item))}`);
    }
  }

  return parts.length === 0 ? "" : `?${parts.join("&")}`;
}

function validateIdempotencyKey(value: string): string {
  if (value.length === 0 || value.length > 255 || !/^[\x21-\x7e]+$/u.test(value)) {
    throw new ApiClientConfigurationError(
      "Idempotency key must contain 1 to 255 visible ASCII characters.",
    );
  }

  return value;
}

function serializeJsonBody(body: unknown): string {
  try {
    const serialized = JSON.stringify(body);
    if (serialized === undefined) {
      throw new ApiClientConfigurationError("Request body must be JSON serializable.");
    }
    return serialized;
  } catch (error) {
    if (error instanceof ApiClientConfigurationError) {
      throw error;
    }
    throw new ApiClientConfigurationError("Request body must be JSON serializable.");
  }
}

function contentType(response: Response): string {
  return (response.headers.get("content-type") ?? "").split(";", 1)[0]?.trim().toLowerCase() ?? "";
}

function isJsonContentType(value: string): boolean {
  return value === "application/json" || value.endsWith("+json");
}

function safeProblemString(value: unknown, maximumLength: number): string | undefined {
  return typeof value === "string" &&
    value.length > 0 &&
    value.length <= maximumLength &&
    !/[\u0000-\u001f\u007f]/u.test(value)
    ? value
    : undefined;
}

function parseProblem(status: number, text: string): ApiProblemError | undefined {
  try {
    const value: unknown = JSON.parse(text);
    if (typeof value !== "object" || value === null || Array.isArray(value)) {
      return undefined;
    }

    const problem = value as Record<string, unknown>;
    const title = safeProblemString(problem.title, 200) ?? "API request failed";
    const code = safeProblemString(problem.code, 128);
    return new ApiProblemError(status, code, title);
  } catch {
    return undefined;
  }
}

async function readBoundedText(response: Response, maximumBytes: number): Promise<string> {
  const declaredLength = response.headers.get("content-length");
  if (declaredLength !== null && /^\d+$/u.test(declaredLength) && Number(declaredLength) > maximumBytes) {
    throw new ApiResponseTooLargeError(response.status, maximumBytes);
  }

  if (response.body === null) {
    return "";
  }

  const reader = response.body.getReader();
  const chunks: Uint8Array[] = [];
  let totalBytes = 0;
  try {
    while (true) {
      const result = await reader.read();
      if (result.done) {
        break;
      }

      totalBytes += result.value.byteLength;
      if (totalBytes > maximumBytes) {
        await reader.cancel();
        throw new ApiResponseTooLargeError(response.status, maximumBytes);
      }
      chunks.push(result.value);
    }
  } finally {
    reader.releaseLock();
  }

  const bytes = new Uint8Array(totalBytes);
  let offset = 0;
  for (const chunk of chunks) {
    bytes.set(chunk, offset);
    offset += chunk.byteLength;
  }

  return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
}

function isAbort(error: unknown, signal: AbortSignal | undefined): boolean {
  return signal?.aborted === true ||
    (error instanceof DOMException && error.name === "AbortError") ||
    (typeof error === "object" && error !== null && "name" in error && error.name === "AbortError");
}

function isSignalAborted(signal: AbortSignal | undefined): boolean {
  return signal?.aborted === true;
}

export class ApiClient {
  readonly #baseUrl: string;
  readonly #tokenProvider: BearerTokenProvider;
  readonly #fetch: typeof fetch;
  readonly #maxResponseBytes: number;

  public constructor(options: ApiClientOptions) {
    this.#baseUrl = normalizeApiBaseUrl(options.baseUrl);
    this.#tokenProvider = options.tokenProvider;
    this.#fetch = options.fetch ?? globalThis.fetch;
    if (typeof this.#fetch !== "function") {
      throw new ApiClientConfigurationError("A Fetch API implementation is required.");
    }

    const maximum = options.maxResponseBytes ?? DEFAULT_MAX_RESPONSE_BYTES;
    if (!Number.isSafeInteger(maximum) || maximum <= 0) {
      throw new ApiClientConfigurationError("Maximum response size must be a positive safe integer.");
    }
    this.#maxResponseBytes = maximum;
  }

  public get<T>(path: string, options: ApiRequestOptions = {}): Promise<T | undefined> {
    return this.#request<T>("GET", path, options);
  }

  public post<TResponse, TBody = unknown>(
    path: string,
    options: ApiRequestWithBodyOptions<TBody>,
  ): Promise<TResponse | undefined> {
    return this.#request<TResponse>("POST", path, options, options.body);
  }

  public put<TResponse, TBody = unknown>(
    path: string,
    options: ApiRequestWithBodyOptions<TBody>,
  ): Promise<TResponse | undefined> {
    return this.#request<TResponse>("PUT", path, options, options.body);
  }

  public patch<TResponse, TBody = unknown>(
    path: string,
    options: ApiRequestWithBodyOptions<TBody>,
  ): Promise<TResponse | undefined> {
    return this.#request<TResponse>("PATCH", path, options, options.body);
  }

  public delete<T>(path: string, options: ApiRequestOptions = {}): Promise<T | undefined> {
    return this.#request<T>("DELETE", path, options);
  }

  async #request<T>(
    method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE",
    path: string,
    options: ApiRequestOptions,
    body?: unknown,
  ): Promise<T | undefined> {
    const safePath = assertRelativeApiPath(path);
    if (isSignalAborted(options.signal)) {
      throw new ApiRequestAbortedError();
    }

    let token: string | null;
    try {
      token = await this.#tokenProvider.getAccessToken();
    } catch (error) {
      if (isAbort(error, options.signal)) {
        throw new ApiRequestAbortedError();
      }
      throw error;
    }

    if (isSignalAborted(options.signal)) {
      throw new ApiRequestAbortedError();
    }

    const headers = new Headers({ Accept: "application/json" });
    if (token !== null) {
      headers.set("Authorization", `Bearer ${validateBearerToken(token)}`);
    }
    if (body !== undefined) {
      headers.set("Content-Type", "application/json");
    }
    if (options.idempotencyKey !== undefined) {
      headers.set("Idempotency-Key", validateIdempotencyKey(options.idempotencyKey));
    }

    const url = new URL(`${safePath.slice(1)}${encodeQuery(options.query)}`, this.#baseUrl);
    if (url.origin !== new URL(this.#baseUrl).origin) {
      throw new ApiClientConfigurationError("API request URL must remain on the configured origin.");
    }

    const request: RequestInit = { method, headers };
    if (body !== undefined) {
      request.body = serializeJsonBody(body);
    }
    if (options.signal !== undefined) {
      request.signal = options.signal;
    }

    let response: Response;
    try {
      response = await this.#fetch(url.toString(), request);
    } catch (error) {
      if (isAbort(error, options.signal)) {
        throw new ApiRequestAbortedError();
      }
      throw new ApiNetworkError();
    }

    if (response.status === 204) {
      return undefined;
    }

    let text: string;
    try {
      text = await readBoundedText(response, this.#maxResponseBytes);
    } catch (error) {
      if (isAbort(error, options.signal)) {
        throw new ApiRequestAbortedError();
      }
      if (error instanceof ApiResponseTooLargeError) {
        throw error;
      }
      throw new ApiResponseParseError(response.status);
    }

    const responseContentType = contentType(response);
    if (!response.ok) {
      if (responseContentType === "application/problem+json") {
        const problem = parseProblem(response.status, text);
        if (problem !== undefined) {
          throw problem;
        }
      }
      throw new ApiHttpError(response.status);
    }

    if (!isJsonContentType(responseContentType) || text.length === 0) {
      throw new ApiResponseParseError(response.status);
    }

    try {
      return JSON.parse(text) as T;
    } catch {
      throw new ApiResponseParseError(response.status);
    }
  }
}
