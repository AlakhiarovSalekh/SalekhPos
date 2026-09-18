# Billing reconciliation

**Trigger:** Invoice balance, charges or credits disagree

## Immediate actions

Stop further mutation of affected invoice, reconstruct from immutable charge/credit evidence, compare request hashes and provider references, and execute repair through an audited operator path.

## Evidence to retain

Retain trace ids, operation ids, affected service/version, timestamps, sanitized provider status, and immutable audit references. Never paste access tokens, cookies, connection strings, payment data, or secret material into the incident record.

## Recovery acceptance

The triggering metric returns below threshold, synthetic checks pass, no retry storm remains, and a second operator verifies the recovery evidence.
