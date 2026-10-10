# TC-260: Ticket Domain Fields, Initial Status & Ownership Specification

- **Document ID:** `ARCH-CONTRACT-02`
- **Jira Issue:** `TC-260` (Chốt Ticket fields, initial status và ownership)
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT / BLOCKING REVIEW
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, Frontend Web Team, Database / DevOps Engineers

---

## 1. Mục đích & Nguyên tắc cốt lõi

Tài liệu này là **Technical Contract bắt buộc** chuẩn hóa toàn bộ cấu trúc trường dữ liệu (fields), trạng thái khởi tạo (initial status), cơ chế concurrency control, quy tắc sở hữu (ownership) và giải quyết xung đột tài liệu giữa ERD, OpenAPI và Implementation Specification.

### Nguyên tắc bất biến (Invariants)
1. **Contract-First:** Mọi thay đổi code hoặc database migration phải tuân thủ tuyệt đối cấu trúc trong tài liệu này.
2. **Tách biệt quyền sở hữu (Ownership) và phân công (Assignment):**
   - `RequesterId` xác định người tạo và sở hữu ticket, **bất biến suốt vòng đời** của ticket (không thể bị gán sang người khác).
   - `AssignedAgentId` và `SupportTeamId` thể hiện đơn vị kỹ thuật chịu trách nhiệm giải quyết, có thể thay đổi linh hoạt theo quy trình Triage và Assignment.
3. **POST /tickets tạo Draft (P0-1 Blocker):**
   - Lệnh `POST /api/v1/tickets` là **Tạo Ticket Draft**. Khách hàng có thể lưu nháp nhiều lần trước khi nộp.
   - Nộp ticket là một command tường minh riêng biệt: `POST /api/v1/tickets/{id}/submit`.

---

## 2. Đặc tả chi tiết các trường dữ liệu (Ticket Field Contract)

