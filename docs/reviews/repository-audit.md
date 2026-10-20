# Hồ sơ lịch sử: Rà soát kho mã và tích hợp

> **Tài liệu lịch sử.** Các phát hiện, mức độ và số dòng thuộc mốc rà soát ngày 2026-10-09; việc còn tồn tại cần đối chiếu lại mã mới.
> Chú thích tiếng Việt ngày 2026-10-20; phần bản gốc bên dưới giữ nguyên. Nguồn đang áp dụng: [hướng dẫn hiện hành](../README.md).

<!-- original-record -->
# Repository, Git Branch & Integration Audit

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

## Kết luận và phạm vi

**Chưa đủ điều kiện promote staging/main.** Có code Auth/JWT, Ticket create/detail và frontend trên dev; không được đánh giá chúng là absent dựa vào main. main có AI contract/HITL mà dev chưa chứa. Hai P0 là idempotency cross-user replay (đã tái hiện endpoint) và production seed/default credentials (source xác nhận, deployment exploitation chưa test). 13 P1 và 5 P2 có evidence/owner/action trong [technical-debt-register.md](technical-debt-register.md).

Audit đọc tất cả 7 remote branches cùng 4 local refs; tree/diff của các ancestor C/T/F chứng minh không có unique commits bỏ sót so với D. Chi tiết primary review trên D, AI changes M/A và CI changes Q; không checkout/switch các nhánh khác. Read-only GitHub/Jira APIs được dùng để đối chiếu hiện trạng, không dựa các số liệu cũ trong attachments. Không sửa business code, không merge/cherry-pick/rebase/push, không tạo PR/issues hay thay đổi Jira/settings.

## 1. Bảo toàn checkout gốc và baseline

Checkout gốc: `D:/Users/Documents/HUCE/HK1_2026-2027/8. ThapCam/Code/ai-it-service-desk`, branch `frontend`, HEAD `6b124e73e9cb95350a505a24a548cb663ab1b7de`. Initial status: 9 entries `A `; diff unstaged rỗng và không untracked. Snapshot gồm toàn bộ index entries (mode/blob/path), status, branch/HEAD, unstaged/untracked và SHA-256 9 files. Fetch cập nhật remote refs/FETCH_HEAD, add worktree thêm ref/worktree metadata — đây là thay đổi Git metadata được yêu cầu, không đổi index gốc.

Các worktree có trước: checkout gốc frontend, `C:/Users/ducan/orca/workspaces/ai-it-service-desk/dev` tại B và `.../main` tại B. Không dùng hay sửa hai checkout Orca. Audit worktree mới tạo thành công; pull `Already up to date`, branch audit, status clean, HEAD D. Không có AGENTS.md áp dụng ở audit tree hoặc các thư mục cha được kiểm tra.

| File đã stage ở checkout gốc | SHA-256 ban đầu (để đối chiếu cuối audit) |
|---|---|
| docs/architecture/01-rbac-and-api-permission-matrix.md | 74DD477256FB1C1915E2B13DB9AA5948619D574F741869B547B0CA93DE04E12B |
| docs/architecture/02-ticket-domain-and-field-contract.md | F48BD063E80BC9C2F79A81DF98D37312CFEB4787430FF7F05C8078CA3C47CCA8 |
| docs/architecture/03-ticket-lifecycle-and-visibility-baseline.md | B1DB9E026124335F1D9C570FB94D104D75B41193FA59741A5A680C2620D5BE1A |
| docs/architecture/04-ticket-state-transition-and-reopen-matrix.md | 8640042DB4C237DE6453223AF240462A457BCB18A6AA5B66B84C71F76F3E5C29 |
| docs/architecture/05-comment-and-internal-note-privacy-contract.md | B144C2DDEDFCEDDE76BA9942DF59825389C70F97BC7324A48F0A4A00C92CABB4 |
| docs/architecture/06-ticket-assignment-policy.md | E5201957D4E3E334DEC6E8FB662E55AF7F476A7B7CE823C28E3E7AE4637508C8 |
| docs/architecture/07-attachment-security-limits-and-policy.md | AFD7484EF85EECC0BED9B9820C1B9F913FE6B018ACE6844BE64EB7B3CFB812EA |
| docs/architecture/08-sprint-acceptance-and-governance-record.md | FDAA2CF34444ABA76DA9A4A4AC9FC086BD004E37CFDCBA86DE5054D90919252C |
| docs/architecture/README.md | AC76A3BDAB5C1E6E686A963B4B3BA0BCF24DF9744C12117D62AC087C11051EA0 |

**Xác minh cuối bằng snapshot độc lập: **PASS** — frontend/HEAD giữ nguyên; toàn bộ index entries (mode/blob/path), status 9 staged A, unstaged diff rỗng, untracked rỗng, SHA-256 của cả 9 files đều bằng ban đầu. origin/dev được kiểm lại bằng git ls-remote vẫn D. Không có tracked business-code/index changes trong audit trước tạo reports.**

## 2. Branch inventory

