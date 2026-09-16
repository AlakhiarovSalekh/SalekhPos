export type TaxProfile = Readonly<{
  id: string; code: string; name: string; countryCode: string;
  pricesIncludeTax: boolean; isActive: boolean; version: number;
  createdAt: string; updatedAt: string;
}>;
export type TaxProfilePage = Readonly<{ items: readonly TaxProfile[]; nextCursor: string | null }>;
export type TaxRate = Readonly<{
  id: string; profileId: string; branchId: string | null; categoryCode: string;
  ratePercent: number; effectiveFrom: string; effectiveUntil: string | null;
  isActive: boolean; version: number;
}>;
export type TaxCalculation = Readonly<{
  profileId: string; rateId: string | null; categoryCode: string; ratePercent: number;
  netAmount: number; taxAmount: number; grossAmount: number; pricesIncludeTax: boolean;
}>;
