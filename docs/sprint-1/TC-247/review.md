# TC-247 — Lập permission matrix API

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: RBAC/API permission matrix và negative scenarios. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Claim và policy hiện có

[RoleNames.cs:L9](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Identity/RoleNames.cs#L9), [Registered policies:L52](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Program.cs#L52) và [JWT claims:L29](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Identity/JwtTokenGenerator.cs#L29) xác nhận subject/role lấy từ issued token. RequireRequester cho Requester/Agent/Admin; RequireAgent cho Agent/Admin; RequireAdmin chỉ Admin.

## 2. Permission matrix đối chiếu với API

| Resource/action | Requester | Agent | Admin | Implementation / guard gap |
|---|---|---|---|---|
| Register/login | Anonymous allowed | Anonymous allowed | Anonymous allowed | Register luôn cấp Requester; [Anonymous auth endpoints:L31](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs#L31) |
| Auth me | Authenticated own identity | Như Requester | Như Requester | .RequireAuthorization, profile lấy từ principal |
| Create ticket | Own identity | Own identity | Own identity | [TicketEndpoints.cs:L61](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L61); scope replay cache thiếu requester |
| List own tickets | Own only | Own personal tickets | Own personal tickets | [TicketService.cs:L58](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L58); staff queue khác chưa có API |
| Read detail | Own only | Hiện global detail; target assigned/team scope | Hiện global detail; target management scope | [TicketService.cs:L84](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L84); không có team/assignment model |
| Update Draft / submit | Đề xuất owner + Draft | Chỉ own Draft | Chỉ own Draft | Chưa có API; permission không tự biến create hiện tại thành Draft |
| Assign / transition | Không tự xử lý staff workflow | Đề xuất assigned or trusted Dispatch | Explicit action + scope + state/revision | Chưa có command endpoint; [Permission contract:L73](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/02-rbac-permission-matrix.md#L73) |
| Public comment | Đề xuất owner/public view | Đề xuất authorized staff | Đề xuất authorized management | Chưa có backend route |
| Internal note | Không được đọc/ghi | Đề xuất authorized staff | Đề xuất authorized management | DTO/query phải loại internal fields, chưa có runtime flow |
| KB | Published + visibility | Author/editor; publish chỉ explicit grant | Explicit editorial administration | Future contract; không suy quyền publish từ Agent/Lead |
| Analytics/admin | Own aggregates đề xuất | Team aggregates đề xuất | Org/config theo explicit grant | Chưa có business API |

## 3. Positive/negative verification handoff

| Scenario | Kết quả target | Evidence hiện tại |
|---|---|---|
| A list/detail own ticket | 200, counts/paging chỉ own | Source + Application tests PASS |
| B đọc ticket A | 404, không content/metadata leak | Source filter + unit test PASS; HTTP PostgreSQL NOT RUN |
| A và B dùng cùng Idempotency-Key | Mỗi owner có response riêng; changed payload bị conflict theo approved policy | FAIL ở source design: key chỉ idempotency:ticket:key; chưa chạy reproduction HTTP |
| Agent ngoài team đọc A | Deny object scope | Target chưa enforce; role-only bypass hiện có |
| Requester gọi admin diagnostic | 403 trước handler | Policy source verified; integration NOT RUN |
| Không token / invalid JWT | 401, không side effect | JWT validation source; integration NOT RUN |
| Requester gọi internal-note hoặc tự nâng isInternal | Deny và public projection không leak | API chưa implement; future negative test |
| Authorized actor nhưng wrong state/stale revision | Proposed 422/409, không write/event | Target guard chưa implement; không báo PASS |

## 4. Security blockers và handoff

Global cache key: [TicketEndpoints.cs:L36](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L36). PR #5 còn draft, head 0ec26ebc778b8f202170882fa663caa541dbe566; không có trên baseline dev. Body PR báo historical PostgreSQL hotfix run, nhưng không thay baseline acceptance hiện tại. Anh Nguyen phụ trách enforcement/migration; QA chạy real PostgreSQL negatives; Frontend chỉ dùng reviewed grants để render.

Một permission cần authenticated identity + granted action + resource scope, rồi state/revision nếu mutation. HTTP denial semantics của target chỉ được áp dụng khi endpoint đã implement, không dùng 404 vì route thiếu làm bằng chứng authorization.

## 5. Kết luận

Ma trận và negative-case handoff đã cập nhật. Security acceptance còn blocked; Jira Done được giữ như snapshot, không suy thành API đã tuân thủ đủ target.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #8](https://github.com/supportflow-ai/ai-it-service-desk/pull/8): merged, base dev, head docs/tc-246-247-rbac; merge SHA 1111a9ba401bd2cadd44fbcf760c044b4af76c75.
- [PR #5](https://github.com/supportflow-ai/ai-it-service-desk/pull/5): not merged, base dev, head fix/p0-idempotency-isolation; head SHA 0ec26ebc778b8f202170882fa663caa541dbe566, draft.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-247-api-permission-matrix chỉ bổ sung deliverable hiện tại của TC-247.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
