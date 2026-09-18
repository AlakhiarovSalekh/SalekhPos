export type MobileThemeScheme = "light" | "dark" | "unspecified" | null | undefined;

export type MobileColors = Readonly<{
  background: string;
  surface: string;
  surfaceMuted: string;
  text: string;
  muted: string;
  primary: string;
  primaryPressed: string;
  onPrimary: string;
  border: string;
  danger: string;
  warningBackground: string;
  warningBorder: string;
  warningText: string;
  successBackground: string;
  successBorder: string;
  successText: string;
}>;

export const lightColors: MobileColors = Object.freeze({
  background: "#F4F7FB",
  surface: "#FFFFFF",
  surfaceMuted: "#EEF3F8",
  text: "#10213A",
  muted: "#53657D",
  primary: "#075E54",
  primaryPressed: "#06483F",
  onPrimary: "#FFFFFF",
  border: "#C7D2E0",
  danger: "#A6192E",
  warningBackground: "#FFF4CE",
  warningBorder: "#DFC46A",
  warningText: "#6B4F00",
  successBackground: "#E8F5ED",
  successBorder: "#9FCDB5",
  successText: "#185C3A",
});

export const darkColors: MobileColors = Object.freeze({
  background: "#07110F",
  surface: "#0F1D19",
  surfaceMuted: "#13251F",
  text: "#EDF7F2",
  muted: "#9DAFAA",
  primary: "#55D6AA",
  primaryPressed: "#78E8C2",
  onPrimary: "#05231B",
  border: "#263C35",
  danger: "#FFB7A8",
  warningBackground: "#302713",
  warningBorder: "#735F28",
  warningText: "#F3D37B",
  successBackground: "#0F2A21",
  successBorder: "#2C6A53",
  successText: "#8FE0BE",
});

export function resolveMobileColors(scheme: MobileThemeScheme): MobileColors {
  return scheme === "dark" ? darkColors : lightColors;
}
