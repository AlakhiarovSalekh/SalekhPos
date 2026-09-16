export type AnalyticsOverview = Readonly<{
  branchId: string;
  from: string;
  to: string;
  currency: string | null;
  completedSales: number;
  grossSales: number;
  completedReturns: number;
  refunds: number;
  netRevenue: number;
  averageTicket: number;
  distinctProductsSold: number;
  positiveStockProducts: number;
  zeroStockProducts: number;
  negativeStockProducts: number;
}>;

export type SalesTrendPoint = Readonly<{
  bucketStart: string;
  currency: string | null;
  completedSales: number;
  grossSales: number;
  refunds: number;
  netRevenue: number;
}>;
export type SalesTrend = Readonly<{
  branchId: string;
  from: string;
  to: string;
  points: readonly SalesTrendPoint[];
}>;

export type StoreComparisonRow = Readonly<{
  branchId: string;
  currency: string | null;
  completedSales: number;
  grossSales: number;
  refunds: number;
  netRevenue: number;
  averageTicket: number;
}>;

export type StoreComparison = Readonly<{
  from: string;
  to: string;
  items: readonly StoreComparisonRow[];
}>;
