import { ManagerShell } from "@/features/sales/components/ManagerShell";
import { RegisterWorkspace } from "@/features/operations/RegisterWorkspace";

export default function RegistersPage() {
  return <ManagerShell><div className="manager-content"><div className="page-heading"><div><p className="eyebrow">REGISTERS</p><h1>Register control<span className="accent">.</span></h1><p>Provision and review branch registers without weakening trusted-terminal security.</p></div></div><RegisterWorkspace /></div></ManagerShell>;
}
