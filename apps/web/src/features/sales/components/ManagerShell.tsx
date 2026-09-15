"use client";

import Link from "next/link";

export function ManagerShell({ organizationId, branchId, branchName, children }: { organizationId?: string; branchId?: string; branchName?: string; children: React.ReactNode }) {
  const query = organizationId && branchId ? `?organization=${encodeURIComponent(organizationId)}&branch=${encodeURIComponent(branchId)}` : "";
  return <main className="manager-shell">
    <header className="manager-header">
      <Link className="brand" href="/dashboard">Salekh<span>Pos</span><span className="brand-dot" /></Link>
      <nav aria-label="Management">
        <Link href={`/sales${query}`}>Sales</Link>
        <Link href={`/returns${query}`}>Returns</Link>
        <Link href="/pricing">Pricing</Link>
        <Link href="/registers">Registers</Link>
        <Link href="/operations">Operations</Link>
        <Link href="/customers">Customers</Link>
        <Link href="/suppliers">Suppliers</Link>
        <Link href="/employees">Employees</Link>
        <Link href="/purchasing">Purchasing</Link>
        <Link href="/reporting">Reports</Link>
        <Link href="/dashboard">Account</Link>
      </nav>
      <div className="scope-chip" title={branchId}>{branchName ?? (branchId ? `Branch ${branchId.slice(0, 8)}` : "Choose a branch")}</div>
    </header>
    {children}
  </main>;
}
