import { ApiClientConfigurationError } from "./errors.js";
import { assertUuid } from "./validation.js";

function encodeSegment(segment: string): string {
  if (segment.length === 0) {
    throw new ApiClientConfigurationError("API path segments cannot be empty.");
  }

  return encodeURIComponent(segment).replace(/[!'()*]/gu, (character) =>
    `%${character.charCodeAt(0).toString(16).toUpperCase()}`,
  );
}

function suffix(segments: readonly string[]): string {
  return segments.length === 0 ? "" : `/${segments.map(encodeSegment).join("/")}`;
}

export function organizationPath(organizationId: string, ...segments: readonly string[]): string {
  const organization = assertUuid(organizationId, "organizationId");
  return `/api/v1/organizations/${organization}${suffix(segments)}`;
}

export function branchPath(
  organizationId: string,
  branchId: string,
  ...segments: readonly string[]
): string {
  const organization = assertUuid(organizationId, "organizationId");
  const branch = assertUuid(branchId, "branchId");
  return `/api/v1/organizations/${organization}/branches/${branch}${suffix(segments)}`;
}
