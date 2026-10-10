# API contract matrix — Frontend / Backend

Ngày kiểm chứng: 2026-10-10 (Asia/Bangkok). Kết luận tích hợp: **BLOCKED**; build thành công không chứng minh ticket workflow đã tích hợp.

## Baseline, phạm vi và cách đọc evidence

- **D** = `origin/dev` tại `6b124e73e9cb95350a505a24a548cb663ab1b7de`. Checkout audit sạch sau fetch và pull fast-forward, nhánh `audit/frontend-backend-integration`.
- **F** = `origin/frontend` tại `063489b97c1f706cebeaeea1d3038f0e63a39fd7`; chỉ dùng đối chiếu tài liệu, không merge.
- Mọi đường dẫn source bên dưới là repository-relative và thuộc **D**, trừ khi ghi **F**. Kết quả FIX-01/02/03 là thay đổi **chưa commit** trên nhánh audit từ D; không gán SHA baseline cho mã đã sửa.
- Implemented = có route/handler thật, không đồng nghĩa E2E PASS. Partial = thiếu phần contract; Mock = dữ liệu dựng tại frontend; Missing = không có handler được đăng ký; Incompatible = hai phía khác ý nghĩa/shape.
- Inventory theo `src/backend/ServiceDesk.Api/Program.cs:143–145` và extension mapping, quét calls trong toàn bộ `src/frontend/service-desk-web/src`. Không suy diễn endpoint từ tên hàm, Jira Done hoặc swagger document version.
- Prefix thực tế là `/api`. `/api/v1` trong tài liệu F chưa được code đăng ký. Paths “dự kiến” dưới đây chỉ là backlog, chưa là contract được duyệt.

## Danh mục route và caller

Các alias trong bảng đều mở rộng thành đường dẫn chính xác:

| Alias | Source path tại D |
| --- | --- |
| AUTH | src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs |
| TICKET | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs |
| DIAG | src/backend/ServiceDesk.Api/Endpoints/DiagnosticEndpoints.cs |
| PROGRAM | src/backend/ServiceDesk.Api/Program.cs |
| AUTH-SVC | src/backend/ServiceDesk.Infrastructure/Identity/AuthService.cs |
| TICKET-SVC | src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs |
| FA | src/frontend/service-desk-web/src/features/auth/api/authApi.ts |
| FT | src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts |
| WORKSPACE | src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx |
| ROUTES | src/frontend/service-desk-web/src/routes/AppRoutes.tsx |

**10 route backend** = 3 auth + 2 ticket + 3 diagnostic + 2 system. Swagger chỉ được bật ở Development. **12 HTTP call expressions** = 3 FA + 9 FT. FT có thêm một hàm timeline mock, tổng FA/FT là 13 hàm. Triage/requestInfo có call expression nhưng chưa có UI sử dụng thực tế; không đếm việc bấm nút placeholder như request.

