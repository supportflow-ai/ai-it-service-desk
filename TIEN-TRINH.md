# TIẾN TRÌNH DỰ ÁN — BACKEND (ai-it-service-desk)

> **Cập nhật:** 20/10/2026  
> **Nhánh phát triển:** `feature/ticket-lifecycle-workflow` (dựa trên `origin/dev`)  
> **Trạng thái:** Toàn bộ 319 automated tests trên Solution đều **PASS 100%**.

---

## 1. TỔNG QUAN CÁC MODULE

| Module | Nghiệp vụ | Trạng thái | Chi tiết |
|---|---|:---:|---|
| **1. Identity & Auth** | Đăng ký, đăng nhập, JWT, RBAC | ✅ **Hoàn thành** | Đã hoàn thiện từ PR #26: `/api/auth/register`, `/api/auth/login`, `/api/auth/me`. |
| **2. Ticketing Lifecycle** | Quản lý vòng đời Ticket | ✅ **Hoàn thành** | Đã hoàn thiện: tạo vé sinh mã `IT-YYYY-NNNN`, Idempotency CAS lock, phân trang, và bộ 6 lệnh chuyển trạng thái (`claim`, `resolve`, `reopen`, `close`, `cancel`, `update`). |
| **3. Knowledge Base** | Bài viết IT & hướng dẫn nội bộ | ⏳ **Chưa làm** | Tiếp theo: CRUD bài viết, danh mục (chưa cần AI). |
| **4. Analytics & SLA** | Thống kê số lượng & thời gian xử lý | ⏳ **Chưa làm** | Tiếp theo: Đo deadline theo Priority, cảnh báo trễ hạn SLA. |
| **5. AI Assistant & Triage** | AI phân loại & gợi ý giải pháp | ⏸️ **Chờ AI-BE** | Sẵn sàng với Domain Contract & NoOp fallback. |

---

## 2. CHI TIẾT TÍNH NĂNG MỚI ĐÃ BỔ SUNG TRÊN NHÁNH NÀY

### Vòng đời Ticket (State Transitions)
Triển khai State Machine đầy đủ không làm thay đổi database schema (giữ nguyên quy tắc ADR-014):
```text
Submitted ──(Agent Claim)──► InProgress ──(Agent/Admin Resolve)──► Resolved ──(Close)──► Closed
                                                                      │
                                                                      └──(Reopen)──► InProgress
Submitted ──(Cancel)──► Cancelled
```

### Danh sách Endpoint Ticket hiện hành:
- `POST /api/tickets`: Tạo vé mới (`Submitted`), kèm `Idempotency-Key` và sequence number `IT-YYYY-NNNN`.
- `GET /api/tickets/me`: Danh sách vé của Requester hiện tại (kèm phân trang an toàn).
- `GET /api/tickets/{id}`: Xem chi tiết vé (Requester xem vé của mình; Agent/Admin xem toàn hệ thống).
- `POST /api/tickets/{id}/update`: Chủ vé cập nhật tiêu đề, mô tả, danh mục khi vé ở trạng thái `Submitted`.
- `POST /api/tickets/{id}/claim`: Agent nhận xử lý vé (`Submitted` $\rightarrow$ `InProgress`).
- `POST /api/tickets/{id}/resolve`: Agent hoặc Admin đánh dấu vé đã xử lý (`InProgress` $\rightarrow$ `Resolved`).
- `POST /api/tickets/{id}/reopen`: Chủ vé hoặc Admin mở lại vé (`Resolved` $\rightarrow$ `InProgress`).
- `POST /api/tickets/{id}/close`: Chủ vé hoặc Admin đóng hoàn tất vé (`Resolved` $\rightarrow$ `Closed`).
- `POST /api/tickets/{id}/cancel`: Chủ vé hoặc Admin hủy vé (`Submitted` $\rightarrow$ `Cancelled`).

---

## 3. KIỂM THỬ TỰ ĐỘNG

| Project | Số lượng test | Kết quả |
|---|---|:---:|
| `ServiceDesk.Domain.Tests` | 118 | ✅ **PASS** |
| `ServiceDesk.Application.Tests` | 69 | ✅ **PASS** |
| `ServiceDesk.ArchitectureTests` | 26 | ✅ **PASS** |
| `ServiceDesk.IntegrationTests` | 106 | ✅ **PASS** (chạy trên PostgreSQL 17 thật qua Testcontainers) |
| **Tổng cộng** | **319** | ✅ **100% PASS** |
