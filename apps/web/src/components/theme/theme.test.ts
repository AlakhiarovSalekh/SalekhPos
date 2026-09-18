import { describe, expect, it } from "vitest";
import { normalizeTheme, oppositeTheme, resolveTheme } from "./theme";

describe("theme helpers", () => {
  it("accepts only supported persisted themes", () => {
    expect(normalizeTheme("dark")).toBe("dark");
    expect(normalizeTheme("light")).toBe("light");
    expect(normalizeTheme("system")).toBeNull();
    expect(normalizeTheme(null)).toBeNull();
  });

  it("uses persisted preference before system preference", () => {
    expect(resolveTheme("light", true)).toBe("light");
    expect(resolveTheme("dark", false)).toBe("dark");
  });

  it("falls back to the system preference", () => {
    expect(resolveTheme(null, true)).toBe("dark");
    expect(resolveTheme(null, false)).toBe("light");
  });

  it("switches between the only two supported themes", () => {
    expect(oppositeTheme("dark")).toBe("light");
    expect(oppositeTheme("light")).toBe("dark");
  });
});
