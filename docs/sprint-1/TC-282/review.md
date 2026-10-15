# TC-282 — Chốt Definition of Ready/Done và review rule

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: Nguồn scaffold DoD và đề xuất DoR/DoD/review cho Sprint 1. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Nguồn DoD đã tìm được

Nguồn mà governance record tham chiếu có trong local ignored context pack. Đưa bản byte-identical vào [source-scaffold-definition-of-done.md](source-scaffold-definition-of-done.md), cùng [source-testing-and-quality.md](source-testing-and-quality.md); SHA-256/provenance trong manifest. Không còn chỉ dẫn người review tới đường dẫn không có trong Git.

Đây là **scaffold scope/DoD**, out-of-scope Ticket features và Sprint 1 business flows; câu “không business endpoint” thuộc scaffold stage. Không áp dụng máy móc câu đó để loại bỏ Ticket endpoints đã merge. Không có upstream commit/approval cho local source được xác minh; snapshot không tự trở thành signed Sprint 1 policy. Confluence search theo exact filename không trả kết quả; GitHub context-pack repo 404 chỉ chứng minh inaccessible tại thời điểm lookup.

## 2. Definition of Ready — đề xuất cho Sprint 1

Trước implementation: issue có owner/sprint/outcome/scope/AC; input dependency rõ và accessible; contract source/version rõ, phân biệt verified/proposed/TBD; unresolved enum/schema/security decision có decision owner, không âm thầm chọn; có positive/negative scenarios và môi trường test; mỗi issue có branch và PR riêng.

Tài liệu/review task được Ready khi có nguồn và câu hỏi/blocker đủ cụ thể cho reviewer; không bắt buộc implementation của người khác hoàn thành mới viết review.

## 3. Definition of Done — đề xuất theo loại deliverable

| Loại task | Điều kiện DoD đề xuất | Không dùng làm bằng chứng Done |
|---|---|---|
| Tài liệu/review | Nội dung đáp ứng issue; source SHA/line/provenance và review date; findings/test limits; reviewer approval; PR merge theo policy | Nhãn APPROVED tự ghi, Jira Done đơn độc, draft PR tồn tại |
| Implementation | Approved contract; scoped code; required build/tests PASS; real PostgreSQL cho migration/sequence/xmin/atomicity; relevant negative/security scenarios; reviewer approval | SQLite/compile-only thay PostgreSQL, ignored failure, main tests thay dev |
| Parent acceptance | Dependency evidence được review; contract→domain→DB→API→FE→tests khép kín; blockers xử lý hoặc explicit authorized disposition | Children Done nhưng parent mismatch chưa resolve |
| Sprint acceptance | Goal/AC rõ; clean-environment auth/create/list/detail; ownership/security baseline; QA report theo SHA/environment; reviewed defects/carry-over; PM decision | Historical accepted-with-carry-over note khi current gates chưa đạt |

Doc-only PR không tự chạy lại full suite trên mỗi nhánh giống hệt code. Baseline snapshot dùng chung phải đúng code SHA và phạm vi; nếu code thay đổi, refresh relevant evidence. Build failure của baseline không làm review note vô ích, nhưng phải hiển thị blocker và chưa đánh dấu implementation/release DoD.

## 4. Review và Git rules — pending team approval

Một issue → một branch → một PR vào dev; branch từ latest dev, conventional commit/PR title có một primary Jira ID. Related issue IDs chỉ là dependency/evidence. Historical grouped PRs giữ nguyên, không empty commit/cherry-pick lại để giả branch isolation.

Reviewer kiểm scope/AC, contract compatibility, authority/privacy, tests thực chạy và limitations. Proposed tối thiểu một approval từ reviewer chuyên môn độc lập; RBAC cần Backend + Frontend review, domain cần Backend, Sprint acceptance cần QA input và PM review. Tác giả không tự ký thay reviewer. Đây là đề xuất tổ chức review, không chứng minh branch protection đã bật.

Manifest có Jira/owner/sprint/branch/base_sha/date/sources/results/blockers/acceptance. Mỗi required gate ghi PASS/FAIL/NOT RUN + reason. CI Jenkins source: [Current Jenkins gate:L35](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/Jenkinsfile#L35) chỉ exclude integration ở execution và chưa có explicit integration stage; cần DevOps review gate trước release. Không sửa pipeline/protection trong task này.

## 5. Review checklist hiện tại

Scaffold source accessible: PASS. Sprint 1 policy extension: PENDING REVIEW. Current solution gate: FAIL CS8602; PostgreSQL/runtime foundation: NOT RUN. Historical governance label [Historical governance record:L12](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/docs/architecture/08-sprint-acceptance-and-governance-record.md#L12) không xác nhận các gate này.

Kết luận: nguồn DoD đã được khôi phục thành artifact tracked; DoR/DoD và review extension sẵn sàng để team duyệt. Giữ Jira Done, acceptance hồ sơ mới pending_review.

## Lịch sử và Evidence Manifest

- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch docs/tc-282-definition-of-done chỉ bổ sung deliverable hiện tại của TC-282.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
