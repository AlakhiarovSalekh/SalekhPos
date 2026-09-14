import { describe, expect, it } from "vitest";

import { parseRuntimeConfig, RuntimeConfigError } from "../../src/config/runtimeConfig";

describe("parseRuntimeConfig", () => {
  it("normalizes a secure API origin", () => {
    expect(
      parseRuntimeConfig({
        appEnvironment: "production",
        apiBaseUrl: "https://api.salekhpos.example/",
      }),
    ).toEqual({
      environment: "production",
      apiBaseUrl: "https://api.salekhpos.example",
    });
  });

  it("allows HTTP only for development loopback", () => {
    expect(
      parseRuntimeConfig({ appEnvironment: "development", apiBaseUrl: "http://localhost:5080" }),
    ).toEqual({ environment: "development", apiBaseUrl: "http://localhost:5080" });

    expect(() =>
      parseRuntimeConfig({ appEnvironment: "staging", apiBaseUrl: "http://localhost:5080" }),
    ).toThrow(RuntimeConfigError);
    expect(() =>
      parseRuntimeConfig({ appEnvironment: "development", apiBaseUrl: "http://192.168.1.9:5080" }),
    ).toThrow(RuntimeConfigError);
  });

  it.each([
    "https://user:secret@example.com",
    "https://example.com/api",
    "https://example.com?tenant=a",
    "https://example.com#fragment",
  ])("rejects a non-origin API URL: %s", (apiBaseUrl) => {
    expect(() => parseRuntimeConfig({ appEnvironment: "production", apiBaseUrl })).toThrow(
      RuntimeConfigError,
    );
  });
});
