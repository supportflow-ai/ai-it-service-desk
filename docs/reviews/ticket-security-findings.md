# Ticket Operations Security: audit findings và implementation plan

Ngày kiểm tra: 2026-10-10; branch docs/ticket-operations-security.
Repository: https://github.com/supportflow-ai/ai-it-service-desk.
Worktree: D:/Users/Documents/HUCE/HK1_2026-2027/8. ThapCam/Code/ai-it-service-desk-ticket-security.
D = origin/dev, **6b124e73e9cb95350a505a24a548cb663ab1b7de**.
F = origin/frontend, **063489b97c1f706cebeaeea1d3038f0e63a39fd7**.
Toàn bộ findings dùng commit D trừ evidence reference ghi F. Branch docs mới xuất phát D, không có source changes.

## 1. Kết luận / phạm vi đã xác minh

**P0 còn trên dev:** POST create có global idempotency replay không scope subject và không kiểm tra owner trước trả DTO. Detail query bảo vệ requester ownership, nhưng replay bypass query đó. Không có assignment/comment/internal-note/attachment backend vertical slice trên D; không kết luận có internal-note leak thực tế khi chưa có note persistence/API.

MinIO signing adapter có thật, chưa nối attachment use case; timeline frontend là mock và Workspace gọi routes không map. Phân quyền Agent hiện rộng; team/Dispatch chưa có. Enum drift khiến UI diễn giải Closed sai. Build backend/unit PASS không xác nhận isolation HTTP/multi-instance/storage: Docker unavailable, 25 integration tests fixture FAIL; frontend build FAIL.

Đầu ra:
- docs/architecture/05-assignment-and-comment-policy.md: 12 ASG rules,14 COM rules,27 proposed assignment/comment tests.
- docs/architecture/06-attachment-security.md:20 ATT rules,24 proposed tests,10 decision gates.
- Báo cáo này:14 findings, priority plan và validation/preservation evidence.

Không chỉnh business code, không áp dụng migration, không thay auth, không commit/push/PR/merge. Implementation plan là handoff; các security gaps chưa được sửa bằng việc tạo tài liệu.

### Source coverage

Đã đọc README, ADR-002/005/006/007/010; Ticket entity/status/DTO/command/service/endpoints/DbContext; policies/JWT generator/CurrentUser; frontend Workspace/ticketApi/types/apiClient/ProtectedRoute/AppRoutes; MinIO port/adapter/options/DI/config/compose/NGINX; Auth/Ticket/DB integration fixtures và TicketService SQLite tests; Jenkinsfile. Đối chiếu audit reports đang có và domain draft, không sửa chúng.

Các remote refs đã fetch và đọc tree + route/entity evidence:

| Branch | Full SHA | Module situation |
|---|---|---|
| origin/dev | 6b124e73e9cb95350a505a24a548cb663ab1b7de | Create/detail + JWT + frontend Workspace; operations missing; MinIO adapter |
| origin/frontend | 063489b97c1f706cebeaeea1d3038f0e63a39fd7 | Cùng create/detail baseline và policy docs tự ghi APPROVED |
| origin/main | e044f470d38cb9d00a4adca9e59de45abbde3ff5 | AI domain TriagePolicy, MinIO scaffold; không TicketEndpoints/operations này |
| origin/feature/core-ticket-lifecycle | 5425b62b84913964c129b9235744a7dd03bd3a4f | Tên branch không chứng minh lifecycle; chưa operations APIs/entities |
| origin/feature/tc-200-create-ticket | 9df98c27a5d50b269ce5ba71dacd1e0839f7c46d | Create/detail, không assignment/comments/attachments |
| origin/feature/tc-203-quality-gate-and-integration-environment | e834e74a50ecc4986ca329bc2a217ffc14af8cf4 | AI domain contracts/tests; operations missing |
| origin/feature/tc-275-276-277-ai-triage-contract | b529b2cdd138b273ad5f89efb18ccb9f5a399313 | AI domain TriagePolicy/HITL contracts, không privacy-aware ticket-context pipeline |

Searched tree names và TicketEndpoints/Ticket fields chỉ chứng minh các operations thuộc scope hiện thiếu ở refs đọc; không phải audit sâu toàn bộ feature code. AI không hoàn toàn “không có”: domain triage contract có ở main/features, runtime DI ở D dùng NoOp; không có evidence internal-note context gửi provider.

Local fix/p0-idempotency-isolation có pending candidate sửa P0 + tests/migration, chưa commit/integrated D; không tính candidate này như đã fix dev. Các worktree docs RBAC/domain/audit là phụ thuộc tài liệu chưa merge.

