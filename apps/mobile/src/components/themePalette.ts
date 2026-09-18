export type MobileThemePalette = Readonly<{
  background: string;
  surface: string;
  text: string;
  muted: string;
  border: string;
  danger: string;
  warning: string;
  warningSurface: string;
  success: string;
  successSurface: string;
}>;

export const lightPalette: MobileThemePalette = Object.freeze({
  background: "#F4F7FB",
  surface: "#FFFFFF",
  text: "#10213A",
  muted: "#53657D",
  border: "#C7D2E0",
  danger: "#A6192E",
  warning: "#6B4F00",
  warningSurface: "#FFF4CE",
  success: "#17613F",
  successSurface: "#E8F5ED",
});

export const darkPalette: MobileThemePalette = Object.freeze({
  background: "#07110F",
  surface: "#0F1D19",
  text: "#EDF7F2",
  muted: "#9DAFAA",
  border: "#263C35",
  danger: "#FFB7A8",
  warning: "#F3D37B",
  warningSurface: "#302713",
  success: "#8FE0BE",
  successSurface: "#0F2A21",
});
