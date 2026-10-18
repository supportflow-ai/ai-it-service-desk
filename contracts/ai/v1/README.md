# Classification v1 input correlation

The OpenAPI shape is unchanged. The backend computes inputHash; a provider treats it as
opaque and echoes it exactly with requestId, ticketId and ticketRevision. A provider must
not infer authorization or recompute the hash using its own normalization.

Backend canonical hash algorithm: ai-input-v2.

1. UTF-8 bytes of "ai-input-v2" followed by one zero byte.
2. Trim title; encode UTF-8; append its byte length as a 32-bit big-endian integer and its bytes.
3. Treat null description as empty; trim and append its UTF-8 byte length and bytes likewise.
4. SHA-256 over those bytes, encoded as 64 lowercase hexadecimal characters.

There is no lowercasing, Unicode normalization or newline rewriting. Example:
title "VPN issue", description "Cannot connect" produces
e737e044b7ef976a6f2f2e97f1f2aaee633a2205cc16d051316bf2139d5dc02c.

Length prefixes prevent field-boundary ambiguity. This algorithm is unrelated to
ticket-create idempotency fingerprints, which retain their existing encoding.
No AI records are persisted and no provider is connected in this release.
