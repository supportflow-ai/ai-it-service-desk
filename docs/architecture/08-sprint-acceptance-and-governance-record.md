> **Tài liệu lịch sử — không dùng làm chỉ dẫn triển khai hiện hành.**
> Phạm vi bản gốc: hợp đồng ngày 2026-09-29. Chú thích bổ sung ngày 2026-10-19; không sửa nội dung hay ký duyệt lại bản gốc.
> Nguồn hiện hành: [hợp đồng backend](current-backend-contract.md), [quy tắc bảo mật](security-design-rules.md), [quyết định còn mở](pending-business-decisions.md).
> Kết luận nghiệm thu dưới đây chỉ là bản ghi lịch sử, không xác nhận HEAD hoặc trạng thái Jira hôm nay. Hồ sơ TC-285 có đánh giá khác tại SHA/ngày khác; đối chiếu DEC-08 và hồ sơ Sprint 1. Không làm theo lệnh merge/push hoặc thay enum trong bản gốc từ nhiệm vụ chỉnh tài liệu.

<!-- original-record -->
# TC-199, TC-285, TC-326: Sprint Acceptance, Evidence Audit & Governance Record

- **Document ID:** `ARCH-CONTRACT-08`
- **Jira Issues Covered:**
  - `TC-199`: Chốt Ticket domain model và requester visibility rules (Parent Acceptance)
  - `TC-264`: Architecture review Ticket domain (Evidence Audit)
  - `TC-277`: Review AI scope và HITL boundary (Evidence Audit)
  - `TC-282`: Chốt Definition of Ready/Done và review rule (Evidence Audit)
  - `TC-285`: Sprint 1 Acceptance Review và quyết định Carry-over
  - `TC-326`: Sprint 2 Acceptance + Risk Review Framework
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / GOVERNANCE RECORD
- **Date:** 2026-09-29
- **Target Audience:** Toàn bộ thành viên dự án, Scrum Master, QA Lead, Tech Leads

---

## 1. Rà soát Evidence các Task đã Done (TC-264, TC-277, TC-282)

Theo quy định quản lý chất lượng và `DoD` (Definition of Done) đã chốt tại `TC-282`, một task không được coi là hoàn thành chỉ qua lời nói mà phải có bằng chứng kỹ thuật (Technical Evidence) liên kết rõ ràng:

| Jira Issue | Tiêu đề | Trạng thái hiện tại | Bằng chứng kỹ thuật đối soát (Technical Evidence) | Kết luận đối soát |
|---|---|---|---|---|
| **TC-264** | Architecture review Ticket domain | Done | - Báo cáo rà soát kiến trúc Clean Architecture & Modular Monolith trong `ai-it-service-desk-claude-context-pack/docs/02-architecture-baseline.md`<br>- Phân tích ranh giới Domain vs Application trong `ADR-001`, `ADR-002`<br>- Xác lập các Invariant của Ticket tại `ARCH-CONTRACT-02`, `ARCH-CONTRACT-03`. | **HỢP LỆ & ĐỦ ĐIỀU KIỆN ĐÓNG** |
| **TC-277** | Review AI scope và HITL boundary | Done | - PR #1 (`feature/tc-275-276-277-ai-triage-contract`) đã merge vào main (commit `b529b2c`, `e044f47`)<br>- Xác lập rõ ranh giới HITL (Human-in-the-Loop): AI chỉ gợi ý phân loại (Category, Impact, Urgency), kỹ thuật viên quyết định chấp thuận/ghi đè<br>- Bộ kiểm thử `AIAssistance/ConfidencePolicyTests.cs`, `TriagePolicyTests.cs` (100% pass). | **HỢP LỆ & ĐỦ ĐIỀU KIỆN ĐÓNG** |
| **TC-282** | Chốt Definition of Ready/Done và review rule | Done | - Tài liệu quy định DoR/DoD tại `ai-it-service-desk-claude-context-pack/docs/08-scaffold-scope-and-definition-of-done.md`<br>- Quy trình code review, kiểm thử bắt buộc (Unit + Integration Testcontainers), quy định cấm merge direct vào dev/main. | **HỢP LỆ & ĐỦ ĐIỀU KIỆN ĐÓNG** |

---

## 2. Tiêu chuẩn Nghiệm thu Đóng Task cha Ticket Domain (TC-199)

