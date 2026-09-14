import type { ConfigContext, ExpoConfig } from "expo/config";

export default ({ config }: ConfigContext): ExpoConfig => ({
  ...config,
  name: "SalekhPos",
  slug: "salekhpos-mobile",
  version: "0.1.0",
  platforms: ["ios", "android"],
  orientation: "portrait",
  scheme: "salekhpos",
  userInterfaceStyle: "automatic",
  locales: {
    en: "./locales/en.json",
    az: "./locales/az.json",
    ka: "./locales/ka.json",
  },
  ios: {
    bundleIdentifier: "com.salekhpos.mobile",
    supportsTablet: true,
  },
  android: {
    package: "com.salekhpos.mobile",
    adaptiveIcon: {
      backgroundColor: "#0B172A",
    },
  },
  plugins: [
    "expo-router",
    "expo-localization",
    [
      "expo-secure-store",
      {
        configureAndroidBackup: true,
        faceIDPermission:
          "Allow SalekhPos to use Face ID to protect your signed-in session.",
      },
    ],
    [
      "expo-camera",
      {
        cameraPermission:
          "Allow SalekhPos to use the camera when you choose to scan a barcode.",
        recordAudioAndroid: false,
      },
    ],
  ],
  experiments: {
    typedRoutes: true,
  },
  extra: {
    apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? "",
    appEnvironment: process.env.EXPO_PUBLIC_APP_ENV ?? "development",
  },
});
