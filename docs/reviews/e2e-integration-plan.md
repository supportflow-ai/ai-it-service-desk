# E2E integration plan và validation record

Ngày: 2026-10-10. Base **D** = `origin/dev` SHA `6b124e73e9cb95350a505a24a548cb663ab1b7de`; branch `audit/frontend-backend-integration`. Mọi source path trong tài liệu thuộc D; FIX-01/02/03 là delta chưa commit trên branch này.

**Real E2E: NOT RUN / BLOCKED.** Docker daemon unavailable, ticket list/queue/commands/timeline chưa có backend, status enum chưa đồng nhất. Không có bằng chứng frontend↔API↔PostgreSQL flow chạy xuyên suốt; synthetic helper/type fixtures không tính E2E.

## Entry gates và test environment

| Gate | Điều kiện | Evidence D / trách nhiệm | Current |
| --- | --- | --- | --- |
| G-01 revisions | Ghi SHA của FE/API/schema trên cùng candidate integration revision | Program + endpoint inventory, matrix; Integrator | Baseline ghi đầy đủ; fixes chưa commit |
| G-02 security | P0 replay isolation, payload conflict, atomic concurrent/multi-host replay PASS PostgreSQL | TicketEndpoints.cs:33–50; Backend + Kiệt | BLOCKED; current dev vẫn vulnerable source path |
| G-03 contracts | Duyệt persisted enum/lifecycle, owner/staff scope, command routes/error envelope/revision, comment privacy | TicketStatus.cs; TS types.ts; Architect/Product | BLOCKED; không tự chọn business behavior trong test |
| G-04 migrations | Fresh PostgreSQL via MigrateAsync và existing-data upgrade PASS; identity/ticket schema + seed | Infrastructure/Identity/IdentityDataSeeder.cs; DatabaseTests.cs currently EnsureCreated | NOT VERIFIED; Docker |
| G-05 services | Real PostgreSQL, API, frontend, NGINX; MinIO nếu attachment scope; both api-1/api-2 | docker-compose.yml; deploy/nginx/nginx.conf | NOT RUN |
| G-06 smoke | /health ready; /api info; login/me through gateway; SPA deep-link refresh; no CORS/base-prefix break | Program; vite.config.ts; launchSettings.json; frontend Dockerfile | NOT RUN |
| G-07 frontend | npm ci/lint/build, Node regression9PASS | FIX artifacts/test files | PASS, warnings recorded |
| G-08 release quality | All nonintegration + Integration + real E2E PASS, no unresolved P0/P1 | Jenkinsfile currently lacks full gates | BLOCKED |

D-qualified source shorthand above resolves to `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs`, `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs`, `src/frontend/service-desk-web/src/features/tickets/types.ts`, `src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs`, `tests/ServiceDesk.IntegrationTests/DatabaseTests.cs`, `src/frontend/service-desk-web/vite.config.ts`, `src/backend/ServiceDesk.Api/Properties/launchSettings.json`, `src/frontend/service-desk-web/Dockerfile`, `src/backend/ServiceDesk.Api/Program.cs`. Required patches/tests are audit deltas, not baseline source.

Provision isolated synthetic identities Requester A, Requester B, eligible Agent C, Agent D, Admin E (single roles plus separate dual-role fixtures). Không sử dụng production user/ticket, seed passwords hoặc real token trong báo cáo/logs. Obtain real JWT through auth API/approved test provisioning; do not fabricate auth role locally for E2E. Register API always assigns Requester; staff fixtures do not use incoming register role to provision privileges.

Run through same-origin gateway and direct backend test client for permission negatives. Separate sessions/browser contexts for A/B/C/D/E; clear cookies/localStorage before each scenario. Persist data in real DB, assert rows/counts/history; timezone fixed for display fixtures, assert UTC instants independently. Block unexpected network mocking or request interception that returns fake business data. Browser runner (e.g. Playwright) is **proposed backlog**, not installed or executed in this task.

Health shows DB check only. Verify MinIO separately where relevant; HTTP200 /api does not prove DB migration/authorization. Validate compose two-host idempotency with requests pinned to distinct test hosts as well as via NGINX, not random load balancing alone.

## Ten required end-to-end scenarios

“Expected” for absent command/state rules is an acceptance target conditional on G-03. Exact transitions, actors and status codes must be filled from approved contract before automation; no made-up rule is treated as approved.

