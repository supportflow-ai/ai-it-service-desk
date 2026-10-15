# Consolidated dev integration — 2026-10-15

This integration combines all remote branch histories into dev: main (AI contract),
TC-203 (CI/environment), and PR #5 (persistent requester-scoped idempotency).
Other existing feature and documentation heads were already ancestors of dev.

## Compatibility resolutions

- Preserve persisted TicketStatus values 0–6; append Triaged=7, Assigned=8,
  PendingExternal=9. Match frontend numeric values to that backend contract.
  This integrates enum primitives, not a complete lifecycle state machine.
- Retain Ticket.Create invariants and requester paging while integrating database
  idempotency, transaction locks, payload fingerprints and replay receipts.
- Consume paged ticket responses in the frontend and drive server pagination.
- Remove the duplicated /api prefix from AI client paths. AI HTTP handlers,
  provider/persistence and full Agent commands remain future implementation work.
- Add the missing Identity schema migration. Existing tables are retained via
  CREATE IF NOT EXISTS; Down deliberately retains accounts/roles to avoid data
  loss, and reapplying Up is supported. Incompatible pre-existing schemas must be
  reviewed separately; this repair does not silently alter their columns.
- Refresh the EF snapshot without recreating the already-applied paging index.
- Serialize PostgreSQL startup migration/seeding across API replicas and Worker.
- Fix the integration nullable compile failure and adapt the replay mutation test
  to the Ticket entity's private setters. Keep production Identity startup enabled
  in the two-host security fixture.
- Update both smoke scripts to the actual auth/create/list/detail contracts;
  wire frontend regressions and an isolated Docker gateway smoke job into CI.

## Validation

Local Release solution build passes with zero warnings/errors. Domain 100,
Application 40 and Architecture 6 tests pass; frontend production build/lint pass
(existing bundle-size/fast-refresh warnings) and all 11 frontend regressions pass.
EF reports no pending model changes. Local Docker remains unavailable, so real
PostgreSQL integration and full Docker smoke are verified by the pull request CI
before the integration is merged into dev.

New regression coverage checks cross-language status values, the requester
paging client, and ticket-only PostgreSQL upgrade plus rollback/reapply with
preservation of existing account and ticket data. Existing Auth, ownership,
idempotency/concurrency/two-host and invalid-token suites remain required.

Historical audit reports describe their original SHAs. This consolidation does
not claim that all product modules, Agent resource scoping, AI runtime, Knowledge
Base/RAG, SLA or Analytics are now complete.
