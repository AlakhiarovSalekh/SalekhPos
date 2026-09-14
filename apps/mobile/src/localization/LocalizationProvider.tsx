import { getLocales } from "expo-localization";
import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
} from "react";

import { resolveLocale, type SupportedLocale } from "./localization";
import { translate, type TranslationKey } from "./resources";

type LocalizationValue = Readonly<{
  locale: SupportedLocale;
  setLocale: (locale: SupportedLocale) => void;
  t: (key: TranslationKey, values?: Readonly<Record<string, string>>) => string;
}>;

const LocalizationContext = createContext<LocalizationValue | null>(null);

export function LocalizationProvider({ children }: PropsWithChildren) {
  const [locale, setLocale] = useState<SupportedLocale>(() =>
    resolveLocale(getLocales().map((entry) => entry.languageTag)),
  );
  const t = useCallback(
    (key: TranslationKey, values?: Readonly<Record<string, string>>) =>
      translate(locale, key, values),
    [locale],
  );
  const value = useMemo(() => ({ locale, setLocale, t }), [locale, t]);

  return (
    <LocalizationContext.Provider value={value}>
      {children}
    </LocalizationContext.Provider>
  );
}

export function useLocalization(): LocalizationValue {
  const value = useContext(LocalizationContext);
  if (value === null) {
    throw new Error("useLocalization must be used within LocalizationProvider.");
  }
  return value;
}