## 2. Jira và governance evidence

Đọc trực tiếp issue descriptions/status/owners/links và TC-211 comment ngày10/10. Rovo search có warning nguồn không đầy đủ; không tuyên bố đã đọc toàn bộ Confluence/PRD/context pack.

| Jira | Live status / owner | Handoff / mismatch |
|---|---|---|
| [TC-294](https://evolving-kg-drift.atlassian.net/browse/TC-294) | To Do / Đức Anh | Assignment policy → TC-295 |
| [TC-207](https://evolving-kg-drift.atlassian.net/browse/TC-207) | To Do / Anh Nguyen | TC-295 commands, TC-296 audit, TC-297 UI Thu Hoài, TC-298 QA Kiệt |
| [TC-303](https://evolving-kg-drift.atlassian.net/browse/TC-303) | To Do / Đức Anh | Public/internal policy → TC-304 |
| [TC-209](https://evolving-kg-drift.atlassian.net/browse/TC-209) | To Do / Anh Nguyen | TC-304 commands, TC-305 query, TC-306 UI Thu Hoài, TC-307 QA Kiệt; email/chat excluded |
| [TC-308](https://evolving-kg-drift.atlassian.net/browse/TC-308) | To Do / Đức Anh | Limits → TC-309 |
| [TC-210](https://evolving-kg-drift.atlassian.net/browse/TC-210) | To Do / Anh Nguyen | TC-309 Backend, TC-310 UI Thu Hoài, TC-311 QA Kiệt; virus scanning/image preview excluded |
| [TC-211](https://evolving-kg-drift.atlassian.net/browse/TC-211) | Done / Nguyễn Thị Thu Hoài | Layout/actions upstream dependencies still To Do; TC-312/313/314 Done |

All11 subtasks TC-295/296/297/298/304/305/306/307/309/310/311 fetched live, all To Do. TC-294/303/308 each zero comments, descriptions no explicit quantitative approval. TC-211 public comment links commit106909cca3ed09e4687dda667a213c6d60841a8f and asks architect review; proves frontend code evidence, not end-to-end backend/security acceptance.

F policy docs contain APPROVED labels dated2026-09-29, 10MiB/30MiB/10 files/900s URLs/author deletion1h, role Lead, /api/v1 and PendingUser auto-resume. D differs; new contract flags Proposed reconciliation rather than silently replace approved-looking history. Do not change Jira status/assignee during audit.

## 3. Finding register

Severity: P0 immediate cross-user confidentiality defect; P1 security/release/contract blocker; P2 bounded adapter/dependency risk. “Confirmed source” = deterministic source path; not HTTP exploit test passed. “Risk” = prerequisite/runtime/approval still needed. Missing use case is implementation gap, not an already reachable vulnerable endpoint.

| ID | Severity / class | Branch + commit | Evidence path | Impact | Proposed owner / first action |
|---|---|---|---|---|---|
| OPS-SEC-01 | P0 / confirmed source bug | origin/dev @ D | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:33–50 | Foreign requester ticket DTO replay | Anh Nguyen + Đức Anh: validate/integrate persistent isolated hotfix through CI, not here |
| OPS-SEC-02 | P1 / confirmed missing atomicity, runtime race risk | origin/dev @ D | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:37,45,50; docker-compose.yml:37–82 | Duplicate tickets, inconsistent retries across api-1/api-2, changed payload accepted | Backend: PG unique record + transaction + fingerprint, regression barrier tests |
| OPS-SEC-03 | P1 / authorization contract gap | origin/dev @ D | src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs:75–84 | Every Agent can read any ticket; no trusted team scope | Backend TC-295 + architect TC-294: decide and implement resource predicates |
| OPS-SEC-04 | P1 / implementation missing | origin/dev @ D | src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs:3–15; src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:20,66 | No authorized assignment/private comments/audit server flow | Backend TC-295/296/304/305 implement approved rules before feature enable |
| OPS-SEC-05 | P1 / confirmed contract bug | origin/dev @ D | src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:5–11; src/frontend/service-desk-web/src/features/tickets/types.ts:3–12 | Closed5 interpreted PendingUser5, wrong buttons/status | Backend + Thu Hoài: preserve persisted values, reconcile enum and lifecycle compatibility |
| OPS-SEC-06 | P1 / attachment implementation gap | origin/dev @ D | src/backend/ServiceDesk.Application/Common/Interfaces/IObjectStorageService.cs:12–39; src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs; src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs | No ticket auth/metadata/type/quota/integrity/cleanup boundary | Backend TC-309 + architect TC-308: approved gated streaming slice |
| OPS-SEC-07 | P1 / future exposure risk | origin/dev @ D; F policy reference | src/backend/ServiceDesk.Infrastructure/Storage/MinioObjectStorageService.cs:35–44; F docs/architecture/07-attachment-security-limits-and-policy.md | Raw signed URL transferable, endpoint minio:9000 not browser-accessible; strict isolation unmet if exposed | Backend/DevOps: authenticated API delivery; integration test routing/signatures |
| OPS-SEC-08 | P1 / confirmed deployment defaults, exploit risk unverified | origin/dev @ D | docker-compose.yml:43–52,66–75,124–125; src/backend/ServiceDesk.Api/appsettings.json:11–16 | Root storage credentials + HTTP if defaults used; broad privilege | DevOps + Đức Anh: separate account, explicit secrets/TLS/private policy, validate actual deployment |
| OPS-SEC-09 | P2 / adapter limitation/race risk | origin/dev @ D | src/backend/ServiceDesk.Infrastructure/Storage/MinioObjectStorageService.cs:23–44,54–61 | Signing ignores cancellation port; check-create race; GET URL != object proof | Backend adapter: typed failure/cancellation boundary, provisioning outside request, real tests |
| OPS-SEC-10 | P1 / unsafe logging confirmed, actual exposure risk | origin/dev @ D | src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:39,64,95; src/frontend/service-desk-web/src/shared/api/apiClient.ts:16–19,37 | Axios error retains bearer/content; logging raw error can expose them | Thu Hoài: safe error mapper/status/correlation only + capture-log regression |
| OPS-SEC-11 | P1 / validation and CI gate gap | origin/dev @ D | tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:19,40–54; Jenkinsfile | Docker tests blocked; MinIO stub; Jenkins excludes Integration | QA Kiệt + DevOps: real PG+MinIO and multi-host stage required |
| OPS-SEC-12 | P1 / confirmed build blocker | origin/dev @ D | src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:10,225; src/frontend/service-desk-web/src/features/tickets/components/TicketListPage.tsx:2 | Frontend production build fails before Vite bundle | Thu Hoài: fix TS errors then rerun build in separate implementation change |
| OPS-SEC-13 | P1 / governance/compatibility blocker | origin/frontend @ F + origin/dev @ D | docs/architecture/05-comment-and-internal-note-privacy-contract.md;06-ticket-assignment-policy.md;07-attachment-security-limits-and-policy.md | Conflicting routes/states/limits/roles/scanning/consumer IDs | Đức Anh: resolve OPS-DEC/ATT-DEC and link acceptance evidence, no silent rule substitution |
| OPS-SEC-14 | P2 / installed advisory exposure risk | origin/dev @ D | src/frontend/service-desk-web/package-lock.json | 2 high package advisories; runtime reachability not proven | Thu Hoài + QA: dependency reachability/update review, no auto npm audit fix |

Finding register dùng repository-relative source paths; các đoạn reproduction có thể viết tên file ngắn sau evidence đầy đủ. D/F phía trên cung cấp full commit SHA cho từng row; F policy evidence chỉ là reference, không phải dev implementation.

### OPS-SEC-01: requester isolation bypass via cached response

Evidence D: src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:28 verifies GUID subject,33–39 builds global idempotency:ticket:{trimmedKey} and immediately returns cached DTO201. It never calls GetTicketById ownership predicate on hit. Payload includes Description,Title,CategoryId,RequesterDepartmentId (Application/Ticketing/Dtos/TicketDto.cs). JWT middleware does run, so this is authenticated cross-requester leak, not anonymous auth bypass.

Reproduce in disposable test environment: register R-A/R-B; use separate HttpClient Bearer credentials. A POST /api/tickets with key K and valid content; B POST same endpoint/key within10min on same instance. Actual by control flow: B receives A.Id/number/title/description,201 and A Location. Expected: B gets only a B-owned ticket, never A data. B GET A.Id still404 per TicketService.cs:79–81; that does not repair prior disclosure.

Existing TicketEndpointTests.CreateTicket_WithIdempotencyKey_ReturnsSameTicketOnDuplicateRequest tests one identity only. Add cross-subject/anonymous/expired/wrong-payload and detail assertions. Runtime HTTP reproduction **NOT RUN**, Docker fixture unavailable; source defect unambiguous. No real user data/token used in audit. Candidate hotfix branch not part of origin/dev; review its migration, transaction boundaries and test results before integration.

### OPS-SEC-02: races, instance split and payload collision

Evidence same file D: cache TryGet → await Create → cache Set is not atomic; key fingerprint absent; process-local IMemoryCache and TTL10min. Compose has two API services. Reproduction design: barrier two requests same subject/K/payload to one host or two hosts → both can miss and create; count DB tickets, not just equal response IDs. After TTL replay can create again; same K different body returns old DTO without409. Failed create does not set cache, but commit succeeded then timeout/cache fail has no persistent binding.

Expected: subject+method+resource+key unique persistent record, canonical fingerprint, creation+binding transaction, current ACL on replay; failure rollback; expiration/reuse explicit. No claim that these races were run against PostgreSQL. Backend + QA must test multi-host/timeout/failure/TTL before release; adding requesterId only to memory key isn't sufficient.

### OPS-SEC-03: role policy lacks resource/team scope

Evidence D: Program.cs:53 grants RequireAgent to Agent/Admin; TicketService.cs:79 skips owner filter for every Agent/Admin. ICurrentUser only identity/roles, Ticket has no SupportTeamId/grant. Source walk: Agent C can request arbitrary existing ticket GUID and query selects it regardless assignment/team. Requester cross-owner get is correctly constrained and existing SQLite test asserts null.

Whether cross-team Agent read is forbidden in production depends on final team policy. Reference F assignment spec requires same-team eligibility; broad Agent data access is a P1 proposed contract gap, not falsely declared against an approved tenant rule. Proposed team/Dispatch/resource auth, fail closed until populated. Test role-only Agent outside team404, matching team read200, revoked membership denied, visible unauthorized command403; evaluate multi-role actor projection.

### OPS-SEC-04: operation API/database boundaries missing

Evidence D: Ticket.cs:3–15, ApplicationDbContext.cs:22, TicketEndpoints.cs:20,66, ticketApi.ts:24–35,54–76. Call assign/comments/internal-notes/timeline with valid token: routes are absent, expected route404, not feature success. Workspace self-assign uses literal me, comment endpoint accepts client isInternal in FE payload, no expectedRevision. Timeline Date.now mock isn't audit evidence.

Expected approved ASG/COM command authorization, revision CAS, immutable event and server query projection. No confirmed existing internal-note leakage because no persisted notes or note APIs. Future implementation must reject isInternal mass assignment and same-parent IDOR. Tests OPS-A01..11, OPS-C01..16; Backend TC-295/296/304/305, Frontend TC-297/306. Fake layout success must not close backend/security acceptance.

### OPS-SEC-05: enum mismatch controls wrong operations

Evidence D full paths: src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs and src/frontend/service-desk-web/src/features/tickets/types.ts; Workspace:187–195 disables comments/attachments only when FE Closed8. Feed backend Closed5 to view: FE labels PendingUser, write buttons are enabled; backend InProgress2 displays Triaged and self-assign.

Confirmed mapping bug by source, UI browser run not executed; nonexistent command API prevents proving Closed write exploit today. Fix shared serialization compatibility, preserve existing DB0–6; append proposed statuses only after approval/migration. Unknown status UI read-only, never cast arbitrary integer enum. QA snapshots of all legacy status values and real endpoint guards include Admin. Target lifecycle policy in docs/ticket-domain-lifecycle remains Proposed.

### OPS-SEC-06: storage port is not an authorized attachment module

Evidence D full path IObjectStorageService.cs supports signing and bucket operations only; no attachment table/controller/upload size/MIME/parser checks, key generation/parent ACL/quota compensation. Workspace attachment button195 has no handler. Try attachment API paths returns404; not a currently exposed arbitrary upload exploit.

Expected typed use case/metadata/private ACL before bytes, parent visibility, bounded actual-byte validation, reservations/CAS, storage compensation and integrity. No unchecked publish or unlimited uploads. Attachment document ATT-01..20 and OPS-T01..24 are requirements, not implementation. Backend TC-309, policy TC-308 and real QA TC-311 first.

### OPS-SEC-07: raw presign cannot enforce recipient identity

Evidence D MinioObjectStorageService.cs:35–44 signs supplied bucket/object; default3600s and no caller/metadata proof. F attachment spec prescribes15min raw URL after issuance ACL. SDK6.0.5 source signs bearer capability; recipients can reuse without ticket JWT until expiry. D DI endpoint=minio:9000 via compose is private container DNS, README host-port URLs do not reflect actual compose.

Reproduce future direct flow: A obtains URL, B/noJWT fetches same URL before expiry; storage authenticates signature, not identity. Also test URL from browser network for DNS/TLS/signature failures; don't rewrite host after signing. Current API never exposes such URL, so exploit remains **Risk / not deployed**, not proven current data leak.

Expected chosen default API content stream reauthorizes subject/parent per request, no raw external URL; copy API URL→B404/noJWT401. Private bucket+short TTL mitigate capability exposure but cannot satisfy “B never downloads copied raw URL”. Proposed alternative requires explicit acceptance and test of that limitation. [MinIO SDK6.0.5](https://github.com/minio/minio-dotnet/blob/6.0.5/Minio/ApiEndpoints/ObjectOperations.cs).

### OPS-SEC-08: production-oriented compose falls back to insecure storage config

Evidence D docker-compose.yml sets ASPNETCORE_ENVIRONMENT=Production yet shares root env credentials with API/worker and UseSsl=false; MinIO own root variables have known defaults. No inspected deployment policy provisioning. **No claim bucket is public**: compose not publishing9000/9001, actual network/secret overrides/IAM unknown. appsettings checked-in local defaults aren't proof leaked live secrets.

Reproduction/config check: with env unset, inspect docker compose config in safe environment with secrets redacted; review app permissions/root identity/anonymous bucket actions and TLS transport. Do not print resolved real credentials. Expected separate scoped app account, private bucket provisioned, enforced secret/TLS environment readiness. Actual deployment verification NOT RUN (Docker unavailable). DevOps owner proposed, Đức Anh review; ATT-10/T10 validates actual policy before enable.

### OPS-SEC-09: bucket provisioning race, cancellation and missing-object handling

Evidence D MinioObjectStorageService.cs:57 if BucketExists false then MakeBucket no known-already-owned handling; methods23/35 accept CancellationToken but never inspect it. SDK signing overload6.0.5 has no token, so “just pass cancellationToken” is not a valid fix. Port doesn't stat/open/delete so signing success cannot establish object integrity/existence.

Reproduction design: two adapter instances see absent bucket simultaneously, create race; canceled token still enters presign; auth/missing bucket/key/network timeout behaviors in real storage. No production call site found by rg except adapter/interface/test stub; do not assert actual startup failure. Expected external provisioning, typed safe errors, pre-cancellation/timeout guard and Stat/read integrity. Avoid generic catch marking every error success. OPS-T13/18/19 and adapter unit fault injection required; P2 until exposed.

### OPS-SEC-10: raw frontend error logging contains sensitive request state

Evidence D: src/frontend/service-desk-web/src/shared/api/apiClient.ts:16–19 adds Authorization,37 rejects original Axios error; src/features/tickets/components/AgentTicketWorkspace.tsx:39,64 console.error(error),95 logs validation object; Login/Register/CreateTicket also log raw errors. DTO content/token can remain in error.config, including comment body.

Executed a synthetic AxiosError probe with SYNTHETIC_SENTINEL token and SYNTHETIC_NOTE content; three assertions true: rawErrorConfigIncludesAuthorization, serializedErrorIncludesToken, serializedErrorIncludesContent. No real credentials or note content used. This verifies payload retention, not deployed console collection/exfiltration. Actual Workspace runtime log leak is pending E2E/telemetry capture.

Expected log only safe code/status/correlation, never whole request/response/error.config or form values. Frontend safe mapper; backend logs inspected only storage bucket and identity emails/errors, no ticket-body/MinIO URL logging call found. Email PII retention scope separately review; do not invent server note/token log evidence. OPS-C13/T04/T20, capture full browser+server+APM sinks incl role changes and login errors.

### OPS-SEC-11: coverage does not gate security integration

Evidence D tests/ServiceDesk.Application.Tests/Ticketing/TicketServiceTests.cs uses SQLite (10cases, owner filter regression); PG xmin excluded for SQLite. Integration factory swaps MinIO with stub, DatabaseTests uses EnsureCreated/CanConnect, not fresh migration from zero assertions. Jenkinsfile runs Category!=Integration, builds Docker images but no PG/MinIO test stage.

Actual run25 Auth/Ticket/DB tests fails fixture construction: DockerUnavailableException; npipe://./pipe/docker_engine timeout. Assertions never exercise API. Expected CI real PostgreSQL migrations and MinIO private IAM/integrity, multi-instance barrier tests, no treating skip as pass. QA/DevOps blocked release gate. Unit/architecture success not isolation proof; proposed tests51 remain NOT RUN because module absent.

### OPS-SEC-12: frontend typecheck fails

npm ci succeeds, npm run build exit1:
- AgentTicketWorkspace.tsx:10 Option unused (TS6133).
- AgentTicketWorkspace.tsx:225 Button success property not in ButtonProps (TS2322).
- TicketListPage.tsx:2 Space unused (TS6133).
These are baseline defects, no changes made. Expected production bundle build exits0. Thu Hoài separate fix, rerun baseline contracts/role UI/error tests. Build on Node24.13.1 (README expects22LTS); report this environment difference, don't claim failed solely because unsupported Node.

### OPS-SEC-13: contradictory “approved” contracts and Jira integration evidence

F docs05 auto-resume PendingUser on comment; docs06 self-assign Assigned empty/Lead override and uncertain Unassign target state; docs07 raw signed URL15min/file limits/author delete1h and mismapped TC-310/311 roles. D only3roles (no Lead), existing/api prefix and7status enum; domain draft proposes safe append, explicit resume/unassign→Triaged, no MVP edit/delete.

Reproduce by git show F:path and live Jira fetch; compare tables, not outdated audit counts. Expected one signed-off decision set with source/compatibility/test acceptance; Actual documents/code/live status disagree. Proposed new rules are not already approved. Đức Anh resolves OPS-DEC01..07/ATT-DEC01..10, links governance evidence, unblocks Backend/Frontend. No Jira write during audit.

### OPS-SEC-14: dependency advisory inventory

npm ci warns2high packages; npm audit --json exit1 identifies brace-expansion and source-map-js; npm ls confirms brace-expansion1.1.18 under ESLint and5.0.9 under typescript-estree, source-map-js1.2.1 under Vite/PostCSS. Source: D package-lock.json; observed restore follows lockfile, no npm audit fix applied.

High advisory metadata does not prove public API exploit: paths are frontend build/lint tooling here, reachability/untrusted sourcemap exposure not established. Proposed P2 triage, Thu Hoài + QA review transitive safe updates and re-run ci/build/lint without lockfile force churn. Primary advisory links returned by npm: [brace-expansion recursion](https://github.com/advisories/GHSA-qhr7-859c-m2p7), [source-map-js offsets](https://github.com/advisories/GHSA-68fv-2mgg-jv7q).

## 4. Implementation sequence / dependencies / release gates

| Order | Work / Jira | Owner | Concrete output and acceptance |
|---|---|---|---|
| 0 | P0 replay isolation, separate hotfix candidate | Backend Anh Nguyen, architect Đức Anh, QA Kiệt | Persistent subject/method/resource/key + hash; ticket/binding atomic; same/different user/payload, race2hosts, failure/TTL/auth/detail tests; real PG gate before integrate |
| 1 | Resolve policy + domain/RBAC dependencies TC-294/303/308, TC-246/247/199/299 | Đức Anh | Approved team/Dispatch/status/version/route/reply/limits/privacy/no-scanner acceptance; evidence links and compatibility migration plan |
| 2 | Repair existing frontend build/logging; enum alignment | Thu Hoài + Backend | Build0, shared enum mapping preserving persisted0–6, raw Axios logs removed; unknown status disabled; HTTP ownership regressions remain |
| 3 | Team/active/grants, revision/CAS, immutable restricted audit/migration foundation | Anh Nguyen + QA/DevOps | Backfill validates legacy rows; trusted capability policies; fresh+upgrade PG migration passes; membership fencing audited |
| 4 | Assignment TC-295/296 → TC-297 → TC-298 | Backend/Frontend/QA | ASG rules commands, 1winner CAS2hosts, immutable history, no owner mutation, UI safe409/error states |
| 5 | Public/internal TC-304/305 → TC-306 → TC-307 | Backend/Frontend/QA | Immutable parent visibility, strict DTO, public/staff queries prefilter, no sentinel leakage, auth/cursor/cache/downstream tests |
| 6 | Attachment TC-309 → TC-310 → TC-311, after privacy/policy | Backend/Frontend/QA + DevOps | API streams, private IAM, metadata/quota/compensation/integrity, approved limits, honest NotScanned and explicit risk gate; actual MinIO tests |
| 7 | Integrated Workspace TC-211 / sprint acceptance | Thu Hoài + QA + Đức Anh | End-to-end real create→queue→assign→transition→comment/note→attach→resolve; backend ready, no mock evidence; build + storage + security gates |
| 8 | Deferred channels / scanning task only after explicit approval | Owner TBD | Privacy propagation tested before export/search/AI/notification/scanner enabled, no expansion in this task |

Integration plan preserves Git history: implement on scoped branches from refreshed dev, merge through reviewed PRs only when user authorizes. Do not cherry-pick uncommitted hotfix worktree or overwrite source on frontend/audit/docs. Sequence feature/fix → dev → staging → main with build/unit/PG/MinIO/security and rollback gates; no such merge/push performed.

Migration approach: expand schema nullable/new tables, validate/backfill routing/identity keys, quarantine inconsistent rows, enforce constraints; no numeric renumber or fake assignee. Rollback application only with backwards-compatible schema and feature flags disabled; retain immutable audit/comments/attachment metadata. Never drop populated data/volumes for release rollback. Orphan compensation must be exercised independent of DB transaction. Ops/docs choices cannot replace business approval.

## 5. Validation execution evidence

Commands run in isolated worktree at D on10/10/2026; SDK10.0.300, Node24.13.1, npm11.8.0. Build và source inspection trong phạm vi đã liệt kê; không phải xác minh toàn bộ security behavior. Không thêm business tests trong task docs-only này.

| Command | Result | Evidence / limit |
|---|---|---|
| git fetch origin --prune | PASS exit0 | Original branch/index/files preserved |
| git worktree add -b docs/ticket-operations-security ../ai-it-service-desk-ticket-security origin/dev | PASS | New clean checkout, tracking origin/dev |
| git pull --ff-only origin dev | PASS, Already up to date | HEAD == origin/dev full SHA D, initial status empty |
| dotnet build src/backend/ServiceDesk.sln -c Release | PASS exit0 | 0 errors/0 warnings |
| dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration" | PASS exit0 | Domain4 + Application11 + Architecture6 =21; Integration excluded |
| dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Auth\|FullyQualifiedName~Ticket\|FullyQualifiedName~Database" --logger "trx;LogFileName=operations-audit.trx" | FAIL exit1 / environment BLOCKED | Filter used actual OR character without escaping in PowerShell;25 fixture failures,0passed, DockerUnavailableException npipe timeout; API assertions not executed |
| npm ci (frontend directory) | PASS exit0 | Lockfile install272packages;2high advisory warnings |
| npm run build (frontend directory) | FAIL exit1 | Three TS errors OPS-SEC-12; Vite bundle not reached |
| npm audit --json | FAIL exit1 / advisory inventory | 2high packages, OPS-SEC-14; no remediation applied |
| Synthetic Axios error retention probe | PASS |3booleans true, synthetic data only; no runtime UI exploit test claim |
| MinIO real storage/IAM/signature/integrity tests | NOT RUN | No existing real storage suite, fixture uses stub; Docker unavailable |
| New OPS-A/C/T scenario matrix | NOT RUN |51 designed tests, missing server modules; not implemented here |
| Self-review + git diff --check + explicit new-doc scan | See final verification below | Untracked docs require explicit whitespace/structure inspection; no source edits |

Test artifact (ignored, not a deliverable): tests/ServiceDesk.IntegrationTests/TestResults/operations-audit.trx has counters total25/executed25/failed25/passed0, failure before action tests at fixture constructor. Environment block is a merge/release blocker until CI with Docker succeeds; do not mark Integration passed based on unit/SQLite.

Re-run exact validation:
~~~powershell
dotnet build src/backend/ServiceDesk.sln -c Release
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~Auth|FullyQualifiedName~Ticket|FullyQualifiedName~Database" --logger "trx;LogFileName=operations-audit.trx"
Push-Location src/frontend/service-desk-web
npm ci
npm run build
npm audit --json
Pop-Location
~~~

## 6. Source and branch verification commands

~~~powershell
git status --short
git branch --show-current
git rev-parse HEAD
git rev-parse origin/dev
git diff --name-only
git diff --cached --name-only
git ls-files --others --exclude-standard
git diff --check
git show origin/dev:src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs
git show origin/dev:src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs
git show origin/dev:src/backend/ServiceDesk.Infrastructure/Storage/MinioObjectStorageService.cs
git show origin/frontend:docs/architecture/05-comment-and-internal-note-privacy-contract.md
git show origin/frontend:docs/architecture/06-ticket-assignment-policy.md
git show origin/frontend:docs/architecture/07-attachment-security-limits-and-policy.md
rg -n "MapPost|MapGet|RequireAuthorization" src/backend/ServiceDesk.Api/Endpoints
rg -n "GetPresigned|EnsureBucket|ClassifyAsync" src tests
rg -n "console.error|console.log|Authorization" src/frontend/service-desk-web/src
~~~

Before/after preservation uses SHA256 over tracked+untracked nonignored file contents, index entries, working diff, status/branch/HEAD for5protected worktrees. Ignored build outputs not business files. Exact comparison result recorded after final self-review. Original frontend was clean at start; nine earlier staged architecture files are already user-committed at063489b, not staged now. RBAC has2staged docs; domain3untracked; audit3untracked; hotfix pending18files. Do not falsely report9files still staged.

## 7. Self-review and changed files

Checks required: all findings identified with branch/fullSHA alias/path/severity/impact/reproduction/expected/actual/owner/test; actual bug distinct from future exposure risk. ASG/COM/ATT rules each actor/action/precondition/result/negative; Closed/Cancelled/read semantics clear; no Requester internal bytes or raw signed URL; no arbitrary size cap as approved. Assignment+audit transactional, attachment compensation explicit, no mutation of existing enum or lifecycle in audit. PM decisions marked Proposed/TBD, live Jira mismatch and scanning exclusion preserved.

Changed files only:
1. docs/architecture/05-assignment-and-comment-policy.md
2. docs/architecture/06-attachment-security.md
3. docs/reviews/ticket-security-findings.md

Expected final Git state:3untracked docs, tracked diff empty, index empty; no source/migration/package-lock changes. No commit/push/merge. Release security outcome remains BLOCKED by P0 on dev, unimplemented operations, policy decisions, frontend build and real integration validation. Completion of documentation is not completion of remediation.

## 8. Final verification (2026-10-10)

Self-review PASS:46 rule rows,51 proposed test scenarios,14 findings; each rule matrix has all five columns (rule, actor/action, precondition, behavior, negative/acceptance), UTF-8 without BOM, no trailing whitespace/conflict markers, balanced code fences. git diff --check exit0; tracked diff and cached diff empty; exactly three requested documentation files untracked. No business source, migration, lockfile or protected index changed.

| Protected worktree / branch | HEAD giữ nguyên | Status giữ nguyên | SHA256 index / file contents before = after |
|---|---|---|---|
| ai-it-service-desk / frontend | 063489b97c1f706cebeaeea1d3038f0e63a39fd7 | Clean, nine prior documents already committed by user | E8CC1B64D99673C0383082F40359AA2715AE78BD1DF07951E3EEA44AE603EE71 / D6DBB62907BCBE0AF8E56420F5C8D52C2E5155539A04672210F5FB4BF88AECC8 |
| ai-it-service-desk-audit / audit/repository-audit-20261009 | 6b124e73e9cb95350a505a24a548cb663ab1b7de | 3 untracked audit reports | 669616480AE2D509B49012874F8CF187551DA24F1A530B10A296BBEDF5C8781A / 2BDC37F4D91DD332334569CCD971235AB195423BB34F3F4231D898805FE23B58 |
| ai-it-service-desk-p0-idempotency / fix/p0-idempotency-isolation | 6b124e73e9cb95350a505a24a548cb663ab1b7de | Pending hotfix source/tests/migration/report | 669616480AE2D509B49012874F8CF187551DA24F1A530B10A296BBEDF5C8781A / 45A0762571E83415A2CF9BD580DF675C7B028C4120E78251A6A60AA89481C28A |
| ai-it-service-desk-rbac / docs/tc-246-247-rbac | 6b124e73e9cb95350a505a24a548cb663ab1b7de | 2 staged RBAC documents | A1C97ACBAAB4F2FDB5589A4BD79ABAFF181AE04738D476187CEA64A0319F8241 / 00077A40A7DEFC9F2C6731B0CAA3EEB68A02099AD57CF542328EC99802124697 |
| ai-it-service-desk-ticket-domain / docs/ticket-domain-lifecycle | 6b124e73e9cb95350a505a24a548cb663ab1b7de | 3 untracked domain documents | 669616480AE2D509B49012874F8CF187551DA24F1A530B10A296BBEDF5C8781A / 90AACC62B8CC5CF6FCD83031AB7CB7566A9317D9B67A8098F1EF76B46E430CEC |

Branch/status/HEAD/index entries/working diff/all tracked+untracked nonignored file hashes compare identical for all five roots. Local main/dev worktrees were not operated on. Shared remote refs fetched as explicitly requested; no rewrite of local branch history. New docs branch HEAD remains D; no audit commit made, no push/PR/merge. No deployed security remediation claimed.

