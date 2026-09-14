import { SalesListView } from "@/features/sales/components/SalesListView";

export default async function SalesPage({ searchParams }: { searchParams: Promise<{ organization?: string; branch?: string; after?: string }> }) {
  const query = await searchParams;
  return <SalesListView organizationId={query.organization} branchId={query.branch} after={query.after} />;
}