| Ref | Tip SHA label | Commit cuối | Vai trò / tình trạng |
|---|---|---|---|
| origin/dev | D | 28/09/2026, PR #4 | Auth + ticket create/detail + frontend tích hợp |
| origin/main; origin/HEAD | M | 17/09/2026, PR #1 | Scaffold + AI contracts, chưa chứa Auth/Ticket implementation dev; origin/HEAD là symbolic alias |
| origin/frontend | F | 27/09/2026, TC-312–314 | UI Auth/Requester/Agent; đã là ancestor D |
| origin/feature/core-ticket-lifecycle | C | 16/09/2026, fix role registration | Auth/JWT; đã merge #2 vào dev |
| origin/feature/tc-200-create-ticket | T | 18/09/2026, code-review feedback | Create/detail API/persistence/tests; đã merge #3 |
| origin/feature/tc-275-276-277-ai-triage-contract | A | 14/09/2026, AI contract | Đã merge main #1, chưa có ở dev |
| origin/feature/tc-203-quality-gate-and-integration-environment | Q | 17/09/2026, TC-203 | CI/smoke/process; ancestor main + 2 unique commits, chưa vào dev |
| local frontend (checkout gốc) | D | 28/09/2026, PR #4 | Tracking origin/frontend; 15 ahead/0 behind sau fetch; 9 staged docs |
| local dev (worktree Orca khác) | B | 31/08/2026, README | Tracking origin/dev; 0 ahead/25 behind; không chỉnh checkout này |
| local main (worktree Orca khác) | B | 31/08/2026, README | Tracking origin/main; 0 ahead/2 behind; không chỉnh checkout này |
| local audit/repository-audit-20261009 | D | baseline PR #4 | Worktree mới, tracking origin/dev; chỉ 3 reports mới |

Remote có **7 nhánh thực**; local có **4 nhánh** sau tạo audit. Không có staging local/remote. `git tag --list` rỗng. Tip/commit dates là từ Git, không dùng ngày merge như ngày authored commit. Quyền branch remote được đọc trực tiếp, không suy ra từ refs local.

### Ahead/behind, merge-base

| Compared with origin/dev | Branch-only (ahead dev) | Dev-only (behind dev) | Merge-base SHA label | Kết luận |
|---|---:|---:|---|---|
| origin/main | 2 | 25 | B | Diverged; AI thiếu ở dev |
| origin/frontend | 0 | 15 | F | Fully integrated ancestor |
| origin/feature/core-ticket-lifecycle | 0 | 22 | C | Fully integrated ancestor |
| origin/feature/tc-200-create-ticket | 0 | 12 | T | Fully integrated ancestor |
| origin/feature/tc-275-276-277-ai-triage-contract | 1 | 25 | B | Unintegrated into dev; integrated main |
| origin/feature/tc-203-quality-gate-and-integration-environment | 4 | 25 | B | Contains main's 2 + 2 unique TC-203 commits |

`git rev-list --left-right --count origin/main...origin/dev` trả **2 25**, tính lại trong audit này. Những con số giống audit cũ là kết quả xác minh mới. `git merge-base` trả B. `git log origin/main..Q` chỉ có 9010486 và e834e74; Q chứa M, vì thế không merge lặp A riêng và main riêng.

### Các commit chỉ có ở dev (25)

| SHA | Commit subject |
|---|---|
| 6b124e73e9cb95350a505a24a548cb663ab1b7de | Merge pull request #4 from supportflow-ai/frontend |
| 106909cca3ed09e4687dda667a213c6d60841a8f | feat(agent): Hoàn thiện Agent Ticket Workspace layout và Command Actions (TC-312, TC-313, TC-314) |
| cca272a99970e898512ea456e3ff52d92f1fbb09 | fix(frontend): Cập nhật menu điều hướng, fix lỗi 401 interceptor và đồng bộ enum TicketStatus (TC-198, TC-259) |
| 3c84ed20258f824bfa695e9dd8accd946572c7af | Merge pull request #3 from supportflow-ai/feature/tc-200-create-ticket |
| de7525855794517d198c51a71cba325bde7e997c | feat(ticket): Cải thiện UX xử lý Loading, Empty và Error State (TC-272). Xây dựng giao diện xem chi tiết Ticket (TC-274). fix(ticket): Thêm sự kiện chuyển hướng cho nút Hủy quay về trang danh sách (TC-271) |
| 01dc4d7c12cc4fe2eecf41594f89915789c9ff95 | feat(ticket): Kết nối API submit form và xử lý chuyển trang (TC-268). Xây dựng giao diện Danh sách Ticket của Requester (TC-271) |
| 9df98c27a5d50b269ce5ba71dacd1e0839f7c46d | fix(backend): address code review feedback for TC-200 (blockers 1 & 2, majors 4 & 5, minor 6) |
| 5afd2a60822404c074d2eceb511eb7c76ade8482 | fix(infra): add DbContext and Migration metadata attributes to AddTicketTable |
| 8f438f6b499cfca470513620b4b08f0d2d044327 | feat(api): add POST /api/tickets endpoint and integration tests (TC-266) |
| 31461a5b9dd16b5ccb64a18bfe105ecb9668bb38 | feat(app): implement TicketService with validation and tests (TC-265) |
| 11150946f1ecbd01e1bdcdf51376e5a88724436b | feat(app): add CreateTicket command, DTO, and service interface (TC-265) |
| d75102e6f4bcffe7905a678484ebdd2f1f2af5f6 | feat(infra): add AddTicketTable migration with IT-YYYY-NNNN format (TC-265) |
| 981cc8b371f63d8cd30c8f92031e57a5dfdacb83 | feat(infra): configure Ticket EF mapping (TC-265) |
| e06b18187bffe473f77bf3bd0e703d66e5f0f008 | feat(domain): add Ticket entity, TicketStatus enum, TicketCategories constants (TC-265) |
| 1f1a69a7b288ea14fd89a85acfe610a6455350e7 | feat(domain): add TicketCategories constants (TC-265) |
| 78b16629b4a2afa58eca70a02a389bbf1edb0646 | feat(ticket): Dựng Create Ticket form chuẩn theo Spec v1.2 (TC-267) |
| bbaf56fb15ada21b986991acbee6f4ff4f8de57e | feat(auth): Thêm trang Đăng ký, sửa URL prefix và data contract khớp với Backend (TC-256, TC-257) |
| 81221f7fbc27aa80f1bf594f7ff4fa0773c09fbe | Merge pull request #2 from supportflow-ai/feature/core-ticket-lifecycle |
| 5425b62b84913964c129b9235744a7dd03bd3a4f | fix(identity): enforce requester role on registration and rollback on role failure |
| f0f6a5d368e813fa4c3eec939d0412ef29b587c8 | feat(identity): implement auth endpoints, policies, and tests (TC-252 to TC-255) |
| a7a5c7b7b59d00849635aff4fa45c1e2f4c4251d | feat(identity): configure Identity roles and seed 3 default roles + users (TC-251) |
| a717ac09f773db90540136bea3cb364b38e09f11 | feat: Xử lý logout, 401, expired session và async states (TC-259) |
| 84b048be87eb36dd565d5f997db5232ddb438acc | feat: Xây dựng giao diện Đăng nhập và form validation (TC-256) |
| 52421f5b600ab589d00c9c76c0308df00801c424 | feat: Thêm Implement ProtectedRoute và Role-aware navigation (TC-256). |
| a9ddbd0aad72056b40c55c42a202d3755c603e1b | feat: Tạo auth API client và session state (TC-257) |

