import { boundedArray, exactKeys, integer, isoDate, object, text } from "@/lib/boundedJson";
import type { LocalizationSettings, LocalizationWriteResult } from "./types";

export function parseLocalizationSettings(value: unknown): LocalizationSettings {
  const x = object(value, "localization settings");
  exactKeys(x, ["countryCode", "defaultLocale", "defaultCurrency", "timeZone",
    "supportedLocales", "firstDayOfWeek", "version", "updatedAt"]);
  const locales = boundedArray(x.supportedLocales, "supported locales", 20)
    .map((item, index) => text(item, `supported locale ${index}`, 35, 2));
  return {
    countryCode: text(x.countryCode, "country code", 2, 2),
    defaultLocale: text(x.defaultLocale, "default locale", 35, 2),
    defaultCurrency: text(x.defaultCurrency, "default currency", 3, 3),
    timeZone: text(x.timeZone, "time zone", 100, 1),
    supportedLocales: locales,
    firstDayOfWeek: integer(x.firstDayOfWeek, "first day", 1, 7),
    version: integer(x.version, "version", 1, Number.MAX_SAFE_INTEGER),
    updatedAt: isoDate(x.updatedAt, "updated at"),
  };
}
export function parseLocalizationWriteResult(value: unknown): LocalizationWriteResult {
  const x = object(value, "localization write result");
  exactKeys(x, ["settings", "applied"]);
  if (typeof x.applied !== "boolean") throw new Error("Invalid localization applied flag");
  return { settings: parseLocalizationSettings(x.settings), applied: x.applied };
}
