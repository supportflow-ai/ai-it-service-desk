> **Đề xuất — chưa cấp phép triển khai.**
> Các mô tả “hiện tại/dev” và kết quả kiểm tra bên dưới chỉ thuộc ngày/SHA nguồn trong bản gốc, không mô tả HEAD mới.
> Đối chiếu [hợp đồng hiện hành](current-backend-contract.md), [quyết định còn mở](pending-business-decisions.md) và [quy tắc bảo mật](security-design-rules.md). Chú thích bổ sung ngày 2026-10-19; giữ bản gốc để truy vết.

## Tóm tắt tiếng Việt: Vai trò và luồng nghiệp vụ

Đối chiếu người yêu cầu, nhân viên hỗ trợ và quản trị viên ở mốc rà soát gốc. Phạm vi quyền sau PR #28 nằm trong hợp đồng hiện hành; quyền ghi và phân công cần được quyết định theo DEC-02.

Chú thích bổ sung ngày 2026-10-20; bản gốc tiếp tục được giữ để truy vết.

<!-- original-record -->
# Actor & use-case contract — TC-246 / TC-196

Ngày rà soát: 2026-10-09. Trạng thái: **READY FOR REVIEW**, chưa có Backend/Frontend/QA sign-off.
Branch tài liệu: `docs/tc-246-247-rbac`; baseline `origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de`.
Tài liệu này hoàn thiện nội dung bàn giao TC-246; không tuyên bố implementation hay Jira đã nghiệm thu.
Ma trận API, HTTP denial và security findings nằm trong [02-rbac-permission-matrix.md](02-rbac-permission-matrix.md).

## 1. Nguồn, trạng thái và giới hạn

| Nguồn đã đọc | Evidence | Ý nghĩa |
|---|---|---|
| README.md; toàn bộ ADR-001–010 tại docs/adr | Baseline dev SHA trên; ADR-005 chọn Identity/JWT, ADR-002 tách Application/Infrastructure, ADR-010 yêu cầu PostgreSQL thật | Foundation; README không chứng minh mọi business permission đã chạy |
| RoleNames.cs / PolicyNames.cs | src/backend/ServiceDesk.Domain/Identity tại dev SHA trên | Code có đúng Requester/Agent/Admin và3 coarse policies |
| Program.cs; AuthEndpoints.cs; TicketEndpoints.cs; AuthService.cs; CurrentUser.cs; JwtTokenGenerator.cs; tests Identity/Auth | Full paths và lines ở ma trận implementation/security findings của tài liệu02 | Source authoritative cho hành vi hiện có; tests có mặt không đồng nghĩa pass |
| Frontend AuthContext.tsx / ProtectedRoute.tsx / AppRoutes.tsx / authApi.ts / apiClient.ts | src/frontend/service-desk-web/src tại dev SHA trên | UI access map và session UX; không phải nguồn quyền API |
| 9 architecture docs tại frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7 | docs/architecture/01-rbac-and-api-permission-matrix.md,03-ticket-lifecycle-and-visibility-baseline.md,04-ticket-state-transition-and-reopen-matrix.md,06-ticket-assignment-policy.md và các reference liên quan | Reference branch khác, chưa có trên dev; labels APPROVED không thay live review evidence |
| Hotfix riêng fix/p0-idempotency-isolation, HEAD6b124e73… + working-tree diff | src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs; docs/reviews/p0-idempotency-remediation.md tại worktree hotfix | Chưa commit/integrate; P0 replay vẫn tồn tại trên dev. Không copy hotfix vào docs branch |

Jira đọc trực tiếp ngày rà soát, không sửa trạng thái/comment/assignee:

