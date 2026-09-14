import Link from "next/link";
import { InventoryWorkspace } from "@/features/inventory/InventoryWorkspace";

export default function InventoryPage() {
  return <main className="workspace inventory-page"><nav><Link className="brand" href="/">Salekh<span>Pos</span><span className="brand-dot" /></Link><span className="quiet">WEB OPERATIONS</span></nav><div className="inventory-content"><InventoryWorkspace /></div></main>;
}
