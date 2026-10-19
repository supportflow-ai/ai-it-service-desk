> **Tài liệu lịch sử — không dùng làm chỉ dẫn triển khai hiện hành.**
> Phạm vi bản gốc: hợp đồng ngày 2026-09-29. Chú thích bổ sung ngày 2026-10-19; không sửa nội dung hay ký duyệt lại bản gốc.
> Nguồn hiện hành: [hợp đồng backend](current-backend-contract.md), [quy tắc bảo mật](security-design-rules.md), [quyết định còn mở](pending-business-decisions.md).
> Đính chính vị trí policy: nghiệp vụ quyền Ticketing thuộc Application/Ticketing/Authorization, không thuộc Common hoặc Infrastructure/Identity. Ví dụ có Lead/team/assignment phía dưới chưa phải mã đang triển khai; xem DEC-02.

<!-- original-record -->
# TC-246 & TC-247: Actor Matrix, RBAC Scope & API Permission Specification

- **Document ID:** `ARCH-CONTRACT-01`
- **Jira Issues:** `TC-246` (Phân tích actor và use case cốt lõi), `TC-247` (Lập permission matrix API), Reconcile `TC-196` (Chốt actor, RBAC và contract truy cập)
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, Frontend Web Team, QA / Security Team

---

## 1. Bối cảnh & Mục đích

### 1.1 Vấn đề quản lý và kỹ thuật hiện tại
1. **Trạng thái Jira không nhất quán:** Jira issue `TC-196` ("Chốt actor, RBAC và contract truy cập") đã được đánh dấu `Done`, nhưng hai subtask cốt lõi là `TC-246` ("Phân tích actor và use case cốt lõi") và `TC-247` ("Lập permission matrix API") vẫn ở trạng thái `To Do`.
2. **Xung đột định nghĩa Actor:** Mô tả ban đầu của Jira `TC-196` chỉ liệt kê 3 actor: `Requester`, `Agent`, `Admin`. Tuy nhiên, Technical Baseline (`docs/01-product-and-domain-context.md`, `ADR-005`, Implementation Spec) quy định rõ hệ thống gồm 4 actor: `Requester`, `Agent`, `Lead` (Support Team Lead), `Admin`. **Quy tắc dự án:** Không được phép loại bỏ `Lead` khi chưa có Change Request (CR) chính thức được phê duyệt.
3. **Lỗ hổng phân quyền tài nguyên (P0-4 Blocker):** Hiện tại, endpoint tra cứu ticket của Backend (`GetTicketById`) chỉ kiểm tra xem user có token hợp lệ hay không, hoặc cho phép bất kỳ Agent nào xem mọi ticket mà không kiểm tra ràng buộc `AssignedAgentId` hay `SupportTeamId`. Điều này vi phạm nghiêm trọng nguyên tắc **Resource Authorization**.

### 1.2 Nguyên tắc phân quyền thống nhất (Uniform Resource Authorization)
Phân quyền trong hệ thống **không chỉ dựa vào Role (RBAC)** mà bắt buộc phải kết hợp **Resource-based Authorization (ABAC / Predicates)**:
- Cùng một bộ policy phân quyền áp dụng nhất quán cho: Ticket entity, Comment, Internal Note, Attachment, Audit History và AI Assistance.
- **Bảo mật tuyệt đối (Zero Leakage):** `Internal Note` không bao giờ được tuần tự hóa (serialize) hoặc gửi về cho `Requester`, kể cả khi Requester trực tiếp gọi endpoint chi tiết hoặc timeline.

---

## 2. Ma trận Actor & Use Case cốt lõi (TC-246)

