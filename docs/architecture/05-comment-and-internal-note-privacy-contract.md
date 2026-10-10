# TC-303: Comment & Internal Note Privacy and Visibility Contract

- **Document ID:** `ARCH-CONTRACT-05`
- **Jira Issue:** `TC-303` (Chốt comment/internal-note visibility rules) — **Sprint 2 P0 Security / Highest Priority**
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT / SECURITY MANDATE
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, Frontend Web Team, Security & QA Team
- **Direct Handoff Recipients:**
  - `TC-304`: Backend Comment & Internal Note Domain Entities + Invariants
  - `TC-305`: Backend Timeline Aggregation & Serialization Services
  - `TC-306`: Frontend Timeline Component & Distinct Internal Note Styling
  - `TC-307`: Automated Security Regression Tests for Zero Data Leakage

---

## 1. Mục đích & Cam kết An toàn thông tin (Security Mandate)

Trong hệ thống hỗ trợ CNTT, các kỹ thuật viên (Agents) và quản lý (Leads) cần một kênh trao đổi chuyên môn riêng ngay trên ticket để ghi lại chẩn đoán kỹ thuật, đánh giá nhạy cảm, nguyên nhân gốc rễ, thông tin tài khoản nội bộ hoặc trao đổi với các nhà cung cấp bên ngoài.
Thông tin này được gọi là **Internal Note (Ghi chú nội bộ)**.

### Quy tắc an toàn số 1 (Golden Security Rule)
> **Internal Note tuyệt đối không bao giờ được phép rò rỉ tới người yêu cầu (Requester), dưới bất kỳ hình thức nào:**
> 1. Không xuất hiện trong API response (kể cả GET chi tiết, GET timeline, GET search, GET notifications).
> 2. Không được gửi toàn bộ dữ liệu về Frontend rồi dùng JavaScript/CSS để ẩn.
> 3. Không được gửi qua webhook, push notification hoặc email gửi cho Requester.

---

## 2. Mô hình Dữ liệu & Ràng buộc toàn vẹn (Data Model & Invariants)

Hệ thống hợp nhất lưu trữ Comment và Internal Note trong cùng một bảng `TicketComments` để đảm bảo thứ tự thời gian tự nhiên (chronological ordering) nhưng phân biệt chặt chẽ qua cờ `IsInternal`:

```csharp
public class TicketComment
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } // Max 4000 ký tự
    public bool IsInternal { get; private set; } // true = Internal Note, false = Public Comment
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    // Domain Invariant: Chỉ Agent, Lead, Admin mới được tạo Internal Note
    public static Result<TicketComment> Create(
        Guid ticketId,
        Guid authorId,
        string content,
        bool isInternal,
        ICurrentUser author)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length < 2)
            return Result.Failure<TicketComment>("Nội dung bình luận không được rỗng (tối thiểu 2 ký tự).");

        if (isInternal && !author.IsInRole(RoleNames.Agent) && !author.IsInRole(RoleNames.Lead) && !author.IsInRole(RoleNames.Admin))
        {
            // Requester tuyệt đối không được phép tạo Internal Note
            return Result.Failure<TicketComment>("Requester không có quyền tạo ghi chú nội bộ.");
        }

        return Result.Success(new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = authorId,
            Content = content.Trim(),
            IsInternal = isInternal,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}
```

---

## 3. Đặc tả API Endpoints & Ma trận Phân quyền

### 3.1 Endpoint Thêm Public Comment
- **Route:** `POST /api/v1/tickets/{id}/comments`
- **Thẩm quyền:** `Requester` (chủ sở hữu), `Agent` (thuộc team hoặc được assign), `Lead`, `Admin`.
- **Payload:**
  ```json
  {
    "content": "Tôi đã khởi động lại máy nhưng vẫn gặp lỗi màn hình xanh."
  }
  ```
- **Hành vi:**
  - Tự động gán `IsInternal = false`.
  - Nếu người gửi là `Requester` và Ticket đang ở `PendingUser`: Tự động trigger event `RequesterRepliedEvent` chuyển trạng thái ticket sang `InProgress`.

### 3.2 Endpoint Thêm Internal Note
- **Route:** `POST /api/v1/tickets/{id}/internal-notes`
- **Thẩm quyền:** Chỉ dành riêng cho `Agent`, `Lead`, `Admin` có quyền truy cập ticket.
- **Payload:**
  ```json
  {
    "content": "Kiểm tra log dump cho thấy lỗi driver card mạng Intel v2.1. Cần remote update driver."
  }
  ```
