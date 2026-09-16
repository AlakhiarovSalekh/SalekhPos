import { boundedArray, exactKeys, decimal, integer, isoDate, object, text, uuid } from "@/lib/boundedJson";
import type { AnalyticsOverview, SalesTrend, SalesTrendPoint, StoreComparison, StoreComparisonRow } from "./types";

function currency(value: unknown): string | null {
  if (value === null) return null;
  const result = text(value, "currency", 3, 3);
  if (!/^[A-Z]{3}$/u.test(result)) throw new Error("Invalid analytics currency");
  return result;
}

export function parseAnalyticsOverview(value: unknown): AnalyticsOverview {
  const x = object(value, "analytics overview");
  exactKeys(x, ["branchId", "from", "to", "currency", "completedSales", "grossSales",
    "completedReturns", "refunds", "netRevenue", "averageTicket", "distinctProductsSold",
    "positiveStockProducts", "zeroStockProducts", "negativeStockProducts"]);
  return {
    branchId: uuid(x.branchId, "branch"), from: isoDate(x.from, "from"), to: isoDate(x.to, "to"),
    currency: currency(x.currency), completedSales: integer(x.completedSales, "completed sales", 0, 1_000_000_000),
    grossSales: decimal(x.grossSales, "gross sales", { min: 0 }), completedReturns: integer(x.completedReturns, "completed returns", 0, 1_000_000_000),
    refunds: decimal(x.refunds, "refunds", { min: 0 }), netRevenue: decimal(x.netRevenue, "net revenue", { min: -1e15, max: 1e15 }),
    averageTicket: decimal(x.averageTicket, "average ticket", { min: 0 }), distinctProductsSold: integer(x.distinctProductsSold, "products sold", 0, 1_000_000_000),
    positiveStockProducts: integer(x.positiveStockProducts, "positive stock", 0, 1_000_000_000),
    zeroStockProducts: integer(x.zeroStockProducts, "zero stock", 0, 1_000_000_000),
    negativeStockProducts: integer(x.negativeStockProducts, "negative stock", 0, 1_000_000_000),
  };
}
function parseTrendPoint(value: unknown): SalesTrendPoint {
  const x = object(value, "sales trend point");
  exactKeys(x, ["bucketStart", "currency", "completedSales", "grossSales", "refunds", "netRevenue"]);
  return {
    bucketStart: isoDate(x.bucketStart, "bucket start"), currency: currency(x.currency),
    completedSales: integer(x.completedSales, "completed sales", 0, 1_000_000_000),
    grossSales: decimal(x.grossSales, "gross sales", { min: 0 }),
    refunds: decimal(x.refunds, "refunds", { min: 0 }),
    netRevenue: decimal(x.netRevenue, "net revenue", { min: -1e15, max: 1e15 }),
  };
}

export function parseSalesTrend(value: unknown): SalesTrend {
  const x = object(value, "sales trend");
  exactKeys(x, ["branchId", "from", "to", "points"]);
  return {
    branchId: uuid(x.branchId, "branch"), from: isoDate(x.from, "from"), to: isoDate(x.to, "to"),
    points: boundedArray(x.points, "trend points", 366).map(parseTrendPoint),
  };
}
function parseStoreRow(value: unknown): StoreComparisonRow {
  const x = object(value, "store comparison row");
  exactKeys(x, ["branchId", "currency", "completedSales", "grossSales", "refunds", "netRevenue", "averageTicket"]);
  return {
    branchId: uuid(x.branchId, "branch"), currency: currency(x.currency),
    completedSales: integer(x.completedSales, "completed sales", 0, 1_000_000_000),
    grossSales: decimal(x.grossSales, "gross sales", { min: 0 }),
    refunds: decimal(x.refunds, "refunds", { min: 0 }),
    netRevenue: decimal(x.netRevenue, "net revenue", { min: -1e15, max: 1e15 }),
    averageTicket: decimal(x.averageTicket, "average ticket", { min: 0 }),
  };
}

export function parseStoreComparison(value: unknown): StoreComparison {
  const x = object(value, "store comparison");
  exactKeys(x, ["from", "to", "items"]);
  return {
    from: isoDate(x.from, "from"), to: isoDate(x.to, "to"),
    items: boundedArray(x.items, "store comparison items", 500).map(parseStoreRow),
  };
}