| ID / Jira linkage | Actor, preconditions và steps | Positive assertions (real UI + API + DB) | Mandatory negative / error assertions | Current source và status |
| --- | --- | --- | --- | --- |
| E2E-01 Register/Login; TC-252–255 | A registers unique email with password matching existing BE8+digit/case policy; UI redirects login; login; refresh; GET/auth/me | Register201 + real Requester role; login200 JWT; me200 same user; actual token Bearer; profile null supported; no auto-login assumption | Duplicate registration400; wrong password401 does not show protected view; register spoof Agent/Admin still Requester; missing/invalid/expired JWT protected401; Requester agent/admin diagnostics403 | FA, AuthContext, AuthEndpoints/AuthService; **NOT RUN** |
| E2E-02 Create; TC-268 | A POST via CreateTicketPage valid3fields; follow Location/detail; read DB |201 id/ticketNumber; statusSubmitted1; trimmed title/description; server requester=A; exactly one persisted row; UTC timestamps | Empty/whitespace, title101, trimmed description<10, invalid category400; noJWT401; cannot inject requester=B; isolated K replay/security gate tests below | TICKET create + TICKET-SVC exist; **NOT RUN/P0 BLOCKED** |
| E2E-03 My list/detail; TC-269–274 | Seed A and B tickets; A opens real list, paging/filter then A detail and deep-link refresh | Only A rows/counts; stable createdAt/id ordering, no page duplicates; detail same id/content; canonical enum labels; empty state | B markers absent in rows/count/cursor/filter; bad paging contract400; nonexistent/invalidGuid404; unauth401 | /tickets/me absent; detail exists; **BLOCKED** |
| E2E-04 Cross-requester read deny; TC-246/247/273 | B knows A ticket GUID; bypass hidden UI, call direct detail/list/timeline/attachment surfaces when available | Own ticket200; foreign detail404 per current non-disclosing service; no A content in body | Spoof owner query/body ignored/rejected; user token remains B; unauthorized401; every read/comment/file surface negative independently; no ownership bypass via idempotency | Current GetTicketByIdAsync filters nonstaff owner; missing broader surfaces; **NOT RUN** |
| E2E-05 Agent queue; TC-206/290–293 | C opens workspace queue with eligible unassigned/assigned/status fixtures | Real GET and approved staff scope; correct counts/paging/filter; select ticket detail same ID; canonical status | A direct queue API denied (approved403 contract); eligible/ineligible staff/team filters; Admin behavior only per approved matrix; no DemoPage pass | Queue UI DemoPage + no query; **BLOCKED** |
| E2E-06 Agent assignment; TC-294–298 | Eligible C self-assigns eligible ticket; separate dispatch/reassign case only if approved | API accepted contract; persisted assignee/status per decision; audit actor/old/new/time; UI refresh same real record; send real id or documented self command (not unapproved 'me') | A denied; ineligible target/status rejected; stale revision409; C/D simultaneous acquisition: one accepted, other conflict; DB one current assignee/audit coherent; no silent overwrite | FT.assignTicket exists, no handler/entity assignee; **BLOCKED** |
| E2E-07 Transitions/request information; TC-299–302 | C assigned as required; execute approved Start, request-info, resume guards using current revision | Real mutations/state/time/history; UI numeric interpretation matches; refresh/reopen browser persisted; structured errors displayed correctly | Illegal transitions, unknown status, role denied, missing required input, stale revision; no trusting UI button hiding; Request Information placeholder fails entry gate | All command backend missing; requestInfo unusedUI; **BLOCKED** |
| E2E-08 Public comment/internal note; TC-303–307 | C writes public marker then private marker; A reads real timeline in separate context; A replies if approved | Public readable authorized participants; private available staff only; real author/time/ticket association; timeline persists across refresh | A spoof isInternal or note route denied; B cross-ticket denied; private marker absent in Requester JSON/export/search/notification/AI/logs; Closed rules exactly approved, no default auto-resume | addComment HTTP missinghandler; timeline mock; **BLOCKED** |
| E2E-09 Resolve; TC-208/301/302 | C on approved resolvable state, submits resolution and revision | Persisted resolution/status/time/audit; Requester sees approved public outcome; queue updates; no duplicate audit | Empty resolution, insufficient permission, illegal state, stale revision conflict, any internal resolution detail visibility per policy | FE form exists; no BE resolve/entity resolution; **BLOCKED** |
| E2E-10 Requester tracking/confirmation/close/reopen; TC-201/208/209/299 | A follows persisted timeline/resolution, then confirm/close/reopen only within approved actor/window guards | Approved mutation/terminal/reopen states recorded with real audit; requester feedback remains owned; Closed display correct numeric | B cannot confirm/reopen A ticket; outside window/unsupported state denied; missing reason per policy; no assumed automation; API rejects regardless UI | No confirmation/close/reopen API/UI, policy unresolved; **BLOCKED** |