| Actor | Use Case | Phạm vi tài nguyên (Resource Scope) | Quyền hạn (Action) | Mức độ hiển thị phản hồi (Response Visibility) | Backend Policy / Predicate | Kịch bản phủ định (Negative Case & Status Code) |
|---|---|---|---|---|---|---|
| **Requester** | Tạo Ticket Draft | Sở hữu cá nhân (`RequesterId == Self`) | Create, Update Draft, Delete Draft | Bản thân Draft | `RequireRole("Requester")` | User chưa đăng nhập → `401 Unauthorized` |
| **Requester** | Gửi Ticket (Submit) | Sở hữu cá nhân (`RequesterId == Self`) | Execute `/submit` | Public Ticket View | `IsTicketOwner` | Submit ticket của người khác → `403 Forbidden` / `404 Not Found` |
| **Requester** | Xem chi tiết Ticket | Chỉ ticket của chính mình (`RequesterId == Self`) | Read | **Public View Only** (Không có Internal Note, không có Agent AI analysis chuyên sâu) | `IsTicketOwner` | Truy cập ticket của người khác → `404 Not Found` (ngăn quét ID) |
| **Requester** | Phản hồi thông tin | Ticket của mình ở trạng thái `PendingUser` | Create Public Comment | Public View | `IsTicketOwner AND Status == PendingUser` | Phản hồi khi ticket đã Closed → `422 Unprocessable Entity` |
| **Requester** | Đóng / Mở lại | Ticket của mình ở trạng thái `Resolved` | Execute `/close`, `/reopen` | Public View | `IsTicketOwner AND Status == Resolved` | Đóng ticket của người khác → `403 Forbidden` |
| **Agent** | Xem danh sách Ticket | Ticket được gán cho mình HOẶC thuộc Support Team mà Agent tham gia | Read List | **Agent Projection** (Bao gồm Team, SLA, Priority, AI Suggestions) | `IsAssignedToAgent OR IsMemberOfSupportTeam` | Xem ticket của team khác mà không được phân công → Không hiển thị trong danh sách |
| **Agent** | Xem chi tiết Ticket | `AssignedAgentId == Self` HOẶC `SupportTeamId IN CurrentUser.TeamIds` | Read Detail | **Agent Projection** (Gồm Public Comments, Internal Notes, Triage Data, AI Suggestions) | `HasTicketTeamOrAssignmentAccess` | Xem ticket thuộc team khác → `403 Forbidden` / `404 Not Found` |
| **Agent** | Xử lý Ticket | Ticket do mình phụ trách (`AssignedAgentId == Self`) | Execute `/start`, `/request-info`, `/resolve`, Ghi Internal Note | Agent Projection | `IsAssignedAgent` | Agent trong cùng team nhưng không được assign thực hiện Start/Resolve → `403 Forbidden` |
| **Support Team Lead** | Triage & Phân công | Toàn bộ ticket thuộc Support Team mình phụ trách (`SupportTeamId IN CurrentUser.LedTeamIds`) | Execute `/triage`, `/assign`, `/unassign` | Agent Projection + Lead Operations | `IsSupportTeamLead` | Triage ticket thuộc team do Lead khác quản lý → `403 Forbidden` |
| **Support Team Lead** | Override & Can thiệp | Ticket thuộc Team của Lead | Toàn quyền xử lý trong phạm vi Team | Agent Projection | `IsSupportTeamLead` | Can thiệp ticket ngoài Team → `403 Forbidden` |
| **Administrator** | Quản trị hệ thống | Toàn bộ hệ thống (System Scope) | Full Access (Read, Audit, Reassign, Configure) | **Full System Projection** | `RequireRole("Admin")` | Non-admin gọi endpoint quản trị → `403 Forbidden` |

---

## 3. Định nghĩa Predicate chuẩn (TC-247)

Để tránh logic phân quyền bị phân mảnh hoặc rò rỉ rải rác trong Controller/Endpoint, các predicate phân quyền tài nguyên sau đây được định nghĩa tập trung trong tầng `Application/Common/Security` hoặc `Infrastructure/Identity`:

