# Technical Debt & Blocker Register

Ngày audit: 09/10/2026 (Asia/Bangkok, UTC+07). Repository: https://github.com/supportflow-ai/ai-it-service-desk.

Baseline được fetch/pull mới: **origin/dev @ 6b124e73e9cb95350a505a24a548cb663ab1b7de**. Worktree audit: `D:/Users/Documents/HUCE/HK1_2026-2027/8. ThapCam/Code/ai-it-service-desk-audit`. Nhánh: `audit/repository-audit-20261009`.

Quy ước SHA dùng xuyên suốt (mọi viết tắt dưới đây trỏ chính xác SHA đầy đủ):

| Nhãn | Branch | Commit SHA |
|---|---|---|
| D | origin/dev | 6b124e73e9cb95350a505a24a548cb663ab1b7de |
| M | origin/main | e044f470d38cb9d00a4adca9e59de45abbde3ff5 |
| A | origin/feature/tc-275-276-277-ai-triage-contract | b529b2cdd138b273ad5f89efb18ccb9f5a399313 |
| Q | origin/feature/tc-203-quality-gate-and-integration-environment | e834e74a50ecc4986ca329bc2a217ffc14af8cf4 |
| F | origin/frontend | 106909cca3ed09e4687dda667a213c6d60841a8f |
| C | origin/feature/core-ticket-lifecycle | 5425b62b84913964c129b9235744a7dd03bd3a4f |
| T | origin/feature/tc-200-create-ticket | 9df98c27a5d50b269ce5ba71dacd1e0839f7c46d |
| B | common baseline; local main/dev trước audit | eceaaa28c9ec377353ae354d92f33b0f1fd40dd1 |

Paths đều relative repository tại SHA/branch ghi kèm, số sau dấu `:` là line của snapshot đó. Path thiếu ở một branch được ghi rõ là absence evidence qua tree/route scan. Owner là **đề xuất**, không thay đổi assignee Jira. P0 = chặn đưa ra môi trường có dữ liệu/thành viên thật; P1 = chặn tích hợp/release hoặc nghiệm thu feature liên quan; P2 = debt/planned scope. Không coi mọi planned feature missing là bug.

## Tóm tắt và evidence levels

20 findings: **2 P0, 13 P1, 5 P2**. Chưa sửa business code trong audit. Mỗi finding phân biệt source-confirmed issue, isolated endpoint probe và runtime chưa xác minh. Owner được đề xuất theo Jira live ngày 09/10/2026: Đức Anh (architecture/governance), Anh Nguyen (Backend Core), Nguyễn Thị Thu Hoài (Frontend; TC-312–314 hiển thị tên Nguyen Thi Thu Hoai), Hương Nguyễn (AI), VÕ NGUYỄN ANH KIỆT (QA/DevOps). Git author identity không được tự đồng nhất với Jira account.