| Nhóm dữ liệu | Tên trường | Kiểu dữ liệu | Bắt buộc (Draft) | Bắt buộc (Submitted) | Bắt buộc (Triaged) | Bắt buộc (Resolved) | Mô tả & Quy tắc nghiệp vụ |
|---|---|---|---|---|---|---|---|
| **Identity** | `Id` | `Guid` | **Có** | **Có** | **Có** | **Có** | Primary Key, UUIDv7 hoặc UUIDv4. |
| **Identity** | `TicketNumber` | `string` (max 20) | *Không (null)* | **Có** | **Có** | **Có** | Mã hiển thị thân thiện (VD: `TK-2026-00001`). Sinh tuần tự an toàn khi `Submit`. |
| **Ownership** | `RequesterId` | `Guid` | **Có** | **Có** | **Có** | **Có** | Foreign Key tới `AspNetUsers.Id`. Bất biến sau khi tạo. |
| **Department**| `RequesterDepartmentId` | `Guid?` | *Không (null)* | **Có** | **Có** | **Có** | Snapshot phòng ban của Requester tại thời điểm `Submit`. Bất biến sau đó. |
| **Core Content**| `Title` | `string` (max 200) | **Có** (>= 5 ký tự) | **Có** | **Có** | **Có** | Tiêu đề ngắn gọn mô tả sự cố hoặc yêu cầu. |
| **Core Content**| `Description` | `string` (max 4000) | **Có** (>= 10 ký tự)| **Có** | **Có** | **Có** | Nội dung chi tiết vấn đề (Markdown hoặc Plain Text). |
| **Classification**| `CategoryId` | `Guid?` | *Không* | *Không* | **Có** | **Có** | FK tới `TicketCategories`. Phải được điền khi qua bước Triage. |
| **Classification**| `Impact` | `Impact?` (enum 1-3) | *Không* | *Không* | **Có** | **Có** | Mức độ ảnh hưởng: `Low (1)`, `Medium (2)`, `High (3)`. |
| **Classification**| `Urgency` | `Urgency?` (enum 1-3) | *Không* | *Không* | **Có** | **Có** | Mức độ khẩn cấp: `Low (1)`, `Medium (2)`, `High (3)`. |
| **Classification**| `Priority` | `Priority?` (enum 1-4)| *Không* | *Không* | **Có** | **Có** | Độ ưu tiên: `P1 (Critical)`, `P2 (High)`, `P3 (Medium)`, `P4 (Low)`. Tính tự động qua `PriorityMatrix(Impact, Urgency)`. |
| **Workflow** | `Status` | `TicketStatus` (enum 0-8)| **Có** (Draft = 0) | **Có** (Submitted = 1)| **Có** (Triaged = 2) | **Có** (Resolved = 7) | Trạng thái vòng đời của ticket. |
| **Assignment** | `SupportTeamId` | `Guid?` | *Không* | *Không* | **Có** (hoặc Assign) | **Có** | Đội hỗ trợ phụ trách (IT Support, Network, Infra, App Ops). |
| **Assignment** | `AssignedAgentId` | `Guid?` | *Không* | *Không* | *Không* | **Có** | Agent trực tiếp xử lý ticket. Bắt buộc khi chuyển sang InProgress/Resolved. |
| **Resolution** | `Resolution` | `string?` (max 4000) | *Không* | *Không* | *Không* | **Có** | Tóm tắt giải pháp khắc phục. Bắt buộc khi gọi `/resolve`. |
| **Resolution** | `ResolutionCategoryId` | `Guid?` | *Không* | *Không* | *Không* | *Tùy chọn* | Phân loại nguyên nhân gốc rễ (Root cause category). |
| **Concurrency** | `Revision` | `int` | **Có** (Khởi tạo = 1) | **Có** | **Có** | **Có** | Concurrency version. Tăng 1 sau mỗi lần chuyển trạng thái hoặc cập nhật. |
| **Concurrency** | `RowVersion` / `xmin` | `uint` / `byte[]` | **Có** | **Có** | **Có** | **Có** | PostgreSQL system token chống Lost Update. |
| **Audit Timestamps**| `CreatedAt` | `DateTimeOffset` | **Có** | **Có** | **Có** | **Có** | Thời điểm tạo bản nháp Draft. |
| **Audit Timestamps**| `UpdatedAt` | `DateTimeOffset` | **Có** | **Có** | **Có** | **Có** | Thời điểm cập nhật dữ liệu gần nhất. |
| **Lifecycle Times**| `SubmittedAt` | `DateTimeOffset?` | *null* | **Có** | **Có** | **Có** | Thời điểm Requester chính thức gửi ticket. |
| **Lifecycle Times**| `TriagedAt` | `DateTimeOffset?` | *null* | *null* | **Có** | **Có** | Thời điểm hoàn tất phân loại Triage. |
| **Lifecycle Times**| `AssignedAt` | `DateTimeOffset?` | *null* | *null* | *null* | **Có** | Thời điểm phân công Agent. |
| **Lifecycle Times**| `StartedAt` | `DateTimeOffset?` | *null* | *null* | *null* | **Có** | Thời điểm Agent nhấn Start (InProgress). |
| **Lifecycle Times**| `ResolvedAt` | `DateTimeOffset?` | *null* | *null* | *null* | **Có** | Thời điểm ticket được chuyển sang Resolved. |
| **Lifecycle Times**| `ClosedAt` | `DateTimeOffset?` | *null* | *null* | *null* | **Có** | Thời điểm ticket được đóng vĩnh viễn (Closed). |

---

## 3. Quyết định kiến trúc xử lý xung đột tài liệu (Document Conflict Resolution)

