import { SaleDetailView } from "@/features/sales/components/SaleDetailView";

export default async function SalePage({ params, searchParams }: { params: Promise<{ saleId: string }>; searchParams: Promise<{ organization?: string; branch?: string }> }) {
  const [route, query] = await Promise.all([params, searchParams]);
  return <SaleDetailView saleId={route.saleId} organizationId={query.organization} branchId={query.branch} />;
}
