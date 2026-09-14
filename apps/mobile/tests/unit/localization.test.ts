import { describe, expect, it } from "vitest";

import { resolveLocale } from "../../src/localization/localization";
import { resources, translate } from "../../src/localization/resources";

describe("localization", () => {
  it.each([
    [["az-Latn-AZ"], "az"],
    [["ka-GE"], "ka"],
    [["en-US"], "en"],
    [["fr-FR", "az-AZ"], "az"],
    [[], "en"],
  ] as const)("resolves %j to %s", (languageTags, expected) => {
    expect(resolveLocale(languageTags)).toBe(expected);
  });

  it("keeps every supported locale structurally complete", () => {
    const englishKeys = Object.keys(resources.en).sort();
    expect(Object.keys(resources.az).sort()).toEqual(englishKeys);
    expect(Object.keys(resources.ka).sort()).toEqual(englishKeys);
  });

  it("interpolates named values", () => {
    expect(translate("en", "dashboard.welcome", { name: "Ada" })).toBe("Signed in as Ada");
  });
});