FA/TICKET/TICKET-SVC/FT aliases expand in [contract matrix](api-contract-matrix.md). Existing source tests: `tests/ServiceDesk.IntegrationTests/{AuthEndpointTests.cs,AuthNegativeTests.cs,TicketEndpointTests.cs,HealthCheckTests.cs,DatabaseTests.cs}`. Unit source: `tests/ServiceDesk.Application.Tests/Ticketing/TicketServiceTests.cs` (SQLite/EnsureCreated); these do not substitute PostgreSQL migrations, real browser or multi-instance tests.

## Security, concurrency và serialization additions

| Test ID | Required assertions | Owner / gate |
| --- | --- | --- |
| SEC-01 | A/K vs B/K: never same unauthorized ticket DTO; B-owned persisted output or contract conflict independent of A data | Backend + QA / G-02 |
| SEC-02 | Same A/K/payload replay same id/number, one row; changed payload409 designated error code, no write | Backend / G-02 |
| SEC-03 | Concurrent same scope and two API hosts: one durable ticket; consistent response; rollback failed create leaves no successful replay; timeout retry deterministic | Backend real PostgreSQL / G-02 |
| SEC-04 | Approved TTL boundaries incl expiry and failure: no wrong owner/payload replay, documented reuse behavior; frozen-time fixture not busy sleeps | Backend / G-02 |
| SEC-05 | No JWT or malformed subject cannot replay; direct API role/ownership negatives independently of routes | QA / G-02,G-03 |
| SER-01 | JSON camelCase fields including nullable dept/fullName; Guid validity; required vsoptional; no arbitrary fields used as privileges | Backend/FE / G-03 |
| SER-02 | Each persisted status0–6 and unknown value fixture; labels/actions agree with API; no implicit remapping without migration | Architect/FE / G-03 |
| SER-03 | UTC createdAt round-trip PostgreSQL, browser locale display, deterministic sort ties; no local timezone mutation | QA / G-04 |
| ERR-01 |400 errors/errors[] decoding,401 login vs expired token,403 roles,foreign404,409 revision vs idempotency,503/network; no accidental logout/retry duplication | FE/Backend / G-03 |
| ATT-01 | Approved filepolicy/signature/private access; A/B file negative; internal-note file visibility; metadata/object rollback/orphan cleanup | TC-308–311 after core gates |
| LOG-01 | Synthetic password/token/private-note markers absent from console/network artifact exports/logger; retain status/correlation only | FE/security / G-06 |
| ROUTE-01 | Single-role/dual-role Admin/Agent/Requester direct routes vs APIs; SPA refresh/proxy /api not /api/v1; compose2host readiness | FE/QA / G-06 |

Tests are proposed backlog unless explicitly in execution table below. Missing endpoints are blocked tests, not skipped “success”. Mocks only allowed for isolated unit failure injection; real E2E never substitutes business responses with mock arrays.

## Commands thực sự chạy và kết quả

Môi trường: Windows PowerShell, .NET SDK10.0.300, Node24.13.1, npm11.8.0. Commands chạy ở root integration worktree, trừ npm chạy trong `src/frontend/service-desk-web`. Backend solution and target framework restored/build successfully; no backend code changed.

| Command | Result | Evidence / limits |
| --- | --- | --- |
| git fetch origin --prune | PASS | Fetch trước new checkout; no original index edits |
| git worktree add -b audit/frontend-backend-integration ../ai-it-service-desk-integration origin/dev | PASS | Unique branch/path verified first, no --force |
| git pull --ff-only origin dev | PASS | New audit worktree Already up to date; clean status; HEAD==origin/dev D |
| dotnet build src/backend/ServiceDesk.sln -c Release | PASS |0errors,0warnings |
| dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration" | PASS | Domain4 + Application11 + Architecture6 =21passed |
| dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration" --logger "trx;LogFileName=integration-audit.trx" | **FAIL / infrastructure BLOCKED** |28failed,0passed,0skipped in integration project; Docker fixture setup failed before assertions. Nonmatching projects are not additional test passes |
| docker info --format '{{.ServerVersion}}' | FAIL | Docker Desktop Linux pipe `npipe:////./pipe/dockerDesktopLinuxEngine` absent; Testcontainers default `npipe://./pipe/docker_engine` unavailable/timeouts |
| npm ci | PASS |272packages;2high advisories/deprecation warnings; no audit fix, no package/lockdiff |
| npm run lint (baseline D) | FAIL |6errors: unused Option/Space;4explicit-any sites;1AuthContext fastrefresh warning |
| npm run build (baseline D) | FAIL |3TSerrors: unused Option, Button success prop, unused Space |
| node --test tests/frontend/production-typecheck.test.mjs tests/frontend/auth-contract.test.mjs (RED before fixes) | FAIL expected |2tests failed with actual production/type contract diagnostics |
| node --test tests/frontend/production-typecheck.test.mjs (after FIX-01) | PASS |1production compiler test |
| node --test tests/frontend/auth-contract.test.mjs (after FIX-02) | PASS |1fixture compiler test with positive/null + negative missingpassword/invalidrole |
| node --test tests/frontend/http-error.test.mjs tests/frontend/production-typecheck.test.mjs tests/frontend/auth-contract.test.mjs (final) | PASS |9tests =7HTTP guard +1production compile +1auth type contract |
| npm run lint (final) | PASS |0errors; existing1fastrefresh warning AuthContext.tsx:74 |
| npm run build (final) | PASS | Vite6.4.3,3126modules; bundle ~1.243MB (~393KB gzip), chunk>500KB warning |
| Separate patch chain check/apply in temporary index; compare hashes with current9source/test files | PASS | FIX-01→FIX-02→FIX-03; actual index untouched |
| Real compose/API/browser E2E smoke | **NOT RUN** | Docker not available; no stack startup/runtime/network/browser assertions |
| Jenkins pipeline execution | NOT RUN | No runner execution requested/provided; inspected file only |