| ID / Feature | HTTP Method | Path | Frontend Caller | Backend Handler | Request DTO | Response DTO | Auth Policy | Status | Jira ID |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| API-01 Register | POST | /api/auth/register | FA.register; Register.tsx | AUTH:13 → AUTH-SVC.RegisterAsync | RegisterRequest: email, password; fullName?, role? | 201 {token,user}; 400 {errors:string[]} | Anonymous; server luôn gán Requester | Implemented; profile persistence Partial | TC-252, TC-254 |
| API-02 Login | POST | /api/auth/login | FA.login; AuthContext | AUTH:33 → LoginAsync | LoginRequest: email,password | 200 {token,user}; invalid credentials 401 | Anonymous | Implemented; runtime test blocked | TC-252, TC-253, TC-255 |
| API-03 Restore user | GET | /api/auth/me | FA.getCurrentUser; AuthContext | AUTH:53 → GetCurrentUserAsync | — | 200 UserInfo; 401/404 | Authenticated | Implemented; fullname nullable, FIX-02 | TC-254, TC-255 |
| API-04 My tickets | GET | /api/tickets/me | FT.getMyTickets; TicketListPage | **Không có**; guid detail route không match “me” | FE không gửi paging/filter | FE mong TicketDto[]; BE route miss 404 | Chưa có policy cho list | Missing | TC-201, TC-269–274 |
| API-05 Ticket detail | GET | /api/tickets/{id:guid} | FT.getTicketById; TicketDetailPage + WORKSPACE | TICKET:66 → GetTicketByIdAsync | Guid route id | 200 TicketDto; 404 missing/foreign requester; 401 | RequireRequester + owner filter; Agent/Admin bypass owner filter | Incompatible status enum; ownership có source enforcement | TC-201, TC-269, TC-274 |
| API-06 Create ticket | POST | /api/tickets (mapped “/”) | FT.createTicket; CreateTicketPage | TICKET:20 → CreateTicketAsync | CreateTicketCommand: title,description,categoryId | 201 full TicketDto + Location; FE khai báo subset id,ticketNumber; 400 {error}; 401 | RequireRequester; subject Guid | Partial; P0 replay isolation | TC-268 |
| API-07 Agent queue | GET dự kiến | Chưa chốt path; /api/tickets?scope=queue chỉ là candidate | /agent-workspace → DemoPage; không có queue HTTP call | Không có | Paging/filter TBD | TBD | TBD; staff scope cần duyệt | Missing / page scaffold | TC-206, TC-290–293 |
| API-08 Triage ticket | POST (FE) | /api/tickets/{id}/triage | FT.triageTicket; chưa có caller UI | Không có | FE {categoryId,impact,urgency}, strings | Untyped response; backend TBD | Chưa có endpoint policy | Missing | TC-202, TC-275: AI contract, chưa chứng minh mutation API |
| API-09 Assign | POST (FE) | /api/tickets/{id}/assign | FT.assignTicket; WORKSPACE sends assignedAgentId:'me' | Không có | FE {assignedAgentId}; sentinel chưa có BE contract | TBD | TBD; assignment policy chưa được hợp nhất | Missing | TC-294–298 |
| API-10 Start | POST (FE) | /api/tickets/{id}/start | FT.startTicket; WORKSPACE.handleStart | Không có | FE không body | TBD | TBD | Missing | TC-208, TC-299–302 |
| API-11 Request information | POST (FE) | /api/tickets/{id}/request-information | FT.requestInfo không được UI gọi; nút chỉ message.info | Không có | FE {message} | TBD | TBD | Missing | TC-208, TC-301–302 |
| API-12 Resolve | POST (FE) | /api/tickets/{id}/resolve | FT.resolveTicket; WORKSPACE resolve form | Không có | FE {resolution} | TBD | TBD | Missing | TC-208, TC-301–302 |
| API-13 Close | Chưa có | TBD; không tự chọn command path | Không có caller | Không có | TBD: user confirmation/auto-close policy | TBD | TBD | Missing | TC-208, TC-299 |
| API-14 Reopen | Chưa có | TBD | Không có caller | Không có | TBD: reason/window/state | TBD | TBD | Missing | TC-208, TC-299 |
| API-15 Public comment | POST (FE) | /api/tickets/{id}/comments | FT.addComment; WORKSPACE isInternal:false; Requester UI chưa có | Không có | FE {content,isInternal} | TBD | TBD; ownership/status guards | Missing | TC-303–307 |
| API-16 Internal note | POST (FE) | Cùng /comments, isInternal:true | FT.addComment; WORKSPACE | Không có | FE dùng discriminator; separate /notes chỉ là đề xuất policy | TBD | Chưa enforce staff-only | Missing; privacy blocker trước triển khai | TC-303–307 |
| API-17 Timeline | Không HTTP | FT.getTicketTimeline trả array tại client | WORKSPACE.fetchWorkspaceData | Không có | id chỉ dùng tạo fake event | TicketTimelineEventDto[] mock Date.now()-1h | Không có BE visibility filter | Mock | TC-305, TC-312–314 |
| API-18 Attachments | Chưa có | TBD | WORKSPACE có button không handler | Không có ticket attachment endpoint | TBD | TBD | TBD | Missing; storage abstraction không phải API | TC-308–311 |
| API-19 Diagnostic auth | GET | /api/diag/protected | Không có FE caller | DIAG:11 | — | {message}; 200/401 | Authenticated | Implemented | TC-253–255 |
| API-20 Diagnostic agent | GET | /api/diag/agent-only | Không có FE caller | DIAG:15 | — | {message}; 200/401/403 | RequireAgent: Agent hoặc Admin | Implemented | TC-253–255 |
| API-21 Diagnostic admin | GET | /api/diag/admin-only | Không có FE caller | DIAG:19 | — | {message}; 200/401/403 | RequireAdmin: Admin | Implemented | TC-253–255 |
| API-22 Health | GET | /health | Không có app caller | PROGRAM:148 MapHealthChecks | — | status,checks[],totalDuration; 200/503 | Anonymous | Implemented; DB check, không chứng minh MinIO/E2E | TC-323: gate liên quan, không ticket sở hữu endpoint |
| API-23 API info | GET | /api | Không có app caller | PROGRAM:170 | — | {service,version,status}; 200 | Anonymous | Implemented | Không xác định Jira implementation |
| API-24 Requester reply/confirm | Chưa có | TBD | Detail chỉ đọc; chưa reply/confirm command | Không có | TBD | TBD | TBD owner/lifecycle | Missing | TC-209, TC-208; cần xác nhận scope |

