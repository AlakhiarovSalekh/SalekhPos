import { describe, expect, it } from "vitest";

import { darkPalette, lightPalette } from "./themePalette";

describe("mobile theme palettes", () => {
  it("keeps semantic light and dark surfaces distinct", () => {
    expect(lightPalette.background).not.toBe(darkPalette.background);
    expect(lightPalette.surface).not.toBe(darkPalette.surface);
    expect(lightPalette.text).not.toBe(darkPalette.text);
  });

  it("uses explicit semantic feedback colors in both themes", () => {
    for (const palette of [lightPalette, darkPalette]) {
      expect(palette.danger).toMatch(/^#[0-9A-F]{6}$/u);
      expect(palette.warning).toMatch(/^#[0-9A-F]{6}$/u);
      expect(palette.success).toMatch(/^#[0-9A-F]{6}$/u);
      expect(palette.border).toMatch(/^#[0-9A-F]{6}$/u);
    }
  });
});
