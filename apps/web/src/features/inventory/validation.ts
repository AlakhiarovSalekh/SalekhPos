import type { MovementInput, MovementKind } from "./types";

export type MovementDraft = { productId: string; kind: MovementKind; quantity: string; reason: string; occurredAt: string };
export type ValidationResult = { value?: MovementInput; errors: Partial<Record<keyof MovementDraft, string>> };
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const quantityPattern = /^(?:0|[1-9]\d*)(?:\.\d{1,6})?$/;

export function validateOrganizationId(value: string): string | null {
  return uuidPattern.test(value.trim()) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value.trim()) ? null : "Enter a valid organization ID.";
}

export function validateMovement(draft: MovementDraft, now = new Date()): ValidationResult {
  const errors: ValidationResult["errors"] = {};
  const productId = draft.productId.trim();
  if (!uuidPattern.test(productId) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(productId)) errors.productId = "Choose a valid product.";
  const quantity = draft.quantity.trim();
  const quantityParts = quantity.match(quantityPattern);
  const [integer = "", fraction = ""] = quantity.split(".");
  if (!quantityParts || integer.length > 14 || !/[1-9]/.test(integer + fraction)) errors.quantity = "Use a positive quantity with at most 6 decimal places.";
  const reason = draft.reason.trim();
  if (reason.length > 200 || /[\u0000-\u001f\u007f-\u009f\ud800-\udfff]/u.test(reason)) errors.reason = "Reason must be 200 safe characters or fewer.";
  const occurred = new Date(draft.occurredAt);
  if (!draft.occurredAt || Number.isNaN(occurred.getTime()) || occurred.getTime() > now.getTime() + 5 * 60_000) errors.occurredAt = "Choose a valid time no more than 5 minutes in the future.";
  if (Object.keys(errors).length) return { errors };
  return { errors, value: { productId, kind: draft.kind, quantity, reason: reason || null, occurredAt: occurred.toISOString() } };
}
