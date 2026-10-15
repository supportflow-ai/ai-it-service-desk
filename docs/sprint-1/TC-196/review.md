# TC-196 — Chốt actor, RBAC và contract truy cập

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: RBAC parent review, claim/policy và API/UI access contract. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Hồ sơ task cha và dependency

| Input | Evidence riêng | Trạng thái |
|---|---|---|
| Actor/use cases | [TC-246 draft PR #14](https://github.com/supportflow-ai/ai-it-service-desk/pull/14), commit [1bd53b1](https://github.com/supportflow-ai/ai-it-service-desk/commit/1bd53b1eefea6fe80e982d1c88c40c5c6a0de8df) | Draft, chưa merge/sign-off |
| API permission matrix | [TC-247 draft PR #15](https://github.com/supportflow-ai/ai-it-service-desk/pull/15), commit [d712277](https://github.com/supportflow-ai/ai-it-service-desk/commit/d712277891da12b87295b75d98f25d6d36c773f2) | Draft, security gaps ghi rõ |
| Contract đã merge | [Actor contract:L50](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/01-actor-use-cases.md#L50) và [Permission contract:L73](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/02-rbac-permission-matrix.md#L73) | Historical PR #8 gộp hai subtask |

Các hồ sơ subtask nằm trên nhánh riêng. Task cha tham chiếu PR/commit cố định; không giả định file của chúng đã tồn tại trên dev.

## 2. API/UI access contract

| Thành phần | Hành vi verified | Điểm cần review |
|---|---|---|
| Roles | Requester/Agent/Admin trong [RoleNames.cs:L9](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Domain/Identity/RoleNames.cs#L9) | Lead chỉ proposed Agent + Dispatch grant |
| Claim names | NameIdentifier, sub, role claim trong [JWT role issuance:L37](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Identity/JwtTokenGenerator.cs#L37) | Privilege freshness/revocation không được chứng minh bằng UI logout |
| Policies | RequireRequester/Agent/Admin trong [Registered policies:L52](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Api/Program.cs#L52) | Coarse roles chưa đủ ownership/team/assignment scope |
| Requester ownership | Own filter ở [TicketService.cs:L58](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs#L58) | Replay response cũng phải cùng owner |
| UI | [UI route permissions:L101](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/routes/AppRoutes.tsx#L101) và [ProtectedRoute:L28](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/frontend/service-desk-web/src/routes/components/ProtectedRoute.tsx#L28) | Requester-only UI route trong khi API RequireRequester cho cả Agent/Admin; cần chốt personal ticket UX cho multi-role |
| Future commands | Proposed matrix có action/state/revision | Chưa implement; không cấp quyền bằng frontend placeholder |

## 3. Parent acceptance checklist

| Gate | Kết quả | Evidence / cần người review |
|---|---|---|
| Ba actor và năm use case đầu tiên có matrix | READY FOR REVIEW | Hai draft PR riêng ở trên |
| Lead mapping rõ và không tự thêm role | PASS về mô tả | Business/Backend sign-off vẫn pending |
| Claim/policy names đối chiếu source | PASS source inspection | Policy runtime tests NOT RUN |
| API/UI scopes không mâu thuẫn | BLOCKED | Personal ticket route role mismatch và thiếu staff scope |
| Negative ownership/replay enforcement | BLOCKED | Global cache; Agent broad detail |
| Required test/build gate | BLOCKED | 33 tests PASS nhưng toàn solution FAIL; PostgreSQL NOT RUN |
| Reviewer sign-off Backend + Frontend | PENDING | Không có approval mới được xác minh cho hồ sơ này |

## 4. Decision note

Đề xuất giữ ba role hiện có; chốt Lead bằng explicit team-scoped dispatch grant sau review, không tin client role/teamId. Đề xuất giữ API authority và thống nhất UI personal-ticket access với Backend/Frontend, không sửa UI/auth tại đây.

Kết luận acceptance: **BLOCKED / PENDING REVIEW**. Jira đang Done nhưng review hiện tại chưa đủ để xác nhận runtime RBAC hoặc ký duyệt thay reviewer. Historical merged PR tiếp tục được giữ nguyên; hồ sơ này là traceability bổ sung.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.
- [PR #8](https://github.com/supportflow-ai/ai-it-service-desk/pull/8): merged, base dev, head docs/tc-246-247-rbac; merge SHA 1111a9ba401bd2cadd44fbcf760c044b4af76c75.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-196-rbac-acceptance chỉ bổ sung deliverable hiện tại của TC-196.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
