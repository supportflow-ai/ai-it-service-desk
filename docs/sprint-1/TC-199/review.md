# TC-199 — Chốt Ticket domain model và requester visibility rules

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **In review**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Closed-loop contract/domain/DB/API/frontend/tests acceptance. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Closed-loop acceptance checklist

| Layer | Evidence thực tế | Kết luận |
|---|---|---|
| Contract fields | [TC-260 draft PR #17](https://github.com/supportflow-ai/ai-it-service-desk/pull/17), commit [9c6a3f6](https://github.com/supportflow-ai/ai-it-service-desk/commit/9c6a3f60550f3989016d6dd1ef17f5bfc8365a4b); [Field decision register:L138](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/03-ticket-domain-contract.md#L138) | READY FOR REVIEW; Draft/validation/snapshot/numbering decisions chưa chốt |
| Contract lifecycle/visibility | [TC-261 draft PR #18](https://github.com/supportflow-ai/ai-it-service-desk/pull/18), commit [64341ce](https://github.com/supportflow-ai/ai-it-service-desk/commit/64341ce03a1b872d0ebc9c6299773e90cc7365d1); [Lifecycle proposal:L86](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/04-ticket-lifecycle-policy.md#L86) | BLOCKED bởi enum compatibility và missing commands |
| Architecture review | [TC-264 draft PR #19](https://github.com/supportflow-ai/ai-it-service-desk/pull/19), commit [04e4666](https://github.com/supportflow-ai/ai-it-service-desk/commit/04e466682fb8b585a0be11edc23f1cae81d3dbb1) | Naming/port/extensibility review pending |
| Domain | [Ticket.cs:L21](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs#L21) | Creation invariants hiện có; initial Submitted, không phải target Draft |
| DB mapping/migrations | [ApplicationDbContext.cs:L81](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs#L81); tickets + paging index migrations | Number required; snapshot nullable; không có Identity migration/model snapshot trong tree inspected |
| API DTO/endpoint | [TicketEndpoints.cs:L66](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L66); [PagedTicketResult:L3](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Application/Ticketing/Dtos/PagedTicketResult.cs#L3) | Create + own list + detail có source; Draft/Submit DTO/routes chưa có |
| Frontend | [Frontend ticket types:L6](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/features/tickets/types.ts#L6); [Requester list API client:L8](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts#L8) | FAIL contract alignment: numeric meanings2–6 khác backend, list client còn array |
| Tests | 33 non-integration tests PASS | Overall gate FAIL CS8602; no PostgreSQL/E2E run |
| Reviewer/QA sign-off | Jira TC-199 In review; TC-204 QA In Progress | PENDING; không dùng Jira status thay proof |

Task cha tham chiếu subtask PR/commit cố định chưa merge, không nhập thay đổi của chúng vào nhánh này.

## 2. Những điểm đã cải thiện so với audit cũ

PR #12 đã thêm domain factory và tests; PR #13 đã thêm own-list paging/index/test cases. Không tiếp tục ghi rằng dev hoàn toàn thiếu domain invariants hoặc GET /tickets/me. Chúng chưa khép kín frontend/DB/QA acceptance.

## 3. Blocker register và handoff

| ID | Blocker | Owner / evidence cần trước khi đóng |
|---|---|---|
| TCA-01 | Draft vs Submitted và enum numbering mâu thuẫn contract | Đức Anh review decision với Backend/Frontend; approved contract + safe migration/wire plan |
| TCA-02 | Frontend array response và status decode không khớp backend | Frontend phối hợp Backend, regression/paged-list/status roundtrip proof |
| TCA-03 | Replay cache thiếu requester; Agent detail global | Backend enforcement/atomic persistence; QA positive/negative real-DB tests; PR #5 chưa merge |
| TCA-04 | CS8602 integration project | Backend/QA sửa nullable test và rerun whole solution, không hạ warnings-as-errors |
| TCA-05 | Fresh Identity migrations/snapshot chưa có trong inspected tree | Backend migrations; QA clean/upgrade/rollback PostgreSQL proof |
| TCA-06 | Department snapshot source và target lifecycle guards chưa có | Backend/Architect chốt provenance và scoped future capabilities; không fabricate fields |

Seeder gọi MigrateAsync trước role lookup: [Identity migration startup:L34](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs#L34). Thiếu migration là source-level risk hiện tại; lỗi 42P01 trong PR #5 body là historical report ở nhánh khác, không phải reproduction trên dev hôm nay.

## 4. Quyết định review

**BLOCKED / PENDING REVIEW**: chưa có closed-loop equality giữa approved contract, code, DB, API, frontend và tests. Không đề xuất đóng TC-199 lúc còn những blocker này. Jira In review được giữ nguyên; đây là review deliverable, không nhận thêm implementation ownership.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #9](https://github.com/supportflow-ai/ai-it-service-desk/pull/9): merged, base dev, head docs/ticket-domain-lifecycle; merge SHA bee4e2dc07ecdbf44677db2905b945adb9f6d88d.
- [PR #12](https://github.com/supportflow-ai/ai-it-service-desk/pull/12): merged, base dev, head feature/tc-262-263-ticket-domain-invariants; merge SHA fe41eecf920d59010e0d89e72079ab84edb32488.
- [PR #13](https://github.com/supportflow-ai/ai-it-service-desk/pull/13): merged, base dev, head feature/tc-269-270-requester-ticket-queries; merge SHA 249608e24b1bf4c64773f18bcb734fd529c1d554.
- [PR #5](https://github.com/supportflow-ai/ai-it-service-desk/pull/5): not merged, base dev, head fix/p0-idempotency-isolation; head SHA 0ec26ebc778b8f202170882fa663caa541dbe566, draft.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch audit/tc-199-ticket-contract-acceptance chỉ bổ sung deliverable hiện tại của TC-199.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
