import { defineConfig } from "vitest/config";
import { fileURLToPath } from "node:url";

export default defineConfig({
  resolve: {
    alias: {
      "@salekhpos/packages-api-client": fileURLToPath(new URL("../../packages/api-client/src/index.ts", import.meta.url)),
      "expo-crypto": fileURLToPath(new URL("./tests/unit/stubs/expoCrypto.ts", import.meta.url)),
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
  },
  test: {
    environment: "node",
    include: ["tests/unit/**/*.test.ts"],
    coverage: {
      reporter: ["text", "json-summary"],
    },
  },
});
