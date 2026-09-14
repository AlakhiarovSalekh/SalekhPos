export const appEnvironments = [
  "development",
  "test",
  "staging",
  "production",
] as const;

export type AppEnvironment = (typeof appEnvironments)[number];

export type RuntimeConfig = Readonly<{
  apiBaseUrl: string;
  environment: AppEnvironment;
}>;

export class RuntimeConfigError extends Error {
  readonly code: "invalid_environment" | "invalid_api_base_url";

  constructor(
    code: RuntimeConfigError["code"],
    message: string,
  ) {
    super(message);
    this.name = "RuntimeConfigError";
    this.code = code;
  }
}

export function parseRuntimeConfig(input: Readonly<Record<string, unknown>>): RuntimeConfig {
  const environment = input.appEnvironment;
  if (
    typeof environment !== "string" ||
    !appEnvironments.includes(environment as AppEnvironment)
  ) {
    throw new RuntimeConfigError(
      "invalid_environment",
      "EXPO_PUBLIC_APP_ENV must be development, test, staging, or production.",
    );
  }
  const appEnvironment = environment as AppEnvironment;

  if (typeof input.apiBaseUrl !== "string" || input.apiBaseUrl.trim().length === 0) {
    throw new RuntimeConfigError(
      "invalid_api_base_url",
      "EXPO_PUBLIC_API_BASE_URL is required.",
    );
  }

  let url: URL;
  try {
    url = new URL(input.apiBaseUrl);
  } catch {
    throw new RuntimeConfigError(
      "invalid_api_base_url",
      "EXPO_PUBLIC_API_BASE_URL must be an absolute URL.",
    );
  }

  const isDevelopmentLoopback =
    appEnvironment === "development" &&
    url.protocol === "http:" &&
    (url.hostname === "localhost" || url.hostname === "127.0.0.1");

  if (url.protocol !== "https:" && !isDevelopmentLoopback) {
    throw new RuntimeConfigError(
      "invalid_api_base_url",
      "The API base URL must use HTTPS. HTTP is allowed only for development loopback.",
    );
  }

  if (
    url.username.length > 0 ||
    url.password.length > 0 ||
    url.search.length > 0 ||
    url.hash.length > 0 ||
    (url.pathname !== "/" && url.pathname !== "")
  ) {
    throw new RuntimeConfigError(
      "invalid_api_base_url",
      "The API base URL must be an origin without credentials, path, query, or fragment.",
    );
  }

  return Object.freeze({
    apiBaseUrl: url.origin,
    environment: appEnvironment,
  });
}
