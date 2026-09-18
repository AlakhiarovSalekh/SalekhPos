# Billing/subscription failures

**Trigger:** Billing or subscription error rate rises

## Immediate actions

Separate validation/conflict failures from database/provider failures. Preserve idempotency keys. Never manually edit ledger/subscription rows without an audited repair procedure.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
