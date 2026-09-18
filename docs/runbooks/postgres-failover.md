# PostgreSQL failover

**Trigger:** Primary database is unavailable

## Immediate actions

Confirm managed failover state, pause destructive maintenance, ensure clients reconnect with bounded retry, validate RLS/runtime role on the new primary, and run consistency checks before declaring recovery.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
