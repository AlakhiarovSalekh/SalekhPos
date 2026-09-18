# Worker failures

**Trigger:** Background jobs fail repeatedly

## Immediate actions

Inspect failure class and dependency health, verify retry policy, stop retry storms for terminal errors, and preserve operation identity for replay.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
