# Authorization denial spike

**Trigger:** 403 rate increases unexpectedly

## Immediate actions

Check recent role/grant changes, issuer configuration and tenant scope. Treat suspicious distributed denials as an abuse signal. Do not weaken permissions to clear the alert.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
