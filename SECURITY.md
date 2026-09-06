# Security

SalekhPos is under development. No production release or security certification
is currently claimed. The implementation status and remaining controls are tracked
in `docs/architecture/master-implementation-plan.md` and its coverage matrix.

Report suspected vulnerabilities privately to the project owner through an
existing private contact channel. If this repository is hosted on GitHub with
private vulnerability reporting enabled, use **Security → Report a vulnerability**.
There is currently no dedicated public security email or guaranteed response SLA.
Do not include customer records, working credentials, access tokens, or full
connection strings in public issues or CI logs. Provide a minimal reproduction,
affected version/commit, expected behavior, impact, and redacted evidence.

Runtime database credentials must belong to the restricted `salekhpos_runtime`
role; migration credentials belong to a separate administrative context. Never
reuse the disposable test credentials outside local tests. Production secrets
must come from the selected platform's managed secret storage, with access and
rotation defined before deployment. Removing a leaked secret from the current
file does not revoke it: revoke/rotate it and inspect access and Git history.

The local CI gate checks direct/transitive NuGet advisories and uses a pinned,
checksum-verified Gitleaks release for the working tree and available Git history.
GitHub Actions use commit SHAs, read-only permissions, ephemeral hosted runners,
and no production secrets. Dependency advisories require network access; an
unavailable or incomplete audit is a failure, not a clean scan. Automated scans
cannot establish absence of vulnerabilities or replace security review.
