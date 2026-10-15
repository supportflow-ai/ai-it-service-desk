# TC-285 — Acceptance review và quyết định carry-over

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **To Do**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Sprint 1 acceptance assessment, defects, carry-over và retro inputs. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Sprint Goal/AC và phạm vi thời gian

Đây là rà soát ngày 2026-10-15, không backdate thành biên bản cuối Sprint tháng 9. Jira Sprint 1 id6 vẫn active với goal rỗng trong dữ liệu trả về, start/end đã ở tháng9; không tự đóng sprint hoặc tạo goal chính thức.

Tiêu chí acceptance lấy từ [TC-204](https://evolving-kg-drift.atlassian.net/browse/TC-204): Requester login → create ticket → list → detail, wrong owner bị chặn; auth + vertical slice end-to-end và security baseline pass. Đây là parent outcome, không phải nội dung Sprint.goal đã được điền.

## 2. Acceptance matrix theo evidence hiện tại

| Goal / gate | Evidence | Kết luận |
|---|---|---|
| Auth/token foundation | Policy/JWT source; 9 frontend regression tests có Auth DTO coverage | SOURCE VERIFIED; HTTP security/E2E NOT RUN |
| Ticket creation invariants | PR #12, Domain15/Application12 PASS trên dev | PASS current Submitted baseline; Draft contract unresolved |
| Requester list/detail | PR #13 source own paging; Application tests PASS | UI/E2E BLOCKED: [Requester list API client:L8](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts#L8) còn array; backend paged object |
| Status rendering | [Persisted backend enum:L3](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs#L3) vs [Frontend ticket types:L6](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/features/tickets/types.ts#L6) | FAIL semantic alignment codes2–6 |
| Wrong-owner security | Own query source/unit tests; global replay key ở [TicketEndpoints.cs:L36](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs#L36) | BLOCKED: no PostgreSQL HTTP run; replay/Agent scope gap |
| Backend required gate | 33 tests PASS; solution command exit1, CS8602 | FAIL, không báo toàn backend green |
| Frontend compile/lint | Build/lint PASS, 9/9 regression PASS | PASS trong phạm vi compile/contract tests |
| Clean-environment E2E / QA regression | TC-283 In Progress, TC-284 In Progress; đều assignee VÕ NGUYỄN ANH KIỆT, 0 comments | PENDING QA REPORT, không nhận ownership QA |
| Contract acceptance | [TC-199 draft PR #20](https://github.com/supportflow-ai/ai-it-service-desk/pull/20), commit [04132fc](https://github.com/supportflow-ai/ai-it-service-desk/commit/04132fc9479541c48f2ae172c974d06a2fd2e848) | BLOCKED / PENDING REVIEW |
| AI/HITL readiness | [TC-277 draft PR #21](https://github.com/supportflow-ai/ai-it-service-desk/pull/21), commit [de028f4](https://github.com/supportflow-ai/ai-it-service-desk/commit/de028f417d4876284a96bf24de7bda0d1b6b7fca) | Main-only filtered test evidence; dev integration missing; không tự thêm AI vào S1 acceptance gate |
| Governance rules | [TC-282 draft PR #22](https://github.com/supportflow-ai/ai-it-service-desk/pull/22), commit [6bce37e](https://github.com/supportflow-ai/ai-it-service-desk/commit/6bce37eda94d9a6b85bdb7579c8b9dd454746f78) | Source recovered; Sprint 1 extension chưa sign-off |

QA TC-204 comment10044 ghi đang chờ TC-199; public comment đọc trực tiếp ngày audit. Có dependency vòng giữa contract acceptance và QA: tách approved baseline contract khỏi requirement có đủ runtime QA proof, phối hợp reviewer để mở vòng, không tự đóng một bên. Snapshot QA metadata/comment nằm trong qa-evidence.json.

## 3. Defect và carry-over register — đề xuất, chưa đổi sprint

| ID | Finding / mức ưu tiên đề xuất | Handoff owner | Evidence cần để hoàn tất |
|---|---|---|---|
| S1-01 | P0 global idempotency cross-owner replay | Backend Core + QA; PR #5 còn draft | Reviewed fix vào dev, atomic/requester isolation real-DB negatives |
| S1-02 | P1 FE list paged-object và enum decode mismatch | Frontend + Backend | Reviewed wire contract, requester list/detail/status integration proof |
| S1-03 | P1 CS8602 integration project | Backend/QA | Full solution build/test green, không disable warning rule |
| S1-04 | P1 Agent broad detail, target resource-scope thiếu | Backend/Architect + QA | Scope model/handlers và outside-scope denial proof |
| S1-05 | P1 fresh Identity schema/migration risk | Backend Core + QA | PostgreSQL clean/upgrade/rollback evidence; historical PR #5 schema failure không được bỏ qua |
| S1-06 | Contract Draft/Submitted/enum decisions chưa duyệt | Đức Anh review + Backend/Frontend | Signed decision/compatibility plan, cập nhật approved source |
| S1-07 | E2E/regression report chưa có tại inspected QA issues | QA owner | TC-283/284 evidence theo dev SHA/environment và actual security results |

Không ấn định ngày sprint/owner transition hoặc chuyển issue sang sprint khác bằng tài liệu này. Các owner trên là handoff theo role/issue hiện có, không giao thêm ticket hay nhắn cho họ.

## 4. Retro inputs

Grouped PRs khiến traceability không một-một; main/dev divergence khiến evidence ở nhánh khác bị hiểu nhầm là integrated; nhiều tài liệu APPROVED/proposed mâu thuẫn; green typecheck không bắt wire semantics; QA chờ contract trong khi contract closure chờ QA. Đề xuất dùng pinned manifests, một issue/PR, distinct contract approval vs runtime acceptance, và môi trường PostgreSQL reproducible.

## 5. Quyết định nghiệm thu đề xuất

**BLOCKED — PENDING QA EVIDENCE AND HUMAN REVIEW.** Có tiến bộ về source/invariants/frontend gates nhưng chưa có auth/create/list/detail/security end-to-end evidence trên dev baseline. Không xác nhận “ACCEPTED WITH CARRY-OVER” chỉ từ [Historical governance record:L12](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/08-sprint-acceptance-and-governance-record.md#L12). Carry-over table là proposal cần PM/QA review; Jira TC-285 vẫn To Do và không bị sửa.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #5](https://github.com/supportflow-ai/ai-it-service-desk/pull/5): not merged, base dev, head fix/p0-idempotency-isolation; head SHA 0ec26ebc778b8f202170882fa663caa541dbe566, draft.
- [PR #12](https://github.com/supportflow-ai/ai-it-service-desk/pull/12): merged, base dev, head feature/tc-262-263-ticket-domain-invariants; merge SHA fe41eecf920d59010e0d89e72079ab84edb32488.
- [PR #13](https://github.com/supportflow-ai/ai-it-service-desk/pull/13): merged, base dev, head feature/tc-269-270-requester-ticket-queries; merge SHA 249608e24b1bf4c64773f18bcb734fd529c1d554.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-285-sprint1-acceptance chỉ bổ sung deliverable hiện tại của TC-285.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