Integration artifacts are ignored output: `tests/ServiceDesk.IntegrationTests/TestResults/integration-audit.trx`; inspect initialization failure, do not count failure as executed permission assertions. New Node tests import actual TypeScript helper with native Node TS support, verified only on Node24.13.1. Before wiring tests into Node22 Docker/CI, pin and verify a TS-capable Node22 version or an explicit runner; don't assume any22.x works. No new package dependency/framework added.

The build warnings are not errors; missing APIs/P0/enum remain blockers even after frontend compilation passes. Auth compiler fixture does not issue real HTTP request or assert runtime JWT; assertions in blocked .NET integration tests have not been validated here.

## Proposed CI and rollout gates

1. Restore/build backend and frontend on pinned supported runtimes; lint;9new regressions; all existing nonintegration tests. Save redacted reports.
2. Docker-enabled runner starts real PostgreSQL/Testcontainers, runs migrations on fresh and upgraded fixture DB; real Auth/Ticket integration, P0 multi-host races. Use proper TRX publisher/conversion (Jenkins junit '*.trx' needs verification).
3. Start candidate FE/API/gateway stack at matching revisions; health and transport smoke; execute E2E01–10 only after approved contracts/implementation. Assert real DB/network traces, private marker absence, no mock timeline.
4. Repeat owner/security cases against gateway and pinned api-1/api-2; no performance/security conclusion from single host.
5. Fail gates on real integration/E2E failure, unresolvedP0/P1, or unexpected fake business response. Attach actor/endpoint/status/DB assertion evidence; sanitize credentials/Authorization/request bodies.
6. Review and explicit approval before PR/push/staging/main actions. This task makes no such changes.

Suggested owner: Kiệt QA for automation TC-323; Anh Nguyen Backend for fixtures/migrations/commands; Thu Hoai frontend and UI assertions; Đức Anh contracts/security; DevOps Docker-enabled Jenkins and artifacts. TC-323 remains To Do from Jira read. Gate table records limits, not Jira state changes.

Rollback for the three technical fixes: revert reviewed logical fix commits (when created later) or apply corresponding reverse patch on clean authorized branch in reverse03→02→01 order; no DB migration/data rollback is required because this audit changed no backend schema. Do not reverse patches in the original protected worktrees. Future endpoint/schema/enum rollout must supply its own tested additive/compatibility rollback; audit's patches are not a migration plan.

## Final self-review checklist

- [x] Real backend registration traced; all frontend calls inventoried incl unused command functions and mock timeline.
- [x] Numeric persisted status/nullableDTO/errors/routes/claims/paging identified; no lifecycle/permission invented.
- [x] Each implemented logical fix has a meaningful test and separate diff; patch chain reproduces current9files.
- [x] Release build + frontend lint/build +21existing nonintegration +9newtests verified.
- [x] Integration failure cause clearly recorded, no Docker/E2E PASS assertion.
- [ ] PostgreSQL migration/security integration gate successful.
- [ ] Real E2E01–10 including private-note/ownership/assignment/state and stack proxy smoke successful.
- [ ] AllP0/P1 gaps fixed on reviewed integration revision.
- [ ] CI gates executed and release acceptance approved.

Outstanding unchecked items keep integration/release **BLOCKED**, not completed by these documentation or compile fixes.
