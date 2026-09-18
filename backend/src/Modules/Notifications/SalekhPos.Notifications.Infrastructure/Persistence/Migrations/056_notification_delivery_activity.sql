BEGIN;

CREATE INDEX ix_notification_external_activity
  ON notifications.external_deliveries(
    organization_id,
    created_at DESC,
    delivery_id DESC
  )
  INCLUDE(
    notification_id,
    channel,
    recipient_subject,
    status,
    attempt_count,
    next_attempt_at,
    last_error_code,
    updated_at
  );

COMMIT;
