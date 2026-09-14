export type PaginationState<T> = Readonly<{
  items: readonly T[];
  nextCursor: string | null;
  seenCursors: ReadonlySet<string>;
}>;

export function firstPageState<T>(items: readonly T[], nextCursor: string | null): PaginationState<T> {
  return Object.freeze({ items: Object.freeze([...items]), nextCursor, seenCursors: new Set(nextCursor === null ? [] : [nextCursor]) });
}

export function appendPage<T extends Readonly<{ id?: string; productId?: string }>>(
  state: PaginationState<T>,
  items: readonly T[],
  nextCursor: string | null,
): PaginationState<T> {
  if (state.nextCursor === null) throw new Error("No next page is available.");
  if (nextCursor !== null && state.seenCursors.has(nextCursor)) throw new Error("Pagination cursor cycle detected.");
  const identity = (item: T) => item.id ?? item.productId;
  const known = new Set(state.items.map(identity));
  const merged = [...state.items];
  for (const item of items) if (!known.has(identity(item))) { known.add(identity(item)); merged.push(item); }
  const seen = new Set(state.seenCursors);
  if (nextCursor !== null) seen.add(nextCursor);
  return Object.freeze({ items: Object.freeze(merged), nextCursor, seenCursors: seen });
}
