# TC-246 — Phân tích actor và use case cốt lõi

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Actor/use-case matrix và ranh giới role/permission. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Actor/use-case matrix đã đối chiếu

[Actor contract:L50](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/01-actor-use-cases.md#L50) và [RoleNames.cs:L9](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Identity/RoleNames.cs#L9) xác nhận code có Requester/Agent/Admin. Lead được giữ như actor nghiệp vụ đề xuất, chưa có role/JWT claim độc lập.

| Actor | Auth | Ticket | Knowledge / analytics | Hiện trạng / giới hạn |
|---|---|---|---|---|
| Requester | Register/login/me | Tạo ticket của mình; list/detail own tickets | Published KB/own metrics theo đề xuất | Auth và create/list/detail có source; KB/metrics chưa implement |
| Agent | Login/me, agent diagnostic | Policy cho phép tạo ticket cá nhân; detail hiện đọc được ticket khác; queue/triage/assign/lifecycle chưa có API | KB authoring/team metrics theo đề xuất | Agent role không chứng minh team-scoped access đã tồn tại |
| Admin | Login/me, admin diagnostic | Current detail bypass ownership; quyền quản trị target cần action/scope cụ thể | Publish/config/org analytics theo đề xuất | Không suy ra có quyền bỏ qua mọi state/revision |
| Support Team Lead | Đề xuất Agent + trusted team Dispatch grant | Điều phối trong team; không tự start/resolve nếu thiếu grant | Quyền publish/analytics phải cấp riêng | Chờ review mapping; không tự thêm role thứ tư |
| AI/worker | Service capability đề xuất | Advisory; không sở hữu core ticket state | Chỉ Published/ACL-filtered KB khi được triển khai | Main AI contract không đồng nghĩa dev integration |

API ownership/list: [TicketService.cs:L58](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L58). Agent/Admin broad detail: [TicketService.cs:L84](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L84). UI grants: [UI route permissions:L101](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/routes/AppRoutes.tsx#L101).

## 2. Năm use case bàn giao đầu tiên

| Use case | Positive scenario | Negative scenario / expected target | Evidence / trạng thái |
|---|---|---|---|
| Đăng ký | Server cấp Requester | Client xin Admin không được nâng quyền | [Registration role assignment:L46](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Identity/AuthService.cs#L46) — source verified, HTTP regression chưa chạy |
| Đăng nhập/me | Valid token đọc own profile | Missing/invalid token bị từ chối | Auth endpoints + JWT source; integration NOT RUN |
| Create | Identity từ JWT; valid title/description/category | Owner không lấy từ body; foreign idempotency key không được replay data người khác | [TicketEndpoints.cs:L28](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L28) — global cache là blocker |
| Own list | Filter trước count/paging | B đổi URL/query không thấy ticket của A | [TicketService.cs:L60](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L60) — Application test pass, FE paged-object integration còn lệch |
| Detail | Owner đọc ticket | Foreign Requester nhận 404; Agent ngoài scope target bị deny | [TicketService.cs:L86](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L86) — Requester source verified, Agent scope chưa có |

## 3. Phân biệt role, permission và visibility

Role là membership từ principal; permission là action; scope là ownership/team/assignment lấy từ server; state/revision là guard riêng. UI menu chỉ biểu diễn quyền, không thay API enforcement. Missing scope data không được dùng làm lý do cấp global Agent access trong contract target.

Không coi các enum, dispatch grant, KB publishing hay analytics proposal trong tài liệu cũ là code đã chạy. Reviewer Backend Core + Frontend cần xác nhận Lead mapping và use-case scopes trước sign-off.

## 4. Kết luận

Actor matrix có evidence cập nhật và sẵn sàng review; business Lead và future-use-case grants vẫn pending. PR #8 đã merge cả actor/RBAC trên một nhánh: hồ sơ mới bổ sung traceability hiện tại, không viết lại lịch sử đó.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #8](https://github.com/supportflow-ai/ai-it-service-desk/pull/8): merged, base dev, head docs/tc-246-247-rbac; merge SHA 1111a9ba401bd2cadd44fbcf760c044b4af76c75.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-246-actor-use-cases chỉ bổ sung deliverable hiện tại của TC-246.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