Jira issue `TC-199` là task cha (Parent Issue) của toàn bộ phân hệ Ticket Domain trong Sprint 1. Task này chỉ được phép chuyển sang `Done` khi chuỗi liên kết sau đây hoàn toàn khớp nhau (Không còn bất kỳ điểm gãy nào):

$$\text{Contract} \longrightarrow \text{Code (Domain Entity)} \longrightarrow \text{DB Mapping (EF Core)} \longrightarrow \text{API DTO} \longrightarrow \text{FE Type} \longrightarrow \text{Tests}$$

### Bảng Kiểm Tra Đối Soát Khép Kín (Closed-Loop Acceptance Checklist)
1. **Contract:** `ARCH-CONTRACT-02` (Fields) & `ARCH-CONTRACT-03` (Lifecycle) đã được phê duyệt.
2. **Domain Entity:** `Ticket` entity trong `ServiceDesk.Domain/Ticketing` phản ánh đúng các thuộc tính, `Status` enum 0-8, không có `Cancelled`.
3. **DB Mapping:** Migration EF Core cấu hình `TicketNumber` nullable cho Draft, sequence an toàn khi submit, `RequesterDepartmentId` snapshot.
4. **API DTO:** Tách biệt `CreateTicketDraftRequest` và `SubmitTicketRequest`.
5. **Frontend Type:** `TicketStatus` trong `src/types/ticket.ts` đồng bộ chính xác enum 0-8.
6. **Tests:** Kiểm thử Domain Invariants (`ServiceDesk.Domain.Tests/Ticketing`) đạt 100% pass rate.

---

## 3. Biên bản Nghiệm thu Sprint 1 & Quyết định Carry-over (TC-285)

Biên bản này được lập bởi Đức Anh (Lead/PM) để đánh giá kết quả thực tế của Sprint 1 theo 5 mục chuẩn mực:

### 3.1 Goal (Mục tiêu Sprint 1)
- Xây dựng nền tảng xác thực (Authentication), phân quyền (Identity/RBAC), và vertical slice đầu tiên của Ticket (Create Draft/Submit/List/Detail) tuân thủ Clean Architecture.

