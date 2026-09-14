import { ReturnDetailView } from "@/features/returns/components/ReturnDetailView";

export default async function ReturnPage({ params, searchParams }: { params: Promise<{ returnId: string }>; searchParams: Promise<{ organization?: string; branch?: string }> }) {
  const [route, query] = await Promise.all([params, searchParams]);
  return <ReturnDetailView returnId={route.returnId} organizationId={query.organization} branchId={query.branch} />;
}