```csharp
public static class TicketAuthorizationPredicates
{
    // 1. Requester sở hữu ticket
    public static bool IsTicketOwner(Ticket ticket, ICurrentUser user)
        => ticket.RequesterId == user.UserId;

    // 2. Agent trực tiếp được phân công
    public static bool IsAssignedAgent(Ticket ticket, ICurrentUser user)
        => ticket.AssignedAgentId.HasValue && ticket.AssignedAgentId.Value == user.UserId;

    // 3. Agent thuộc Support Team đang xử lý ticket
    public static bool IsTeamMember(Ticket ticket, ICurrentUser user)
        => ticket.SupportTeamId.HasValue && user.TeamIds.Contains(ticket.SupportTeamId.Value);

    // 4. Lead của Support Team đang phụ trách ticket
    public static bool IsTeamLead(Ticket ticket, ICurrentUser user)
        => ticket.SupportTeamId.HasValue && user.LedTeamIds.Contains(ticket.SupportTeamId.Value);

    // 5. System Administrator
    public static bool IsSystemAdmin(ICurrentUser user)
        => user.Roles.Contains(RoleNames.Admin);

    // 6. Tổng hợp quyền đọc chi tiết ticket (Read Ticket Detail Scope)
    public static bool CanViewTicket(Ticket ticket, ICurrentUser user)
    {
        if (IsSystemAdmin(user)) return true;
        if (user.IsInRole(RoleNames.Requester) && IsTicketOwner(ticket, user)) return true;
        if (user.IsInRole(RoleNames.Agent) && (IsAssignedAgent(ticket, user) || IsTeamMember(ticket, user))) return true;
        if (user.IsInRole(RoleNames.Lead) && IsTeamLead(ticket, user)) return true;
        return false;
    }

    // 7. Quyền xem Internal Note
    public static bool CanViewInternalNotes(Ticket ticket, ICurrentUser user)
    {
        if (IsSystemAdmin(user)) return true;
        if (user.IsInRole(RoleNames.Agent) && (IsAssignedAgent(ticket, user) || IsTeamMember(ticket, user))) return true;
        if (user.IsInRole(RoleNames.Lead) && IsTeamLead(ticket, user)) return true;
        return false; // Requester TUYỆT ĐỐI KHÔNG ĐƯỢC PHÉP
    }
}
```

---

## 4. Ma trận phân quyền chi tiết cho API Endpoints (TC-247)

| HTTP Method | API Endpoint | Chức năng nghiệp vụ | Allowed Actors | Authorization Guard / Predicate | Lọc dữ liệu đầu ra (Output Filtering) | Negative Code |
|---|---|---|---|---|---|---|
| `POST` | `/api/v1/tickets` | Tạo Ticket Draft | Requester | `RequireRole("Requester")` | Trả về Draft DTO | `401`, `400` |
| `GET` | `/api/v1/tickets/me` | Lấy danh sách ticket của tôi | Requester | `RequireRole("Requester")` | Filter DB: `RequesterId == CurrentUserId` | `401` |
| `GET` | `/api/v1/tickets` | Danh sách ticket của Agent/Team | Agent, Lead, Admin | `RequireAnyRole("Agent", "Lead", "Admin")` | Filter DB theo Team membership hoặc Assignment | `401`, `403` |
| `GET` | `/api/v1/tickets/{id}` | Lấy chi tiết ticket | Requester, Agent, Lead, Admin | `CanViewTicket(ticket, user)` | **Nếu Requester:** loại bỏ Internal Notes, loại bỏ AI Internal Suggestions | `401`, `404` |
| `PUT` | `/api/v1/tickets/{id}` | Cập nhật thông tin Draft | Requester | `IsTicketOwner AND Status == Draft` | Updated Draft DTO | `403`, `409`, `422` |
| `DELETE` | `/api/v1/tickets/{id}` | Hủy bản nháp Draft | Requester | `IsTicketOwner AND Status == Draft` | `204 No Content` | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/submit` | Nộp ticket (chuyển sang Submitted) | Requester | `IsTicketOwner AND Status == Draft` | Ticket Public DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/triage` | Phân loại & tính toán Priority | Agent, Lead, Admin | `Status == Submitted AND (IsTeamLead OR IsTeamMember OR IsAdmin)` | Ticket Agent DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/assign` | Phân công Support Team / Agent | Agent, Lead, Admin | Xem chi tiết tại TC-294 Assignment Policy | Ticket Agent DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/start` | Bắt đầu xử lý ticket | Assigned Agent | `Status == Assigned AND IsAssignedAgent` | Ticket Agent DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/request-info` | Yêu cầu Requester làm rõ | Assigned Agent | `Status == InProgress AND IsAssignedAgent` | Public Message DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/reply` | Requester phản hồi thông tin | Requester | `Status == PendingUser AND IsTicketOwner` | Public Comment DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/resolve` | Hoàn thành xử lý ticket | Assigned Agent, Lead, Admin | `Status == InProgress AND (IsAssignedAgent OR IsTeamLead OR IsAdmin)` | Ticket Agent DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/close` | Xác nhận đóng ticket | Requester, Admin | `Status == Resolved AND (IsTicketOwner OR IsAdmin)` | Ticket Public DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/reopen` | Mở lại ticket đã giải quyết | Requester, Agent, Lead, Admin | `Status == Resolved AND WithinReopenWindow` | Ticket View DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/comments` | Thêm bình luận công khai | Requester, Agent, Lead, Admin | `CanViewTicket(ticket, user)` | Comment DTO | `403`, `422` |
| `POST` | `/api/v1/tickets/{id}/internal-notes` | Thêm ghi chú nội bộ | Agent, Lead, Admin | `CanViewInternalNotes(ticket, user)` | Internal Note DTO | `403 Forbidden` (Nếu Requester gọi) |
| `GET` | `/api/v1/tickets/{id}/timeline` | Lấy lịch sử hội thoại & timeline | Requester, Agent, Lead, Admin | `CanViewTicket(ticket, user)` | Server filter: strip all `IsInternal == true` nếu là Requester | `401`, `404` |
| `POST` | `/api/v1/tickets/{id}/attachments` | Upload tài liệu đính kèm | Requester, Agent, Lead, Admin | `CanViewTicket(ticket, user) AND CanModifyTicket` | Attachment Meta DTO | `403`, `413`, `415` |
| `GET` | `/api/v1/tickets/{id}/attachments/{attId}` | Download tài liệu đính kèm | Requester, Agent, Lead, Admin | `CanViewTicket(ticket, user) AND (Not InternalAttachment OR CanViewInternalNotes)` | Presigned URL / Binary stream | `403`, `404` |
| `GET` | `/api/v1/tickets/{id}/history` | Xem Audit Log lịch sử ticket | Agent, Lead, Admin | `CanViewInternalNotes(ticket, user)` | Full Audit Entry DTOs | `403 Forbidden` (Nếu Requester) |
| `GET` | `/api/v1/tickets/{id}/ai-suggestions` | Lấy gợi ý phân loại / giải pháp AI | Agent, Lead, Admin | `CanViewInternalNotes(ticket, user)` | AI Suggestions List | `403 Forbidden` (Nếu Requester) |