### 3.2 Passed (Các hạng mục đã hoàn thành đạt chuẩn)
- [x] Hạ tầng scaffold, Docker Compose, NGINX reverse proxy, MinIO, PostgreSQL pgvector.
- [x] Kiến trúc kiểm thử Testcontainers PostgreSQL chạy ổn định trên CI.
- [x] Hoàn thành phân hệ Identity (TC-251 đến TC-255, PR #2): Đăng ký, đăng nhập, cấp phát JWT token, phân quyền 3 role mặc định (`Requester`, `Agent`, `Admin`).
- [x] Hoàn thành hợp đồng AI Triage và HITL boundary (TC-275, TC-276, TC-277).
- [x] Hoàn thành các hợp đồng kiến trúc Sprint 1 (`TC-246`, `TC-247`, `TC-260`, `TC-261`).

### 3.3 Failed & Contract Gaps (Các sai lệch và lỗi kỹ thuật phát hiện)
- [!] **P0-1 Initial status:** Backend `CreateTicketAsync` đang tự ý set `Status = Submitted` thay vì `Draft`.
- [!] **P0-2 TicketStatus enum:** Backend đang thiếu các trạng thái `Triaged`, `Assigned`, `PendingExternal`, gây nguy cơ lệch dữ liệu với Frontend.
- [!] **P0-4 Resource authorization:** Endpoint tra cứu ticket chưa ràng buộc kiểm tra Team / AssignedAgent đối với Agent.
- [!] `AISD-S1-09-T02` (Regression/security work package của QA) chưa hoàn tất trên môi trường tích hợp.

### 3.4 Carry-over (Hạng mục chuyển giao bắt buộc sang Sprint 2)
1. **Sửa đổi toàn diện P0 Contract Drift của Backend:** Refactor `TicketService` theo đúng `ARCH-CONTRACT-02` (tạo Draft, lệnh Submit riêng, đồng bộ enum 0-8).
2. **Khép kín QA Regression Package `AISD-S1-09-T02`:** Chạy tự động trên môi trường tích hợp có Testcontainers.

### 3.5 Decision (Quyết định của PM/Lead)
$$\mathbf{ACCEPTED\ WITH\ CARRY\text{-}OVER}$$
- Sprint 1 được nghiệm thu về mặt nền tảng kiến trúc và Identity.
- Các hạng mục lệch contract (P0-1 đến P0-4) được phong tỏa và coi là điều kiện tiên quyết (Prerequisite Blocker) phải giải quyết ngay trong 2 ngày đầu Sprint 2 trước khi Backend tiến hành code lifecycle chuyên sâu.

---

## 4. Khung Đánh giá Rủi ro & Nghiệm thu Sprint 2 (TC-326 Framework)

`TC-326` là chốt chặn cuối cùng của Sprint 2, chỉ được kích hoạt vào ngày cuối Sprint (dự kiến 03/10/2026).

### 4.1 Điều kiện Tiên quyết để Mở đợt Review (Pre-conditions)
- Tất cả các PR của Sprint 2 (Lifecycle, Assignment, Comment, Attachment) đã được merge vào `dev`.
- Không có lỗ hổng bảo mật P0/P1 nào đang mở.

### 4.2 Bộ Tiêu chí Đánh giá Nghiệm thu Sprint 2
1. **Lifecycle E2E Pass:** Ticket đi thông suốt qua 9 trạng thái mà không phát sinh lỗi bất thường.
2. **Zero Internal Note Leakage:** Kiểm thử thâm nhập tự động xác nhận Requester không thể thấy Internal Note dưới mọi hình thức API.
3. **Attachment Security:** MinIO lưu trữ tệp private, upload chặn 100% file thực thi và file quá 10MB, link download presigned hết hạn sau 15 phút.
4. **AI HITL & Degraded Mode:** Khi AI provider gặp sự cố hoặc timeout, hệ thống tự động fallback về xử lý thủ công (NoOp/Degraded) mà không làm gián đoạn luồng xử lý ticket của Agent.

---

## 5. Kế hoạch Xử lý 3 Rủi ro Quy trình & Git Branching (Leader Governance Plan)

Bên cạnh các task Jira, Đức Anh với vai trò Leader/Architect chính thức đưa ra phương án giải quyết 3 rủi ro quy trình nghiêm trọng được phát hiện:

### Rủi ro 1: Nhánh `dev` và `main` bị phân kỳ (Diverged Branches)
- **Hiện trạng:**
  - PR #1 (`feature/tc-275-276-277-ai-triage-contract`) bị merge nhầm trực tiếp vào `main`.
  - PR #2 (`feature/core-ticket-lifecycle`) được merge vào `dev`.
  - Kết quả: `dev` ahead `main` 25 commits nhưng behind `main` 2 commits.
- **Hành động khắc phục (Remediation Plan):**
  1. Tạo PR đồng bộ (Sync PR) đưa các commit từ `main` về `dev`: `git checkout dev && git merge origin/main --no-ff -m "chore(git): reconcile diverged commits from main into dev"`.
  2. Giải quyết xung đột nếu có, chạy toàn bộ suite test và push lên `dev`.

### Rủi ro 2: Thiếu nhánh `staging` trong quy trình Release
- **Hiện trạng:** Repo chỉ có `dev`, `main` và các nhánh feature/frontend; thiếu nhánh `staging` theo chuẩn kiến trúc `dev → staging → main`.
- **Hành động khắc phục:**
  1. Tạo nhánh `staging` phân nhánh từ `dev` sau khi đợt close-out Sprint 1 hoàn tất.
  2. GitHub Actions kiểm tra commit vào `staging`. Triển khai tự động được hoãn sang thay đổi riêng khi có môi trường và phê duyệt triển khai; xem ADR-013.

### Rủi ro 3: `dev` và `main` chưa được bật Branch Protection
- **Hiện trạng:** Cả `dev` và `main` trên GitHub đang để `protected: false`, cho phép direct push và bypass review.
- **Hành động khắc phục (Khóa Branch Protection Rules trên GitHub):**
  - Bật *Require a pull request before merging* (Tối thiểu 1 approval từ Tech Lead).
  - Bật *Require status checks to pass before merging*: GitHub Actions job `backend` của Backend quality gates phải xanh. Repo frontend dùng `frontend`, infra dùng `smoke`; chọn checks sau khi workflow chạy thành công trên nhánh đích.
  - Bật *Do not allow bypassing the above settings* (Kể cả Admin cũng phải đi qua PR).
  - Cấm hoàn toàn `git push --force` trên `main`, `staging`, `dev`.
