# API SLO degradation

**Trigger:** API error ratio or latency alert fires

## Immediate actions

Freeze risky deploys; compare release marker; inspect error classes and dependency health; scale only if saturation is proven; rollback when a release regression is confirmed.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
