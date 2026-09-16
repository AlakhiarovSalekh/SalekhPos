import { boundedArray, decimal, exactKeys, integer, isoDate, object, optionalUuid, text, uuid } from "@/lib/boundedJson";
import type { TaxCalculation, TaxProfile, TaxProfilePage, TaxRate } from "./types";

function bool(value: unknown, label: string): boolean {
  if (typeof value !== "boolean") throw new Error(`Invalid ${label}`);
  return value;
}
function nullableDate(value: unknown, label: string): string | null {
  return value === null ? null : isoDate(value, label);
}
export function parseTaxProfile(value: unknown): TaxProfile {
  const x = object(value, "tax profile");
  exactKeys(x, ["id","code","name","countryCode","pricesIncludeTax","isActive","version","createdAt","updatedAt"]);
  return { id: uuid(x.id,"id"), code: text(x.code,"code",40,1), name: text(x.name,"name",120,1),
    countryCode: text(x.countryCode,"country",2,2), pricesIncludeTax: bool(x.pricesIncludeTax,"prices include tax"),
    isActive: bool(x.isActive,"active"), version: integer(x.version,"version",1,Number.MAX_SAFE_INTEGER),
    createdAt: isoDate(x.createdAt,"created"), updatedAt: isoDate(x.updatedAt,"updated") };
}
export function parseTaxProfilePage(value: unknown): TaxProfilePage {
  const x = object(value, "tax profile page"); exactKeys(x, ["items","nextCursor"]);
  return { items: boundedArray(x.items,"items",100).map(parseTaxProfile), nextCursor: optionalUuid(x.nextCursor,"next cursor") };
}
export function parseTaxRate(value: unknown): TaxRate {
  const x = object(value, "tax rate");
  exactKeys(x, ["id","profileId","branchId","categoryCode","ratePercent","effectiveFrom","effectiveUntil","isActive","version"]);
  return { id: uuid(x.id,"id"), profileId: uuid(x.profileId,"profile id"), branchId: optionalUuid(x.branchId,"branch id"),
    categoryCode: text(x.categoryCode,"category",40,1), ratePercent: decimal(x.ratePercent,"rate",{ min:0,max:100 }),
    effectiveFrom: isoDate(x.effectiveFrom,"effective from"), effectiveUntil: nullableDate(x.effectiveUntil,"effective until"),
    isActive: bool(x.isActive,"active"), version: integer(x.version,"version",1,Number.MAX_SAFE_INTEGER) };
}
export function parseTaxRates(value: unknown): readonly TaxRate[] {
  return boundedArray(value,"tax rates",500).map(parseTaxRate);
}
export function parseTaxCalculation(value: unknown): TaxCalculation {
  const x = object(value, "tax calculation");
  exactKeys(x, ["profileId","rateId","categoryCode","ratePercent","netAmount","taxAmount","grossAmount","pricesIncludeTax"]);
  return { profileId: uuid(x.profileId,"profile id"), rateId: optionalUuid(x.rateId,"rate id"),
    categoryCode: text(x.categoryCode,"category",40,1), ratePercent: decimal(x.ratePercent,"rate",{ min:0,max:100 }),
    netAmount: decimal(x.netAmount,"net"), taxAmount: decimal(x.taxAmount,"tax"), grossAmount: decimal(x.grossAmount,"gross"),
    pricesIncludeTax: bool(x.pricesIncludeTax,"prices include tax") };
}
