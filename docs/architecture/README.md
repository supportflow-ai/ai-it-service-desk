> Current backend boundaries: [ADR-011](../adr/ADR-011-backend-module-boundaries.md),
> [AI ADR-012](../adr/ADR-012-ai-contract-and-future-transport.md) and the
> [rollout runbook](../runbooks/backend-refactor-rollout.md).
> The lifecycle/SLA material below describes planned product capabilities. This refactor
> preserves existing Submitted creation/API behavior and defers those state transitions.

# Architecture Contracts & Technical Governance Index

Chào mừng đến với thư mục **Architecture Contracts & Governance** của dự án **AI-Assisted Internal IT Service Desk** (`supportflow-ai/ai-it-service-desk`).

Thư mục này là **Nguồn Chân Lý Kỹ Thuật Duy Nhất (Single Source of Technical Truth)**, chứa các hợp đồng kiến trúc (Technical Contracts), ma trận phân quyền, đặc tả vòng đời và các quyết định kiến trúc được chuẩn hóa bởi **Đức Anh (Leader / Architect / PM)**.

---

## 1. Nguyên tắc Pháp lý & Thứ tự Ưu tiên Kỹ thuật (Precedence Hierarchy)

Khi phát sinh bất kỳ sự mâu thuẫn hay không nhất quán nào giữa các nguồn tài liệu trong dự án, toàn bộ thành viên (Backend, Frontend, QA, AI Engineer) bắt buộc phải tuân theo thứ bậc ưu tiên sau:

$$\mathbf{Implementation\ Specification\ \&\ Architecture\ Contracts} \ \ > \ \ \mathbf{OpenAPI\ Spec} \ \ > \ \ \mathbf{ERD\ Schema} \ \ > \ \ \mathbf{Jira\ Backlog\ Stories}$$

*Mọi thay đổi đối với hợp đồng kỹ thuật đều phải được đề xuất qua Change Request (CR) bằng Pull Request vào thư mục `docs/architecture/` trước khi tiến hành viết code.*

---

## 2. Bảng Danh mục Hợp đồng Kiến trúc (Architecture Contracts Matrix)

| Mã hợp đồng | Tệp đặc tả kỹ thuật | Jira Issues liên quan | Sprint | Nhóm chuyên môn | Trọng số ưu tiên |
|---|---|---|---|---|---|
| **ARCH-CONTRACT-01** | [`01-rbac-and-api-permission-matrix.md`](./01-rbac-and-api-permission-matrix.md) | `TC-196`, `TC-246`, `TC-247` | Sprint 1 | Identity & Security | P0 — Core Security |
| **ARCH-CONTRACT-02** | [`02-ticket-domain-and-field-contract.md`](./02-ticket-domain-and-field-contract.md) | `TC-260`, `TC-199` | Sprint 1 | Ticket Domain | P0 — Blocker |
| **ARCH-CONTRACT-03** | [`03-ticket-lifecycle-and-visibility-baseline.md`](./03-ticket-lifecycle-and-visibility-baseline.md) | `TC-261`, `TC-199` | Sprint 1 | Ticket Lifecycle | P0 — Blocker |
| **ARCH-CONTRACT-04** | [`04-ticket-state-transition-and-reopen-matrix.md`](./04-ticket-state-transition-and-reopen-matrix.md) | `TC-299`, `TC-300`..`302` | Sprint 2 | State Machine | P0 — Highest |
| **ARCH-CONTRACT-05** | [`05-comment-and-internal-note-privacy-contract.md`](./05-comment-and-internal-note-privacy-contract.md) | `TC-303`, `TC-304`..`307` | Sprint 2 | Privacy & Collaboration | P0 — Highest Security |
| **ARCH-CONTRACT-06** | [`06-ticket-assignment-policy.md`](./06-ticket-assignment-policy.md) | `TC-294`, `TC-295` | Sprint 2 | Helpdesk Operations | P1 — High |
| **ARCH-CONTRACT-07** | [`07-attachment-security-limits-and-policy.md`](./07-attachment-security-limits-and-policy.md) | `TC-308`, `TC-309`..`311` | Sprint 2 | Storage & Security | P1 — High |
| **ARCH-CONTRACT-08** | [`08-sprint-acceptance-and-governance-record.md`](./08-sprint-acceptance-and-governance-record.md) | `TC-199`, `TC-264`, `TC-277`, `TC-282`, `TC-285`, `TC-326` | S1 & S2 | QA, PM & Release Governance | P0 — Governance |

---

## 3. Bản đồ Handoff Triển khai cho Các Đội ngũ

### 3.1 Đối với Backend Core Team
1. **P0 Refactoring:** Dừng ngay việc mở rộng lifecycle trên `TicketStatus` cũ (0-6). Đọc ngay `ARCH-CONTRACT-02` và `ARCH-CONTRACT-03` để sửa `CreateTicketAsync` thành tạo `Draft` và thêm lệnh `Submit`.
2. **State Machine:** Triển khai 11 lệnh chuyển trạng thái theo bảng ma trận tại `ARCH-CONTRACT-04`.
3. **Data Protection:** Cài đặt bộ lọc truy vấn database chặn hoàn toàn `Internal Note` đối với Requester theo `ARCH-CONTRACT-05`.
4. **File Storage:** Cấu hình MinIO bucket private và tích hợp sinh Presigned URL (15m TTL) theo `ARCH-CONTRACT-07`.

### 3.2 Đối với QA & Security Team
1. Sử dụng trực tiếp các Negative Test Cases được định nghĩa chi tiết trong từng Contract làm Test Scenario cho đợt Regression Testing.
2. Kiểm tra chặt chẽ điều kiện nghiệm thu tại `ARCH-CONTRACT-08` trước khi ký duyệt Acceptance cho Sprint 1 (`TC-285`) và Sprint 2 (`TC-326`).
