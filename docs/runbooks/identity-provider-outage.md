# Identity provider outage

**Trigger:** OIDC discovery, login or refresh fails

## Immediate actions

Keep existing valid sessions within policy, reject unverifiable new authentication, verify provider status and DNS/TLS, and do not bypass token validation.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