### Các commit chỉ có ở main (2)

| SHA | Commit subject |
|---|---|
| e044f470d38cb9d00a4adca9e59de45abbde3ff5 | Merge pull request #1 from supportflow-ai/feature/tc-275-276-277-ai-triage-contract |
| b529b2cdd138b273ad5f89efb18ccb9f5a399313 | feat: hoàn thành định nghĩa AI triage contract, confidence rules và HITL boundary (TC-275, TC-276, TC-277) |

### TC-203 unique so với main (2)

| SHA | Commit subject |
|---|---|
| e834e74a50ecc4986ca329bc2a217ffc14af8cf4 | chore(devops): [TC-203] standardize PR/branch conventions, CI quality gates, and smoke hooks |
| 9010486ca8706f0678c3e97b12c589f007cf6c13 | chore(TC-203): standardize pull request quality checklist |

### Conflict simulation

D ↔ M hoặc D ↔ A: `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs` add/add. D ↔ Q: thêm `src/frontend/service-desk-web/package.json` và `package-lock.json` content conflict. Kết quả `git merge-tree --write-tree` không merge live refs; giữ nguyên files/index. Program.cs, test project file auto-merge được nhưng cần semantic review auth, health DB config và test references. Tree diff giữa hai tips có “deleted files” do Q chưa chứa dev commits; **không phải** evidence Q cố ý xóa Auth/Ticket. Dùng merge-base/triple-dot để phân biệt changes thực tế.

## 3. Pull requests và remote governance

