> **Tài liệu lịch sử — không dùng làm chỉ dẫn triển khai hiện hành.**
> Phạm vi bản gốc: hợp đồng ngày 2026-09-29. Chú thích bổ sung ngày 2026-10-19; không sửa nội dung hay ký duyệt lại bản gốc.
> Nguồn hiện hành: [hợp đồng backend](current-backend-contract.md), [quy tắc bảo mật](security-design-rules.md), [quyết định còn mở](pending-business-decisions.md).
> Đính chính sơ đồ mục 3: xác thực → cú pháp → tải trong phạm vi nhìn thấy → quyền hành động → revision → trạng thái/đối tượng đích → ghi nguyên tử. Không kiểm revision trước quyền như bản gốc. Ticket ngoài phạm vi phải trả 404 dù revision đúng hay sai; assignment chưa triển khai, phạm vi vai trò theo DEC-02.

<!-- original-record -->
# TC-294: Ticket Assignment Policy & Actor Permissions Specification

- **Document ID:** `ARCH-CONTRACT-06`
- **Jira Issue:** `TC-294` (Chốt assignment rules và actor permissions)
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, Frontend Web Team, Security Team
- **Direct Handoff Recipient:**
  - `TC-295`: Backend Assignment Service, Validation Rules & Concurrency Tests

---

## 1. Mục đích & Nguyên tắc Phân công (Assignment Principles)

Phân công ticket là mắt xích quyết định hiệu năng vận hành của IT Helpdesk. Nếu không có contract rõ ràng, Backend sẽ gặp tình trạng "Agent tự tiện gán ticket cho bất kỳ ai", "Gán cho Agent không thuộc team phụ trách", hoặc "Hai Agent cùng bấm nhận một ticket gây xung đột dữ liệu".

### Các nguyên tắc nền tảng
1. **Ràng buộc phân cấp Team-Agent:**
   - Một Agent chỉ có thể được phân công ticket nếu Agent đó **là thành viên tích cực (Active Member) của Support Team đang phụ trách ticket**.
   - Không được phân công chéo (gán ticket thuộc Network Team cho một Agent thuộc Application Team mà không chuyển Support Team trước).
2. **Quyền hạn rõ ràng theo vai trò:**
   - `Agent`: Được quyền tự nhận ticket (`SelfAssign`) cho chính mình nếu ticket thuộc team của mình. Không được tự ý ép gán ticket cho Agent khác trừ khi được cấp quyền.
   - `Support Team Lead`: Được toàn quyền điều phối, phân công (`AssignAgent`, `AssignTeam`, `Unassign`) mọi ticket trong Team mình quản lý.
   - `Admin`: Toàn quyền điều phối trên toàn hệ thống.
3. **Kiểm soát xung đột đồng thời (Concurrency Guard):**
   - Mọi thao tác phân công bắt buộc phải truyền kèm `Revision` hoặc kiểm tra token `xmin`. Nếu hai kỹ thuật viên cùng bấm nhận 1 ticket, người đến sau sẽ nhận mã lỗi `409 Conflict`.

---

## 2. Đặc tả Ngữ nghĩa 4 Lệnh Phân công (Command Semantics)

