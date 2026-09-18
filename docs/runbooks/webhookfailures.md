# Webhook delivery failures

**Trigger:** Outbound integrations fail or backlog grows

## Immediate actions

Identify affected provider classes, verify DNS/TLS/HTTP status distribution, pause only the failing connection, retain payload references and hashes, and replay using original event identity.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
