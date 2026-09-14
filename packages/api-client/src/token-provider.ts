import { ApiClientConfigurationError } from "./errors.js";

export interface BearerTokenProvider {
  getAccessToken(): Promise<string | null>;
}

export interface InMemoryBearerTokenProvider extends BearerTokenProvider {
  setAccessToken(token: string | null): void;
  clear(): void;
}

function validateToken(token: string): string {
  if (
    typeof token !== "string" ||
    token.length === 0 ||
    token.trim() !== token ||
    /[\u0000-\u0020\u007f]/u.test(token)
  ) {
    throw new ApiClientConfigurationError("Bearer token is empty or contains unsafe characters.");
  }

  return token;
}

export function createInMemoryBearerTokenProvider(
  initialToken: string | null = null,
): InMemoryBearerTokenProvider {
  let accessToken = initialToken === null ? null : validateToken(initialToken);

  return {
    async getAccessToken(): Promise<string | null> {
      return accessToken;
    },
    setAccessToken(token: string | null): void {
      accessToken = token === null ? null : validateToken(token);
    },
    clear(): void {
      accessToken = null;
    },
  };
}

export function validateBearerToken(token: string): string {
  return validateToken(token);
}
