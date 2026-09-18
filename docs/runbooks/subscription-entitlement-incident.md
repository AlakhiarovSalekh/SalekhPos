# Subscription entitlement incident

**Trigger:** Effective entitlement does not match active plan

## Immediate actions

Read subscription row and matching entitlement snapshot version, verify plan definition and operation history, then regenerate through an audited repair command rather than direct table edits.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