| Jira | Trạng thái live / owner | Handoff / dependency |
|---|---|---|
| [TC-196](https://evolving-kg-drift.atlassian.net/browse/TC-196) | Done / Đức Anh | Outcome actor/RBAC/claim-policy/API-UI contract; out-of-scope implement Identity/JWT/UI |
| [TC-246](https://evolving-kg-drift.atlassian.net/browse/TC-246) | To Do / Đức Anh | Actor/use-case cho auth/ticket/KB/analytics; không direct blocker |
| [TC-247](https://evolving-kg-drift.atlassian.net/browse/TC-247) | To Do / Đức Anh | API permissions; input cho TC-248/249/250 |
| [TC-248](https://evolving-kg-drift.atlassian.net/browse/TC-248) | To Do / Anh Nguyen | Review claim/policy/API/Application enforcement; phụ thuộc TC-247 theo description |
| [TC-249](https://evolving-kg-drift.atlassian.net/browse/TC-249) | To Do / Nguyễn Thị Thu Hoài | Route/menu/action map, phụ thuộc TC-247 |
| [TC-250](https://evolving-kg-drift.atlassian.net/browse/TC-250) | To Do / VÕ NGUYỄN ANH KIỆT | Positive/negative security test matrix, phụ thuộc TC-247 |
| [TC-260](https://evolving-kg-drift.atlassian.net/browse/TC-260), [TC-261](https://evolving-kg-drift.atlassian.net/browse/TC-261) | In Progress / Đức Anh | Fields, requester ownership, initial status/lifecycle |
| [TC-294](https://evolving-kg-drift.atlassian.net/browse/TC-294), [TC-299](https://evolving-kg-drift.atlassian.net/browse/TC-299), [TC-303](https://evolving-kg-drift.atlassian.net/browse/TC-303), [TC-308](https://evolving-kg-drift.atlassian.net/browse/TC-308) | To Do / Đức Anh | Assignment, transitions/reopen, notes/privacy, attachments |
| [TC-216](https://evolving-kg-drift.atlassian.net/browse/TC-216), [TC-329](https://evolving-kg-drift.atlassian.net/browse/TC-329), [TC-330](https://evolving-kg-drift.atlassian.net/browse/TC-330) | To Do / Đức Anh | KB Draft/Published/Archived; only Published for end-user/AI retrieval; detailed grants below are review proposal |
| [TC-230](https://evolving-kg-drift.atlassian.net/browse/TC-230) | To Do / Đức Anh | SLA/KPI/UTC/window; analytics role scopes below are proposal, chưa được issue này xác nhận |

TC-196 Done trong khi subtasks chưa review là governance discrepancy; không tự chuyển chúng sang Done.
Rovo search có cảnh báo một số nguồn không query được; các issue trong bảng đã fetch riêng. Không claim đã tìm mọi Confluence document.

## 2. Role, permission, ownership và state

- **Role**: trusted membership nhóm nghiệp vụ; hiện claim strings case-sensitive theo constants Requester/Agent/Admin. Role không chứa ticket ID.
- **Permission**: action như Ticket.Read, Ticket.AssignOther, Knowledge.Publish; names ở tài liệu02 là proposed policies, chưa có trong PolicyNames.cs.
- **Ownership**: Ticket.RequesterId bằng GUID identity từ validated principal; không dựa email, department, request body, URL userId hoặc UI persona.
- **Resource scope**: assignment/team membership/KB ACL lấy từ dữ liệu server authoritative. RequesterDepartmentId không phải SupportTeamId.
- **Resource state**: điều kiện lifecycle riêng. Có permission không tự làm một transition hợp lệ; Admin cũng không bypass graph/revision.
- **Projection**: quyền gọi endpoint và các trường có thể đọc là hai phép kiểm khác nhau.

Công thức contract: AuthenticatedIdentity AND GrantedAction AND ResourceScope AND AllowedState AND ValidRevision.
Read không đòi revision/state mutation; create chưa có resource thì server tự gán owner.
Khác actor nhưng cùng role vẫn có thể có scope khác nhau.

## 3. Actor definitions và business Lead

| Actor | Trách nhiệm | Scope | Giới hạn |
|---|---|---|---|
| Requester | Tạo yêu cầu của mình; xem/theo dõi; bổ sung thông tin/public comment; close/reopen khi rule cho phép | Own tickets; public projections; Published KB có visibility phù hợp; own metrics đề xuất | Không chọn owner khác, assignment/status/internal flag/roles; không đọc ticket requester khác, internal notes/audit/AI staff data |
| Agent | Xem queue được phép; self-assign; xử lý assigned ticket, triage được cấp quyền; public comment/internal note; KB authoring; team aggregates | Valid current assignment OR active support-team membership; mutation theo assigned/dispatch grant | Role Agent không cho global read/mutate; cùng team chưa assigned không được start/resolve; không quản lý role |
| Admin | Quản trị role/config; điều phối và audit ticket trong organization đang quản trị; KB publish/archive; organization aggregates/export được kiểm soát | Explicit permissions theo từng row; current project chưa có tenant model | Không bypass lifecycle/concurrency, không impersonate Requester qua create body; privileged mutations cần reason/audit |
| Support Team Lead (business actor được giữ từ reference frontend) | Dispatch/override trong team mình phụ trách | Proposed: Agent + server-authoritative team-scoped Ticket.Dispatch grant | **Không có RoleNames.Lead hoặc Lead JWT claim trên dev**. Mapping này chờ TC-248 review; không âm thầm xóa Lead hay tự thêm role thứ4 |
| AI/indexer/worker (system actor) | Gợi ý/phục vụ use case được cấp capability | Server service identity; Published/ACL-filtered KB; ticket context đúng scope | Không đóng vai requester, grant role, tự lifecycle transition; không bỏ kiểm ACL vì chạy background |

ACT-04 ở dưới định nghĩa assignment validity. Missing assignment/team/grant model phải fail closed cho staff scope cần nó, không fallback “mọi Agent”.
Unrouted Submitted tickets chưa có team chỉ Admin/explicit dispatcher đã được cấp scope phù hợp mới điều phối; default Agent không có global unrouted queue.

## 4. Rules có ID, ví dụ và acceptance criteria

| Rule | Contract | Ví dụ | Acceptance criteria |
|---|---|---|---|
| ACT-01 | Identity phải từ validated JWT; business subject là một GUID nhất quán giữa NameIdentifier/sub | A gửi body requesterId=B hoặc header role=Admin | Existing register/create ignores untrusted role/unknown owner fields; planned privileged DTOs reject400; owner luôn A; missing/invalid identity401; role không được cấp403 |
| ACT-02 | Mỗi action cần explicit permission + scope; unknown policy/claim không cấp quyền | Agent gọi admin role endpoint trực tiếp |403 trước side effect; giữ identity audit, không log token/payload nhạy cảm |
| ACT-03 | Owner là immutable requester identity cho create/replay; cùng department không có quyền chéo | A và B cùng department, A đọc T_B hoặc đổi URL/list filter |404 cho object không visible; list/count/pagination chỉ own; DTO/caches không chứa ticket B |
| ACT-04 | Agent read: valid active assignment OR active current-team membership. Valid assignment phải chưa bị revoke và còn hợp lệ với current team; không tin client teamIds | Agent Z ngoài team và assignment thử GET T_A | Deny object scope theo HTTP rule02; grant/revoke assignment/team có hiệu lực server; missing scope data không broad-access |
| ACT-05 | Workflow/field mutations cần Assigned Agent hoặc explicit team dispatch grant; collaboration theo từng row; Admin intervention có reason/audit | Agent Y cùng team nhưng chưa assigned bấm Resolve; Lead X assign trong Team Z |403, không write/audit-success/event; dispatcher không vượt team; stale revision409 cho caller đã được authorize |
| ACT-06 | State gate tách action grant; draft edit/delete/submit chỉ owner+Draft; terminal Closed không mutate/reopen mặc định | Admin resolve Closed; Requester sửa Submitted như draft |422, không side effect; reopen từ Resolved cần allowed window/reason; detailed graph TC-299 chưa sign-off thì action fail closed |
| ACT-07 | Requester projection không internal notes, internal attachments, staff audit/AI analysis; lọc tại query/serializer/search/cache | Requester đọc detail/timeline/search hoặc dùng includeInternal=true | Không private bytes, private IDs/count/snippets/URLs trong response; internal-note endpoint Requester-only token403 |
| ACT-08 | End-user/search/RAG chỉ Published và ACL-visible KB; draft edit chỉ author/scoped editor; publish/archive explicit authority | Requester query exact title Draft; Agent edit draft tác giả khác | Hidden article404 hoặc absent search results; forbidden write403; stale version409, invalid lifecycle422 |
| ACT-09 | Analytics lọc scope trước aggregate; own/team/org permissions riêng; raw export explicit Admin grant | Agent Team X yêu cầu teamId=Z hoặc requesterId=B filter |403 cho explicit forbidden scope; không trả count/rows của scope ngoài quyền; raw export nonAdmin403 |
| ACT-10 | UI kiểm UX; API enforce mọi request.403 không clear session;401 clear/restore theo frontend flow | Devtools bật nút admin hoặc gọi URL API bỏ ProtectedRoute | API vẫn403; valid session còn sau403; missing token401. Hidden button không được ghi là security evidence |
| ACT-11 | Role mutation Admin-only, allowlist current roles, audited; không self-elevate qua register/profile. Revocation cần SLA được duyệt | Register role=Admin; Admin downgrade user rồi dùng token cũ | Registration chỉ Requester; forbidden mutation403. Proposed sensitive management check phải từ chối revoked privilege ở request kế tiếp; backend phải chốt freshness mechanism trước triển khai |
| ACT-12 | Replay scope theo authenticated requester/method/resource/key, bind payload, atomic across nodes | A/B cùng key K; A đổi body;8 concurrent retry | B không nhận ticket A; A same body same result; changed body409; count1 concurrent; expired/failure policy và real DB tests theo hotfix report |
| ACT-13 | Background/AI không tăng quyền human actor; retrieval/projection giữ ACL | AI suggestion dùng draft KB bị hidden hoặc tự Resolve | Không hidden source/text/URL; không transition tự động; accept/override chỉ authorized assigned staff, có revision/audit |

Current dev chỉ thực thi một phần các rules; rule table là acceptance contract, không là claim đã pass.
Multi-role dùng union explicit grants nhưng vẫn scope/state; không có implicit Admin inheritance ngoài các row.
“My requests” luôn public projection; privileged staff endpoint chỉ trả staff projection khi authorization staff thành công, dù cùng user cũng có Requester role.

## 5. Actor/use-case matrix

P=permission row trong tài liệu02; mỗi UC có example và acceptance cụ thể. Các lifecycle/team-dependent UC là **target** cho review; không tạo endpoint trong task này.

| UC | Actor / use case | Permission | Ví dụ | Acceptance |
|---|---|---|---|---|
| UC-A01 | Anonymous đăng ký | AUTH-01 | Gửi role=Admin |201 chỉ Requester; invalid input400; không privileged claim |
| UC-A02 | Anonymous login | AUTH-02 | Sai mật khẩu / đúng mật khẩu |401 generic /200 token; không role do client chọn |
| UC-A03 | Authenticated xem mình | AUTH-03 | A gọi me | Chỉ profile A; unknown subject404 current; missing identity401 |
| UC-A04 | Admin quản lý role | AUTH-04 | Agent tự grant Admin |403; Admin authorized change audited và invalidate privileged access theo ACT-11 |
| UC-T01 | R/A/Admin tạo own ticket | TKT-01 | Agent tạo yêu cầu cá nhân | Owner=caller;201; status current Submitted=1, không tự đổi sang Draft trong RBAC task |
| UC-T02 | R/A/Admin theo dõi own ticket | TKT-02 | A filter requesterId=B | Foreign explicit filter403; normal query/count chỉ own, public fields |
| UC-T03 | Agent/Admin xem queue | TKT-03 | Agent X xem Team X | Chỉ allowed scope; outside explicit team403; no accidental global query |
| UC-T04 | R/A/Admin đọc detail | TKT-04 | Requester B đoán ID T_A |404; scoped Agent/Admin permitted200; projection đúng caller capability |
| UC-T05 | Owner sửa draft | TKT-05 | A sửa Draft A / Submitted A | Success valid revision /422; owner không đổi |
| UC-T06 | Owner xóa draft | TKT-06 | A xóa Draft B |404; own Draft204; không xóa submitted |
| UC-T07 | Owner submit draft | TKT-07 | A submit Draft A hai lần | First accepted; invalid state/revision denied; không side effects kép |
| UC-T08 | Assigned/scoped dispatcher/Admin triage | TKT-08 | Default Agent ngoài team sửa category |403/404 theo visibility; reason/revision và state checks khi allowed |
| UC-T09 | Scoped Agent self-assign | TKT-09 | Hai Agent cùng nhận queue item | Caller được chọn từ principal; một winner, loser409; không nhận thay người khác |
| UC-T10 | Dispatcher/Admin assign/unassign/team | TKT-10–12 | Target Agent thuộc team khác/inactive | Forbidden actor403; invalid target422; admin/lead đúng scope có audit và concurrency |
| UC-T11 | Assigned Agent/Admin start/request-info/resolve | TKT-13–15 | Team member chưa assigned Start |403; đúng assigned state được phép; owner/reason/state giữ invariant |
| UC-T12 | Owner/Admin close | TKT-16 | Owner close Resolved | Success hợp lệ; ngoài owner404; invalid source state422 |
| UC-T13 | Owner/assigned staff/Admin reopen | TKT-17 | Resolved trong window / Closed | Allowed reason+window+revision /422; không tạo state enum mới |
| UC-T14 | Visible actors đọc/ghi public comment | TKT-18–19 | A reply PendingUser | Public comment persisted; không internal flag; không tự đổi status nếu TC-299 chưa định nghĩa |
| UC-T15 | Scoped staff đọc/ghi internal note | TKT-20–21 | Requester-only token yêu cầu internal note |403; chỉ permitted staff sees note; public projection không note content/IDs/count |
| UC-T16 | Visible actors đọc timeline | TKT-22 | Requester poll ticket timeline | Chỉ public events; không hidden-note metadata |
| UC-T17 | Scoped staff/Admin audit | TKT-23 | Requester lấy history |403; allowed actor chỉ scope hợp lệ; privileged changes audited |
| UC-T18 | Visible actors upload/download attachment | TKT-24–25 | A dùng attachment ID của ticket B |404; private note attachment cần staff scope; size/MIME/lifecycle gates riêng TC-308 |
| UC-T19 | Assigned staff nhận/accept/override AI | TKT-26–27 | Requester gửi accept AI |403; assigned actor có revision/reason/audit; AI không auto transition |
| UC-K01 | R/A/Admin đọc/search Published KB | KB-01–02 | Tìm Published article ngoài ACL |404/absent; no restricted snippet/count; Draft/Archived không end-user retrieval |
| UC-K02 | Agent/Admin tạo KB draft | KB-03 | Requester tự tạo article |403; author=current identity; no client-created Published bypass |
| UC-K03 | Author/scoped editor/Admin sửa draft | KB-04 | Agent sửa draft tác giả khác |403/404; own permitted editor valid version success |
| UC-K05 | Scoped author/editor/publisher/Admin đọc draft | KB-07 | Agent reload draft mình viết / Requester preview draft | Authorized staff200; Requester403; unrelated editor404; no public preview leak |
| UC-K04 | Explicit publisher/Admin publish/archive | KB-05–06 | Agent không publisher gọi Publish |403; permitted Admin state/version checks; archive mất visibility end-user |
| UC-N01 | R/A/Admin metrics cá nhân | ANA-01 | A yêu cầu metrics B | Scope caller only; không raw ticket B |
| UC-N02 | Agent/Admin team analytics | ANA-02 | Agent X requestTeamZ |403; aggregate/date scope enforced trước query |
| UC-N03 | Admin organization analytics | ANA-03 | Agent gọi org overview |403; approved aggregate projection, không implicitly full PII |
| UC-N04 | Admin explicit analytics export | ANA-04 | Agent direct export CSV |403; privileged audited scope-limited export, no internal-note/token secrets |

## 6. Decision note và review blockers

| Decision ID | Proposed decision / current fact | Input cần chốt | Owner / acceptance |
|---|---|---|---|
| DEC-01 | Giữ3 implemented roles; giữ business Lead, proposed Agent + team-scoped dispatch capability | Frontend reference yêu cầu Lead; dev/Jira196 không có role thứ4 | TC-248 Backend + Đức Anh thống nhất mapping/CR nếu cần; không client Lead claim tự cấp quyền |
| DEC-02 | Endpoint đang chạy là /api, không /api/v1; matrix target dùng /api để giảm drift | Frontend reference dùng /api/v1 | Backend/FE review version strategy, không đổi API prefix chỉ bằng docs |
| DEC-03 | RBAC không remap TicketStatus; current create Submitted=1. Draft-edit contract chỉ áp dụng khi có Draft use case | TC-260/261/299; frontend reference enum9/string khác dev7/numeric | Đức Anh + Backend/FE chốt lifecycle/data migration riêng; không gọi Submitted hiện có là bug chỉ từ label APPROVED |
| DEC-04 | Ưu tiên404 cho object invisible;403 cho forbidden function/action trên object visible | Legacy frontend doc có Agent outside-team403; một số row cho403/404 | TC-248/250 review unified denial precedence ở02; chosen404 là proposal, không claim dev đã scoped Agent |
| DEC-05 | KB publisher capability và analytics own/team/org/export scopes như matrix là least-privilege proposal | TC-330/230 còn To Do; chưa có schemas/endpoints | Đức Anh + Backend/FE review; endpoint không expose trước authoritative scope/ACL |
| DEC-06 | Parent Done không chứng minh acceptance; READY FOR REVIEW không đồng nghĩa APPROVED | Jira TC-196/subtasks discrepancy; doc frontend acceptance claims chưa có reviewer evidence | Đức Anh ghi reviewer/commit/test links sau review; task này không tự edit Jira |

Fixtures acceptance: Requester A/B; Agent X assigned Team X; Agent Y cùng team nhưng chưa assigned; Agent Z ngoài team; Admin M; Lead L có trusted dispatch grant Team X; Draft/Submitted/InProgress/PendingUser/Resolved/Closed; KB Draft/Published/Archived.
SupportTeamId/AssignedAgentId chỉ là **target fixture fields**, chưa có trong current Ticket.cs; không dựng test giả rồi claim current backend hỗ trợ.

Demo5 use cases cốt lõi: create own, read own/other, self-assign versus assign-other, public/internal comment, Admin role change.
Mỗi demo dùng ba main actors; positive/negative expectations lấy từ permission IDs tương ứng, không chạy trên dữ liệu thật khi P0 chưa giải quyết.

## 7. Self-review và handoff

- Đã phân biệt role/action/owner/team/state/projection; ownership luôn identity equality, không department equality.
- Mỗi ACT/UC/DEC có ID, example hoặc scenario, acceptance và source/dependency.
- Có permission IDs tương ứng cho auth/ticket/assignment/transitions/comments/notes/KB/analytics; không implicit staff global read.
- Contract future và behavior dev được tách rõ; Lead/prefix/lifecycle không được tự coi đã implement hoặc đã duyệt.
- TC-248 review API/Application mapping; TC-249 review frontend routes/actions; TC-250 implement/execute negative test matrix.
- Build/test evidence, findings, backend/frontend blockers và preservation check ở tài liệu02.
- Chỉ tạo01-actor-use-cases.md và02-rbac-permission-matrix.md trong worktree mới; không thay business/auth code, không push/merge hoặc cập nhật Jira.