### 3.1 Xung đột giữa ERD và Implementation Spec
- **ERD ban đầu:** Đặt `TicketNumber` và `RequesterDepartmentId` là `NOT NULL` ngay trong bảng `Tickets`, nhưng phần chú thích ERD lại ghi: *"RequesterDepartmentId là snapshot lúc submit"*.
- **Implementation Spec:** Định nghĩa luồng nghiệp vụ gồm: `Create/Edit Draft` → `Submit Ticket`. Bản nháp chưa thể có số hiệu chính thức và chưa thể snapshot phòng ban vì người dùng có thể đổi phòng ban trong quá trình giữ bản nháp hoặc hủy nháp.
- **Quyết định chính thức của Architect (Đức Anh):**
  1. Trong Database Schema: Cột `TicketNumber` và `RequesterDepartmentId` được cấu hình là `NULLABLE` ở tầng vật lý, nhưng có CHECK constraint hoặc Domain Invariant bảo đảm:
     - Nếu `Status == TicketStatus.Draft`: `TicketNumber` được phép `NULL`, `RequesterDepartmentId` được phép `NULL`.
     - Nếu `Status != TicketStatus.Draft`: `TicketNumber` **bắt buộc NOT NULL** và `RequesterDepartmentId` **bắt buộc NOT NULL**.
  2. Cơ chế sinh `TicketNumber`:
     - Định dạng: `TK-{YYYY}-{00000}` (VD: `TK-2026-00001`).
     - Thời điểm sinh: Chỉ sinh **duy nhất một lần tại thời điểm lệnh `/submit` thực thi thành công**.
     - Đảm bảo an toàn Concurrency: Dùng PostgreSQL Sequence chuyên dụng (`ticket_number_seq`) hoặc bảng khóa sequence `SELECT nextval('ticket_number_seq')`. Tuyệt đối không dùng `SELECT MAX(TicketNumber) + 1` vì gây Race Condition khi nhiều người cùng submit.
  3. Cơ chế Snapshot `RequesterDepartmentId`:
     - Tại thời điểm `/submit`, hệ thống đọc `DepartmentId` hiện tại của Requester từ `AspNetUsers` hoặc Identity Profile và ghi cứng vào `Tickets.RequesterDepartmentId`. Sau khi submit, dù Requester có chuyển phòng ban thì phòng ban trên ticket vẫn giữ nguyên để phục vụ báo cáo SLA/Analytics chính xác.

---

## 4. Biên bản Đánh giá & Yêu cầu sửa đổi khẩn cấp cho Backend (Blocking Review)

Qua rà soát code hiện tại của Backend (đặc biệt trong `TicketService` và các API endpoints liên quan), phát hiện **4 sai lệch nghiêm trọng so với hợp đồng (P0 Drift)**:

```text
[LỖI 1] TicketService.CreateTicketAsync() đang tự ý gán Status = TicketStatus.Submitted.
        => Sai Contract: POST /tickets phải tạo Status = TicketStatus.Draft.

[LỖI 2] DTO CreateTicketRequest đang bắt buộc CategoryId (Required).
        => Sai Contract: Requester khi tạo Draft không bắt buộc phải biết Category.
           Category chỉ bắt buộc khi Triage hoặc tùy chọn khi Submit.

[LỖI 3] RequesterDepartmentId đang bị để null ngay cả khi ticket chuyển thành Submitted.
        => Vi phạm Invariant: Ticket đã submit bắt buộc phải có Snapshot Department.

[LỖI 4] Thiếu hoàn toàn Command Endpoint POST /api/v1/tickets/{id}/submit.
        => Không có luồng chuyển từ Draft sang Submitted.
```

### Yêu cầu hành động đối với Backend Core Team (Blocking Acceptance)
1. **Sửa đổi Command & DTO:**
   - Đổi `CreateTicketRequest` thành `CreateTicketDraftRequest`: chỉ gồm `Title` và `Description`.
   - `POST /api/v1/tickets` trả về `TicketDraftDto` với `Status = Draft (0)`.
2. **Bổ sung Submit Endpoint:**
   - Viết endpoint `POST /api/v1/tickets/{id}/submit`.
   - Trong UseCase `SubmitTicketHandler`:
     - Kiểm tra quyền sở hữu (`IsTicketOwner`).
     - Kiểm tra trạng thái hiện tại phải là `Draft`.
     - Sinh `TicketNumber` từ sequence.
     - Snapshot `RequesterDepartmentId`.
     - Gán `SubmittedAt = Clock.UtcNow`.
     - Chuyển `Status = TicketStatus.Submitted`.
     - Tăng `Revision++`.
3. **Cập nhật Unit & Integration Tests:**
   - Viết test đảm bảo tạo Draft thành công không cần `CategoryId`.
   - Viết test đảm bảo không thể submit ticket của user khác (`403/404`).
   - Viết test đảm bảo submit hai lần sẽ báo lỗi (`422 Unprocessable Entity`).
