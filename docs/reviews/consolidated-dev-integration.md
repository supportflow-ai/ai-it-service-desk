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
- Build MinIO from its upstream security release because the configured public
  container registry rejects pulls. Include its upstream license in the image.
  Use the ASP.NET runtime for Worker because Infrastructure requires that framework.
- Register Data Protection explicitly for the generic Worker host and gate NGINX
  on health checks of both API nodes; initial full-stack smoke caught these
  deployment-only failures. Assert that all seven services remain running.

## Validation

Local Release solution build passes with zero warnings/errors. Domain 100,
Application 41 and Architecture 6 tests pass; frontend production build/lint pass
(existing bundle-size/fast-refresh warnings) and all 11 frontend regressions pass.
EF reports no pending model changes. PR #24 CI confirms all 57 Integration-project
tests pass against real PostgreSQL (including 8 endpoint unit tests). The security
workflow also passes all tests. Local Docker remains unavailable; full stack smoke
is verified in PR CI before merge. Initial stack smoke exposed an unavailable
MinIO image registry; the source build above replaces that dependency.

New regression coverage checks cross-language status values, the requester
paging client, and ticket-only PostgreSQL upgrade plus rollback/reapply with
preservation of existing account and ticket data. Existing Auth, ownership,
idempotency/concurrency/two-host and invalid-token suites remain required.

Historical audit reports describe their original SHAs. This consolidation does
not claim that all product modules, Agent resource scoping, AI runtime, Knowledge
Base/RAG, SLA or Analytics are now complete.
