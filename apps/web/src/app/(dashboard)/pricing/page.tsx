import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { PricingWorkspace } from "@/features/operations/PricingWorkspace";

export default function PricingPage() {
  return <ManagerShell><div className="manager-content"><div className="page-heading"><div><p className="eyebrow">PRICING</p><h1>Price control<span className="accent">.</span></h1><p>Schedule organization-wide or branch-specific prices and verify the effective price before sale.</p></div></div><PricingWorkspace /></div></ManagerShell>;
}
