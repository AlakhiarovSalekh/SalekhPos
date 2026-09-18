# Handler contract

Handlers receive a verified, replay-protected envelope. They may not access raw secrets. They must validate event type and schema version, execute in an organization-scoped transaction, use the event id as an idempotency key, and return a typed outcome: accepted, ignored, retryable, or rejected.