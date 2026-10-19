# ADR-012: AI classification v1 contract and future transport

Status: contract foundation accepted; live transport deferred.

Canonical contract: `contracts/ai/v1/openapi.yaml` (OpenAPI 3.1, JSON-compatible YAML).
Backend publishes a versioned artifact and owns its semantics. `ai-service` consumes
a pinned commit/checksum copy. Breaking wire changes require a new major contract;
compatible changes must update fixtures, validators and both repositories together.

The future `POST /v1/ticket-classifications` accepts an authorized snapshot only:
request ID, ticket ID, revision, deterministic title/description hash and text. It
returns the same correlation values and at most one suggestion for each of category,
impact and urgency. Only named taxonomy values and finite scores in [0,1] are valid.
Null score means absent evidence, never a manufactured confidence. Unknown properties,
privileged fields, numeric enums, duplicate suggestions and stale/cross-request results
are rejected. No user token, secret, internal notes or unrelated data is transmitted.

Backend computes priority/confidence bands, authorizes reads and owns every state
transition and human accept/override/reject decision. AI has no business database write
credentials. Its response cannot perform actions. Existing `xmin` is a database token,
not an automatically interchangeable long business revision. No revision column or
AI persistence is added until its lifecycle and ownership have a separate implementation.

## Future HTTP adapter requirements (not connected in this phase)

- HTTPS; a dedicated scoped service bearer credential from secret configuration.
  Never forward end-user JWTs. Rotate credentials outside source control.
- Per-attempt timeout 3 seconds; total operation budget 5 seconds, including delay.
- At most one retry for transport failures or 502/503/504. Retry 429 only when its
  Retry-After fits the remaining budget; otherwise fall back immediately.
- Do not retry validation, 401/403 or caller cancellation. Correlate every attempt
  with the same request ID; classification is read-only but provider costs may repeat.
- Circuit opens after five consecutive transport/availability failures, waits thirty
  seconds, then permits one half-open probe. Success closes; failure reopens.
- Map provider failures to controlled Unavailable/Timeout/InvalidResponse results.
  Keep ticket workflows usable, do not leak provider errors, text or credentials.
- Always use the contract mapper before returning an application result. Apply bounded
  response size, credential redaction and request cancellation in the future adapter.

Production DI registers only NoOp. Fake adapters live in tests and cannot accidentally
enable an external provider. Current CI validates schemas, fixtures, mappings and NoOp;
transport/circuit integration tests are required before introducing a real adapter.
