export type MutationIntent = Readonly<{ fingerprint: string; idempotencyKey: string }>;

export function mutationIntent(fingerprintParts: readonly unknown[], previous: MutationIntent | null,
  createId: () => string = () => crypto.randomUUID()): MutationIntent {
  const fingerprint = JSON.stringify(fingerprintParts);
  if (previous?.fingerprint === fingerprint) return previous;
  const idempotencyKey = createId().toLowerCase();
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(idempotencyKey)
      || idempotencyKey === "00000000-0000-0000-0000-000000000000") {
    throw new TypeError("The operation identifier is invalid.");
  }
  return { fingerprint, idempotencyKey };
}
