> **Tài liệu lịch sử — không dùng làm chỉ dẫn triển khai hiện hành.**
> Phạm vi bản gốc: hợp đồng ngày 2026-09-29. Chú thích bổ sung ngày 2026-10-19; không sửa nội dung hay ký duyệt lại bản gốc.
> Nguồn hiện hành: [hợp đồng backend](current-backend-contract.md), [quy tắc bảo mật](security-design-rules.md), [quyết định còn mở](pending-business-decisions.md).
> Bảng 0–8 và yêu cầu JSON chuỗi phía dưới đã được thay thế cho phạm vi hiện hành: giữ nghĩa 0–6, thêm Triaged=7/Assigned=8/PendingExternal=9; JSON vẫn là số. Không diễn giải lại dữ liệu đã lưu.

<!-- original-record -->
# TC-261: Ticket Lifecycle Baseline & Requester Visibility Contract

- **Document ID:** `ARCH-CONTRACT-03`
- **Jira Issue:** `TC-261` (Chốt requester visibility và lifecycle baseline)
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT / P0 ALIGNMENT
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, Frontend Web Team, QA Team

---

## 1. Mục đích & Vấn đề giải quyết

### 1.1 Khắc phục xung đột Enum FE vs BE (P0-2 & P0-3 Blocker)
Trước khi tài liệu này được ban hành, có sự không nhất quán nghiêm trọng giữa Backend và Frontend:
- **Backend (Phiên bản lệch lạc cũ):** Chỉ có 7 trạng thái: `Draft (0), Submitted (1), InProgress (2), PendingUser (3), Resolved (4), Closed (5), Cancelled (6)`.
- **Frontend & Technical Baseline chuẩn:** Có 9 trạng thái: `Draft (0), Submitted (1), Triaged (2), Assigned (3), InProgress (4), PendingUser (5), PendingExternal (6), Resolved (7), Closed (8)`.

**Hậu quả nếu không đồng bộ:**
Nếu Backend gửi giá trị số (numeric enum) `InProgress = 2`, Frontend sẽ hiểu nhầm ticket đang ở trạng thái `Triaged` (vì index 2 ở FE là Triaged). Nếu Backend gửi `Resolved = 4`, FE hiểu là `InProgress`.
Đồng thời, baseline **không có trạng thái `Cancelled`** (Draft có thể bị xóa bằng `DELETE`, ticket đã submit không được cancel tùy tiện mà đi qua lifecycle). `Reopened` là **Domain Event**, không phải một State.

