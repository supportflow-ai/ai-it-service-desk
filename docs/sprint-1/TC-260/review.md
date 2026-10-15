# TC-260 — Chốt Ticket fields, initial status và ownership

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Ticket fields, initial status, ownership, numbering và snapshot. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Field contract verified trên baseline mới

[Ticket.cs:L21](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L21) là domain factory đã merge qua PR #12; không lặp lại nhận xét cũ rằng invariants còn hoàn toàn nằm trong service.

| Field | Hành vi hiện có | Mapping / target gap |
|---|---|---|
| Id / RequesterId | Server UUID; requester từ principal; Guid.Empty bị reject | Owner private setter; [Ticket.cs:L31](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L31) |
| Title | Trim, nonblank, tối đa 100 sau trim | DB max 200 là storage capacity, không tự đổi validation thành 200 |
| Description | Trim, tối thiểu 10; chưa có max cap ở factory | Không coi generic 4000 constant là rule đã enforce |
| CategoryId | Một trong 11 code, ordinal/case-sensitive | DB varchar20; không phải category GUID |
| Status | Submitted=1 khi tạo | Draft=0 tồn tại enum nhưng chưa có createDraft/submit use case |
| TicketNumber | null! trước insert; DB-generated, required, unique, max30 | [ApplicationDbContext.cs:L81](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs#L81) và migration; không chứng minh an toàn sau sequence >9999 |
| RequesterDepartmentId | Factory nhận optional snapshot; service hiện không truyền nên null | [TicketService.cs:L25](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L25); chưa có trusted department/profile source |
| CreatedAt / UpdatedAt | Một DateTime.UtcNow truyền vào factory, hai trường bằng nhau | Factory nhận timestamp từ caller; không tự suy rằng mọi caller đã UTC-normalize |
| xmin | EF shadow concurrency token cho PostgreSQL | Chưa có command DTO expectedRevision hoặc conflict handler |

Validation source: [Ticket.cs:L40](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L40), [Ticket.cs:L51](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L51), [Ticket.cs:L56](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L56). DTO: [Current TicketDto:L9](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Application/Ticketing/Dtos/TicketDto.cs#L9).

## 2. Hiện trạng, yêu cầu cũ và proposal

| Chủ đề | Contract cũ / proposal | Kết luận audit |
|---|---|---|
| Initial state | ARCH-CONTRACT-02 yêu cầu Draft; later specification đề xuất giữ current Submitted và thêm Draft API riêng | Unresolved business contract; không tự sửa current create hoặc báo compliant |
| Numbering | IT vs TK; nullable Draft vs DB required | Giữ persisted data; chốt additive migration sau review |
| Department | Mandatory snapshot ở tài liệu cũ, nullable code | Cần trusted source/provenance; không fabricate snapshot |
| Title/description | Các cap/min khác nhau giữa reference và runtime | Ghi chính xác current normalized limits; new limits pending review |
| Priority/category | Main AI enums/priority matrix khác reference code/type | Contract target cần explicit mapping; không merge enum ordinal as-is |

[Field decision register:L138](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/03-ticket-domain-contract.md#L138) là decision register Proposed/TBD, không phải quyết định đã phê duyệt. Tài liệu có lines/source trước PR #12 vẫn là historical snapshot; hồ sơ này cập nhật factory behavior.

## 3. Field/invariant verification

Domain 15/15 gồm normalized creation, blank/short/invalid category/empty owner và IsOwnedBy; Application 12/12 gồm service creation và requester paging. Test source: [Domain invariants tests:L12](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/tests/ServiceDesk.Domain.Tests/Ticketing/TicketTests.cs#L12). Đây là tests của Submitted baseline, không phải Draft target acceptance.

Numbering/unique/xmin/fresh-schema/sequence-boundary phải chạy PostgreSQL thật; hiện NOT RUN. Comment trong mapping khẳng định LPAD không truncate không được dùng làm evidence: cần negative boundary 9999→10000 trên DB thật trước sign-off.

## 4. Handoff

Backend Core chốt Draft/versioning/migration và numbering; Frontend đồng bộ approved field/wire contract; Architect review decision register; QA kiểm normalization, null/limits, ownership, sequence và snapshot provenance. Không giao thêm ownership implementation cho Đức Anh trong PR tài liệu này.

Kết luận: field review sẵn sàng; runtime alignment với tất cả contract vẫn có blocker.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #9](https://github.com/supportflow-ai/ai-it-service-desk/pull/9): merged, base dev, head docs/ticket-domain-lifecycle; merge SHA bee4e2dc07ecdbf44677db2905b945adb9f6d88d.
- [PR #12](https://github.com/supportflow-ai/ai-it-service-desk/pull/12): merged, base dev, head feature/tc-262-263-ticket-domain-invariants; merge SHA fe41eecf920d59010e0d89e72079ab84edb32488.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-260-ticket-field-contract chỉ bổ sung deliverable hiện tại của TC-260.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
