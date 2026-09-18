# Synchronization backlog

**Trigger:** Oldest offline sync message exceeds objective

## Immediate actions

Check ingestion availability, device proof verification and poison messages. Keep per-device ordering and idempotency. Quarantine malformed input instead of dropping valid backlog.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