Anonymous HTTP status có thể được status-code middleware thêm body; không giả định luôn là ProblemDetails. Các response error explicit hiện tại dùng {error} hoặc {errors}; FE không có decoder thống nhất. 409 của workspace là UI branch dự kiến, chưa có ticket command thực tế trả conflict tại D.

## Data model compatibility

| Field / concern | Backend tại D | Frontend tại D / audit delta | Compatibility / tác động |
| --- | --- | --- | --- |
| id / ticketNumber | Guid / DB-generated string | string / string | Phù hợp JSON; validation GUID do route constraint |
| title | Required; service raw length <=100 rồi trim; DB max200 | Create form max100 | Happy path khớp; API vs DB giới hạn khác, không tự đổi sang200 |
| description | Required nonblank, trimmed length>=10; DTO string | Payload string; DTO description?; form raw min10 | Partial; 10 spaces có thể qua form nhưng server400; tighten DTO/input sau test |
| categoryId | Exact case TicketCategories.All, varchar20 | ACC NET VPN MAIL SW HW PRINT ACCESS EQUIP SEC OTHER | Vocabulary 11 keys khớp; TS union không validate JSON runtime |
| status | DTO int; DB integer, domain enum0–6 | TS enum0–8, semantic khác | **Incompatible**, xem bảng bên dưới; dữ liệu cũ cần migration/backward compatibility trước đổi BE |
| requesterDepartmentId | Guid? nullable, JSON nullable | Không khai báo | FE bỏ trường được; thiếu declaration nếu UI sử dụng |
| requesterId | Entity ownership, lấy authenticated subject; không có DTO | Không có | Không thêm client-provided owner; owner authorization ở BE |
| priority / impact / urgency | Không có fields trong Ticket/DTO/create | Optional priority P1–P4; triage request có impact/urgency strings | Scaffold; không coi badge/fallback là dữ liệu thật |
| assignee / resolution | Không có Ticket/DTO fields | Optional assignedAgentId/name,resolution | Partial display; mutation/schema cần backlog |
| updatedAt / lifecycle times | Entity có UpdatedAt; không expose DTO; lifecycle chưa triển khai | createdAt string; thiếu revision/lifecycle times | UTC format source hợp lý, DB round-trip chưa test được |
| concurrency | Shadow xmin PostgreSQL; không ở DTO/request | FE có message409, không gửi token | Thiếu optimistic-concurrency protocol cho future commands |
| userId,email,roles | UserInfo string,string,List<string>; roles Requester/Agent/Admin | Same Role union | Claim/response vocabulary phù hợp source; real JWT tests blocked |
| fullName | UserInfo string?; register echo; login/me null do không persist | Baseline string → FIX-02 string\|null | Fixed compile contract, không thêm profile persistence |
| password | Register/Login required string | Baseline optional → FIX-02 required | Fixed compile contract; wire validation vẫn BE |
| JSON naming | ASP.NET web defaults camelCase; explicit token/user lowercase | camelCase objects and axios plain JSON | Phù hợp source; chưa có generated schema/runtime decoder |
| Dates | DateTime.UtcNow create; timestamptz | ISO string, render qua JS date | Cần assert offset/round-trip/locale sau PostgreSQL gate |
| Paging/sorting/filter | List handler chưa có | AntD client-side pageSize10; GET/me không query | Không có server paging contract, TC-270 còn To Do |
| Transport | /api; JWT Bearer | config VITE_API_BASE_URL hoặc /api; Axios interceptor | Default paths khớp; /api/v1 docs F không khớp |

Evidence model: `src/backend/ServiceDesk.Domain/Ticketing/{Ticket.cs,TicketStatus.cs,TicketCategories.cs}`; `src/backend/ServiceDesk.Application/Ticketing/{Dtos/TicketDto.cs,Commands/CreateTicketCommand.cs}`; TICKET-SVC; `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs`; `src/frontend/service-desk-web/src/features/tickets/types.ts`; `src/backend/ServiceDesk.Application/Identity/Models/{UserInfo.cs,LoginRequest.cs,RegisterRequest.cs}`; AUTH-SVC; `src/frontend/service-desk-web/src/features/auth/types.ts`.

### Enum mapping: tuyệt đối không renumber DB một cách âm thầm

| Numeric | Backend domain / stored interpretation D | Frontend interpretation D |
| --- | --- | --- |
| 0 | Draft | Draft |
| 1 | Submitted | Submitted |
| 2 | InProgress | Triaged |
| 3 | PendingUser | Assigned |
| 4 | Resolved | InProgress |
| 5 | Closed | PendingUser |
| 6 | Cancelled | PendingExternal |
| 7 | Không định nghĩa | Resolved |
| 8 | Không định nghĩa | Closed |