| Thuộc tính phân tích | Lệnh 1: `SelfAssign` (Tự nhận ticket) | Lệnh 2: `AssignAgent` (Phân công kỹ thuật viên) | Lệnh 3: `AssignTeam` (Chuyển giao cho Đội hỗ trợ) | Lệnh 4: `Unassign` (Hủy phân công kỹ thuật viên) |
|---|---|---|---|---|
| **API Endpoint** | `POST /api/v1/tickets/{id}/self-assign` | `POST /api/v1/tickets/{id}/assign-agent` | `POST /api/v1/tickets/{id}/assign-team` | `POST /api/v1/tickets/{id}/unassign` |
| **Allowed Actors** | Agent (thuộc team ticket), Lead, Admin | Support Team Lead (của team), Admin | Support Team Lead, Agent (escalate), Admin | Support Team Lead, Admin, hoặc chính Assigned Agent |
| **Trạng thái nguồn (Source States)** | `Triaged`, `Assigned` | `Triaged`, `Assigned`, `InProgress` | `Submitted`, `Triaged`, `Assigned`, `InProgress` | `Assigned`, `InProgress` |
| **Trạng thái đích (Target State)** | `Assigned` (Nếu nguồn là Triaged) | `Assigned` (nếu chưa Start) hoặc giữ `InProgress` | `Assigned` (reset AssignedAgent về null) | Chuyển về `Triaged` hoặc `Assigned` (với `AssignedAgentId = null`) |
| **Kiểm tra Team Membership** | Người gọi bắt buộc thuộc `ticket.SupportTeamId` | `TargetAgentId` bắt buộc thuộc `ticket.SupportTeamId` | `TargetTeamId` phải là một Support Team đang Active | Không áp dụng |
| **Kiểm tra Active User** | User đang hoạt động (`IsActive == true`) | Target Agent phải là `IsActive == true` và không bị khóa | Không áp dụng | Không áp dụng |
| **Xử lý phân công hiện tại** | Cho phép nhận nếu chưa ai nhận hoặc Lead override | Ghi đè `AssignedAgentId` mới, lưu lịch sử chuyển giao | Reset `AssignedAgentId = null` để team mới tự phân công | Xóa `AssignedAgentId`, gán `null` |
| **Audit Event** | `TicketSelfAssignedEvent` | `TicketAgentAssignedEvent` | `TicketTeamAssignedEvent` | `TicketUnassignedEvent` |
| **Mẫu DTO đầu vào** | `{ "revision": 3 }` | `{ "targetAgentId": "uuid", "revision": 3 }` | `{ "targetTeamId": "uuid", "reason": "Chuyển cấp 2", "revision": 3 }` | `{ "reason": "Kỹ thuật viên nghỉ đột xuất", "revision": 3 }` |

---

## 3. Quy trình Kiểm tra Ràng buộc Chi tiết (Validation Flow)

```text
[Request: AssignAgent]
          │
          ▼
1. Tìm Ticket theo Id? ───(Không tìm thấy)───► Trả về 404 Not Found
          │
          ▼
2. Kiểm tra Concurrency (Request.Revision == Ticket.Revision)? ───(Lệch)───► Trả về 409 Conflict
          │
          ▼
3. Quyền của Caller (Caller là Lead của ticket.SupportTeamId HOẶC Admin)? ───(Không)───► Trả về 403 Forbidden
          │
          ▼
4. Trạng thái Ticket có nằm trong [Triaged, Assigned, InProgress]? ───(Không)───► Trả về 422 InvalidState
          │
          ▼
5. TargetAgent có tồn tại và IsActive == true? ───(Không)───► Trả về 422 UserInactive
          │
          ▼
6. TargetAgent có thuộc ticket.SupportTeamId? ───(Không)───► Trả về 422 AgentNotInTeam
          │
          ▼
7. CẬP NHẬT TICKET:
   - Gán AssignedAgentId = TargetAgentId
   - Gán AssignedAt = Clock.UtcNow
   - Nếu Status == Triaged ──► Status = Assigned
   - Tăng Revision++
   - Ghi AuditLog (OldAgent -> NewAgent)
   - Lưu DbContext & Bắn Event TicketAgentAssignedEvent
          │
          ▼
Tránh race condition ──► Trả về 200 OK (Kèm TicketAgentViewDto mới nhất)
```

---

## 4. Bàn giao Kỹ thuật cho TC-295

Đội ngũ Backend khi triển khai `TC-295` cần bảo đảm:
1. Đặt toàn bộ logic trên vào Application Handler/Service (`AssignTicketCommandHandler`).
2. Viết kiểm thử tự động (Unit Test & Integration Test) bao phủ 100% các kịch bản phủ định:
   - Gán Agent của Team khác → Mong đợi lỗi `422 AgentNotInTeam`.
   - Gán cho User đã bị deactivate → Mong đợi lỗi `422 UserInactive`.
   - Hai request phân công cùng lúc với cùng 1 `Revision` → 1 request thành công `200`, 1 request thất bại `409 Conflict`.