| PR | State / base ← head | Merge SHA | Merge time UTC+07 |
|---|---|---|---|
| [#1](https://github.com/supportflow-ai/ai-it-service-desk/pull/1) | MERGED; main ← feature/tc-275-276-277-ai-triage-contract | e044f470d38cb9d00a4adca9e59de45abbde3ff5 | 17/09/2026 16:36:34 |
| [#2](https://github.com/supportflow-ai/ai-it-service-desk/pull/2) | MERGED; dev ← feature/core-ticket-lifecycle | 81221f7fbc27aa80f1bf594f7ff4fa0773c09fbe | 17/09/2026 16:36:45 |
| [#3](https://github.com/supportflow-ai/ai-it-service-desk/pull/3) | MERGED; dev ← feature/tc-200-create-ticket | 3c84ed20258f824bfa695e9dd8accd946572c7af | 19/09/2026 14:52:51 |
| [#4](https://github.com/supportflow-ai/ai-it-service-desk/pull/4) | MERGED; dev ← frontend | 6b124e73e9cb95350a505a24a548cb663ab1b7de | 28/09/2026 23:41:12 |

`gh pr list --state all --limit 100` trả đúng 4 records, không open/closed-unmerged PR. Cả 4 `reviews=[]`, `statusCheckRollup=[]`: không có GitHub review submission/check evidence, không chứng minh rằng không ai từng review ngoài GitHub. TC-203 chưa có PR trong kết quả. Không publish remote trong audit.

GitHub API branches: cả 7 `protected=false`. API rulesets trả `[]`. D combined status `pending` với `total_count=0`, `statuses=[]`; check-runs `total_count=0`; Actions runs `total_count=0`. “pending” này chỉ là aggregate không có status entries, không chứng minh Jenkins đang chạy. Không có server Jenkins build logs hay URL trong repo để kiểm chứng build lịch sử. README/Jenkins viết feature → dev → staging → main nhưng PR #1 đi thẳng main và staging chưa tồn tại. Hướng khắc phục tại AUD-08/10/11 và [branch-integration-plan.md](branch-integration-plan.md).

## 4. Audit từng module / Integration gap matrix

| Feature | Jira ID | Branch / SHA label | File path | Implemented/Partial/Mock/Missing | Evidence | Risk | Owner đề xuất |
|---|---|---|---|---|---|---|---|
| Auth/JWT/policies | TC-251–255, TC-246/247 | D; C → D | src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs; src/backend/ServiceDesk.Api/Program.cs:25; src/backend/ServiceDesk.Infrastructure/Identity/AuthService.cs | Implemented / Partial hardening | Register/Login/Me, signed JWT và policies có code; đăng ký ép Requester. RequireRequester cho Requester/Agent/Admin, RequireAgent cho Agent/Admin. | AUD-02/04; runtime DB chưa pass | Anh Nguyen; Đức Anh; VÕ NGUYỄN ANH KIỆT |
| Auth UI/session/role routes | TC-256–259 | D; F → D | src/frontend/service-desk-web/src/features/auth/context/AuthContext.tsx; src/frontend/service-desk-web/src/shared/api/apiClient.ts; src/frontend/service-desk-web/src/routes/components/ProtectedRoute.tsx | Implemented / Partial | Login/Register/token/me/session restore/logout/401; không có refresh/revocation server. Admin không tự được FE Agent route, cần chốt role hierarchy. | AUD-05/19; FullName chỉ response registration, không persist (AuthService.cs:64,92) | Nguyễn Thị Thu Hoài; Anh Nguyen |
| Ticket create/detail backend | TC-200, TC-265/266 | D; T → D | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs; src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs | Implemented / Partial | POST create, GET guid, requester ownership; Agent/Admin service bỏ ownership filter; tạo Submitted. | AUD-01/03/04/13 | Anh Nguyen |
| Ticket Domain/status/DTO | TC-199, TC-260/261, TC-299 | D vs M/A | src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs; src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs; src/backend/ServiceDesk.Application/Ticketing/Dtos/TicketDto.cs | Partial | Entity + categories + mapping + xmin; khác enum 0–6 vs 0–8; chưa transition methods/assignment/audit. | AUD-06/16 | Đức Anh; Anh Nguyen; Nguyễn Thị Thu Hoài |
| Requester create/detail UI | TC-267/268/274 | D; F → D | src/frontend/service-desk-web/src/features/tickets/components/CreateTicketPage.tsx; src/frontend/service-desk-web/src/features/tickets/components/TicketDetailPage.tsx | Partial | Form/network calls tương ứng API đã có; UI status mapping sai; production build không pass. | AUD-05/06; không kết luận E2E pass | Nguyễn Thị Thu Hoài |
| Requester list/pagination | TC-271/272 | D; F → D | src/frontend/service-desk-web/src/features/tickets/components/TicketListPage.tsx; src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:8; src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs | Partial / Missing API | UI table client pagination pageSize10; GET /tickets/me chưa registered. | AUD-07; không có server pagination contract | Anh Nguyen + Nguyễn Thị Thu Hoài |
| Agent queue/commands | TC-294/299, TC-312–314 | D; F → D | src/frontend/service-desk-web/src/routes/AppRoutes.tsx:108; src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx; src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:48 | Partial / Mock / Missing API | Queue/Admin route DemoPage; detail layout và command clients có; triage UI chỉ alert, request-info chỉ message, backend commands thiếu. | AUD-05/06/07 | Anh Nguyen + Nguyễn Thị Thu Hoài; Đức Anh |
| Public comments/internal notes/timeline | TC-303, TC-313 | D | src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:24; src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:174 | Mock / Missing backend | Timeline hard-coded event; client comments isInternal có, API/persistence/projection thiếu. | AUD-14; chưa có leak note được chứng minh | Đức Anh; Anh Nguyen; Nguyễn Thị Thu Hoài; VÕ NGUYỄN ANH KIỆT |
| Attachment/MinIO | TC-308, TC-313 | D, M, Q | src/backend/ServiceDesk.Infrastructure/Storage/MinioObjectStorageService.cs:23; src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:195 | Partial / Missing ticket integration | Presign/upload/download URL và bucket adapter có thực thi; attachment button không handler, không API/metadata/ACL/limits. | AUD-14/20 | Anh Nguyen; Đức Anh; VÕ NGUYỄN ANH KIỆT |
| AI triage contract/confidence/HITL | TC-275–277, TC-455 | M/A (có); D (chưa integrate); Q chứa M | src/backend/ServiceDesk.Application/AIAssistance/Models/TriageResult.cs; src/backend/ServiceDesk.Domain/AIAssistance/ConfidencePolicy.cs; src/backend/ServiceDesk.Domain/AIAssistance/AISuggestion.cs; src/backend/ServiceDesk.Infrastructure/AI/NoOpTicketClassificationService.cs | Partial / Not integrated | M/A có result/outcomes/projections/confidence0.4/0.75/SEC manual/decisions/hash-revision/tests; D classifier Empty. Chưa real provider/routes/persistence/apply workflow. | AUD-09/15; không coi contract trên M absent toàn repo | Hương Nguyễn; Anh Nguyen; Đức Anh |
| AI frontend/review panels | TC-275–277 (contract linkage; không suy ra ticket UI Done) | M/A/Q; D chưa có | src/frontend/service-desk-web/src/features/ai-assistance/api.ts; src/frontend/service-desk-web/src/features/ai-assistance/components/AgentReviewPanel.tsx; src/frontend/service-desk-web/src/features/ai-assistance/components/RequesterAIPanel.tsx | Partial scaffold / Not integrated | Typed clients/components có trên M/A; chưa mounted AppRoutes, endpoints absent; client /api/ai + base /api cần sửa. | AUD-15 | Hương Nguyễn + Nguyễn Thị Thu Hoài + Anh Nguyen |
| Knowledge/article lifecycle/RAG | TC-216, TC-329–333 | D, M/A, Q; các refs còn lại không thêm module | src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs; src/backend/ServiceDesk.Infrastructure/AI/NoOpEmbeddingService.cs; docker-compose.yml:104 | Missing business implementation / Scaffold infra | Không article entities/services/endpoints/migrations; image pgvector và empty vector chỉ hạ tầng, không có indexing/retrieval workflow. | AUD-17; Jira contract To Do | Đức Anh; Anh Nguyen; Hương Nguyễn |
| SLA/Analytics | TC-230, TC-387–391 | D, M/A, Q; các refs còn lại không thêm module | README.md:60; src/backend/ServiceDesk.Worker/Program.cs; src/backend/ServiceDesk.Application/Common/Interfaces/IClock.cs | Missing | Không SLA targets/clocks/KPI API, database model hay boundary tests; IClock generic tồn tại. | AUD-17; planned scope | Đức Anh; Anh Nguyen; Nguyễn Thị Thu Hoài; VÕ NGUYỄN ANH KIỆT |
| Migrations/integration tests | TC-251/265/266/280 | D; fixture health fix ở Q | src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs; tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs; tests/ServiceDesk.IntegrationTests/DatabaseTests.cs | Partial | Migration ticket có metadata; thiếu Identity/snapshot; auth/ticket/health tests đã viết, tất cả Integration trait; chưa chạy DB runtime trong audit. | AUD-04/12/13 | Anh Nguyen + VÕ NGUYỄN ANH KIỆT |
| CI/CD/governance | TC-203, TC-278–282 | D (pipeline cũ); Q (chưa integrate) | Jenkinsfile; .github/pull_request_template.md; Q:.github/workflows/ci.yml; Q:deploy/jenkins/casc.yaml | Partial / Not integrated | D restore/build/unit/frontend/docker, không integration gate; Q có stage integration/MSTest/workflow/smoke nhưng còn mismatches. Không remote protection/checks/reviews evidence. | AUD-08/10/11 | VÕ NGUYỄN ANH KIỆT; Đức Anh |
| Docs/deploy | TC-242, TC-442–444 | D/M; process/runbooks ở Q | docs/adr/; docs/architecture/.gitkeep; docs/runbooks/.gitkeep; deploy/nginx/nginx.conf; README.md | Implemented ADR / Partial deploy / Missing shared contracts | 10 ADR; Compose NGINX APIx2 PG MinIO worker; D architecture/runbooks .gitkeep; 9 docs staged ở checkout gốc được giữ nguyên, không thành evidence committed. | AUD-18/20 | Đức Anh; VÕ NGUYỄN ANH KIỆT |

## 5. API, Domain và Frontend contracts

| FE/API contract | Backend tại D | Tình trạng / gate |
|---|---|---|
| POST /api/auth/register: email/password/fullName | AuthEndpoints.cs:13 + RegisterRequest.cs | Response token/user khớp. FullName chưa persist; public role input không được cấp Admin |
| POST /api/auth/login; GET /api/auth/me | AuthEndpoints.cs:33/53 | Có; JWT lifetime/issuer/audience/key + role checks; runtime cần DB gate |
| POST /api/tickets: title/description/categoryId | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:20 | Có; response full TicketDto, FE dùng subset id/ticketNumber hợp lệ. Initial Submitted. FE không gửi Idempotency-Key |
| GET /api/tickets/{id:guid} | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:66 | Có; own requester, Agent/Admin toàn ticket theo service hiện tại; xác nhận scope có được phê duyệt |
| GET /api/tickets/me | Không registered | Missing; cần list contract/pagination/filter + ownership |
| POST /api/tickets/{id}/triage | Không registered | Missing; category/impact/urgency wire enums + priority deterministic |
| POST /api/tickets/{id}/assign | Không registered | Missing; FE gửi assignedAgentId='me', chốt self-assign/reassign semantics |
| POST /api/tickets/{id}/start | Không registered | Missing; actor/state/concurrency gate |
| POST /api/tickets/{id}/request-information | Không registered | Missing; FE wrapper có nhưng nút chỉ hiện message |
| POST /api/tickets/{id}/resolve | Không registered | Missing; resolution validation + transition/audit |
| POST /api/tickets/{id}/comments | Không registered | Missing; public/internal permission + requester projection |
| Ticket timeline | ticketApi.ts:24 trả array hard-coded | Mock, không có backend timeline contract |
| Ticket attachment upload/download | Không ticket route; MinIO adapter có | Missing ticket integration/security policy |
| AI /api/ai/ticket-analysis, requester projection, suggestion decision | Chỉ client/type trên M/A, không Map endpoint | Missing runtime; client path '/api/ai' với base '/api' cần sửa khi merge |

Các path FE ở `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:8–77`; Auth backend ở `src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs:11–73`. Contract absence được kiểm cả endpoints, Program composition root và branches, không chỉ main.

| Number | dev BE (D) | FE dev (D) / AI main (M) |
|---:|---|---|
|0|Draft|Draft|
|1|Submitted|Submitted|
|2|InProgress|Triaged|
|3|PendingUser|Assigned|
|4|Resolved|InProgress|
|5|Closed|PendingUser|
|6|Cancelled|PendingExternal|
|7|không định nghĩa|Resolved|
|8|không định nghĩa|Closed|

Integer đã lưu cần mapping có kiểm kê; không đổi enum dựa trên UI rồi giả định DB tự hiểu. API DTO hiện không có requester/assignee/resolution/updatedAt/revision mà FE optional fields dùng; optional không gây parse crash nhưng workspace không đủ data. BE có RequesterDepartmentId nullable nhưng FE chưa nhận; snapshot vẫn null. Error contract hiện create trả `{error}`, auth register `{errors}`, các failure khác chưa thống nhất ProblemDetails. UI 409 handling chưa có backend commands tương ứng.


## 6. Database, test coverage và CI/CD

| Kiểm chứng | Kết quả | Giới hạn |
|---|---|---|
| Fetch/prune + pull --ff-only audit | Pass; audit HEAD = origin/dev = D, clean trước audit | Không switch checkout gốc; không sửa local dev/main ở worktree khác |
| Backend restore ServiceDesk.slnx | Pass | NuGet dependencies restore ngày audit; floating versions hiện có |
| Backend Release build ServiceDesk.slnx | Pass; 0 errors, 0 warnings | Không chứng minh migrations/runtime |
| Backend test Category!=Integration | 21 passed: Domain 4, Application 11, Architecture 6 | Integration assembly báo no matches, không được tính là integration pass |
| Lệnh Jenkins restore ServiceDesk.sln | Pass | .sln và .slnx đều có; không có finding “missing .sln” |
| npm ci --ignore-scripts | Pass; 272 packages | Npm lifecycle scripts bị skip, frontend tsc vẫn chạy; Node24.13.1/npm11.8.0 khác Node22 README |
| npm run build | Fail; exit2, 3 TypeScript errors | Agent Option unused, Button success invalid, TicketList Space unused |
| npm run lint | Fail; exit1, 6 errors +1 warning | Không sửa code trong audit |
| docker compose config --quiet | Pass | Chỉ parse/normalize; không chứng minh image build/runtime |
| Docker daemon | Không truy cập được npipe dockerDesktopLinuxEngine | Không start Docker, không deploy hay đụng volumes người khác |
| Existing integration suite | **28 tests failed / 0 passed, exit1 vì DockerUnavailableException ở fixture (không thực thi nghiệp vụ)** | Environment blocker; không quy runtime migration/auth pass/fail từ lỗi thiếu Docker |
| Endpoint probe TestServer + fake service/current user | Confirmed cross-user replay và concurrency duplicate | Thực thi TicketEndpoints thật; không validate real JWT/DB/load balancer |
| EF migration metadata/SQL generation, không kết nối DB | Only AddTicketTable; pending-model=True; Identity tables=False | Không gán lỗi system xmin chỉ từ dòng migration; cần SQL/runtime validation nếu có nghi vấn |
| Merge-tree dev/main và dev/AI | Exit1: TicketStatus add/add conflict | Chỉ simulation Git objects; không cập nhật branch/index/files |
| Merge-tree dev/TC203 | Exit1: TicketStatus, package.json, package-lock.json | Semantic integration risks tồn tại cả ở file auto-merge |

Không có measured line/branch coverage %. Tests hiện có: auth register/login/me/token expiry/role escalation negative; ticket create/validation/basic idempotency same actor/GET; SQLite service visibility tests; health/DB tests; architecture boundaries. Missing critical coverage: cross-user replay, concurrent/multi-node retries, PostgreSQL sequence boundary/clean migrations/upgrades, lifecycle actor/state/reopen, internal-note/privacy/attachment ACL, frontend contract/E2E, AI provider/persistence/stale API, SLA/KB. Unit sqlite bypasses PostgreSQL sequence/xmin nên 21 pass không giảm mức AUD-04/13.


## 7. Jira live đối chiếu implementation

Nguồn trực tiếp: Atlassian JQL `project=TC AND key IN (...)` và truy vấn auth/frontend cùng `parent=TC-203/TC-216/TC-230`, ngày 09/10/2026; 58 unique issue rows, hai pages đều isLast=true. Chỉ những Jira được đọc dưới đây có trạng thái live được xác nhận. Không suy ra toàn project, Sprint acceptance, actual owner Git identity hoặc Done toàn feature từ số row này. Các mapping module/QA owner dựa requirement và live assignee, không tự assign.

- TC-203 cùng TC-278–282 Done nhưng Q chưa vào D, remote CI gates chưa có. Cần integration evidence rồi nghiệm thu lại scope, không đổi Jira trong audit.
- TC-312/313/314 Done nhưng layout/clients không chứng minh actions/attachment/timeline E2E; audit thấy API missing và build fail.
- TC-265/266 Done có implementation, nhưng còn idempotency/migration/sequence findings. TC-200 parent In Progress hợp lý với integration chưa hoàn tất.
- TC-275/276/277 Done có evidence M/A, cần đánh dấu **implemented contracts / not integrated dev**, không “missing AI contract”.
- TC-196 Done trong khi TC-246/247 To Do; quan hệ parent/subtask theo planning attachment cần governance reconciliation; không tự coi local staged APPROVED labels là review evidence.
- TC-199/260/261 In Progress, TC-299/303 Highest To Do; chính là dependencies cần chốt trước state machine/privacy implementation.
- Knowledge/SLA contracts To Do phù hợp missing business code, không gọi regression.

| Jira | Summary | Trạng thái live | Assignee live |
|---|---|---|---|
| [TC-196](https://evolving-kg-drift.atlassian.net/browse/TC-196) | Chốt actor, RBAC và contract truy cập | Done | Đức Anh |
| [TC-199](https://evolving-kg-drift.atlassian.net/browse/TC-199) | Chốt Ticket domain model và requester visibility rules | In Progress | Đức Anh |
| [TC-200](https://evolving-kg-drift.atlassian.net/browse/TC-200) | Requester tạo ticket mới từ web | In Progress | Anh Nguyen |
| [TC-203](https://evolving-kg-drift.atlassian.net/browse/TC-203) | Thiết lập quality gate và integration environment lặp lại được | Done | VÕ NGUYỄN ANH KIỆT |
| [TC-216](https://evolving-kg-drift.atlassian.net/browse/TC-216) | Chốt Knowledge Article lifecycle, versioning và permissions | To Do | Đức Anh |
| [TC-230](https://evolving-kg-drift.atlassian.net/browse/TC-230) | Chốt SLA targets, clock rules và KPI glossary | To Do | Đức Anh |
| [TC-242](https://evolving-kg-drift.atlassian.net/browse/TC-242) | Chốt architecture, tài liệu, demo và go/no-go | To Do | Đức Anh |
| [TC-246](https://evolving-kg-drift.atlassian.net/browse/TC-246) | Phân tích actor và use case cốt lõi | To Do | Đức Anh |
| [TC-247](https://evolving-kg-drift.atlassian.net/browse/TC-247) | Lập permission matrix API | To Do | Đức Anh |
| [TC-251](https://evolving-kg-drift.atlassian.net/browse/TC-251) | Cấu hình Identity persistence và seed 3 role | Done | Anh Nguyen |
| [TC-252](https://evolving-kg-drift.atlassian.net/browse/TC-252) | Implement Register/Login use cases | Done | Anh Nguyen |
| [TC-253](https://evolving-kg-drift.atlassian.net/browse/TC-253) | Implement JWT, claims và authorization policies | Done | Anh Nguyen |
| [TC-254](https://evolving-kg-drift.atlassian.net/browse/TC-254) | Expose Auth endpoints và integration tests | Done | Anh Nguyen |
| [TC-255](https://evolving-kg-drift.atlassian.net/browse/TC-255) | Chạy negative auth smoke | Done | VÕ NGUYỄN ANH KIỆT |
| [TC-256](https://evolving-kg-drift.atlassian.net/browse/TC-256) | Tạo Login page + form validation | Done | Nguyễn Thị Thu Hoài |
| [TC-257](https://evolving-kg-drift.atlassian.net/browse/TC-257) | Tạo auth API client và session state | Done | Nguyễn Thị Thu Hoài |
| [TC-258](https://evolving-kg-drift.atlassian.net/browse/TC-258) | Implement ProtectedRoute và role-aware navigation | Done | Nguyễn Thị Thu Hoài |
| [TC-259](https://evolving-kg-drift.atlassian.net/browse/TC-259) | Xử lý logout, 401, expired session và async states | Done | Nguyễn Thị Thu Hoài |
| [TC-260](https://evolving-kg-drift.atlassian.net/browse/TC-260) | Chốt Ticket fields, initial status và ownership | In Progress | Đức Anh |
| [TC-261](https://evolving-kg-drift.atlassian.net/browse/TC-261) | Chốt requester visibility và lifecycle baseline | In Progress | Đức Anh |
| [TC-264](https://evolving-kg-drift.atlassian.net/browse/TC-264) | Architecture review Ticket domain | Done | Đức Anh |
| [TC-265](https://evolving-kg-drift.atlassian.net/browse/TC-265) | Implement CreateTicket command + persistence | Done | Anh Nguyen |
| [TC-266](https://evolving-kg-drift.atlassian.net/browse/TC-266) | Expose CreateTicket API + authorization test | Done | Anh Nguyen |
| [TC-267](https://evolving-kg-drift.atlassian.net/browse/TC-267) | Tạo Create Ticket form + client validation | In review | Nguyễn Thị Thu Hoài |
| [TC-268](https://evolving-kg-drift.atlassian.net/browse/TC-268) | Kết nối mutation + success/error navigation | In review | Nguyễn Thị Thu Hoài |
| [TC-271](https://evolving-kg-drift.atlassian.net/browse/TC-271) | Tạo Requester Ticket List page | Done | Nguyễn Thị Thu Hoài |
| [TC-272](https://evolving-kg-drift.atlassian.net/browse/TC-272) | Hoàn thiện loading/empty/error/pagination UX | Done | Nguyễn Thị Thu Hoài |
| [TC-274](https://evolving-kg-drift.atlassian.net/browse/TC-274) | Tạo Requester Ticket Detail page | Done | Nguyễn Thị Thu Hoài |
| [TC-275](https://evolving-kg-drift.atlassian.net/browse/TC-275) | Định nghĩa TriageRequest/TriageResult | Done | Hương Nguyễn |
| [TC-276](https://evolving-kg-drift.atlassian.net/browse/TC-276) | Chốt confidence, fallback và human-review state | Done | Hương Nguyễn |
| [TC-277](https://evolving-kg-drift.atlassian.net/browse/TC-277) | Review AI scope và HITL boundary | Done | Đức Anh |
| [TC-278](https://evolving-kg-drift.atlassian.net/browse/TC-278) | Chuẩn hóa PR/branch/Jira linkage | Done | VÕ NGUYỄN ANH KIỆT |
| [TC-279](https://evolving-kg-drift.atlassian.net/browse/TC-279) | Cấu hình backend/frontend CI checks | Done | VÕ NGUYỄN ANH KIỆT |
| [TC-280](https://evolving-kg-drift.atlassian.net/browse/TC-280) | Xác minh Docker Compose clean integration environment | Done | VÕ NGUYỄN ANH KIỆT |
| [TC-281](https://evolving-kg-drift.atlassian.net/browse/TC-281) | Thêm smoke hook cho auth/ticket | Done | VÕ NGUYỄN ANH KIỆT |
| [TC-282](https://evolving-kg-drift.atlassian.net/browse/TC-282) | Chốt Definition of Ready/Done và review rule | Done | Đức Anh |
| [TC-285](https://evolving-kg-drift.atlassian.net/browse/TC-285) | Acceptance review và quyết định carry-over | To Do | Đức Anh |
| [TC-294](https://evolving-kg-drift.atlassian.net/browse/TC-294) | Chốt assignment rules và actor permissions | To Do | Đức Anh |
| [TC-299](https://evolving-kg-drift.atlassian.net/browse/TC-299) | Chốt transition matrix và reopen policy | To Do | Đức Anh |
| [TC-303](https://evolving-kg-drift.atlassian.net/browse/TC-303) | Chốt comment/internal-note visibility rules | To Do | Đức Anh |
| [TC-308](https://evolving-kg-drift.atlassian.net/browse/TC-308) | Chốt attachment security limits | To Do | Đức Anh |
| [TC-312](https://evolving-kg-drift.atlassian.net/browse/TC-312) | Dựng Agent Ticket Workspace layout | Done | Nguyen Thi Thu Hoai |
| [TC-313](https://evolving-kg-drift.atlassian.net/browse/TC-313) | Tích hợp assignment/status/comment/note/attachment actions | Done | Nguyen Thi Thu Hoai |
| [TC-314](https://evolving-kg-drift.atlassian.net/browse/TC-314) | Hoàn thiện role/action/error states | Done | Nguyen Thi Thu Hoai |
| [TC-326](https://evolving-kg-drift.atlassian.net/browse/TC-326) | Sprint 2 acceptance + risk review | To Do | Đức Anh |
| [TC-329](https://evolving-kg-drift.atlassian.net/browse/TC-329) | Chốt Article fields/lifecycle/version rules | To Do | Đức Anh |
| [TC-330](https://evolving-kg-drift.atlassian.net/browse/TC-330) | Chốt permission matrix Knowledge | To Do | Đức Anh |
| [TC-331](https://evolving-kg-drift.atlassian.net/browse/TC-331) | Review persistence/API implications | To Do | Anh Nguyen |
| [TC-332](https://evolving-kg-drift.atlassian.net/browse/TC-332) | Review admin/editor/read use cases | To Do | Nguyễn Thị Thu Hoài |
| [TC-333](https://evolving-kg-drift.atlassian.net/browse/TC-333) | Tạo lifecycle/visibility test matrix | To Do | VÕ NGUYỄN ANH KIỆT |
| [TC-387](https://evolving-kg-drift.atlassian.net/browse/TC-387) | Định nghĩa response/resolution targets theo priority | To Do | Đức Anh |
| [TC-388](https://evolving-kg-drift.atlassian.net/browse/TC-388) | Định nghĩa clock pause/resume/reopen rules | To Do | Đức Anh |
| [TC-389](https://evolving-kg-drift.atlassian.net/browse/TC-389) | Định nghĩa KPI glossary | To Do | Đức Anh |
| [TC-390](https://evolving-kg-drift.atlassian.net/browse/TC-390) | Review UTC/IClock implementation strategy | To Do | Anh Nguyen |
| [TC-391](https://evolving-kg-drift.atlassian.net/browse/TC-391) | Thiết kế SLA boundary test cases | To Do | VÕ NGUYỄN ANH KIỆT |
| [TC-421](https://evolving-kg-drift.atlassian.net/browse/TC-421) | Review security/API architecture changes | To Do | Đức Anh |
| [TC-442](https://evolving-kg-drift.atlassian.net/browse/TC-442) | Cập nhật README/ADR/runbooks/known limitations | To Do | Đức Anh |
| [TC-455](https://evolving-kg-drift.atlassian.net/browse/TC-455) | Review AI safety/HITL boundary trước release | To Do | Đức Anh |

## 8. Danh sách findings và quyết định tích hợp

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

Mỗi finding có SHA/branch/path, mức xác nhận, tác động, owner, hướng sửa và test gate trong technical-debt register. Thứ tự: P0 security → migrations/idempotency/build → contract và main reconciliation → CI integration → ticket operations/privacy → staging candidate → main release; KB/SLA theo scope đã chốt. Không merge các branch ancestor C/T/F lần nữa; không lấy Q tree thay D.

## 9. Lệnh đã thực hiện / lệnh kiểm chứng

Trong checkout gốc:

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
git diff --cached --name-status
git worktree list
git fetch origin --prune
git worktree add -b audit/repository-audit-20261009 ..\ai-it-service-desk-audit origin/dev
```

Trong audit worktree:

```powershell
git pull --ff-only origin dev
git status --short
git rev-parse HEAD
git rev-parse origin/dev
git for-each-ref --format='%(refname:short)|%(objectname)|%(subject)' refs/heads refs/remotes
git rev-list --left-right --count origin/main...origin/dev
git merge-base origin/main origin/dev
git log --oneline --left-right origin/main...origin/dev
git merge-tree --write-tree origin/dev origin/main
git merge-tree --write-tree origin/dev origin/feature/tc-203-quality-gate-and-integration-environment
git merge-tree --write-tree origin/dev origin/feature/tc-275-276-277-ai-triage-contract
git diff --name-status origin/dev...origin/main
git diff --name-status origin/dev...origin/feature/tc-203-quality-gate-and-integration-environment
git ls-tree -r --name-only origin/dev .github docs deploy
git tag --list
dotnet restore src/backend/ServiceDesk.slnx --packages .cache/packages --disable-parallel
dotnet build src/backend/ServiceDesk.slnx -c Release --no-restore
dotnet test src/backend/ServiceDesk.slnx -c Release --no-build --no-restore --filter 'Category!=Integration' --logger 'trx;LogFileName=audit-unit.trx'
dotnet restore src/backend/ServiceDesk.sln --no-cache
dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=audit-integration.trx' --results-directory .cache/integration
docker compose config --quiet
docker version
# Frontend, working directory src/frontend/service-desk-web:
npm.cmd ci --cache ../../../.cache/npm --ignore-scripts --no-audit --no-fund
npm.cmd run build
npm.cmd run lint
```

Read-only remote commands:

```powershell
gh pr list --repo supportflow-ai/ai-it-service-desk --state all --limit 100 --json number,title,state,baseRefName,headRefName,headRefOid,mergeCommit,mergedAt,url
gh api repos/supportflow-ai/ai-it-service-desk/branches --paginate
gh api repos/supportflow-ai/ai-it-service-desk/rulesets
gh api repos/supportflow-ai/ai-it-service-desk/commits/6b124e73e9cb95350a505a24a548cb663ab1b7de/status
gh api repos/supportflow-ai/ai-it-service-desk/commits/6b124e73e9cb95350a505a24a548cb663ab1b7de/check-runs
gh api repos/supportflow-ai/ai-it-service-desk/actions/runs
# Mỗi PR 1–4: gh pr view N --repo supportflow-ai/ai-it-service-desk --json reviews,statusCheckRollup
```

Probe tạm, source và cách reproduce ở appendix technical-debt register; scratch/cache và test outputs được Git ignore, không phải business code edits. Self-review: kiểm tra inventory/counts, mapping SHA/path và sự phân biệt static/probe/runtime; diff tracked business code phải rỗng. Chỉ 3 reports là deliverable; **chưa commit, chưa push, chưa PR, chưa merge**.
