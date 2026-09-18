# Certificate expiry

**Trigger:** TLS certificate approaches expiration

## Immediate actions

Verify automated issuer health, renew in staging, deploy renewed certificate without exposing private key material, validate full chain and hostname, then confirm external monitoring.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
