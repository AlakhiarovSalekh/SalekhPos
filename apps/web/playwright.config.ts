import { defineConfig } from "@playwright/test";
export default defineConfig({ testDir: "./tests/e2e", timeout: 60_000, workers: 1, retries: 0, use: { baseURL: "http://localhost:3000", trace: "off", screenshot: "only-on-failure" } });
