export type Theme = "light" | "dark";

export function normalizeTheme(value: string | null): Theme | null {
  return value === "light" || value === "dark" ? value : null;
}

export function resolveTheme(stored: string | null, prefersDark: boolean): Theme {
  return normalizeTheme(stored) ?? (prefersDark ? "dark" : "light");
}

export function oppositeTheme(theme: Theme): Theme {
  return theme === "dark" ? "light" : "dark";
}
