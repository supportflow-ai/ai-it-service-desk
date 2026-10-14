# TC-264 — Architecture review Ticket domain

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Architecture boundary, naming và lifecycle extensibility review. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Nguồn review có thể truy cập

Nguồn local được tìm thấy và đưa vào [source-architecture-baseline.md](source-architecture-baseline.md); original context pack bị .gitignore loại khỏi Git. Snapshot có SHA-256 trong manifest, không có upstream commit hoặc chữ ký approval được xác minh. ADR [ADR-001:L1](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/adr/ADR-001-modular-monolith.md#L1) và [ADR-002:L1](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/adr/ADR-002-clean-architecture.md#L1) vẫn là baseline tracked.

## 2. Architecture review trên source hiện tại

| Finding | Evidence cố định | Đánh giá / handoff |
|---|---|---|
| Domain không dependency external | [Domain project:L7](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/ServiceDesk.Domain.csproj#L7) | PASS source + 6 architecture tests; không suy mọi module rule đều covered |
| Creation invariants đã vào domain | [Ticket.cs:L21](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L21) | PASS: owner/title/description/category; PR #12 sửa điểm cũ |
| Domain factory init Submitted | [Ticket.cs:L69](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L69) | Unresolved target contract Draft; cần decision, không sửa enum/state tại đây |
| Service naming khác assembly | [TicketService.cs:L10](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L10) | File ở Infrastructure nhưng namespace Application; assembly vẫn Infrastructure. Review naming/boundary với Backend |
| Use-case implementation trực tiếp EF | [TicketService.cs:L14](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L14) và [ApplicationDbContext.cs:L22](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs#L22) | Concrete EF service là Infrastructure adapter; confirm nơi đặt orchestration/query contracts. Không tự thêm generic repository |
| Application dependency direction | [Application project:L8](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Application/ServiceDesk.Application.csproj#L8) | PASS project direction; namespace naming không được chứng minh bởi assembly dependency tests |
| Application port còn tối thiểu | [Application-owned port:L11](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Application/Common/Interfaces/IApplicationDbContext.cs#L11) | SaveChanges-only interface; ticket adapter chưa dùng port để truy cập aggregate. Cần scoped use-case design, không speculative rewrite |
| xmin có mapping | [ApplicationDbContext.cs:L88](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs#L88) | Storage concurrency preparation; chưa có expectedRevision/command conflict handling |
| API qua ITicketService | [TicketEndpoints.cs:L23](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L23) | PASS boundary source; idempotency ở endpoint vẫn global và thiếu atomic persistence |
| S2 extensibility | Ticket chỉ có Create/IsOwnedBy, private setters | Chưa có transition methods/events, assignment, revision/audit persistence; Proposed graph cần review |

## 3. Naming/domain compatibility

TicketCategories là string code hiện tại; main AI TicketCategory là enum. TicketStatus code0–6 persisted khác main/frontend0–8. Category identity/type và numeric status phải có mapping/version plan; không cherry-pick whole main như một fix kiến trúc. Lifecycle proposal: [Lifecycle proposal:L86](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/04-ticket-lifecycle-policy.md#L86).

## 4. Dependency và verification

Jira upstream AISD-S1-04-T04 được resolve là TC-263, assignee Anh Nguyen, Done; PR #12 và current Domain tests cho evidence trực tiếp. 15 Domain/12 Application/6 Architecture tests PASS; solution gate FAIL CS8602, PostgreSQL NOT RUN.

Architecture PASS ở assembly dependency checks không bao phủ resource authorization, persisted-enum semantics, transition atomicity hoặc fresh Identity schema. Chúng vẫn là review/QA blockers riêng.

## 5. Kết luận

Review note sẵn sàng, có nguồn tracked và snapshot nguồn local. Creation factory đã cải thiện; boundary/naming và S2 guards cần reviewer Backend xác nhận. Không ký architecture acceptance tổng thể thay reviewer.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #9](https://github.com/supportflow-ai/ai-it-service-desk/pull/9): merged, base dev, head docs/ticket-domain-lifecycle; merge SHA bee4e2dc07ecdbf44677db2905b945adb9f6d88d.
- [PR #12](https://github.com/supportflow-ai/ai-it-service-desk/pull/12): merged, base dev, head feature/tc-262-263-ticket-domain-invariants; merge SHA fe41eecf920d59010e0d89e72079ab84edb32488.
- [PR #13](https://github.com/supportflow-ai/ai-it-service-desk/pull/13): merged, base dev, head feature/tc-269-270-requester-ticket-queries; merge SHA 249608e24b1bf4c64773f18bcb734fd529c1d554.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch audit/tc-264-ticket-domain-review chỉ bổ sung deliverable hiện tại của TC-264.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
