# Backend refactor baseline

- Source commit: `f6a0adae01bd642753f02d2beae10abff6f410e3`.
- Branch: `codex/backend-clean-architecture`.
- SDK used for validation: .NET 10.0.300.
- Pre-refactor unit/architecture baseline: 100 Domain + 41 Application + 6 Architecture tests passed during review.
- Published HTTP baseline: `contracts/backend/openapi-baseline.json`, exported from the unmodified API using a no-database test host. Baseline contract test passed before implementation.
- Pre-refactor PostgreSQL integration attempt: 49 tests could not run because Docker Desktop returned HTTP 500 on the engine endpoint. This is an environment failure, not a passing database baseline.
- No existing migration will be rewritten. All database changes are additive migrations and must be validated on Linux CI against PostgreSQL 17, including upgrade from the old migration chain.

API compatibility includes endpoint paths, query defaults, request/response schemas,
numeric ticket statuses, authorization visibility, idempotency scope/replay and
existing error shapes. Contract changes require a separate coordinated release.