---

## 5. Chiến lược phản hồi lỗi và phòng chống rò rỉ thông tin (Information Leakage Prevention)

1. **Ngăn chặn Enumeration Attack (Quét ID Ticket):**
   - Khi một `Requester` cố tình truy cập vào `ticketId` của người khác: Backend **bắt buộc trả về `404 Not Found`** thay vì `403 Forbidden`. Việc trả về `403` sẽ vô tình tiết lộ cho kẻ tấn công biết ID ticket đó tồn tại trong hệ thống.
   - Khi một `Agent` chưa được phân công cố tình truy cập ticket của team khác: Backend trả về `403 Forbidden` kèm ProblemDetails chuẩn.
2. **Khai trừ dữ liệu nhạy cảm ở cấp độ Server-side (No Frontend Hiding):**
   - Dữ liệu `Internal Notes` và `AI Confidence Logs` **tuyệt đối không được gửi qua wire**. Phải dùng AutoMapper / Mapster projection hoặc Projection DTOs riêng biệt (`TicketRequesterDetailDto` vs `TicketAgentDetailDto`).
   - Cấm hoàn toàn việc gửi toàn bộ list comment về FE rồi dùng `v-if` / React `{comment.isInternal && ...}` để ẩn.

---

## 6. Biên bản nghiệm thu & Handoff kỹ thuật

- **Reviewer Backend:** Core Backend Team Lead — Xác nhận sẽ cài đặt `IAuthorizationHandler` và áp dụng Predicates vào Service layer.
- **Reviewer Frontend:** Frontend Team Lead — Xác nhận tách riêng model `TicketPublicView` và `TicketAgentView`.
- **Handoff Action:**
  1. Cập nhật Jira `TC-246` và `TC-247` thành **Done** (kèm link tới tài liệu này).
  2. Xác nhận Jira `TC-196` đã đủ evidence hợp lệ.
