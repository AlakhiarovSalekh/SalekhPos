# Worker queue age

**Trigger:** Queued work exceeds two minutes

## Immediate actions

Confirm consumer health and downstream capacity, scale consumers within safe database/provider limits, apply backpressure to producers, and avoid deleting queued work.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