| ID | Severity | Finding | Evidence class | Owner đề xuất |
|---|---|---|---|---|
| [AUD-01](technical-debt-register.md#aud-01) | P0 | Idempotency cache trả ticket của requester khác | Bug xác nhận bằng probe HTTP trên endpoint thật; service/current-user được giả lập, chưa phải E2E PostgreSQL/JWT. | Anh Nguyen (Backend Core); Đức Anh review security; VÕ NGUYỄN ANH KIỆT xác minh |
| [AUD-02](technical-debt-register.md#aud-02) | P0 | Seed tài khoản đặc quyền và khóa JWT mặc định không có gate môi trường | Cấu hình nguy hiểm xác nhận qua source; khai thác trên deployment thật chưa kiểm thử. P0 áp dụng trước staging/production. | Anh Nguyen + VÕ NGUYỄN ANH KIỆT; Đức Anh duyệt release |
| [AUD-03](technical-debt-register.md#aud-03) | P1 | Idempotency không atomic và không chia sẻ giữa API nodes | Race cùng process xác nhận bằng probe; hành vi multi-node suy luận từ IMemoryCache và topology, chưa chạy E2E. | Anh Nguyen; VÕ NGUYỄN ANH KIỆT test concurrency |
| [AUD-04](technical-debt-register.md#aud-04) | P1 | Migration baseline thiếu Identity và model snapshot | Migration/model gap xác nhận qua generated SQL và EF metadata; lỗi startup thực tế trên PostgreSQL mới chưa chạy vì Docker không hoạt động. | Anh Nguyen; VÕ NGUYỄN ANH KIỆT clean-DB regression |
| [AUD-05](technical-debt-register.md#aud-05) | P1 | Frontend không build và lint không đạt | Bug build xác nhận bằng npm run build/lint trên clean dependencies. | Nguyễn Thị Thu Hoài; VÕ NGUYỄN ANH KIỆT gate |
| [AUD-06](technical-debt-register.md#aud-06) | P1 | TicketStatus có cùng số nhưng khác nghĩa giữa BE/FE/main | Contract bug xác nhận từ source; chưa có ticket status update endpoint để chạy toàn lifecycle. | Đức Anh chốt contract; Anh Nguyen + Nguyễn Thị Thu Hoài triển khai |
| [AUD-07](technical-debt-register.md#aud-07) | P1 | Requester list và Ticket operations API còn thiếu | Integration gap xác nhận bằng route registrations và client calls; chưa gọi hệ thống triển khai thật. | Anh Nguyen (API); Nguyễn Thị Thu Hoài (UI); Đức Anh contract |
| [AUD-08](technical-debt-register.md#aud-08) | P1 | Jenkins trên dev bỏ integration tests và publish TRX bằng junit | Pipeline definition gap xác nhận; không có quyền/log server Jenkins để xác minh runtime publisher. | VÕ NGUYỄN ANH KIỆT; Đức Anh quản trị merge gates |
| [AUD-09](technical-debt-register.md#aud-09) | P1 | main/dev phân kỳ và conflict thật tại TicketStatus | Git divergence và textual conflicts xác nhận bằng rev-list/merge-tree, không thực hiện merge. | Đức Anh; Anh Nguyen + Hương Nguyễn + Nguyễn Thị Thu Hoài giải contract |
| [AUD-10](technical-debt-register.md#aud-10) | P1 | Không có enforced review/checks và staging chưa tồn tại | Remote governance xác nhận từ GitHub API tại thời điểm audit. | Đức Anh cấu hình governance; VÕ NGUYỄN ANH KIỆT xuất checks |
| [AUD-11](technical-debt-register.md#aud-11) | P1 | TC-203 Done trên Jira nhưng chưa integrate và smoke chưa khớp dev | Branch/Jira và contract mismatch xác nhận qua source; Jenkins runtime cần test riêng. | VÕ NGUYỄN ANH KIỆT; Anh Nguyen contract; Đức Anh review |
| [AUD-12](technical-debt-register.md#aud-12) | P1 | Health integration fixture không thay Database config cho health check | Configuration split xác nhận; kết quả health failure cần chạy Docker. TC-203 đã có candidate fix chưa integrated. | VÕ NGUYỄN ANH KIỆT; Anh Nguyen review fixture |
| [AUD-13](technical-debt-register.md#aud-13) | P1 | TicketNumber bị cắt ở sequence vượt 9999 | Bug SQL xác nhận bằng source và semantics PostgreSQL chính thức; chưa chạy boundary query trên PostgreSQL local. | Anh Nguyen; VÕ NGUYỄN ANH KIỆT boundary tests |
| [AUD-14](technical-debt-register.md#aud-14) | P1 | Privacy/internal note và attachment security chưa có E2E enforcement | Missing feature/security gate; không kết luận có internal-note leak đang xảy ra vì endpoint/model chưa tồn tại. | Đức Anh chốt policy; Anh Nguyen backend; Nguyễn Thị Thu Hoài UI; VÕ NGUYỄN ANH KIỆT security tests |
| [AUD-15](technical-debt-register.md#aud-15) | P1 | AI/HITL đã có contract trên main nhưng chưa tích hợp runtime | Partial/Not integrated xác nhận qua từng branch; không gọi là AI hoàn toàn missing. | Hương Nguyễn (AI); Anh Nguyen (API/persistence); Nguyễn Thị Thu Hoài UI; Đức Anh safety |
| [AUD-16](technical-debt-register.md#aud-16) | P2 | Ticket Domain còn model dữ liệu hơn stateful aggregate | Design/contract debt xác nhận; initial Submitted chưa được coi bug vì approved contract chưa integrated. | Đức Anh + Anh Nguyen |
| [AUD-17](technical-debt-register.md#aud-17) | P2 | Knowledge Base và SLA/Analytics chưa có business implementation | Missing trong toàn bộ refs hiện được audit; Jira các contract tương ứng To Do. Không coi planned scope là regression. | Đức Anh contract; Anh Nguyen backend; Hương Nguyễn indexing/RAG; Nguyễn Thị Thu Hoài UI; VÕ NGUYỄN ANH KIỆT QA |
| [AUD-18](technical-debt-register.md#aud-18) | P2 | Docs/process/deployment evidence chưa phù hợp branch hiện tại | Documentation/scaffold debt xác nhận; 9 staged architecture files nằm ngoài committed dev và không được coi approved shared baseline. | Đức Anh; VÕ NGUYỄN ANH KIỆT runbooks |
| [AUD-19](technical-debt-register.md#aud-19) | P2 | Coverage và dependency reproducibility chưa được định lượng | Test/tooling gap xác nhận từ definitions; không gán coverage % không được đo. | VÕ NGUYỄN ANH KIỆT; các module owner duy trì tests |
| [AUD-20](technical-debt-register.md#aud-20) | P2 | Production deployment cần kiểm thử hardening và topology | Cấu hình source xác nhận; runtime risk chưa test vì Docker daemon không hoạt động. | VÕ NGUYỄN ANH KIỆT; Đức Anh security/release review |

## Nguồn và phương pháp

- D được fetch/pull trong worktree sạch, SHA giữ nguyên sau kiểm origin/dev cuối audit. M/A/Q/C/T/F đọc bằng git show/tree/diff không switch checkout.
- GitHub API ngày audit: branches/rulesets/status/check-runs/actions và PR1–4. Remote metadata không có path repository; path README/Jenkins/templates trong AUD-10 là quy trình liên quan, evidence enforcement là API, không suy từ source file.
- Jira live key/status/assignee đầy đủ và module mapping nằm trong repository-audit.md. Jira ID là linkage requirements/fix scope, không phải claim đã tạo bug issue.
- Test kết quả: backend build 0 errors/warnings; 21 unit/architecture pass (4+11+6); frontend build exit2 và lint exit1; 28 existing integration tests fail tại fixture vì DockerUnavailableException; compose config parse pass. Không đo coverage%.
- Probe dùng TicketEndpoints thật, fake ITicketService/ICurrentUser và TestServer; fake delay làm race dễ tái hiện. Không chạm DB thật, không test JWT thực, không chứng minh deployment exploitation. Full probe dưới đây.
- Migration probe dùng provider Npgsql/EF runtime từ restore audit, GenerateScript/HasPendingModelChanges, không mở DB connection. Identity migration absence có SQL + tree evidence; clean DB startup/upgrade cần Docker gate.

External primary references cho semantics, được đọc trực tiếp ngày audit:

- [PostgreSQL 17 string functions — lpad](https://www.postgresql.org/docs/17/functions-string.html): padding giới hạn length có thể cắt chuỗi dài bên phải. AUD-13 kết hợp semantics này với expression source, không dựa comment trong code.
- [Jenkins JUnit Pipeline step](https://www.jenkins.io/doc/pipeline/steps/junit/): publisher nhận JUnit-format XML. AUD-08 đánh giá TRX mismatch; cần mstest hoặc converter và Jenkins runtime test.

## AUD-01

**P0 — Idempotency cache trả ticket của requester khác**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-266, TC-247, TC-421 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:28`
  - `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:36`
  - `tests/ServiceDesk.IntegrationTests/TicketEndpointTests.cs:129`
- Mức độ chắc chắn: Bug xác nhận bằng probe HTTP trên endpoint thật; service/current-user được giả lập, chưa phải E2E PostgreSQL/JWT.
- Evidence: A và B có UserId khác nhau, cùng Idempotency-Key: CROSS_USER_SHARED_KEY_RETURNS_A_TICKET=True, CROSS_USER_CREATE_CALLS=1. Cache lookup trả TicketDto trước khi kiểm tra requester của bản ghi; key không gồm UserId.
- Ảnh hưởng: Người dùng biết hoặc tái sử dụng key có thể nhận ID, title, description của ticket người khác trong cửa sổ cache 10 phút.
- Owner đề xuất: Anh Nguyen (Backend Core); Đức Anh review security; VÕ NGUYỄN ANH KIỆT xác minh.
- Bước xử lý: Scope key theo actor/tenant/operation; lưu requester và payload hash; kiểm tra ownership khi replay; dùng unique constraint và transaction thay cache làm nguồn chân lý.
- Acceptance/test gate: A/B cùng key tuyệt đối không thấy dữ liệu nhau; cùng actor/key/body replay đúng; key/body khác trả conflict theo contract.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-02

**P0 — Seed tài khoản đặc quyền và khóa JWT mặc định không có gate môi trường**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-251, TC-421 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs:56`
  - `src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs:70`
  - `docker-compose.yml:43`
  - `docker-compose.yml:45`
- Mức độ chắc chắn: Cấu hình nguy hiểm xác nhận qua source; khai thác trên deployment thật chưa kiểm thử. P0 áp dụng trước staging/production.
- Evidence: Hosted seeder được đăng ký vô điều kiện, tạo admin/agent với password literal cố định. Compose Production có JWT secret fallback công khai. Override mặc định đặt Development nhưng seeder vẫn không có guard.
- Ảnh hưởng: Deployment dùng mặc định có thể cho phép đăng nhập đặc quyền hoặc giả mạo token; lỗi migration hiện tại không được xem là biện pháp bảo vệ.
- Owner đề xuất: Anh Nguyen + VÕ NGUYỄN ANH KIỆT; Đức Anh duyệt release.
- Bước xử lý: Gate seed test users theo môi trường và opt-in riêng; production fail-fast khi thiếu secret; provisioning admin qua secret riêng; xoay credential/key nếu từng triển khai.
- Acceptance/test gate: Production không tạo test admin/agent và không chấp nhận JWT/default secret; dev fixture được cấu hình tường minh.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-03

**P1 — Idempotency không atomic và không chia sẻ giữa API nodes**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-265, TC-266 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:37`
  - `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:45`
  - `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:50`
  - `docker-compose.yml:38`
  - `deploy/nginx/nginx.conf:1`
- Mức độ chắc chắn: Race cùng process xác nhận bằng probe; hành vi multi-node suy luận từ IMemoryCache và topology, chưa chạy E2E.
- Evidence: 10 POST đồng thời, cùng actor/key/body: CONCURRENT_SAME_KEY_DISTINCT_IDS=10 và CREATE_CALLS=10. Lookup/Create/Set không khóa; api-1/api-2 có cache riêng.
- Ảnh hưởng: Retry/concurrency có thể tạo nhiều ticket và response không nhất quán.
- Owner đề xuất: Anh Nguyen; VÕ NGUYỄN ANH KIỆT test concurrency.
- Bước xử lý: Kết hợp xử lý AUD-01 bằng idempotency record bền vững, unique actor/key, payload hash và transaction; cache chỉ tối ưu.
- Acceptance/test gate: 10 request song song và replay luân phiên hai API node tạo đúng 1 ticket; crash/restart không tạo duplicate.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-04

**P1 — Migration baseline thiếu Identity và model snapshot**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-251, TC-265, TC-280 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs:10`
  - `src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs:32`
  - `tests/ServiceDesk.IntegrationTests/DatabaseTests.cs:29`
- Mức độ chắc chắn: Migration/model gap xác nhận qua generated SQL và EF metadata; lỗi startup thực tế trên PostgreSQL mới chưa chạy vì Docker không hoạt động.
- Evidence: Chỉ có migration 20260918143000_AddTicketTable; generated SQL tạo tickets nhưng không tạo AspNetUsers; HasPendingModelChanges=True; không có ModelSnapshot/Identity migration trong tree. DatabaseTests gọi EnsureCreated dù tên test nói Migrate.
- Ảnh hưởng: DB sạch không có schema Identity để seed/login; kiểm thử EnsureCreated không chứng minh upgrade bằng migrations.
- Owner đề xuất: Anh Nguyen; VÕ NGUYỄN ANH KIỆT clean-DB regression.
- Bước xử lý: Lập baseline migration có Identity + ticket và snapshot hợp lệ; kiểm kê __EFMigrationsHistory/DB hiện có trước quyết định sửa migration cũ; ưu tiên migration bổ sung cho DB đã dùng.
- Acceptance/test gate: Migrate từ DB rỗng và nâng cấp từ baseline hiện có; seed/register/login/create/get đều pass. Không trộn EnsureCreated với Migrate.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-05

**P1 — Frontend không build và lint không đạt**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-271, TC-312, TC-313, TC-314, TC-279 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:10`
  - `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:225`
  - `src/frontend/service-desk-web/src/features/tickets/components/TicketListPage.tsx:2`
  - `src/frontend/service-desk-web/src/features/tickets/components/TicketListPage.tsx:92`
- Mức độ chắc chắn: Bug build xác nhận bằng npm run build/lint trên clean dependencies.
- Evidence: Build exit 2: unused Option, unused Space, Button success prop không tồn tại. Lint exit 1: 6 errors + 1 warning; gồm explicit any ở Agent workspace và TicketList.
- Ảnh hưởng: Không tạo được production frontend/Docker image; PR #4 đã merge mà không có check/review evidence trên GitHub.
- Owner đề xuất: Nguyễn Thị Thu Hoài; VÕ NGUYỄN ANH KIỆT gate.
- Bước xử lý: Sửa unused/imports, Button prop và typed error/DTO; xử lý warning theo lint policy; không vô hiệu hóa compiler/lint chỉ để xanh.
- Acceptance/test gate: npm ci, npm run build, npm run lint pass trên Node 22 theo README và CI; local audit dùng Node 24.13.1.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-06

**P1 — TicketStatus có cùng số nhưng khác nghĩa giữa BE/FE/main**

- Branch: `origin/dev + origin/main`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de; e044f470d38cb9d00a4adca9e59de45abbde3ff5`.
- Jira linkage: TC-260, TC-261, TC-299, TC-274, TC-314 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:5`
  - `src/frontend/service-desk-web/src/features/tickets/types.ts:3`
  - `src/backend/ServiceDesk.Application/Ticketing/Dtos/TicketDto.cs:9`
- Mức độ chắc chắn: Contract bug xác nhận từ source; chưa có ticket status update endpoint để chạy toàn lifecycle.
- Evidence: dev BE InProgress=2, Resolved=4, Cancelled=6; FE/main Triaged=2, InProgress=4, PendingExternal=6. DTO serialize int. Submitted=1 khớp nên create smoke không phát hiện.
- Ảnh hưởng: Ticket đang xử lý có thể hiển thị đã triage; ticket resolved có thể hiện đang xử lý và sai action buttons; đổi enum trực tiếp làm sai dữ liệu đã lưu.
- Owner đề xuất: Đức Anh chốt contract; Anh Nguyen + Nguyễn Thị Thu Hoài triển khai.
- Bước xử lý: Chốt state graph và wire format; map/migrate giá trị persisted có kiểm kê; dùng schema/generation hoặc contract test giữa C#/TS; không giải conflict bằng chọn whole-file ours/theirs.
- Acceptance/test gate: Round-trip tất cả states; dữ liệu cũ ánh xạ đúng; actor/state negative matrix và concurrency tests pass.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-07

**P1 — Requester list và Ticket operations API còn thiếu**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-271, TC-294, TC-299, TC-303, TC-313 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:18`
  - `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:8`
  - `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:48`
  - `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:76`
- Mức độ chắc chắn: Integration gap xác nhận bằng route registrations và client calls; chưa gọi hệ thống triển khai thật.
- Evidence: Backend chỉ POST /api/tickets và GET /api/tickets/{id:guid}; không có /me, /triage, /assign, /start, /request-information, /resolve, /comments. Assign FE dùng assignedAgentId='me' chưa có contract server.
- Ảnh hưởng: Requester list và command workspace không hoạt động E2E. Timeline là mock, attachment button không handler, request-info chỉ message.
- Owner đề xuất: Anh Nguyen (API); Nguyễn Thị Thu Hoài (UI); Đức Anh contract.
- Bước xử lý: Ưu tiên list/detail visibility, rồi explicit command contracts với permission + state preconditions + audit + concurrency; đồng bộ UI, pagination, errors và self-assign identity server-side.
- Acceptance/test gate: API contract matrix bên dưới pass với requester/agent/admin; negative ownership và transitions; queue thật, timeline thật.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-08

**P1 — Jenkins trên dev bỏ integration tests và publish TRX bằng junit**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-203, TC-279, TC-282 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `Jenkinsfile:38`
  - `Jenkinsfile:43`
  - `.github/pull_request_template.md:1`
- Mức độ chắc chắn: Pipeline definition gap xác nhận; không có quyền/log server Jenkins để xác minh runtime publisher.
- Evidence: Filter Category!=Integration loại toàn bộ tests có trait Integration. Không có stage integration/FE lint/smoke. junit nhận *.trx; tài liệu Jenkins yêu cầu JUnit XML. .sln và .slnx đều tồn tại, restore .sln thực tế pass.
- Ảnh hưởng: Build xanh hiện tại không chứng minh DB/auth E2E; reporting có thể không đọc được TRX.
- Owner đề xuất: VÕ NGUYỄN ANH KIỆT; Đức Anh quản trị merge gates.
- Bước xử lý: Tích hợp TC-203 có điều chỉnh; dùng mstest plugin hoặc convert TRX sang JUnit XML; chạy integration + lint + isolated smoke; publish commit status cho đúng SHA.
- Acceptance/test gate: Pipeline fail khi từng gate fail; báo cáo test parse được và checks xuất hiện trên SHA PR. Chạy trên Jenkins Linux agent có Docker.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-09

**P1 — main/dev phân kỳ và conflict thật tại TicketStatus**

- Branch: `origin/dev + origin/main`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de; e044f470d38cb9d00a4adca9e59de45abbde3ff5`.
- Jira linkage: TC-242, TC-442 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:3`
  - `README.md:326`
  - `Jenkinsfile:2`
- Mức độ chắc chắn: Git divergence và textual conflicts xác nhận bằng rev-list/merge-tree, không thực hiện merge.
- Evidence: main...dev left/right=2/25, merge-base eceaaa28c9ec377353ae354d92f33b0f1fd40dd1; merge-tree dev/main exit 1 add/add TicketStatus.
- Ảnh hưởng: dev thiếu AI contract đã trên main; release không thể fast-forward ngay và giải conflict sai sẽ phá enum/data.
- Owner đề xuất: Đức Anh; Anh Nguyen + Hương Nguyễn + Nguyễn Thị Thu Hoài giải contract.
- Bước xử lý: Dùng nhánh tích hợp riêng từ dev, merge main có review contract/data mapping, PR về dev bằng merge commit; tiếp tục staging/main theo gates.
- Acceptance/test gate: Giữ cả lịch sử main/dev; ancestor checks và contract tests; không cherry-pick, rebase nhánh shared, force-push.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-10

**P1 — Không có enforced review/checks và staging chưa tồn tại**

- Branch: `origin/dev + origin/main`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de; e044f470d38cb9d00a4adca9e59de45abbde3ff5`.
- Jira linkage: TC-203, TC-278, TC-282, TC-242 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `README.md:326`
  - `Jenkinsfile:65`
  - `.github/pull_request_template.md:1`
- Mức độ chắc chắn: Remote governance xác nhận từ GitHub API tại thời điểm audit.
- Evidence: 7 remote branches đều protected=false; rulesets=[]; dev status total_count=0, check_runs=0; Actions runs=0. PR #1–#4 reviews=[] và statusCheckRollup=[]. Không local/remote staging, không tags.
- Ảnh hưởng: Quy trình README chưa enforced, có thể merge code không build; thiếu evidence release/prod.
- Owner đề xuất: Đức Anh cấu hình governance; VÕ NGUYỄN ANH KIỆT xuất checks.
- Bước xử lý: Sau CI chạy được, thiết lập PR + reviewer độc lập + required checks đúng tên/SHA; chặn force-push/xóa nhánh protected; tạo staging chỉ sau candidate đạt gates.
- Acceptance/test gate: PR thiếu review/checks không merge được; staging deployment có artifacts và release record. Không thay đổi settings trong audit.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-11

**P1 — TC-203 Done trên Jira nhưng chưa integrate và smoke chưa khớp dev**

- Branch: `origin/feature/tc-203-quality-gate-and-integration-environment`.
- Commit SHA: `e834e74a50ecc4986ca329bc2a217ffc14af8cf4`.
- Jira linkage: TC-203, TC-278, TC-279, TC-280, TC-281 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `Jenkinsfile:1`
  - `deploy/smoke/smoke-test.sh:1`
  - `deploy/jenkins/casc.yaml:1`
  - `.github/workflows/ci.yml:1`
  - `tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:1`
- Mức độ chắc chắn: Branch/Jira và contract mismatch xác nhận qua source; Jenkins runtime cần test riêng.
- Evidence: Branch có 4 commits ngoài dev, gồm 2 main/AI ancestors và 2 TC-203 commits. Có integration stages, MSTest, CI workflow, health DB config fix; smoke shell gọi /api/v1/auth/login và /api/v1/tickets, payload không khớp dev, list không bearer. Jenkins chỉ docker compose build rồi curl localhost không dựng stack, casc tham chiếu branch chore cũ và main; conflict thêm package.json/lock.
- Ảnh hưởng: Merge mù không cung cấp quality gate lặp lại được, có thể kiểm tra stack khác đang chạy.
- Owner đề xuất: VÕ NGUYỄN ANH KIỆT; Anh Nguyen contract; Đức Anh review.
- Bước xử lý: Integrate sau main/AI reconciliation; sửa smoke prefix/payload/auth, job branch, khởi tạo stack isolated + readiness + teardown giữ volumes theo policy; không copy whole tree feature lên dev.
- Acceptance/test gate: Fresh environment auth/create/list được xác minh; status published; reviewer thấy 2 unique TC-203 commits sau main integration.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-12

**P1 — Health integration fixture không thay Database config cho health check**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-255, TC-280 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:25`
  - `src/backend/ServiceDesk.Api/Program.cs:58`
  - `tests/ServiceDesk.IntegrationTests/HealthCheckTests.cs:22`
- Mức độ chắc chắn: Configuration split xác nhận; kết quả health failure cần chạy Docker. TC-203 đã có candidate fix chưa integrated.
- Evidence: Fixture thay DbContextOptions sang container nhưng không override Database:ConnectionString; Program tạo NpgSql health check từ config riêng. Feature TC-203 thêm ConfigureAppConfiguration.
- Ảnh hưởng: /health có thể kiểm tra DB dev khác thay vì test DB; pass/fail phụ thuộc máy, không lặp lại.
- Owner đề xuất: VÕ NGUYỄN ANH KIỆT; Anh Nguyen review fixture.
- Bước xử lý: Đưa config override lên trước service registration; loại options/config delegates cũ nếu cần; test host không dùng dịch vụ ngoài fixture.
- Acceptance/test gate: Health test chỉ kết nối Testcontainer dù DB dev tắt; Docker clean environment pass.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-13

**P1 — TicketNumber bị cắt ở sequence vượt 9999**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-265 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:74`
  - `src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs:26`
  - `tests/ServiceDesk.IntegrationTests/TicketEndpointTests.cs:43`
- Mức độ chắc chắn: Bug SQL xác nhận bằng source và semantics PostgreSQL chính thức; chưa chạy boundary query trên PostgreSQL local.
- Evidence: LPAD(nextval(...)::TEXT,4,'0') cắt bên phải khi chuỗi dài hơn 4. 10000 thành 1000, trái với comment; nếu đã có cùng năm/suffix 1000 thì unique index sẽ từ chối. Regex test chỉ chấp nhận đúng 4 chữ số.
- Ảnh hưởng: Có thể ngừng tạo ticket hoặc mất tính đơn ánh mã khi vượt ngưỡng; tăng varchar(30) không sửa LPAD.
- Owner đề xuất: Anh Nguyen; VÕ NGUYỄN ANH KIỆT boundary tests.
- Bước xử lý: Padding với length=max(4,length(sequence-text)) hoặc formatter giữ đầy đủ sequence; dùng nextval một lần; migration bổ sung cập nhật default SQL, không reset sequence.
- Acceptance/test gate: Test 9999/10000/10001, rollover năm và existing values; số không bị truncate và không duplicate.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-14

**P1 — Privacy/internal note và attachment security chưa có E2E enforcement**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-303, TC-308, TC-313 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:24`
  - `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:174`
  - `src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:195`
  - `src/backend/ServiceDesk.Infrastructure/Storage/MinioObjectStorageService.cs:23`
- Mức độ chắc chắn: Missing feature/security gate; không kết luận có internal-note leak đang xảy ra vì endpoint/model chưa tồn tại.
- Evidence: UI isInternal chỉ là DTO/client flag, timeline hard-coded; chưa có comment/note persistence/filter. Storage adapter presign có triển khai nhưng chưa có ticket attachment endpoint/ACL/MIME/size policy.
- Ảnh hưởng: Không thể nghiệm thu TC-313 toàn bộ; triển khai tương lai có nguy cơ lộ note/file nếu chỉ dựa UI.
- Owner đề xuất: Đức Anh chốt policy; Anh Nguyen backend; Nguyễn Thị Thu Hoài UI; VÕ NGUYỄN ANH KIỆT security tests.
- Bước xử lý: Server-side requester-safe projection, policy checks từng action/object, audit author/time; attachment metadata/ACL/limits, private bucket, presign TTL và MIME thực.
- Acceptance/test gate: Requester own/other tickets không thấy internal note qua API/timeline/AI/log/export; upload/download không vượt giới hạn hoặc ACL.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-15

**P1 — AI/HITL đã có contract trên main nhưng chưa tích hợp runtime**

- Branch: `origin/main + origin/feature/tc-275-276-277-ai-triage-contract; origin/dev`.
- Commit SHA: `e044f470d38cb9d00a4adca9e59de45abbde3ff5; b529b2cdd138b273ad5f89efb18ccb9f5a399313; 6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-275, TC-276, TC-277, TC-455 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Application/AIAssistance/Models/TriageResult.cs:1`
  - `src/backend/ServiceDesk.Application/AIAssistance/Models/TriageViewProjections.cs:1`
  - `src/backend/ServiceDesk.Domain/AIAssistance/AISuggestion.cs:1`
  - `src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs:81`
  - `src/frontend/service-desk-web/src/features/ai-assistance/api.ts:1`
- Mức độ chắc chắn: Partial/Not integrated xác nhận qua từng branch; không gọi là AI hoàn toàn missing.
- Evidence: main/A có typed triage DTO, confidence bands, suggestion decisions, input hash/revision, requester projections và unit tests. dev dùng Empty/NoOp, không API/provider/persistence AI. main client ghép base '/api' với path '/api/ai/...' tạo nguy cơ /api/api/ai khi tích hợp; components chưa mounted AppRoutes.
- Ảnh hưởng: Jira contract Done không chứng minh luồng AI hay HITL end-to-end; revision main chưa nối với Ticket/xmin dev và DTO hiện tại.
- Owner đề xuất: Hương Nguyễn (AI); Anh Nguyen (API/persistence); Nguyễn Thị Thu Hoài UI; Đức Anh safety.
- Bước xử lý: Merge histories sau chốt enum; implement adapters/API authorization/persistence/revision-stale checks; chuẩn hóa client paths; dùng NoOp thật có controlled unavailable fallback.
- Acceptance/test gate: Provider unavailable không chặn workflow; SEC manual review; stale/terminal decisions, actor projection và forbidden AI actions được test server-side.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-16

**P2 — Ticket Domain còn model dữ liệu hơn stateful aggregate**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-199, TC-260, TC-261, TC-299 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs:3`
  - `src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs:29`
  - `src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs:48`
  - `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:48`
- Mức độ chắc chắn: Design/contract debt xác nhận; initial Submitted chưa được coi bug vì approved contract chưa integrated.
- Evidence: Entity public setters, chưa assignee/resolution/events/transitions; department snapshot luôn null; Title service/UI100 so với DB200; trực tiếp DateTime.UtcNow dù có IClock.
- Ảnh hưởng: Mở rộng lifecycle dễ phân tán invariants, snapshot/clock semantics và concurrency chưa đóng contract.
- Owner đề xuất: Đức Anh + Anh Nguyen.
- Bước xử lý: Chốt Draft/Submitted và field/nullability/limits; encapsulate transitions; inject IClock; đưa revision/concurrency token vào command/DTO khi có update.
- Acceptance/test gate: Invariant và actor/state matrix có tests; payload null/boundary không thành 500; schema/API/UI nhất quán.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-17

**P2 — Knowledge Base và SLA/Analytics chưa có business implementation**

- Branch: `origin/dev + origin/main + origin/feature/tc-203-quality-gate-and-integration-environment`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de; e044f470d38cb9d00a4adca9e59de45abbde3ff5; e834e74a50ecc4986ca329bc2a217ffc14af8cf4`.
- Jira linkage: TC-216, TC-329, TC-330, TC-230, TC-387, TC-388, TC-389 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `README.md:60`
  - `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:22`
  - `src/backend/ServiceDesk.Infrastructure/AI/NoOpEmbeddingService.cs:9`
  - `src/backend/ServiceDesk.Worker/Program.cs:4`
- Mức độ chắc chắn: Missing trong toàn bộ refs hiện được audit; Jira các contract tương ứng To Do. Không coi planned scope là regression.
- Evidence: Không article entity/lifecycle/API/indexing/ACL, không SLA clock/targets/KPI endpoints/migrations/tests. pgvector image và empty embeddings chỉ nền tảng. Worker chỉ tạo host + Infrastructure.
- Ảnh hưởng: Không thể demo/ nghiệm thu RAG, Knowledge lifecycle hoặc SLA/KPI; roadmap chưa được triển khai.
- Owner đề xuất: Đức Anh contract; Anh Nguyen backend; Hương Nguyễn indexing/RAG; Nguyễn Thị Thu Hoài UI; VÕ NGUYỄN ANH KIỆT QA.
- Bước xử lý: Chốt article permissions/versioning và SLA clock/glossary trước implementation; chia feature PRs theo dependency; bổ sung worker jobs, indexes, clock tests.
- Acceptance/test gate: Knowledge draft/published visibility; SLA pause/reopen/UTC boundaries; KPI numerator/denominator và freshness có tests.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-18

**P2 — Docs/process/deployment evidence chưa phù hợp branch hiện tại**

- Branch: `origin/dev + origin/main + origin/feature/tc-203-quality-gate-and-integration-environment`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de; e044f470d38cb9d00a4adca9e59de45abbde3ff5; e834e74a50ecc4986ca329bc2a217ffc14af8cf4`.
- Jira linkage: TC-242, TC-442, TC-443, TC-444 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `README.md:188`
  - `docs/architecture/.gitkeep`
  - `docs/runbooks/.gitkeep`
  - `deploy/scripts/.gitkeep`
  - `.github/ISSUE_TEMPLATE/bug_report.md:1`
- Mức độ chắc chắn: Documentation/scaffold debt xác nhận; 9 staged architecture files nằm ngoài committed dev và không được coi approved shared baseline.
- Evidence: 10 ADR tồn tại; dev architecture/runbooks chỉ .gitkeep; runbook/process trên TC-203. README clone Duc-AnhTp thay supportflow-ai và mô tả production-grade chưa có release evidence. Không deploy scripts thực tế, CODEOWNERS hay tags.
- Ảnh hưởng: Thành viên có thể dùng nhầm repo/baseline; approval labels trong tài liệu local không chứng minh review/Jira nghiệm thu.
- Owner đề xuất: Đức Anh; VÕ NGUYỄN ANH KIỆT runbooks.
- Bước xử lý: Review độc lập 9 tài liệu trong PR khác khi chủ sở hữu sẵn sàng; đối chiếu Jira/contract, cập nhật paths/runbook/demo/release criteria theo SHA.
- Acceptance/test gate: Docs có reviewer và evidence; demo data/runbook lặp lại; không đưa 9 staged files vào audit branch.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-19

**P2 — Coverage và dependency reproducibility chưa được định lượng**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-203, TC-279, TC-282 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `src/frontend/service-desk-web/package.json:6`
  - `src/backend/ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj:23`
  - `tests/ServiceDesk.Application.Tests/ServiceDesk.Application.Tests.csproj:18`
  - `tests/ServiceDesk.ArchitectureTests/CleanArchitectureTests.cs:68`
- Mức độ chắc chắn: Test/tooling gap xác nhận từ definitions; không gán coverage % không được đo.
- Evidence: 21 unit/architecture tests pass, không collect coverage, không FE test/E2E scripts. Backend package Version 10.*, test 2.*/8.* floating; no NuGet lock/global.json. Architecture tests kiểm assembly Application, không nhận biết TicketService đặt namespace Application trong assembly Infrastructure.
- Ảnh hưởng: Pass tests hiện tại không chứng minh privacy/lifecycle/AI/SLA hay DB-generated numbering; restore khác ngày có thể đổi package.
- Owner đề xuất: VÕ NGUYỄN ANH KIỆT; các module owner duy trì tests.
- Bước xử lý: Pin SDK/package và lock restore khi có chính sách; đo coverage critical flows; ưu tiên meaningful negative/integration tests và frontend contract/E2E trước target %.
- Acceptance/test gate: Build reproducible; coverage artifact chính xác; required scenarios có assertions, không dùng coverage % làm bằng chứng duy nhất.
- Trạng thái: Open; chưa sửa trong audit.

## AUD-20

**P2 — Production deployment cần kiểm thử hardening và topology**

- Branch: `origin/dev`.
- Commit SHA: `6b124e73e9cb95350a505a24a548cb663ab1b7de`.
- Jira linkage: TC-280, TC-414, TC-421, TC-442 (đối chiếu requirement/implementation, không tạo issue).
- Source paths:
  - `docker-compose.override.yml:17`
  - `deploy/nginx/nginx.conf:7`
  - `src/backend/ServiceDesk.Api/Program.cs:112`
  - `docker-compose.yml:121`
- Mức độ chắc chắn: Cấu hình source xác nhận; runtime risk chưa test vì Docker daemon không hoạt động.
- Evidence: Default compose tự tải override Development và expose ports; NGINX HTTP80, trust forwarded headers không giới hạn proxy trong Program; MinIO/latest và nginx/alpine mutable; readiness frontend/API chỉ service_started. Không có runtime deployment/backup evidence.
- Ảnh hưởng: Staging không nhất thiết giống production; TLS/proxy trust/secret/data backup chưa bảo đảm.
- Owner đề xuất: VÕ NGUYỄN ANH KIỆT; Đức Anh security/release review.
- Bước xử lý: Staging/prod dùng compose/config tường minh không dev override; TLS/known proxies/ports, pinned image digests, readiness, backup/restore drills và secret injection.
- Acceptance/test gate: Clean topology health/smoke hai API nodes; restore DB/objects thử nghiệm; ingress/trust policy và production env được review.
- Trạng thái: Open; chưa sửa trong audit.


## Thứ tự xử lý và accountability

| Nhóm | Thứ tự | Owner chính | Điều kiện đóng |
|---|---|---|---|
| Security | AUD-01/02 trước mọi deployment có người dùng thật | Anh Nguyen + Kiệt, Đức Anh review | Cross-user isolation; production không seed test privileged users hay dùng secret mặc định |
| Reliability | AUD-03/04/05/12/13 | Anh Nguyen / Thu Hoài / Kiệt | Retry/concurrency, clean migration + upgrade, sequence và build gates pass |
| Contracts/history | AUD-06/09/15 | Đức Anh + Backend/AI/FE | Canonical wire/schema/state; main history integrated dev bằng reviewed merge |
| CI/governance | AUD-08/10/11 | Kiệt + Đức Anh | TC-203 sửa và integrated; statuses/reviewer/rules enforced, isolated stack smoke |
| Ticket operations/privacy | AUD-07/14/16 | Anh Nguyen + Thu Hoài, Đức Anh policy | Missing endpoints/permissions/projections/state audit/attachment security có E2E |
| Planned scope/release | AUD-17/18/19/20 | Module owners + Đức Anh | KB/SLA in-scope có tests; docs/runbooks/dependency/production/UAT evidence |

Không “đóng” issue chỉ nhờ merge commit hoặc Done Jira. Evidence phải gắn candidate SHA, environment/toolchain, test result và reviewer. Rollback/merge dependencies xem branch-integration-plan.md. P0/P1 in-scope chưa đóng thì không promote staging/main.

## Appendix A — Reproduce endpoint/migration probes

Probe source là audit-only, không thuộc business source. Scratch ở `.cache/probe/` trong worktree, ignored và không commit. Để chạy lại, đặt source dưới đây làm `.cache/probe/Program.cs`; tạo executable net10.0 csproj tham chiếu `../../src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj` và `../../tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj`, bật implicit usings + nullable. Restore packages vào `.cache/packages`, rồi:

```powershell
dotnet restore .cache/probe/probe.csproj --packages .cache/packages
dotnet run --project .cache/probe/probe.csproj -c Release --no-restore
```

Kết quả audit (ngoài request logs TestServer):

```text
MIGRATIONS=20260918143000_AddTicketTable
MODEL_HAS_PENDING_CHANGES=True
MIGRATION_CREATES_IDENTITY_TABLES=False
MIGRATION_CREATES_XMIN_USER_COLUMN=False
MIGRATION_CREATES_TICKET_TABLE=True
CROSS_USER_SHARED_KEY_RETURNS_A_TICKET=True
CROSS_USER_CREATE_CALLS=1
CONCURRENT_SAME_KEY_DISTINCT_IDS=10
CONCURRENT_SAME_KEY_CREATE_CALLS=10
```

`MIGRATION_CREATES_XMIN_USER_COLUMN` chỉ là string probe phụ, không đủ để kết luận lỗi system-column SQL; register không ghi lỗi xmin giả định. Collision test dùng shared key do auditor chủ động lựa chọn, không yêu cầu đoán key trong production. Concurrent fake service không có thêm unique idempotency constraint, khớp source hiện tại vì TicketService/persistence không lưu key.

```csharp
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Endpoints;
using ServiceDesk.Application.Common.Interfaces;
using ServiceDesk.Application.Ticketing.Commands;
using ServiceDesk.Application.Ticketing.Dtos;
using ServiceDesk.Application.Ticketing.Interfaces;
using ServiceDesk.Domain.Identity;
using ServiceDesk.Infrastructure.Persistence;
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=audit;Username=audit;Password=audit").Options);
Console.WriteLine("MIGRATIONS="+string.Join(",",db.Database.GetMigrations()));
Console.WriteLine("MODEL_HAS_PENDING_CHANGES="+db.GetService<IMigrator>().HasPendingModelChanges());
var sql=db.GetService<IMigrator>().GenerateScript();
Console.WriteLine("MIGRATION_CREATES_IDENTITY_TABLES="+sql.Contains("CREATE TABLE \"AspNetUsers\""));
Console.WriteLine("MIGRATION_CREATES_XMIN_USER_COLUMN="+sql.Contains("xmin xid"));
Console.WriteLine("MIGRATION_CREATES_TICKET_TABLE="+sql.Contains("CREATE TABLE public.tickets"));
var builder=WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser,ProbeCurrentUser>();
var service=new ProbeTicketService();
builder.Services.AddSingleton<ITicketService>(service);
builder.Services.AddAuthorization(o=>o.AddPolicy(PolicyNames.RequireRequester,p=>p.RequireRole(RoleNames.Requester)));
await using var app=builder.Build();
app.Use(async(ctx,next)=>{ctx.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,ctx.Request.Headers["X-Audit-User"].ToString()),new Claim(ClaimTypes.Role,RoleNames.Requester)},"probe"));await next();});
app.UseAuthorization();
app.MapTicketEndpoints();
await app.StartAsync();
using var client=app.GetTestClient();
async Task<TicketDto> Create(string user,string key,string title){
using var req=new HttpRequestMessage(HttpMethod.Post,"/api/tickets/");
req.Headers.Add("X-Audit-User",user);req.Headers.Add("Idempotency-Key",key);
req.Content=JsonContent.Create(new CreateTicketCommand(title,"Valid audit description","NET"));
using var res=await client.SendAsync(req);res.EnsureSuccessStatusCode();return(await res.Content.ReadFromJsonAsync<TicketDto>())!;
}
var a=Guid.NewGuid().ToString();var b=Guid.NewGuid().ToString();
var first=await Create(a,"shared-key","Private A ticket");var second=await Create(b,"shared-key","B ticket");
Console.WriteLine("CROSS_USER_SHARED_KEY_RETURNS_A_TICKET="+(first.Id==second.Id&&second.Title==first.Title));
Console.WriteLine("CROSS_USER_CREATE_CALLS="+service.Count);
var before=service.Count;var duplicates=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>Create(a,"parallel-key","Same payload")));
Console.WriteLine("CONCURRENT_SAME_KEY_DISTINCT_IDS="+duplicates.Select(t=>t.Id).Distinct().Count());
Console.WriteLine("CONCURRENT_SAME_KEY_CREATE_CALLS="+(service.Count-before));
await app.StopAsync();
sealed class ProbeCurrentUser(IHttpContextAccessor accessor):ICurrentUser{
public string? UserId=>accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
public bool IsAuthenticated=>true;public IReadOnlyList<string> Roles=>new[]{RoleNames.Requester};public bool IsInRole(string role)=>role==RoleNames.Requester;}
sealed class ProbeTicketService:ITicketService{
private int count;public int Count=>count;
public async Task<TicketDto> CreateTicketAsync(CreateTicketCommand c,Guid r,CancellationToken ct=default){Interlocked.Increment(ref count);await Task.Delay(100,ct);return new TicketDto(Guid.NewGuid(),"IT-2026-0001",c.Title,c.Description,c.CategoryId,1,DateTime.UtcNow);}
public Task<TicketDto?> GetTicketByIdAsync(Guid id,Guid r,CancellationToken ct=default)=>Task.FromResult<TicketDto?>(null);}
```

## Appendix B — Các giới hạn chưa giải quyết

- Không có Docker daemon nên PostgreSQL 17 real migrations/numbering/xmin, MinIO ACL runtime, NGINX/two-node idempotency, Docker image build và full browser E2E chưa kiểm được. 28 integration failures là environment failures, không 28 product bugs.
- Không có Jenkins build server/log để xác nhận pipeline đang chạy hay plugin thực tế; không nhầm GitHub statuses trống thành lỗi Jenkins đã tái hiện.
- Frontend audit dùng Node24.13.1/npm11.8.0; phải repeat required gates trên Node22 CI baseline khi sửa code. NuGet version ranges floating cần pin để reproducible.
- Chưa chạy unit tests riêng tại M/Q bằng worktree khác; đã đọc code/tests và mô phỏng merge. Không dùng tests có mặt để claim pass runtime.
- 9 staged architecture documents được bảo toàn, chưa review/integrate và không coi là approved shared contracts. Không sửa/push/commit chúng.
- Báo cáo là snapshot ngày audit: nếu refs/Jira/settings đổi, recompute evidence/counts/merge simulations trước integration.
