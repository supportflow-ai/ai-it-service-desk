# TIẾN TRÌNH DỰ ÁN — BACKEND (ai-it-service-desk)

> **Cập nhật:** 20/10/2026  
> **Role phụ trách:** Backend Core  
> **Trạng thái hiện tại:** Hoàn thành toàn bộ **Phase 1 (Nền tảng & Kiến trúc)** và **Phase 2 (Identity & Auth + Ticketing MVP)**. Tất cả 187 automated tests đều PASS.

---

## 1. TỔNG QUAN TIẾN ĐỘ CÁC MODULE

| Module | Nghiệp vụ | Trạng thái | Chi tiết |
|---|---|:---:|---|
| **1. Identity & Auth** | Đăng ký, đăng nhập, JWT, RBAC | ✅ **Hoàn thành** | Đầy đủ API, 3 role (`Requester`, `Agent`, `Admin`), tự động seed Admin từ env. |
| **2. Ticketing Lifecycle** | Quản lý vòng đời Ticket & Comment | ✅ **Hoàn thành** | Đầy đủ 12 API, state machine chuẩn, tự tính Priority, phân quyền chặt chẽ. |
| **3. Knowledge Base** | Bài viết IT & hướng dẫn nội bộ | ⏳ **Chưa làm** | Tiếp theo: CRUD bài viết, danh mục, tìm kiếm text cơ bản (chưa cần AI). |
| **4. Analytics & SLA** | Thống kê số lượng & thời gian xử lý | ⏳ **Chưa làm** | Tiếp theo: Đo deadline theo Priority, cảnh báo trễ hạn SLA, dashboard tổng quan. |
| **5. AI Assistant & Triage** | AI phân loại & gợi ý giải pháp | ⏸️ **Chờ AI-BE** | Đã có sẵn Domain Contract & NoOp fallback. Chờ repo `ai-service` hoàn thiện để nối thật. |

---

## 2. NHỮNG GÌ ĐÃ HOÀN THÀNH CHI TIẾT

### 2.1. Kiến trúc Clean Architecture 4 lớp chuẩn
- **`ServiceDesk.Domain`**: Pure C# logic, không phụ thuộc bất kỳ thư viện ngoài nào (NetArchTest kiểm soát tự động).
- **`ServiceDesk.Application`**: Chứa use cases, DTOs, interfaces/ports (`IIdentityService`, `ITokenService`, `ITicketStore`, `ICurrentUser`, `IClock`).
- **`ServiceDesk.Infrastructure`**: EF Core 10 + Npgsql PostgreSQL, ASP.NET Core Identity, JWT token generator, MinIO client, IdentitySeeder.
- **`ServiceDesk.Api`**: Minimal API endpoints, Global Problem Details mapper (RFC 7807), cấu hình Swagger kèm JWT Bearer.
- **`ServiceDesk.Worker`**: Host background process nền tảng.

---

### 2.2. Module Identity & Authentication (Module 1)
- **Hệ thống 3 Role**:
  - `Requester`: Người dùng gửi yêu cầu IT.
  - `Agent`: Kỹ thuật viên tiếp nhận và xử lý yêu cầu.
  - `Admin`: Quản trị viên hệ thống.
- **Cơ chế cấp quyền & bảo mật**:
  - Tự đăng ký (`/auth/register`) mặc định gán role `Requester`.
  - Đăng nhập (`/auth/login`) trả về JWT Bearer Token chứa `userId`, `email`, `roles`.
  - Khởi động ứng dụng tự động kiểm tra và seed sẵn 3 role vào database.
  - Nếu cấu hình biến môi trường `ADMIN_EMAIL` và `ADMIN_PASSWORD`, hệ thống tự tạo/nâng cấp tài khoản Admin đầu tiên.
  - Chỉ `Admin` mới có quyền gọi API đổi role cho người dùng khác.
- **Danh sách API Auth**:
  - `POST /auth/register`: Đăng ký tài khoản Requester mới.
  - `POST /auth/login`: Đăng nhập, nhận JWT token.
  - `GET /auth/me`: Lấy thông tin user hiện tại kèm danh sách role.
  - `PUT /admin/users/{id}/role`: Admin cập nhật role cho user (`Requester`, `Agent`, `Admin`).

---

### 2.3. Module Ticketing Lifecycle (Module 2)
- **Domain Model**:
  - `Ticket` (Aggregate Root): `Title`, `Description`, `Category`, `Impact`, `Urgency`, `Priority`, `Status`, `RequesterId`, `AssigneeId`, timestamps.
  - `TicketComment` (Entity con): Comment công khai, bất biến (immutable - không cho sửa/xóa), ghi nhận tác giả và thời gian tạo.
- **Ma trận tính Priority tự động**:
  - Tính từ `Impact × Urgency` (P1 đến P4) theo chuẩn ITIL.
  - Client tuyệt đối **không** được tự truyền Priority.
- **State Machine chuẩn (Workflow)**:
  ```text
  Submitted ──(Agent claim / Admin assign)──► InProgress ──(Resolve)──► Resolved ──(Close)──► Closed
                                                                           │
                                                                           └──(Reopen)──► InProgress
  Submitted ──(Cancel)──► Cancelled
  ```
  - `Cancelled` và `Closed` là trạng thái kết thúc (terminal): cấm sửa đổi, cấm gán, cấm thêm comment.
  - Requester chỉ được sửa (`PATCH`) hoặc hủy (`Cancel`) ticket khi còn ở trạng thái `Submitted`.
  - Khi ticket `Resolved`, Requester có thể đóng (`Close`) hoặc mở lại (`Reopen`) nếu chưa hài lòng.
