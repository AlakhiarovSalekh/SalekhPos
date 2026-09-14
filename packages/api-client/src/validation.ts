import { ApiClientConfigurationError } from "./errors.js";

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;
const LOOPBACK_HOSTS = new Set(["localhost", "127.0.0.1", "[::1]"]);

export function isUuid(value: string): boolean {
  return UUID_PATTERN.test(value);
}

export function assertUuid(value: string, name = "identifier"): string {
  if (!isUuid(value)) {
    throw new ApiClientConfigurationError(`${name} must be a canonical UUID.`);
  }

  return value.toLowerCase();
}

export function normalizeApiBaseUrl(value: string): string {
  let url: URL;
  try {
    url = new URL(value);
  } catch {
    throw new ApiClientConfigurationError("API base URL must be an absolute HTTP(S) origin.");
  }

  const isSecure = url.protocol === "https:";
  const isLoopbackDevelopment = url.protocol === "http:" && LOOPBACK_HOSTS.has(url.hostname);
  if (!isSecure && !isLoopbackDevelopment) {
    throw new ApiClientConfigurationError("API base URL must use HTTPS, except for loopback development.");
  }

  if (
    url.username !== "" ||
    url.password !== "" ||
    url.pathname !== "/" ||
    url.search !== "" ||
    url.hash !== ""
  ) {
    throw new ApiClientConfigurationError("API base URL must contain only an origin.");
  }

  return `${url.origin}/`;
}

export function assertRelativeApiPath(path: string): string {
  if (
    !path.startsWith("/") ||
    path.startsWith("//") ||
    path.includes("\\") ||
    path.includes("?") ||
    path.includes("#") ||
    /[\u0000-\u001f\u007f]/u.test(path)
  ) {
    throw new ApiClientConfigurationError(
      "API request path must be an absolute-path reference without an origin, query, or fragment.",
    );
  }

  for (const segment of path.split("/")) {
    let decoded: string;
    try {
      decoded = decodeURIComponent(segment);
    } catch {
      throw new ApiClientConfigurationError("API request path contains invalid percent encoding.");
    }

    if (decoded === "." || decoded === ".." || decoded.includes("/") || decoded.includes("\\")) {
      throw new ApiClientConfigurationError("API request path contains an unsafe segment.");
    }
  }

  return path;
}
