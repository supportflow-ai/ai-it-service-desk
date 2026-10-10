# Frontend / Backend integration gap report

Ngày: 2026-10-10. Trạng thái: **BLOCKED cho integration/release**. Audit và ba sửa đổi kỹ thuật nhỏ đã hoàn tất; ticket operations chưa được triển khai và Docker validation chưa thành công.

## Revision và phạm vi

- Baseline **D**: `origin/dev`, full SHA `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Branch làm việc: `audit/frontend-backend-integration`, worktree riêng `../ai-it-service-desk-integration`; không switch repository gốc.
- Reference **F**: `origin/frontend` SHA `063489b97c1f706cebeaeea1d3038f0e63a39fd7`. F có docs lifecycle/RBAC chưa cùng current implementation; không merge.
- Tất cả finding bên dưới dùng source **D**, branch `origin/dev`, trừ mục FIX ghi rõ delta chưa commit và section reference F. “Confirmed” = xác định từ source hoặc command thất bại; “Risk” = cần runtime/security test, không khẳng định exploit đã xảy ra.
- Không sửa backend, migrations, enum, permission rules, authentication flow, dependencies hoặc API paths. Không commit/push/merge.
- Chi tiết inventory và DTO: [api-contract-matrix.md](api-contract-matrix.md). CI/E2E acceptance: [e2e-integration-plan.md](e2e-integration-plan.md).

## Findings và implementation backlog

Mỗi mục có root cause, evidence, repro, expected/actual, dependency, owner đề xuất, code change và acceptance/tests. Owner là phân công đề xuất; chưa gửi thông báo hoặc đổi Jira.

### GAP-01 — P0 — Cross-requester idempotency replay (Confirmed source; exploit test chưa chạy)

Evidence D: `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:33–50`, `src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs`; `tests/ServiceDesk.IntegrationTests/TicketEndpointTests.cs` chỉ test replay cùng user. Key là `idempotency:ticket:{trimmedKey}`, cache giá trị TicketDto toàn process trong10 phút; replay trả cached DTO trước create service, không gắn owner/method/resource/payload. Trong compose, `deploy/nginx/nginx.conf` có hai API instances và memory cache độc lập.

Repro cần isolated fixture: A POST /api/tickets với K; B authenticated POST cùng K trong cùng instance, trước TTL. Actual theo code: B nhận DTO A với201. Expected: B không bao giờ nhận ticket A; cùng-user/cùng-payload cùng kết quả; khác-payload409 theo contract; concurrent/multi-instance không duplicate. Không cần GET ticket IDOR để gặp lỗi này. Frontend hiện không gửi header này, nhưng direct API client có thể gửi.

Impact: lộ title/description/ticket identifiers giữa người dùng; duplicate khi race/multi-instance. Owner: **Anh Nguyen / Backend Core**, Đức Anh review, Kiệt QA. Dependency: candidate ở `fix/p0-idempotency-isolation` chưa tích hợp; phải review và PostgreSQL tests PASS trước consumer release. Change: persistent requester+method+resource+key hash record, unique scope, transaction cùng ticket, payload fingerprint, expiration policy, rollback; không chỉ thêm owner vào memory key. Tests: A/B isolation; replay; payload conflict; race; two hosts; failed create; TTL; unauthorized; detail regression. AC: không cross-owner response, một durable ticket cho concurrent same scope, T01–T10 của hotfix và real PostgreSQL PASS. Audit này không tuyên bố P0 đã sửa.

### GAP-02 — P1 — Persisted enum semantics khác frontend (Confirmed)

Evidence D: `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:3–11`, `src/backend/ServiceDesk.Application/Ticketing/Dtos/TicketDto.cs`, `src/frontend/service-desk-web/src/features/tickets/types.ts:3–12`, `src/frontend/service-desk-web/src/features/tickets/components/{TicketDetailPage.tsx,TicketListPage.tsx,AgentTicketWorkspace.tsx}`. DB stores integer tại `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs`.

Repro: supply real detail DTO status4 (BE Resolved) to existing FE; FE treats InProgress và workspace chọn các action đang xử lý. Values2–6 đều khác;7–8 FE định nghĩa nhưng BE không có. Expected: cùng persisted numeric một semantic; unknown không mở action. Actual: typed Axios cast không chuyển đổi numeric.

Owner: **Đức Anh/Architect + Anh Nguyen + Nguyen Thi Thu Hoai**, Kiệt fixtures. Dependency: TC-199/260/261/299, duyệt lifecycle, inventory historical DB và reconcile F docs. Change: chốt canonical numeric/wire schema; frontend adapter/enum và command guards theo contract; nếu BE thêm state thì append/migrate có compatibility plan, không renumber dữ liệu. Tests/AC: đủ0–6 và unknown, UI labels/actions/API round-trip, migration + stale clients; không chuyển nghĩa stored status. Chưa implement vì không có một lifecycle đã được thống nhất giữa source/docs.

### GAP-03 — P1 — My tickets API và server paging thiếu (Confirmed)

Evidence D: `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:8–10`, `src/frontend/service-desk-web/src/features/tickets/components/TicketListPage.tsx`, `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:18–84`; Program chỉ map endpoint extension này. /tickets/me không match {id:guid} và không có list route.

Repro: Requester login → /tickets → GET /api/tickets/me; expected owned list, actual route miss404. AntD pageSize10 chỉ cắt array ở client. Jira TC-201/271 Done nhưng [TC-269](https://evolving-kg-drift.atlassian.net/browse/TC-269)/[TC-270](https://evolving-kg-drift.atlassian.net/browse/TC-270) To Do.

Owner: **Anh Nguyen**, Thu Hoai consumer, Kiệt tests. Dependency: ownership contract TC-246/247 và DTO/enum GAP-02. Change: owner-filtered query, ổn định createdAt/id sorting, duyệt paging/filter response trước đổi FE array; đảm bảo records/count/cursors không lộ B. Tests/AC: A/B ownership, unauth401, invalid query400, tie timestamps/multiple pages/no duplicates, empty state, filters/count; TC-273 QA evidence. Không dùng mock list để đánh dấu Done.

### GAP-04 — P1 — Agent queue thiếu (Confirmed)

Evidence D: `src/frontend/service-desk-web/src/routes/AppRoutes.tsx` /agent-workspace render DemoPage; `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs` chỉ create/detail; không queue HTTP caller. Jira TC-206/290 To Do, TC-291/292 In Progress.

Repro: Agent vào queue route; actual demo page không query; expected real eligible ticket queue. Owner: **Anh Nguyen + Thu Hoai**, Kiệt QA. Dependency: approved staff/team scope, GAP-02/03. Change: query endpoint + authorized scope/filter/paging, queue UI/navigation. Tests/AC: staff roles/direct API/requester denied, pagination/filters, click detail correct ID, admin behavior theo approved matrix; fresh DB records visible không fixtures mocked.

### GAP-05 — P1 — Ticket commands chưa có handler/persistence/concurrency protocol (Confirmed absence; future race risk)

Evidence D: `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:49–71`, `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx`; `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs`, `src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs`, `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:83`. Triage/assign/start/request-info/resolve/close/reopen routes không registered; entity không assignee/resolution; xmin shadow không expose command revision. UI sends assignedAgentId:'me' và có message409 nhưng không gửi token.

Repro: workspace assign/start/resolve → route miss; request-info button chỉ placeholder, triage function không UI caller. Expected approved command contract; actual no backend. Owner: **Anh Nguyen**, Đức Anh policy, Thu Hoai UI, Kiệt tests. Dependency: TC-294–298, TC-299–302, lifecycle/RBAC approvals, GAP-02. Change: domain guards + command handlers/schema/audit + revision request/response; chọn self-assign semantics rõ, không cast 'me' thành Guid hoặc tự cho dispatch mọi agent. Tests/AC: legal/illegal state, roles/ownership, eligible target, missing resolution, stale revision409 after one successful mutation, atomic state+audit, approved reopen/close; DB migration rollback. Concurrent behavior chưa thể đánh giá implementation không tồn tại; không ghi “race reproduced” cho commands.

### GAP-06 — P1 — Public/internal/timeline chưa tích hợp; privacy gate (Confirmed mock/missing; leak chưa reproduced)

Evidence D: `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:21–36,74–77`; WORKSPACE reads mock event timestamp Date.now()-1h và posts isInternal; backend TicketEndpoints không comments/notes/timeline handlers/entities.

Repro: refresh workspace → synthetic event timestamp đổi; send public/internal → no route. Expected persisted real timeline, owner/staff authorization, backend excludes internal information from Requester in all channels. Actual layout mock và no enforcement path vì API thiếu. Owner: **Anh Nguyen + Thu Hoai**, Đức Anh TC-303, Kiệt TC-307. Dependency: TC-304/305/306, approved visibility/Closed semantics, GAP-05. Change: persistent author/time/visibility, DTO projection/filter server-side, command policy; decide separate note endpoint vs discriminator; redact AI/search/export/notification/log outputs. Tests/AC: A/B access, requester spoof isInternal, unreadable note ID, private marker absent from Requester JSON/UI/search/export/AI/logs; DB row assertions; no synthetic timeline in production. Không coi hidden tabs là enforcement.

### GAP-07 — P1 — Auth schema deployment path chưa được real migration test (Risk, runtime blocked)

Evidence D: `src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs` là migration ở baseline; thiếu Identity-table migration/model snapshot; `src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs` gọi MigrateAsync trước Identity operations; `tests/ServiceDesk.IntegrationTests/DatabaseTests.cs` dùng EnsureCreatedAsync dù test name nhắc migrate.

Repro cần fresh PostgreSQL DB: migration + startup seed + register/login, không dùng EnsureCreated. Expected Identity schema tạo bằng migration; actual SQL/startup chưa chạy được vì Docker; source test chưa chứng minh migration path. Owner: **Anh Nguyen**, CI/DevOps, Kiệt tests. Dependency: Docker gate và reconcile P0 migration snapshot. Change sau reproduce: reviewed initial Identity migration/snapshot/bootstrap path, additive data-safe rollout; không EnsureCreated trên DB có migration/data. Tests/AC: fresh DB và upgrade existing DB, no table conflicts/data loss, seed/register/login PASS, rollback documented. Không khẳng định fresh deployment đã thất bại ngoài runtime evidence.

### GAP-08 — P1 — CI không gate real integration/E2E hoặc test mới (Confirmed configuration; test publishing Risk)

Evidence D: `Jenkinsfile` unit test stage filter Category!=Integration, frontend build only; không lint/node regression/integration/E2E stages. Docker Build chỉ dev/staging/main, không chạy test stack. `.github/` chỉ templates. junit input glob *.trx cần kiểm tra publisher/plugin conversion, chưa chạy Jenkins để kết luận stage fail.

Expected: security integration + real E2E phải pass trước release; actual pipeline green có thể chỉ chứng minh build/unit. Owner: **CI/DevOps** + Kiệt + Backend/FE. Dependency: Docker-enabled runner, real PostgreSQL fixtures, approved flows. Change: gates trong E2E plan; test artifacts redacted; proper TRX publisher hoặc conversion. Tests/AC: deliberate failing integration/E2E làm pipeline fail, reports đúng counters, mock-only không pass, Node version chạy native TypeScript test. Audit không sửa Jenkins do môi trường runner chưa xác minh.

### GAP-09 — P2 — Route role matrix lệch API (Confirmed mismatch; intended policy TBD)

Evidence D: `src/frontend/service-desk-web/src/routes/AppRoutes.tsx:101–112`, `src/frontend/service-desk-web/src/routes/components/ProtectedRoute.tsx`; `src/backend/ServiceDesk.Api/Program.cs` policies; TICKET-SVC GetTicketByIdAsync.

Repro: Admin-only valid user bị chặn agent workspace trong UI, dù backend staff diagnostics/detail cho phép Admin. Agent-only không vào requester route dù RequireRequester chứaAgent. Expected approved actor matrix nhất quán; actual actor sets khác. Owner: **Đức Anh** TC-246/247 + Thu Hoai + Anh Nguyen. Dependency: approved policy. Change: reconcile route/API/resource guard separately, không thêm Admin mọi nơi theo suy đoán. Tests/AC: single/combined roles in route and directAPI,401/403/foreign404, hide button không substitute API enforcement. Backend staff can read all tickets hiện là source behavior, chưa có team filter contract.

### GAP-10 — P2 — Session/error handling phân loại chưa thống nhất (Confirmed source; races Risk)

Evidence D: `src/frontend/service-desk-web/src/features/auth/context/AuthContext.tsx:20–28`, `src/frontend/service-desk-web/src/shared/api/apiClient.ts`; AuthEndpoints error {errors}/401, TicketEndpoints {error}; workspace maps all409 to xmin message.

Repro: restore /auth/me fails503/network → token bị xoá như invalid session. Future payload-idempotency409 có thể hiện sai xmin message. Expected phân biệt auth failure/transient and domain error reason; actual broad catch/simple status. Old-request401 ending a newer session là risk cần delayed-response test, chưa reproduced. Owner: **Thu Hoai + Anh Nguyen**, Kiệt. Dependency: approved structured error contract. Change: unify error codes/decoder, deliberate retry/logout policy, session generation if race confirmed; not blanket server state changes. Tests/AC:401login vsprotected,403no logout,404foreign,409revision vsfingerprint,400validation,503/network preserve/retry theo policy, delayed stale401; no token in logs. FIX-03 chỉ type-safe status extraction, không giải quyết toàn bộ mục này.

### GAP-11 — P2 — Input/profile/DTO completeness (Confirmed)

Evidence D: `src/frontend/service-desk-web/src/features/auth/components/Register.tsx` min6 vs `src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs` Identity min8 + digit/upper/lower; `src/backend/ServiceDesk.Infrastructure/Identity/AuthService.cs` register echoes FullName nhưng IdentityUser không persist; `src/frontend/service-desk-web/src/features/tickets/components/CreateTicketPage.tsx` raw description min10; TICKET-SVC trim>=10; DB title200 vs service100; `features/tickets/types.ts` description? và missing nullable dept.

Repro: form-valid password6 bị server reject; whitespace description bị400; register fullName rồi login/me → null. Expected UI phản ánh current API validation và nullable profile; actual inconsistent validation/persistence. Owner: **Thu Hoai + Anh Nguyen**, Architect khi cần đổi business limits. Dependency: agreed validation/profile scope. Change: mirror current password constraints descriptively, trim validation, DTO truthfulness, decide profile persistence/fallback separately. Tests/AC: server/UI boundary fixtures, white-space/length/categorycase, nullableprofile rendering no misleading name; title100/101/200 explicit requirement. FIX-02 sửa required password/fullName nullable ở TS, chưa sửa persistence/form rules.

### GAP-12 — P2 — Raw browser error logging có thể chứa credential/request content (Confirmed raw calls; actual sensitive output Risk)

Evidence D: `src/frontend/service-desk-web/src/features/auth/components/{Login.tsx,Register.tsx}`, `features/auth/context/AuthContext.tsx`, `features/tickets/components/{CreateTicketPage.tsx,TicketListPage.tsx,TicketDetailPage.tsx,AgentTicketWorkspace.tsx}`; apiClient adds Authorization. console.error(error) có thể in Axios config/headers/body.

Expected log allowlist correlation/status, không password/token/internal note. Actual arbitrary error object passed to console; chưa capture real credentials, không ghi secret vào báo cáo. Owner: **Thu Hoai**, security review Đức Anh. Dependency: logging contract/privacy GAP-06. Change: shared safe error logger with allowlisted fields, not spread Axios config. Tests/AC: injected synthetic token/password/note markers never present serialized console/logger output; real browser redacted trace. FIX-03 không sanitise logging.

### GAP-13 — P2 — Attachment operation còn placeholder (Confirmed)

Evidence D: `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx` attachment button; `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs` thiếu upload/download mapping; storage abstraction không chứng minh ticket authorization endpoint. `deploy/nginx/nginx.conf` client_max_body_size50M không là approved per-file policy.

Expected authenticated owner/staff upload/download và metadata, actual nohandler. Owner: **Anh Nguyen + Thu Hoai**, Đức Anh TC-308, Kiệt TC-311. Dependency: approved limits/MIME/signature/private access/scan policyTC-308–310, persisted ticket/comment visibility. Change: authorized ticket attachment endpoints + private storage/meta/cleanup, UI binds real calls. Tests/AC: cross-owner reads, note attachments, signature/malware policy, orphan rollback, signedURL scope/expiry; không chọn10MiB hay50M làm policy tự động.

### GAP-14 — P2 — Dependency warnings; P3 — bundle/refresh warnings (Observed, exploit/performance Risk)

Evidence D + unchanged dependency graph: `src/frontend/service-desk-web/{package.json,package-lock.json}`; npm ci reported2 high-severity advisories. Không xác minh exploitability bằng browser hoặc audit fix. Final build warns chunk>500KB; `features/auth/context/AuthContext.tsx:74` lint Fast Refresh warning.
Owner: **Thu Hoai / dependency maintainer**, Kiệt regression. Dependency: advisory triage và compatibility review. Change: inspect npm audit detail + lock updates riêng có test; lazy loading sau đo thực tế; split provider/hook nếu cần. Tests/AC: controlled advisory assessment, lockdiff/regression; measured loading budget approved. Không nâng package hàng loạt hoặc ghi warning thành confirmed runtime exploit. Lint/build vẫn exit0.

## Ba logical fixes đã thực hiện

| Fix / severity baseline | Before → After | Files / evidence | Test và review diff | Scope / AC |
| --- | --- | --- | --- | --- |
| FIX-01 / P1 build blocker, Confirmed command | tsc FAIL3 errors → PASS | WORKSPACE unused Select.Option + invalid AntD Button success prop; TicketListPage unused Space | tests/frontend/production-typecheck.test.mjs; [FIX-01 diff](fixes/FIX-01-frontend-build.patch) | Bỏ unused imports/unsupported prop, giữ Button style/behavior; strict production tsc asserts exit0 |
| FIX-02 / P2 auth TS contract, Confirmed compiler | optional passwords/nonnullable fullname → required password/nullable fullname | src/frontend/service-desk-web/src/features/auth/types.ts; BE Identity models + AUTH-SVC tại D | tests/frontend/auth-contract.test.mjs; contracts/auth-contracts.ts + tsconfig.json; [FIX-02 diff](fixes/FIX-02-auth-dto.patch) | Valid login/register/nullable user compile; missing passwords/invented roles rejected; không thay wire API/persistence |
| FIX-03 / P2 lint/type safety, Confirmed lint | explicit any and unsafe unknown access → unknown + Axios narrowing | WORKSPACE; TicketListPage; new shared/api/httpError.ts | tests/frontend/http-error.test.mjs,7 cases; [FIX-03 diff](fixes/FIX-03-typed-http-errors.patch) | Preserve401/404/409 for Axios; safe network/native/null/primitive; reject arbitrary response-shaped objects; no policy/state changes |

WORKSPACE và TicketListPage aliases chính xác trong contract matrix. FIX-01/02 có RED test run trên baseline rồi GREEN; FIX-03 có unit assertions thực chất và lint regression. Các fixture synthetic ở unit tests chỉ kiểm tra helper/type contract, không là E2E evidence. Axios isAxiosError là library type guard, không là security authentication boundary.

Patch chain đã `git apply --cached --check` và apply vào **temporary index độc lập**, bắt đầu HEAD D, theo01→02→03; index hash của9 source/test files khớp working files. Index thật của audit và các worktree khác không được stage/commit. Các patch là review artifacts, không tự apply trên branch của thành viên.

## Priority, dependency và integration order

1. **P0 isolation**: Backend review candidate hotfix, gate real PostgreSQL concurrent/two-host/rollback/security cases, reconcile migration lineage. Không add Idempotency-Key FE trước security contract này.
2. **P1 contract decision**: Architect + Product reconcile F approved-labelled docs với current DB enum, actor roles, assignments, pending-user/public-comment semantics, request paths. Ghi decision/migration trước code; không renumber existing integer.
3. **P1 basic reads**: migrate/auth-bootstrap verified; own list + stable paging; detail fixtures/negative ownership; queue authorized query + real UI.
4. **P1 mutations**: assignment → allowed transitions → comments/notes/timeline; all transactional, revision-aware, policy/audit tested. Close/reopen/user-confirm theo decision, không mặc định lifecycle.
5. **P2 attachment**: duyệt policy rồi storage/API/visibility/cleanup; không feature expansion ở audit branch.
6. **P2 UX/error/log/input**: structured error codes, session handling, safe logs, currentvalidation mirror; dependency update riêng.
7. **CI + release**: lint/build/unit/type contracts/integration/real E2E gates theo plan; chứng minh staging bằng real DB/network traces redacted. No release with P0/P1 unresolved.

Branches cần phối hợp: origin/dev làm integration base; origin/frontend chỉ review docs/code delta; fix/p0-idempotency-isolation, docs/tc-246-247-rbac, docs/ticket-domain-lifecycle, docs/ticket-operations-security là local candidate worktrees chưa đồng nghĩa integrated dev. Không cherry-pick/merge/reset chúng trong audit này. Review three small fixes as separate patches/PRs sau user authorization; fast-forward/update by merge giữ history khi triển khai sau này, không force-push.

## Changed files và verification

9 source/test files:

- src/frontend/service-desk-web/src/features/auth/types.ts
- src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx
- src/frontend/service-desk-web/src/features/tickets/components/TicketListPage.tsx
- src/frontend/service-desk-web/src/shared/api/httpError.ts
- tests/frontend/production-typecheck.test.mjs
- tests/frontend/auth-contract.test.mjs
- tests/frontend/contracts/auth-contracts.ts
- tests/frontend/contracts/tsconfig.json
- tests/frontend/http-error.test.mjs

6 documentation/review artifacts: api-contract-matrix.md, frontend-backend-gap-report.md, e2e-integration-plan.md trong docs/reviews và3 patchfiles trong docs/reviews/fixes. Tổng15 files; package.json/package-lock/backend/migrations/Jenkinsfile không thay đổi. Build/test generated artifacts ignored.

Validation summary: backend Release build PASS0 errors/warnings; nonintegration21 PASS; integration28 FAIL tại Docker fixture initialization,0passed, assertions chưa chạy; npm ci PASS; final lint PASS0errors/1warning; final frontend build PASS có largechunkwarning; new Node tests9PASS. Commands/output cause và E2E not-run ở plan.

## Self-review và bảo toàn

- Fixes chỉ sửa compile/type errors theo contract đang tồn tại. Mỗi fix có test và patch riêng; patch chain khớp source. Không sửa business enum/policy để “làm UI hoạt động”.
- Findings tách source-confirmed absence/mismatch với risk cần Docker/runtime; không dùng mock hoặc Jira Done để suy diễn acceptance.
- Baseline/branch/SHA/source paths được ghi; unpublished fix không được gán SHA D.
- Security P0 chưa giải quyết trên dev; E2E/PG gate vẫn BLOCKED, không đề nghị release.
- Snapshot trước/sau gồm branch,HEAD,status,index hash,tracked/untracked content hashes ở6 worktree được đối chiếu cuối tác vụ. Repo gốc frontend tại F sạch ngay đầu task:9 tài liệu cũ đã nằm trong commit F, **không còn9 file staged**. Không stage lại chúng.
- Không commit, push, PR hoặc merge tự động. Validation final và preservation digest được ghi sau khi hoàn tất self-review.

### Kết quả preservation cuối tác vụ — PASS

Đối chiếu snapshot trước/sau đã PASS cho cả6 worktree, gồm branch, HEAD, status, SHA-256 index listing, working diff và toàn bộ tracked/untracked non-ignored file contents. Không tính ignored build outputs; protected worktrees không được build/write trong task này.

| Worktree | Branch / HEAD | Trạng thái đầu giữ nguyên ở cuối | Branch/HEAD/index/diff/content |
| --- | --- | --- | --- |
| ai-it-service-desk | frontend / F | Clean;9docs đã user commit tại F | PASS unchanged |
| ai-it-service-desk-audit | audit/repository-audit-20261009 / D |3audit reports untracked | PASS unchanged |
| ai-it-service-desk-p0-idempotency | fix/p0-idempotency-isolation / D | Pending hotfix source/tests/migrations/docs | PASS unchanged |
| ai-it-service-desk-rbac | docs/tc-246-247-rbac / D |2architecture files staged | PASS unchanged |
| ai-it-service-desk-ticket-domain | docs/ticket-domain-lifecycle / D |3reports untracked | PASS unchanged |
| ai-it-service-desk-ticket-security | docs/ticket-operations-security / D |3reports untracked | PASS unchanged |

Audit integration: HEAD==origin/dev D, actual index empty,3tracked source files modified +12untracked files =15review changes. `git diff --check` PASS; report content integrity PASS; patch chain/hash reproduction PASS. Không có commit/push/PR/merge. Reports/patches/tests/source fixes chỉ nằm trong worktree integration.
