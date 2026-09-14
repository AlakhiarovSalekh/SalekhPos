export const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type JsonObject = Record<string, unknown>;

export function object(value: unknown, label: string): JsonObject {
  if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error(`Invalid ${label}`);
  return value as JsonObject;
}

export function exactKeys(value: JsonObject, required: readonly string[], optional: readonly string[] = []): void {
  const allowed = new Set([...required, ...optional]);
  if (required.some(key => !(key in value)) || Object.keys(value).some(key => !allowed.has(key))) {
    throw new Error("Unexpected response shape");
  }
}

export function text(value: unknown, label: string, max: number, min = 0): string {
  if (typeof value !== "string" || value.length < min || value.length > max || [...value].some(char => char < " ")) {
    throw new Error(`Invalid ${label}`);
  }
  return value;
}

export function uuid(value: unknown, label: string): string {
  const result = text(value, label, 36, 36);
  if (!UUID_PATTERN.test(result) || result === "00000000-0000-0000-0000-000000000000") throw new Error(`Invalid ${label}`);
  return result.toLowerCase();
}

export function optionalUuid(value: unknown, label: string): string | null {
  return value === null ? null : uuid(value, label);
}

export function decimal(value: unknown, label: string, options: { min?: number; max?: number } = {}): number {
  const min = options.min ?? 0;
  const max = options.max ?? 1_000_000_000_000;
  if (typeof value !== "number" || !Number.isFinite(value) || value < min || value > max) throw new Error(`Invalid ${label}`);
  return value;
}

export function integer(value: unknown, label: string, min: number, max: number): number {
  if (typeof value !== "number" || !Number.isInteger(value) || value < min || value > max) throw new Error(`Invalid ${label}`);
  return value;
}

export function isoDate(value: unknown, label: string): string {
  const result = text(value, label, 64, 10);
  if (!/Z$|[+-]\d\d:\d\d$/.test(result) || Number.isNaN(Date.parse(result))) throw new Error(`Invalid ${label}`);
  return result;
}

export function boundedArray(value: unknown, label: string, max: number): unknown[] {
  if (!Array.isArray(value) || value.length > max) throw new Error(`Invalid ${label}`);
  return value;
}

export async function boundedJson(response: Response, maxBytes = 262_144): Promise<unknown> {
  const declared = Number(response.headers.get("content-length"));
  if (Number.isFinite(declared) && declared > maxBytes) throw new Error("Response is too large");
  if (!response.body) return null;
  const reader = response.body.getReader();
  const chunks: Uint8Array[] = [];
  let length = 0;
  while (true) {
    const { done, value } = await reader.read();
    if (done) break;
    length += value.byteLength;
    if (length > maxBytes) {
      await reader.cancel();
      throw new Error("Response is too large");
    }
    chunks.push(value);
  }
  const bytes = new Uint8Array(length);
  let offset = 0;
  for (const chunk of chunks) { bytes.set(chunk, offset); offset += chunk.byteLength; }
  const source = new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  return source ? JSON.parse(source) : null;
}
