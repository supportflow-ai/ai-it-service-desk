# TC-261 — Chốt requester visibility và lifecycle baseline

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Ticket lifecycle, requester visibility và enum compatibility. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Requester visibility hiện có

List luôn filter RequesterId trước count/paging/sort ở [TicketService.cs:L56](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L56). Detail foreign Requester trả null ở [TicketService.cs:L90](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L90), endpoint chuyển 404 ở [TicketEndpoints.cs:L106](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L106). Agent/Admin bỏ owner predicate; đây không phải bằng chứng team/assigned visibility đã enforce.

List route mới: [TicketEndpoints.cs:L66](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L66); query page>=1, pageSize 1..100, stable createdAt DESC/id DESC, DTO {items,page,pageSize,totalCount}. [Requester list API client:L8](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts#L8) vẫn khai báo TicketDto[] và return response.data. Tính năng list đã có backend nhưng vertical slice UI chưa khớp wire shape.

## 2. Enum compatibility, không renumber persisted data

Source: [Persisted backend enum:L3](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs#L3), [Frontend ticket types:L3](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/features/tickets/types.ts#L3) và [Main TicketStatus:L9](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs#L9).

| Numeric code | Backend dev | Frontend dev / main enum |
|---|---|---|
| 0 | Draft | Draft |
| 1 | Submitted | Submitted |
| 2 | InProgress | Triaged |
| 3 | PendingUser | Assigned |
| 4 | Resolved | InProgress |
| 5 | Closed | PendingUser |
| 6 | Cancelled | PendingExternal |
| 7 | Không định nghĩa | Resolved |
| 8 | Không định nghĩa | Closed |

Codes 2–6 có nghĩa khác nhau; cast TypeScript không decode/remap response. Ví dụ backend InProgress=2 bị frontend đọc thành Triaged; backend Cancelled=6 bị đọc thành PendingExternal. Build/typecheck PASS không phát hiện semantic mismatch này.

Proposed DEC-05: giữ legacy0–6, append Triaged7/Assigned8/PendingExternal9. Đây là proposal tại [Field decision register:L138](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/03-ticket-domain-contract.md#L138), chưa được migration/wire review; không coi là enum đang chạy. Tài liệu ARCH-CONTRACT-03 lại yêu cầu nine-state order0–8: cần signed compatibility decision trước implementation.

## 3. Lifecycle baseline và giới hạn S1/S2

| Behavior | Hiện tại | Proposal / acceptance cần bổ sung |
|---|---|---|
| Create | [Ticket.cs:L69](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L69) | Draft/submit là capability mới cần quyết định rõ |
| Read list/detail | Có endpoint và ownership predicate | FE paging contract; PostgreSQL HTTP negative proof |
| Triage/assign/start/wait/resolve/close/reopen | Không có backend command routes | [Lifecycle proposal:L86](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/04-ticket-lifecycle-policy.md#L86) là allowlist proposal, có scope/state/revision/audit guards |
| Reopen window / auto-close | Không implement | Numeric windows/automation chưa sign-off; không biến thành rule hiện tại |
| Public/internal visibility | Existing DTO không có note payload | Không chứng minh feature private-note đã an toàn; API chưa tồn tại |

## 4. Acceptance scenarios

Own list/detail source + Application tests PASS; foreign Requester detail unit test PASS; Agent outside scope target BLOCKED; HTTP no-token/foreign owner/paging integration NOT RUN. Required FE cases: paged-object decoding, codes0–6 đúng theo reviewed contract, unknown future code không kích hoạt action, multi-role personal-ticket access. Required lifecycle cases nằm ở proposal, chưa được thực thi.

## 5. Kết luận và handoff

Visibility/lifecycle review đã cập nhật PR #13; các báo cáo cũ nói backend không có requester list không còn đúng với baseline này. Backend/Frontend phải chốt enum và paging contract; QA cần PostgreSQL scope tests. Giữ Jira Done và recorded status riêng với runtime acceptance còn blocked.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #9](https://github.com/supportflow-ai/ai-it-service-desk/pull/9): merged, base dev, head docs/ticket-domain-lifecycle; merge SHA bee4e2dc07ecdbf44677db2905b945adb9f6d88d.
- [PR #13](https://github.com/supportflow-ai/ai-it-service-desk/pull/13): merged, base dev, head feature/tc-269-270-requester-ticket-queries; merge SHA 249608e24b1bf4c64773f18bcb734fd529c1d554.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-261-ticket-lifecycle-visibility chỉ bổ sung deliverable hiện tại của TC-261.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
