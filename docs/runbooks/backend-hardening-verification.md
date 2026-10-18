# Backend hardening verification

Source baseline: 9dd0746450dca4348d6d0fa8a09cfc2a48860020.
Implementation remains on codex/backend-clean-architecture, PR #26 targeting dev.
Read the successful Actions run's head SHA to identify the exact verified revision;
baseline CI does not certify later commits.

## Required checks

Run from the repository root:

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
python -m pip install -r contracts/requirements.txt
python scripts/validate_ai_contract.py
```

The PostgreSQL tests require working Docker and create disposable PostgreSQL 17
containers. Do not replace them with SQLite or EF InMemory. Offline tests include
HTTP contract, mapper, model snapshot and scope rejection tests; their success does
not prove database transaction behavior. If local Docker fails, the exact final
commit must pass PostgreSQL tests on the Linux CI runner.

CI also builds API, Worker and Database images using their existing Dockerfiles.
Test TRX files and the AI contract directory are uploaded as workflow artifacts.
Pipeline scope remains validation, not registry publishing or production deployment.

## Evidence to inspect

- IdentityConcurrencyTests: five simultaneous wrong passwords across two real hosts,
  server error on failed security write, cancellation rollback/lock release,
  independent accounts and mixed correct/incorrect login serialization.
- IdentityPersistenceTests: persisted 15-minute lockout, expiration/reset and atomic
  account/role creation, including reuse of a scope already tracking the user.
- TicketAccessScopeTests: Requester cross-owner 404, Agent/Admin broad detail access
  and own-only lists through production endpoints and real PostgreSQL.
- PersistenceContractTests: current EF model matches the released migration snapshot;
  null read scope is rejected before any database call.
- Architecture tests: all six assemblies classified, internal use cases, explicit
  published contracts, common/API/helper/generic bypass fixtures and AI data minimization.
- Domain/adapter tests: suggestion ownership/enum/value/terminal state, hash ambiguity
  regression/golden vector, result invariants, v1 correlation/unknown-field validation,
  cancellation of NoOp/Fake and preserved requester-safe projections.
- Existing API baseline, migration/data-preservation and two-host idempotency tests.

## Compatibility and rollout

No new migration is intended. Never edit released migrations or regenerate the HTTP
baseline to hide a failed check. Existing status integers, create Submitted behavior,
response-only FullName, self-registration, receipt replay and error/header contracts
remain protected.

Before merge, verify the latest PR head has a successful backend job, required review,
and no model drift. This change does not merge or deploy automatically. Roll back with
the previous application image; there is no schema Down operation for this hardening.

Whole-system smoke remains owned by the infra repository, whose source pins are not
changed in this backend-only increment. Do not claim its previous smoke run validates
this new backend SHA. A coordinated release must update the infra source pin and pass
its gateway smoke gate in a separate infra change.

Deferred increments: team/assignment/Lead, profile/department snapshot, ticket lifecycle,
SLA/audit, live AI, Worker jobs, receipt cleanup, telemetry and JWT privilege freshness.
