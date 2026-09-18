# Tenant isolation incident

**Trigger:** Cross-tenant access is suspected

## Immediate actions

Contain affected credentials, preserve audit evidence, verify RLS and app.organization_id context, identify impacted tenant/resource ranges, rotate credentials if necessary, and notify according to incident policy.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
