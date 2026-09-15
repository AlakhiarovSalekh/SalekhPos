import type { PriceDraft, RegisterDraft } from "./types";

export function validateRegisterDraft(draft: RegisterDraft): string | null {
  if (!/^[A-Za-z0-9][A-Za-z0-9_-]{0,31}$/.test(draft.code)) {
    return "Register code must use 1-32 letters, numbers, dashes, or underscores.";
  }
  if (draft.name.trim() !== draft.name || draft.name.length < 1 || draft.name.length > 100
      || /[\u0000-\u001f\u007f]/u.test(draft.name)) {
    return "Register name must contain 1-100 printable characters without leading or trailing spaces.";
  }
  return null;
}

function utc(value: string): boolean {
  return value.endsWith("Z") && !Number.isNaN(Date.parse(value));
}

export function validatePriceDraft(draft: PriceDraft): string | null {
  if (!(draft.amount > 0) || draft.amount > 99_999_999_999_999.999999) return "Price amount must be greater than zero.";
  if (!/^[A-Z]{3}$/.test(draft.currency)) return "Currency must be a three-letter uppercase ISO code.";
  if (draft.taxRate < 0 || draft.taxRate > 100) return "Tax rate must be between 0 and 100.";
  if (!utc(draft.validFrom)) return "Valid-from must be a UTC timestamp.";
  if (draft.validUntil !== null && (!utc(draft.validUntil) || Date.parse(draft.validUntil) <= Date.parse(draft.validFrom))) {
    return "Valid-until must be a later UTC timestamp.";
  }
  return null;
}
