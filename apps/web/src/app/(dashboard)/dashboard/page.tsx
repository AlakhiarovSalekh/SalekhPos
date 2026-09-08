import Link from "next/link";
import { SessionPanel } from "@/features/auth/SessionPanel";

export default function DashboardPage() {
  return <main className="workspace"><nav><Link className="brand" href="/">Salekh<span>Pos</span><span className="brand-dot" /></Link><span className="quiet">YOUR WORKSPACE</span></nav><section className="workspace-content"><SessionPanel dashboard /></section></main>;
}
