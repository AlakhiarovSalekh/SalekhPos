import { ApiError, getCsrfToken, requestJson } from "@/features/sales/api";
import { uuid } from "@/lib/boundedJson";
import { parseLocalizationSettings, parseLocalizationWriteResult } from "./parsers";
import type { LocalizationSettings, LocalizationWriteResult } from "./types";

const root = (organizationId: string) =>
  `/bff/api/v1/organizations/${uuid(organizationId, "organization")}/localization`;

export async function getLocalizationSettings(organizationId: string): Promise<LocalizationSettings | null> {
  try { return await requestJson(root(organizationId), parseLocalizationSettings); }
  catch (error) { if (error instanceof ApiError && error.kind === "not-found") return null; throw error; }
}

export async function updateLocalizationSettings(organizationId: string, input: {
  countryCode: string; defaultLocale: string; defaultCurrency: string; timeZone: string;
  supportedLocales: readonly string[]; firstDayOfWeek: number; expectedVersion: number | null;
}): Promise<LocalizationWriteResult> {
  const csrf = await getCsrfToken();
  return requestJson(root(organizationId), parseLocalizationWriteResult, {
    method: "PUT",
    headers: { "content-type": "application/json", "X-CSRF-TOKEN": csrf, "Idempotency-Key": crypto.randomUUID() },
    body: JSON.stringify(input),
  });
}
