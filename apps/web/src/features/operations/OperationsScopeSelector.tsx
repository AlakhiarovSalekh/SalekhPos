"use client";

import type { OperationsScope } from "./useOperationsScope";

export function OperationsScopeSelector({ scope, requireBranch = true }: {
  scope: OperationsScope;
  requireBranch?: boolean;
}) {
  return <section className="operations-scope" aria-label="Business scope">
    <label>
      <span>Organization</span>
      <select value={scope.organizationId} disabled={scope.loading}
        onChange={event => scope.setOrganizationId(event.target.value)}>
        {scope.organizations.length === 0 ? <option value="">No organization</option> : null}
        {scope.organizations.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}
      </select>
    </label>
    {requireBranch ? <label>
      <span>Branch</span>
      <select value={scope.branchId} disabled={scope.loading || !scope.organizationId}
        onChange={event => scope.setBranchId(event.target.value)}>
        {scope.branches.length === 0 ? <option value="">No branch</option> : null}
        {scope.branches.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}
      </select>
    </label> : null}
    {scope.error ? <p role="alert" className="operations-error">{scope.error}</p> : null}
  </section>;
}
