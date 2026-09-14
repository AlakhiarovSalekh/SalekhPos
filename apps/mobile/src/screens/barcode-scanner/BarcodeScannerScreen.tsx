import { CameraView, useCameraPermissions, type BarcodeScanningResult } from "expo-camera";
import { useRouter } from "expo-router";
import { useCallback, useState } from "react";
import { StyleSheet, Text, View } from "react-native";

import { AppButton, Screen, colors, textStyles } from "@/components/primitives";
import { useLocalization } from "@/localization/LocalizationProvider";

export function BarcodeScannerScreen() {
  const router = useRouter();
  const { t } = useLocalization();
  const [permission, requestPermission] = useCameraPermissions();
  const [scannedValue, setScannedValue] = useState<string | null>(null);

  const handleBarcodeScanned = useCallback((result: BarcodeScanningResult) => {
    const value = result.data.trim();
    setScannedValue(value);
    if (/^\d{4,64}$/u.test(value)) {
      router.replace({ pathname: "/products", params: { barcode: value } });
    }
  }, [router]);

  const permissionMessage =
    permission?.granted === false
      ? permission.canAskAgain
        ? t("scanner.permissionDenied")
        : t("scanner.permissionSettings")
      : null;

  return (
    <Screen>
      <AppButton onPress={() => router.back()} accessibilityLabel={t("common.back")}>
        {t("common.back")}
      </AppButton>
      <Text style={textStyles.title} accessibilityRole="header">
        {t("scanner.title")}
      </Text>
      <Text style={textStyles.body}>{t("scanner.explanation")}</Text>

      {permission?.granted === true ? (
        <View style={styles.cameraContainer}>
          <CameraView
            accessibilityLabel={t("scanner.title")}
            barcodeScannerSettings={{
              barcodeTypes: ["ean13", "ean8", "upc_a", "upc_e", "code128", "qr"],
            }}
            onBarcodeScanned={scannedValue === null ? handleBarcodeScanned : undefined}
            style={styles.camera}
          />
        </View>
      ) : (
        <AppButton onPress={() => void requestPermission()} disabled={permission?.canAskAgain === false}>
          {t("scanner.start")}
        </AppButton>
      )}

      {permissionMessage === null ? null : (
        <Text accessibilityRole="alert" style={styles.errorText}>
          {permissionMessage}
        </Text>
      )}

      {scannedValue === null ? null : (
        <View style={styles.result} accessible accessibilityLiveRegion="polite">
          <Text style={textStyles.body}>{t("scanner.scanned", { value: scannedValue })}</Text>
          {!/^\d{4,64}$/u.test(scannedValue) ? <Text style={styles.errorText}>{t("scanner.invalidBarcode")}</Text> : null}
          <AppButton onPress={() => setScannedValue(null)}>{t("scanner.scanAgain")}</AppButton>
        </View>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  cameraContainer: { minHeight: 320, overflow: "hidden", borderRadius: 16 },
  camera: { flex: 1, minHeight: 320 },
  result: {
    padding: 16,
    gap: 12,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
  },
  errorText: { color: colors.danger, fontSize: 16, lineHeight: 24 },
});
