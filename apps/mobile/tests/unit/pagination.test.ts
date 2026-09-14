import { describe, expect, it } from "vitest";
import { appendPage, firstPageState } from "../../src/features/pagination";

describe("pagination state", () => {
  it("deduplicates overlapping pages and preserves order", () => {
    const first = firstPageState([{ id: "a" }, { id: "b" }], "cursor-1");
    expect(appendPage(first, [{ id: "b" }, { id: "c" }], null).items).toEqual([{ id: "a" }, { id: "b" }, { id: "c" }]);
  });

  it("rejects loading past the end and cursor cycles", () => {
    expect(() => appendPage(firstPageState([{ id: "a" }], null), [], null)).toThrow("No next page");
    expect(() => appendPage(firstPageState([{ id: "a" }], "cursor-1"), [], "cursor-1")).toThrow("cycle");
  });
});
