export const sessionSchemaVersion = 1 as const;

export type SessionAuthorization = Readonly<{
  roles: readonly string[];
  permissions: readonly string[];
}>;

export type AuthenticatedSession = Readonly<{
  schemaVersion: typeof sessionSchemaVersion;
  accessToken: string;
  refreshToken?: string;
  accessTokenExpiresAt: number;
  subject: string;
  displayName?: string;
  organizationId: string;
  branchId?: string;
  authorization: SessionAuthorization;
}>;

export type SessionValidity = "valid" | "expired";

export class InvalidSessionError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "InvalidSessionError";
  }
}

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu;
const MAX_TOKEN_LENGTH = 16_384;
const MAX_AUTHORIZATION_ENTRIES = 256;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function requireNonEmptyString(value: unknown, field: string, maximum = 512): string {
  if (
    typeof value !== "string" ||
    value.length === 0 ||
    value.length > maximum ||
    value.trim() !== value ||
    /[\u0000-\u001f\u007f]/u.test(value)
  ) {
    throw new InvalidSessionError(`${field} must be a non-empty string.`);
  }
  return value;
}

function optionalNonEmptyString(value: unknown, field: string, maximum = 512): string | undefined {
  if (value === undefined) {
    return undefined;
  }
  return requireNonEmptyString(value, field, maximum);
}

function requireToken(value: unknown, field: string): string {
  const token = requireNonEmptyString(value, field, MAX_TOKEN_LENGTH);
  if (/[\u0000-\u0020\u007f]/u.test(token)) throw new InvalidSessionError(`${field} contains unsafe characters.`);
  return token;
}

function requireStringArray(value: unknown, field: string): readonly string[] {
  if (!Array.isArray(value) || value.length > MAX_AUTHORIZATION_ENTRIES) {
    throw new InvalidSessionError(`${field} must be an array.`);
  }

  const values = value.map((entry) => requireNonEmptyString(entry, field, 128));
  return Object.freeze([...new Set(values)]);
}

function requireUuid(value: unknown, field: string): string {
  const identifier = requireNonEmptyString(value, field, 36);
  if (!UUID_PATTERN.test(identifier) || identifier === "00000000-0000-0000-0000-000000000000") {
    throw new InvalidSessionError(`${field} must be a non-empty canonical UUID.`);
  }
  return identifier.toLowerCase();
}

export function parseSession(value: unknown): AuthenticatedSession {
  if (!isRecord(value) || value.schemaVersion !== sessionSchemaVersion) {
    throw new InvalidSessionError("The stored session version is unsupported.");
  }
  if (!isRecord(value.authorization)) {
    throw new InvalidSessionError("authorization is required.");
  }
  if (
    typeof value.accessTokenExpiresAt !== "number" ||
    !Number.isSafeInteger(value.accessTokenExpiresAt) ||
    value.accessTokenExpiresAt <= 0
  ) {
    throw new InvalidSessionError("accessTokenExpiresAt must be a positive Unix timestamp.");
  }

  const refreshTokenValue = optionalNonEmptyString(value.refreshToken, "refreshToken", MAX_TOKEN_LENGTH);
  const refreshToken = refreshTokenValue === undefined ? undefined : requireToken(refreshTokenValue, "refreshToken");
  const displayName = optionalNonEmptyString(value.displayName, "displayName", 200);
  const branchId = optionalNonEmptyString(value.branchId, "branchId");

  return Object.freeze({
    schemaVersion: sessionSchemaVersion,
    accessToken: requireToken(value.accessToken, "accessToken"),
    ...(refreshToken === undefined ? {} : { refreshToken }),
    accessTokenExpiresAt: value.accessTokenExpiresAt,
    subject: requireNonEmptyString(value.subject, "subject", 256),
    ...(displayName === undefined ? {} : { displayName }),
    organizationId: requireUuid(value.organizationId, "organizationId"),
    ...(branchId === undefined ? {} : { branchId: requireUuid(branchId, "branchId") }),
    authorization: Object.freeze({
      roles: requireStringArray(value.authorization.roles, "authorization.roles"),
      permissions: requireStringArray(
        value.authorization.permissions,
        "authorization.permissions",
      ),
    }),
  });
}

export function getSessionValidity(
  session: Pick<AuthenticatedSession, "accessTokenExpiresAt">,
  nowMilliseconds: number,
  expirySkewMilliseconds = 30_000,
): SessionValidity {
  return session.accessTokenExpiresAt * 1_000 > nowMilliseconds + expirySkewMilliseconds
    ? "valid"
    : "expired";
}

export function serializeSession(session: AuthenticatedSession): string {
  return JSON.stringify(parseSession(session));
}

export function deserializeSession(serialized: string): AuthenticatedSession {
  try {
    return parseSession(JSON.parse(serialized) as unknown);
  } catch (error) {
    if (error instanceof InvalidSessionError) {
      throw error;
    }
    throw new InvalidSessionError("The stored session is not valid JSON.");
  }
}
