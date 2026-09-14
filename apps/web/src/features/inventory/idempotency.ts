import type { MovementInput } from "./types";

export type MovementIntent = { key: string; fingerprint: string };
export function movementFingerprint(input: MovementInput): string {
  return JSON.stringify([input.productId, input.kind, input.quantity, input.reason, input.occurredAt]);
}
export function resolveMovementIntent(input: MovementInput, previous?: MovementIntent, createKey: () => string = () => crypto.randomUUID()): MovementIntent {
  const fingerprint = movementFingerprint(input);
  return previous?.fingerprint === fingerprint ? previous : { key: createKey(), fingerprint };
}
export function isUncertainFailure(status: number | null): boolean { return status === null || status === 408 || status === 429 || (status >= 500 && status <= 599); }
