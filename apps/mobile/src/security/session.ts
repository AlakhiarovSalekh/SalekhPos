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

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function requireNonEmptyString(value: unknown, field: string): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new InvalidSessionError(`${field} must be a non-empty string.`);
  }
  return value;
}

function optionalNonEmptyString(value: unknown, field: string): string | undefined {
  if (value === undefined) {
    return undefined;
  }
  return requireNonEmptyString(value, field);
}

function requireStringArray(value: unknown, field: string): readonly string[] {
  if (!Array.isArray(value)) {
    throw new InvalidSessionError(`${field} must be an array.`);
  }

  const values = value.map((entry) => requireNonEmptyString(entry, field));
  return Object.freeze([...new Set(values)]);
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

  const refreshToken = optionalNonEmptyString(value.refreshToken, "refreshToken");
  const displayName = optionalNonEmptyString(value.displayName, "displayName");
  const branchId = optionalNonEmptyString(value.branchId, "branchId");

  return Object.freeze({
    schemaVersion: sessionSchemaVersion,
    accessToken: requireNonEmptyString(value.accessToken, "accessToken"),
    ...(refreshToken === undefined ? {} : { refreshToken }),
    accessTokenExpiresAt: value.accessTokenExpiresAt,
    subject: requireNonEmptyString(value.subject, "subject"),
    ...(displayName === undefined ? {} : { displayName }),
    organizationId: requireNonEmptyString(value.organizationId, "organizationId"),
    ...(branchId === undefined ? {} : { branchId }),
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
