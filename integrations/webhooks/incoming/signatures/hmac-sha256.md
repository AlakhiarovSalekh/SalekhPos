# Incoming signature verification

Incoming integrations must identify the connection before reading a large body, enforce the configured byte limit, validate timestamp freshness, compute HMAC over raw bytes, and compare in constant time. Failed verification is a 401 without revealing which check failed.