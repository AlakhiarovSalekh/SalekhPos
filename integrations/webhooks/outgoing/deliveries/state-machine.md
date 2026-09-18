# Delivery state machine

`pending -> delivering -> delivered` is the success path. Retryable failures move `delivering -> pending` with a future `next_attempt_at`. Terminal failures or exhausted attempts move to `dead_letter`.

A lease id is mandatory for attempt recording. Lease expiry returns abandoned work to eligibility. Idempotency is keyed by organization + operation id and provider event id.