- **Phân quyền & Bảo mật dữ liệu (Data Privacy)**:
  - Requester chỉ thấy ticket của chính mình. Nếu cố tình truy cập ID ticket của người khác, hệ thống trả về **404 Not Found** (không trả 403 để tránh lộ thông tin sự tồn tại của ticket).
  - Agent và Admin có quyền xem danh sách workspace, lọc theo `status`, `assigneeId`, `requesterId` kèm phân trang (`page`, `pageSize` tối đa 100).
- **Concurrency Control**:
  - Cấu hình Optimistic Concurrency qua trường `Version`.
  - Nếu 2 Agent cùng bấm nhận (Claim) 1 ticket cùng lúc, người thứ hai nhận mã lỗi **409 Conflict** (không bị ghi đè dữ liệu).
- **Danh sách API Ticket**:
  - `POST /tickets`: Tạo ticket mới (`Submitted`).
  - `GET /tickets/me`: Xem danh sách ticket (Requester xem vé của mình; Agent/Admin xem workspace có filter & paging).
  - `GET /tickets/{id}`: Xem chi tiết ticket.
  - `PATCH /tickets/{id}`: Sửa thông tin ticket (chỉ chủ vé, chỉ khi `Submitted`).
  - `POST /tickets/{id}/claim`: Agent nhận xử lý ticket chưa gán (`Submitted` -> `InProgress`).
  - `POST /tickets/{id}/assign`: Admin gán ticket cho Agent.
  - `POST /tickets/{id}/resolve`: Agent được gán hoặc Admin đánh dấu đã giải quyết.
  - `POST /tickets/{id}/reopen`: Requester mở lại ticket khi chưa đạt yêu cầu.
  - `POST /tickets/{id}/close`: Requester đóng ticket hoàn tất.
  - `POST /tickets/{id}/cancel`: Requester hủy ticket khi còn `Submitted`.
  - `POST /tickets/{id}/comments`: Thêm trao đổi công khai vào ticket.
  - `GET /tickets/{id}/comments`: Xem lịch sử trao đổi của ticket.

---

### 2.4. Module AI Assistant (Domain Contract & NoOp)
- Đã xây dựng sẵn kiến trúc Human-in-the-loop (HITL): `AIAnalysis`, `AISuggestion`, vòng đời gợi ý (`Pending`, `Accepted`, `Overridden`, `Rejected`, `Expired`), cơ chế phát hiện stale data (`InputHashComputer`).
- Đang dùng `NoOpTicketClassificationService` và `NoOpEmbeddingService` trả về trạng thái `Unavailable` có kiểm soát, không crash hệ thống, không fake kết quả.

---

### 2.5. Cơ sở dữ liệu & Automated Tests
- Migration EF Core: `20261019174137_AddTicketing` đã map đầy đủ bảng `Tickets`, `TicketComments`, `AspNetUsers`, `AspNetRoles` và các index phục vụ truy vấn nhanh.
- **Bộ test tự động (187/187 PASS)**:
  - Unit tests: Kiểm tra quy tắc nghiệp vụ domain, tính toán priority, chuyển đổi state hợp lệ/bất hợp lệ.
  - Architecture tests: Kiểm tra ranh giới Clean Architecture bằng NetArchTest.
  - Integration tests: Dùng **Docker Testcontainers (PostgreSQL 17 thật)** tự dựng DB cô lập để test toàn bộ luồng Auth, Role, Ticket Lifecycle, Concurrency Claim và Endpoint Security.

---

## 3. CÁC BƯỚC TIẾP THEO (TODO CHO SPRINT TỚI)

1. **Làm Module 3 (Knowledge Base cơ bản)**:
   - Tạo thực thể `Article`, `ArticleCategory`.
   - API viết bài, sửa bài, xuất bản (`Published`/`Draft`).
   - API tìm kiếm bài viết hỗ trợ tự giải quyết sự cố trước khi tạo vé.
2. **Làm Module 4 (Analytics & SLA cơ bản)**:
   - Đo thời gian phản hồi (First Response Time) và thời gian xử lý (Resolution Time).
   - Đánh dấu vi phạm deadline SLA theo Priority (P1: 4h, P2: 8h, P3: 24h, P4: 72h).
   - API thống kê số lượng vé theo trạng thái và độ ưu tiên.
3. **Nối AI Backend (Module 5)**:
   - Khi repo `ai-service` hoàn thiện API/gRPC, thay thế `NoOp` adapter trong Infrastructure bằng HttpClient gọi sang `ai-service`.
4. **Hạ tầng Docker Compose**:
   - Hiện tại `docker-compose.yml` đang trỏ image `minio/minio:latest` bị lỗi trên Docker Hub. Khi được phép chỉnh sửa hạ tầng, đổi sang `quay.io/minio/minio:latest` để chạy full stack container mượt mà.

---

## 4. HƯỚNG DẪN CHẠY & KIỂM THỬ NHANH

### Chạy toàn bộ Test:
```bash
cd D:\1.DH\n4k1\3.ChuyenDe-TH\supportflow-ai\ai-it-service-desk
dotnet test src/backend/ServiceDesk.sln
```

### Chạy API trực tiếp (Local Development):
```bash
cd D:\1.DH\n4k1\3.ChuyenDe-TH\supportflow-ai\ai-it-service-desk\src\backend\ServiceDesk.Api
dotnet run
```
- API Base URL: `http://localhost:5000`
- Swagger UI: `http://localhost:5000/swagger`
- Health check: `http://localhost:5000/health`
