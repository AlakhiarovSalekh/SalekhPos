import { ReturnsListView } from "@/features/returns/components/ReturnsListView";

export default async function ReturnsPage({ searchParams }: { searchParams: Promise<{ organization?: string; branch?: string; after?: string }> }) {
  const query = await searchParams;
  return <ReturnsListView organizationId={query.organization} branchId={query.branch} after={query.after} />;
}
