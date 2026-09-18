# Secret exposure

**Trigger:** Credential or provider secret may be disclosed

## Immediate actions

Revoke/rotate first, preserve evidence without copying secret values, identify access window and dependent systems, invalidate caches/sessions when relevant, and remove the source through approved history-rewrite procedure if committed.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
