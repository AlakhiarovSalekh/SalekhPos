import Constants from "expo-constants";
import { Stack } from "expo-router";
import * as SplashScreen from "expo-splash-screen";
import { StatusBar } from "expo-status-bar";
import { useEffect, useMemo } from "react";
import { SafeAreaProvider } from "react-native-safe-area-context";

import { AppErrorBoundary } from "@/components/AppErrorBoundary";
import { ErrorSurface, LoadingSurface } from "@/components/primitives";
import { parseRuntimeConfig, type RuntimeConfig } from "@/config/runtimeConfig";
import { LocalizationProvider, useLocalization } from "@/localization/LocalizationProvider";
import { SessionProvider, useSession } from "@/state/SessionContext";

void SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <LocalizationProvider>
        <AppErrorBoundary>
          <StatusBar style="dark" />
          <SessionProvider>
            <RootNavigator />
          </SessionProvider>
        </AppErrorBoundary>
      </LocalizationProvider>
    </SafeAreaProvider>
  );
}

function RootNavigator() {
  const { status, restore } = useSession();
  const { t } = useLocalization();
  const configResult = useMemo(() => {
    try {
      return { config: parseRuntimeConfig(Constants.expoConfig?.extra ?? {}) } as const;
    } catch (error) {
      return { error: error instanceof Error ? error : new Error("Invalid configuration.") } as const;
    }
  }, []);

  useEffect(() => {
    if (status !== "restoring") {
      void SplashScreen.hideAsync();
    }
  }, [status]);

  if (status === "restoring") {
    return <LoadingSurface />;
  }
  if ("error" in configResult) {
    return <ErrorSurface title={t("config.title")} message={t("config.message")} />;
  }
  if (status === "error") {
    return (
      <ErrorSurface
        title={t("error.title")}
        message={t("session.restoreError")}
        onRetry={() => void restore()}
      />
    );
  }

  return <AuthenticatedStack runtimeConfig={configResult.config} />;
}

function AuthenticatedStack({ runtimeConfig: _runtimeConfig }: Readonly<{ runtimeConfig: RuntimeConfig }>) {
  const { status } = useSession();
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Protected guard={status === "signedOut"}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
      <Stack.Protected guard={status === "authenticated"}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
    </Stack>
  );
}
