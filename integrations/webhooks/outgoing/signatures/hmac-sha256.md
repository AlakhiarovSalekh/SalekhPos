# Outgoing webhook HMAC-SHA256

Canonical bytes are `timestamp + "." + rawBody`. The worker resolves a secret by opaque secret reference, never from the database value itself. It sends `X-SalekhPos-Timestamp`, `X-SalekhPos-Delivery`, and `X-SalekhPos-Signature: v1=<hex>`.

Consumers must reject timestamps outside a five-minute window and compare signatures in constant time. Secret values must never be logged.