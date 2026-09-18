# Database latency

**Trigger:** PostgreSQL p95 latency exceeds threshold

## Immediate actions

Check connection saturation, locks, slow query fingerprints and storage latency. Do not log SQL parameters containing customer data. Cancel pathological queries only after impact review.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
