export const supportedLocales = ["en", "az", "ka"] as const;
export type SupportedLocale = (typeof supportedLocales)[number];

export function resolveLocale(languageTags: readonly string[]): SupportedLocale {
  for (const tag of languageTags) {
    const language = tag.trim().toLowerCase().split(/[-_]/u)[0];
    if (supportedLocales.includes(language as SupportedLocale)) {
      return language as SupportedLocale;
    }
  }
  return "en";
}