- **Hành vi:**
  - Bắt buộc gán `IsInternal = true`.
  - Nếu `Requester` cố tình gửi request tới endpoint này: Hệ thống **ngay lập tức trả về `403 Forbidden`**.

### 3.3 Endpoint Lấy Timeline Tổng hợp (Unified Timeline API)
- **Route:** `GET /api/v1/tickets/{id}/timeline`
- **Cơ chế lọc phía Server (Server-Side Filtering Pipeline):**

```csharp
public async Task<List<TimelineItemDto>> GetTicketTimelineAsync(Guid ticketId, ICurrentUser user)
{
    var ticket = await _dbContext.Tickets.FindAsync(ticketId);
    if (ticket == null || !TicketAuthorizationPredicates.CanViewTicket(ticket, user))
        throw new NotFoundException("Ticket không tồn tại hoặc bạn không có quyền xem.");

    bool canViewInternal = TicketAuthorizationPredicates.CanViewInternalNotes(ticket, user);

    var query = _dbContext.TicketComments
        .Where(c => c.TicketId == ticketId && !c.IsDeleted);

    // LỌC NGAY TẠI TẦNG TRUY VẤN DATABASE:
    if (!canViewInternal)
    {
        query = query.Where(c => !c.IsInternal);
    }

    var comments = await query.OrderBy(c => c.CreatedAt).ToListAsync();

    return comments.Select(c => new TimelineItemDto
    {
        Id = c.Id,
        AuthorId = c.AuthorId,
        AuthorName = c.Author.FullName,
        AuthorRole = c.Author.Role,
        Content = c.Content,
        IsInternal = c.IsInternal,
        CreatedAt = c.CreatedAt
    }).ToList();
}
```

---

## 4. Đặc tả Giao diện Người dùng (Frontend UX / UI Contract)

### 4.1 Góc nhìn của Requester (Portal Người dùng)
- Timeline chỉ hiển thị danh sách hội thoại qua lại giữa Requester và Helpdesk.
- Không hề có bất kỳ dấu hiệu, placeholder hay khoảng trắng nào gợi ý về sự tồn tại của Internal Note.
- Form nhập chỉ có duy nhất ô "Viết bình luận phản hồi cho IT".

### 4.2 Góc nhìn của Agent / Lead (Portal Kỹ thuật viên)
- Khung bình luận có Tab chuyển đổi rõ ràng:
  - Tab 1: **Phản hồi khách hàng (Public Comment)** — Nút bấm màu xanh / trung tính, placeholder: *"Viết phản hồi cho người yêu cầu..."*.
  - Tab 2: **Ghi chú nội bộ (Internal Note)** — Viền vàng / cảnh báo bảo mật, có nhãn rõ ràng: *"🔒 Ghi chú nội bộ — Khách hàng KHÔNG nhìn thấy nội dung này"*.
- Trên dòng thời gian (Timeline):
  - Internal Note có background màu vàng nhạt (warning/amber background), icon ổ khóa `🔒 Internal Note`, phân biệt hoàn toàn với Public Comment.

---

## 5. Kịch bản Kiểm thử Bảo mật Bắt buộc (Security Acceptance Tests - TC-307)

1. **Test Case 1 (Direct API Leakage Check):**
   - Đăng nhập tài khoản `Agent`, tạo 1 `Internal Note` trên Ticket #1.
   - Đăng nhập tài khoản `Requester` (chủ sở hữu Ticket #1), gọi `GET /api/v1/tickets/{id}` và `GET /api/v1/tickets/{id}/timeline`.
   - **Kỳ vọng:** Response body hoàn toàn không chứa chuỗi nội dung của Internal Note; mảng `timeline` không có phần tử nào có `isInternal: true`.
2. **Test Case 2 (Unauthorized Creation Prevention):**
   - Đăng nhập tài khoản `Requester`, gửi `POST /api/v1/tickets/{id}/internal-notes`.
   - **Kỳ vọng:** Trả về `403 Forbidden`. Không có bản ghi nào được tạo trong DB.
3. **Test Case 3 (Parameter Tampering Defense):**
   - Đăng nhập tài khoản `Requester`, gửi `POST /api/v1/tickets/{id}/comments` kèm body `{"content": "hack", "isInternal": true}`.
   - **Kỳ vọng:** Backend bỏ qua trường `isInternal` từ request và luôn ép giá trị `false`, hoặc từ chối request `400 Bad Request`.
