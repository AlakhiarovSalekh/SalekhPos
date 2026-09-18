import { describe, expect, it } from "vitest";
import { darkColors, lightColors, resolveMobileColors } from "./theme";

describe("mobile theme", () => {
  it("uses dark palette only for an explicit dark system scheme", () => {
    expect(resolveMobileColors("dark")).toBe(darkColors);
    expect(resolveMobileColors("light")).toBe(lightColors);
    expect(resolveMobileColors(null)).toBe(lightColors);
    expect(resolveMobileColors(undefined)).toBe(lightColors);
  });

  it("keeps readable primary foregrounds for both palettes", () => {
    expect(lightColors.onPrimary).not.toBe(lightColors.primary);
    expect(darkColors.onPrimary).not.toBe(darkColors.primary);
  });
});