Ví dụ server trả status4 cho Resolved nhưng workspace có thể hiện action InProgress. Đây là lỗi source-confirmed; chưa chứng minh ticket đã lưu ở4 trong production. Quyết định target lifecycle, có cần Triaged/Assigned/PendingExternal hay không và adapter/migration thuộc Backend + Architect + Product. Audit không thay enum. Test bắt buộc: fixtures từng numeric0–6; unknown numeric không được chọn action; DB inventory từng status; API/FE round-trip; historical records; stale-client compatibility.

## Roles, token và delivery path

- PROGRAM cấu hình issuer/audience/signature/lifetime; RoleClaimType = ClaimTypes.Role. `src/backend/ServiceDesk.Infrastructure/Identity/{JwtTokenGenerator.cs,CurrentUser.cs}` dùng claim subject/NameIdentifier; ticket endpoint yêu cầu parse Guid.
- RequireRequester nhận Requester/Agent/Admin; RequireAgent nhận Agent/Admin. ROUTES chỉ cho ['Requester'] ở /tickets, ['Agent'] ở workspace; Admin đơn-role không vào workspace. Đây là policy mismatch cần chốt, không tự mở route.
- `src/frontend/service-desk-web/src/shared/api/apiClient.ts`: token trong localStorage, Bearer header;401 clear token + auth:unauthorized, trừ login. AuthContext cũng clear token khi bất kỳ lỗi restore nào (kể cả network/503). Không có refresh API; logout local không phải revoke token.
- `vite.config.ts` proxy /api → localhost:5000 khớp `src/backend/ServiceDesk.Api/Properties/launchSettings.json`. Compose `deploy/nginx/nginx.conf` proxy /api/ → hai API instances8080; path không bị đổi prefix.
- `src/frontend/service-desk-web/Dockerfile` có SPA fallback; chạy riêng container frontend không cung cấp API proxy. Phải truy cập compose gateway hoặc cấu hình API origin/CORS tương ứng; real proxy smoke chưa chạy vì Docker.
- `docs/architecture/02-ticket-domain-and-field-contract.md`, `03-ticket-lifecycle-and-visibility-baseline.md`, `01-rbac-and-api-permission-matrix.md` ở **F** không phải current runtime. Không dùng nhãn APPROVED của tài liệu để ghi đè code/data.

## Jira đối chiếu

Jira đọc trực tiếp ngày audit; discovery search có cảnh báo kết quả có thể chưa đầy đủ. “Done” chỉ là workflow status, không là release acceptance.

| Jira evidence | Status quan sát | Kết luận source |
| --- | --- | --- |
| [TC-201](https://evolving-kg-drift.atlassian.net/browse/TC-201), [TC-271](https://evolving-kg-drift.atlassian.net/browse/TC-271), [TC-274](https://evolving-kg-drift.atlassian.net/browse/TC-274) | Done | List UI có, list backend thiếu; detail có nhưng enum lệch |
| [TC-269](https://evolving-kg-drift.atlassian.net/browse/TC-269), [TC-270](https://evolving-kg-drift.atlassian.net/browse/TC-270), TC-273 | To Do | Backend queries/paging + QA chưa đủ evidence |
| [TC-206](https://evolving-kg-drift.atlassian.net/browse/TC-206), [TC-208](https://evolving-kg-drift.atlassian.net/browse/TC-208) | To Do | Queue/transitions chưa implement |
| [TC-312](https://evolving-kg-drift.atlassian.net/browse/TC-312), [TC-313](https://evolving-kg-drift.atlassian.net/browse/TC-313), TC-314 | Done | Shell và HTTP calls có; timeline mock; attachment placeholder; không chứng minh integrated workflow |
| TC-251–255 | Done | Auth code tồn tại; schema/migration và real JWT validation còn gate |
| [TC-268](https://evolving-kg-drift.atlassian.net/browse/TC-268) | In Review | Create happy-path có; P0 idempotency blocker |
| [TC-323](https://evolving-kg-drift.atlassian.net/browse/TC-323) | To Do | E2E task chưa được audit này hoàn thành |
| TC-202, TC-275 | Done | AI DTO/provider contract tồn tại không đồng nghĩa triage-ticket command registered |

Jira mapping cho endpoint mới là đề xuất linkage dựa scope issue; audit không tạo/update Jira. Backlog và test gate: [gap report](frontend-backend-gap-report.md), [E2E plan](e2e-integration-plan.md).

Nguồn framework đối chiếu: [Microsoft System.Text.Json web naming/enum defaults](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties), [ASP.NET Core Minimal API responses](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0). Các kết luận repository ở trên dựa source D, không dựa framework docs thay cho runtime tests.
