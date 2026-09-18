import {
  DynamicColorIOS,
  Platform,
  PlatformColor,
  type ColorValue,
} from "react-native";

import { darkPalette, lightPalette } from "./themePalette";

function adaptiveColor(light: string, dark: string, androidResource: string): ColorValue {
  if (Platform.OS === "ios") {
    return DynamicColorIOS({ light, dark });
  }
  if (Platform.OS === "android") {
    return PlatformColor(androidResource);
  }
  return light;
}

export const colors = Object.freeze({
  background: adaptiveColor(
    lightPalette.background,
    darkPalette.background,
    "?android:attr/colorBackground",
  ),
  surface: adaptiveColor(
    lightPalette.surface,
    darkPalette.surface,
    "?android:attr/colorBackground",
  ),
  text: adaptiveColor(
    lightPalette.text,
    darkPalette.text,
    "?android:attr/textColorPrimary",
  ),
  muted: adaptiveColor(
    lightPalette.muted,
    darkPalette.muted,
    "?android:attr/textColorSecondary",
  ),
  primary: "#075E54" as ColorValue,
  primaryPressed: "#06483F" as ColorValue,
  onPrimary: "#FFFFFF" as ColorValue,
  border: adaptiveColor(
    lightPalette.border,
    darkPalette.border,
    "?android:attr/colorControlNormal",
  ),
  danger: adaptiveColor(
    lightPalette.danger,
    darkPalette.danger,
    "@android:color/holo_red_light",
  ),
  warning: adaptiveColor(
    lightPalette.warning,
    darkPalette.warning,
    "@android:color/holo_orange_light",
  ),
  warningSurface: adaptiveColor(
    lightPalette.warningSurface,
    darkPalette.warningSurface,
    "?android:attr/colorBackground",
  ),
  success: adaptiveColor(
    lightPalette.success,
    darkPalette.success,
    "?android:attr/colorAccent",
  ),
  successSurface: adaptiveColor(
    lightPalette.successSurface,
    darkPalette.successSurface,
    "?android:attr/colorBackground",
  ),
});