### 1.2 Quyết định bắt buộc
1. Toàn bộ Backend (.NET C#), Frontend (TypeScript) và Database (PostgreSQL enum / int column) phải sử dụng **duy nhất bảng mã 9 trạng thái từ 0 đến 8**.
2. Serialization JSON qua HTTP: Bắt buộc cấu hình String Enum Serialization (`JsonStringEnumConverter`) để payload JSON mang giá trị chuỗi rõ ràng (VD: `"status": "InProgress"`), vừa tránh lỗi lệch index, vừa giúp log và API dễ đọc, dễ debug.

---

## 2. Đặc tả Enum Trạng thái Ticket (TicketStatus Standard Baseline)

```csharp
namespace ServiceDesk.Domain.Ticketing;

/// <summary>
/// Chuẩn hóa 9 trạng thái vòng đời của Ticket (0 đến 8).
/// Bất biến trên toàn bộ hệ thống API, DB, và Frontend.
/// </summary>
public enum TicketStatus
{
    Draft = 0,             // Bản nháp của Requester, chưa gửi lên hệ thống hỗ trợ
    Submitted = 1,         // Đã nộp chính thức, chờ IT Triage
    Triaged = 2,           // Đã phân loại Category, Impact, Urgency, Priority
    Assigned = 3,          // Đã phân công Support Team và/hoặc Assigned Agent cụ thể
    InProgress = 4,        // Agent đã bắt đầu xử lý yêu cầu
    PendingUser = 5,       // Đang chờ Requester cung cấp thêm thông tin
    PendingExternal = 6,   // Đang chờ bên thứ 3 (Vendor, Bảo hành, Hardware supplier)
    Resolved = 7,          // Agent đã hoàn thành xử lý và cung cấp giải pháp
    Closed = 8             // Ticket đã được nghiệm thu đóng vĩnh viễn (hoặc tự động đóng sau timeout)
}
```

Tương ứng trên Frontend TypeScript (`src/frontend/service-desk-web/src/types/ticket.ts`):

```typescript
export enum TicketStatus {
  Draft = 0,
  Submitted = 1,
  Triaged = 2,
  Assigned = 3,
  InProgress = 4,
  PendingUser = 5,
  PendingExternal = 6,
  Resolved = 7,
  Closed = 8,
}

export type TicketStatusString =
  | 'Draft'
  | 'Submitted'
  | 'Triaged'
  | 'Assigned'
  | 'InProgress'
  | 'PendingUser'
  | 'PendingExternal'
  | 'Resolved'
  | 'Closed';
```

---

## 3. Sơ đồ Vòng đời (State Machine Flowchart)

```text
                  +-------------+
                  |    Draft    |
                  +------+------+
                         | (Submit)
                         v
                  +-------------+
                  |  Submitted  |
                  +------+------+
                         | (Triage)
                         v
                  +-------------+
                  |   Triaged   |
                  +------+------+
                         | (Assign)
                         v
                  +-------------+
                  |   Assigned  |
                  +------+------+
                         | (Start)
                         v
  +--------------------------------------------+
  |                                            |
  |                InProgress  <---------------+---+ (Reopen: reason required)
  |                  ^    |                    |   |
  | (Reply info)     |    | (Request info)     |   |
  |                  |    v                    |   |
  |             +----+---------+               |   |
  |             |  PendingUser |               |   |
  |             +--------------+               |   |
  |                  ^    |                    |   |
  | (External done)  |    | (Wait vendor)      |   |
  |                  |    v                    |   |
  |           +------+-----------+             |   |
  |           |  PendingExternal |             |   |
  |           +------------------+             |   |
  |                       |                    |   |
  +-----------------------+                    |   |
                          | (Resolve)          |   |
                          v                    |   |
                  +---------------+            |   |
                  |    Resolved   +------------+   |
                  +-------+-------+                |
                          | (Close)                |
                          v                        |
                  +---------------+                |
                  |     Closed    | (Terminal)     |
                  +---------------+----------------+
```

### Quy tắc bất biến của State Machine
- `Reopen` là **Event**: Thực thi lệnh `POST /tickets/{id}/reopen` sẽ chuyển trạng thái từ `Resolved` trở lại `InProgress`, đồng thời bắt buộc phải có `ReopenReason` và ghi Audit Log.
- `Closed` là **Terminal State**: Khi ticket đã ở trạng thái `Closed`, không có bất kỳ lệnh chuyển trạng thái nào được phép thực thi (kể cả Reopen hay Edit). Nếu sự cố tái diễn, Requester phải tạo ticket mới và có thể liên kết tham chiếu (link ticket).

---

## 4. Đặc tả hình chiếu hiển thị (Visibility Projections)

Để bảo mật thông tin nội bộ của đội ngũ IT và tuân thủ nguyên tắc Least Privilege, Backend chia tách rõ ràng hai hình chiếu (Projections):

### 4.1 Requester Projection (`TicketPublicViewDto`)
Áp dụng khi người gọi là `Requester` (chủ sở hữu ticket):
- **Trường hiển thị:**
  - `id`, `ticketNumber`, `title`, `description`
  - `status`, `createdAt`, `submittedAt`, `resolvedAt`, `closedAt`
  - `category` (chỉ hiển thị tên công khai)
  - `assignedAgent` (chỉ hiển thị tên công khai hoặc `IT Support Specialist`, không hiển thị số điện thoại cá nhân)
  - `publicComments` (danh sách bình luận công khai giữa Requester và Agent)
  - `publicAttachments` (các file do Requester upload hoặc Agent chia sẻ cho Requester)
  - `resolution` (nội dung hướng dẫn xử lý gửi cho Requester khi ticket Resolved)
- **Trường TUYỆT ĐỐI ẨN (Bị loại bỏ trước khi serialize JSON):**
  - `internalNotes` (Ghi chú trao đổi kỹ thuật nội bộ)
  - `aiAnalysis` (Tọa độ tự tin, giải thích kỹ thuật, embedding scores của AI)
  - `impact`, `urgency` (trừ khi dự án mở rộng công khai sau này)
  - `slaBreachDetails`, `rootCauseTechnicalNotes`

### 4.2 Agent Projection (`TicketAgentViewDto`)
Áp dụng khi người gọi là `Agent`, `Support Team Lead`, hoặc `Administrator`:
- **Trường hiển thị:** Toàn bộ thông tin của `TicketPublicViewDto` cộng thêm:
  - `impact`, `urgency`, `priority` (mức độ ưu tiên chính xác)
  - `supportTeamId`, `supportTeamName`
  - `assignedAgentId`, `assignedAgentName`
  - `internalNotes` (toàn bộ lịch sử ghi chú nội bộ kèm người tạo)
  - `aiSuggestions` (phân loại gợi ý, độ tự tin, căn cứ gợi ý)
  - `revision`, `xmin` (phục vụ concurrency check)
  - `auditHistory` (lịch sử ai đã thay đổi gì vào lúc nào)

---

## 5. Xử lý hành vi phủ định (Negative Behaviors & Error Responses)

| Tình huống vi phạm | Điều kiện phát hiện | HTTP Status Code | Chi tiết mã lỗi (Error Code) |
|---|---|---|---|
| Chuyển trạng thái bất hợp lệ | Ticket đang `Draft` mà gọi `/resolve` hoặc `/triage` | `422 Unprocessable Entity` | `INVALID_STATE_TRANSITION` (Kèm message: *Cannot transition from Draft to Resolved*) |
| Không đủ điều kiện tiên quyết (Guard failure) | Gọi `/resolve` nhưng trường `Resolution` bị bỏ trống | `422 Unprocessable Entity` | `RESOLUTION_SUMMARY_REQUIRED` |
| Sai thẩm quyền chuyển trạng thái | Requester gọi `/triage` hoặc Agent gọi `/close` | `403 Forbidden` | `ACTION_NOT_PERMITTED_FOR_ROLE` |
| Xung đột đồng thời (Concurrency collision) | Hai Agent cùng bấm nhận xử lý ticket tại cùng 1 Revision | `409 Conflict` | `CONCURRENCY_CONFLICT` (*Ticket was modified by another user. Please reload.*) |
| Truy cập ticket không tồn tại hoặc của người khác | Requester mở URL ticket của phòng ban khác | `404 Not Found` | `TICKET_NOT_FOUND` (Bảo vệ thông tin ID) |
