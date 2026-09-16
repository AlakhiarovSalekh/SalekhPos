export type LocalizationSettings = Readonly<{
  countryCode: string;
  defaultLocale: string;
  defaultCurrency: string;
  timeZone: string;
  supportedLocales: readonly string[];
  firstDayOfWeek: number;
  version: number;
  updatedAt: string;
}>;

export type LocalizationWriteResult = Readonly<{
  settings: LocalizationSettings;
  applied: boolean;
}>;
