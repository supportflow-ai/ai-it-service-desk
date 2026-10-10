# TC-299: Ticket State Transition Matrix & Reopen Policy Specification

- **Document ID:** `ARCH-CONTRACT-04`
- **Jira Issue:** `TC-299` (Chốt transition matrix và reopen policy) — **Sprint 2 P0 / Highest Priority**
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, Frontend Web Team, QA Team
- **Direct Handoff Recipients:**
  - `TC-300`: Backend Ticket State Machine implementation
  - `TC-301`: Domain Events & Lifecycle Audit Trail
  - `TC-302`: Frontend Lifecycle Action Bars & UI State Machine Guards

---

## 1. Nguyên tắc thiết kế Command-Driven Lifecycle (Không dùng Generic Transition)

### 1.1 Khước từ thiết kế Generic `/transition` (Anti-Pattern)
Hệ thống **tuyệt đối không triển khai endpoint đa năng** kiểu `POST /api/v1/tickets/{id}/transition?targetStatus=InProgress`.
- **Lý do:** Generic transition làm mờ nhạt ý định nghiệp vụ (intent), biến tầng Domain thành Anemic Domain Model, khó kiểm soát guard condition riêng biệt của từng bước, và dễ tạo lỗ hổng bảo mật khi người dùng truyền status bất kỳ.
- **Quy chuẩn:** Mỗi bước chuyển trạng thái là một **Explicit Command Endpoint** với DTO đầu vào riêng biệt, thẩm quyền riêng và sự kiện Domain Event độc lập.

---

## 2. Ma trận Chuyển trạng thái chi tiết (State Transition Matrix)

| # | Trạng thái nguồn (From) | Lệnh gọi (Command Endpoint) | Trạng thái đích (To) | Thẩm quyền thực thi (Allowed Actor) | Điều kiện tiên quyết & Invariants (Guards) | Sự kiện Audit / History (Domain Event) |
|---|---|---|---|---|---|---|
| **1** | `Draft` | `POST /tickets/{id}/submit` | `Submitted` | Requester (Chủ sở hữu) | - `Title` >= 5 ký tự<br>- `Description` >= 10 ký tự<br>- Sinh `TicketNumber` hợp lệ<br>- Snapshot `RequesterDepartmentId` | `TicketSubmittedEvent` |
| **2** | `Submitted` | `POST /tickets/{id}/triage` | `Triaged` | Agent, Support Team Lead, Admin | - Bắt buộc cung cấp `CategoryId`<br>- Bắt buộc chọn `Impact` và `Urgency`<br>- Hệ thống tự tính `Priority = PriorityMatrix(Impact, Urgency)` | `TicketTriagedEvent` |
| **3** | `Triaged` | `POST /tickets/{id}/assign` | `Assigned` | Support Team Lead, Agent, Admin | - `SupportTeamId` hợp lệ và đang hoạt động<br>- `AssignedAgentId` (nếu có) phải là active user thuộc Support Team đó | `TicketAssignedEvent` |
| **4** | `Assigned` | `POST /tickets/{id}/start` | `InProgress` | Assigned Agent (hoặc Lead/Admin) | - Ticket đã có `AssignedAgentId`<br>- User thực thi phải chính là `AssignedAgentId` (trừ khi Lead ghi nhận can thiệp) | `TicketStartedEvent` |
| **5** | `InProgress` | `POST /tickets/{id}/request-info` | `PendingUser` | Assigned Agent, Lead | - Bắt buộc cung cấp `Message` (Public Comment gửi cho Requester) giải thích rõ thông tin cần bổ sung | `InformationRequestedEvent` |
| **6** | `PendingUser` | `POST /tickets/{id}/reply` | `InProgress` | Requester (Chủ sở hữu) | - Bắt buộc có nội dung phản hồi `Message`<br>- Tự động reset bộ đếm thời gian chờ phản hồi | `RequesterRepliedEvent` |
| **7** | `InProgress` | `POST /tickets/{id}/wait-external` | `PendingExternal` | Assigned Agent, Lead, Admin | - Cung cấp `VendorName` hoặc `ExternalReference`<br>- Ghi rõ lý do phụ thuộc bên ngoài vào Internal Note | `ExternalDependencyStartedEvent` |
| **8** | `PendingExternal` | `POST /tickets/{id}/resume` | `InProgress` | Assigned Agent, Lead, Admin | - Ghi chú kết quả phản hồi từ bên thứ 3 | `ExternalDependencyResumedEvent` |
| **9** | `InProgress`<br>`PendingExternal` | `POST /tickets/{id}/resolve` | `Resolved` | Assigned Agent, Lead, Admin | - Bắt buộc cung cấp `Resolution` tóm tắt giải pháp khắc phục (min 10 ký tự)<br>- Tùy chọn `ResolutionCategoryId`<br>- Gán `ResolvedAt = Clock.UtcNow` | `TicketResolvedEvent` |
| **10**| `Resolved` | `POST /tickets/{id}/close` | `Closed` | Requester (hoặc Admin / Job tự động đóng) | - Xác nhận nghiệm thu từ Requester<br>- Hoặc quá thời hạn 72 giờ không có khiếu nại (Auto-close Worker)<br>- Gán `ClosedAt = Clock.UtcNow` | `TicketClosedEvent` |
| **11**| `Resolved` | `POST /tickets/{id}/reopen` | `InProgress` | Requester (hoặc Lead/Admin) | - Trong vòng **7 ngày** (168 giờ) kể từ `ResolvedAt`<br>- Bắt buộc cung cấp `ReopenReason` (min 10 ký tự)<br>- Tăng `ReopenCount++` | `TicketReopenedEvent` |

---

## 3. Chính sách Mở lại Ticket (Reopen Policy Specification)

### 3.1 Căn cứ nghiệp vụ
Khi một sự cố đã được Agent đánh dấu `Resolved`, người dùng (Requester) có thể phát hiện lỗi chưa triệt để hoặc vấn đề tái diễn ngay sau đó. Tuy nhiên, việc cho phép mở lại ticket vô thời hạn sẽ phá vỡ báo cáo chỉ số SLA, MTTR (Mean Time to Resolution) và gây nhiễu loạn luồng công việc của kỹ thuật viên.

### 3.2 Các ràng buộc bất biến của Reopen Policy
1. **Khung thời gian cho phép (Time Window):**
   - Ticket chỉ được phép Reopen trong vòng **7 ngày dương lịch (168 giờ)** tính từ mốc thời gian `ResolvedAt`.
   - Nếu `Clock.UtcNow > ResolvedAt.Value.AddDays(7)`: Hệ thống từ chối lệnh Reopen và trả về mã lỗi `422 REOPEN_WINDOW_EXPIRED`. Frontend sẽ ẩn nút "Mở lại" và hiển thị gợi ý *"Thời hạn mở lại ticket đã hết. Vui lòng tạo ticket mới"*.
2. **Thẩm quyền:**
   - `Requester`: Chỉ được mở lại ticket do chính mình sở hữu (`RequesterId == CurrentUserId`).
   - `Support Team Lead`: Được quyền mở lại ticket thuộc phạm vi Team mình phụ trách (hỗ trợ trường hợp phát hiện nghiệm thu sai).
   - `Admin`: Toàn quyền hệ thống.
3. **Dữ liệu bắt buộc khi Reopen:**
   - DTO `ReopenTicketRequest` bắt buộc trường `ReopenReason` (độ dài tối thiểu 10 ký tự, tối đa 1000 ký tự).
   - Lý do này tự động được lưu thành một Public Comment mang nhãn `[TICKET REOPENED]` để cả Requester và Agent cùng theo dõi.
4. **Hành vi Domain khi Reopen thành công:**
   - Trạng thái `Status` chuyển thành `TicketStatus.InProgress`.
   - `ReopenCount` tăng lên 1 (phục vụ thống kê chất lượng xử lý của Agent).
   - Giữ nguyên `AssignedAgentId` và `SupportTeamId` để Agent cũ tiếp tục xử lý, đồng thời gửi thông báo notification/email cho Agent.
   - `ResolvedAt` được reset về `null` (thời điểm resolved trước đó được lưu trong Audit Log).
   - Tăng `Revision++` và cập nhật `UpdatedAt = Clock.UtcNow`.

---

## 4. Định nghĩa Handoff kỹ thuật cho Sprint 2

Tài liệu này là căn cứ bàn giao trực tiếp cho:
- **`TC-300` (Backend Core):** Triển khai State Machine trong Application Service hoặc Domain Aggregate Root. Các phương thức nghiệp vụ:
  ```csharp
  public Result Submit(ICurrentUser user, IClock clock);
  public Result Triage(Guid categoryId, Impact impact, Urgency urgency, ICurrentUser user, IClock clock);
  public Result Assign(Guid supportTeamId, Guid? agentId, ICurrentUser user, IClock clock);
  public Result Start(ICurrentUser user, IClock clock);
  public Result RequestInfo(string message, ICurrentUser user, IClock clock);
  public Result Reply(string message, ICurrentUser user, IClock clock);
  public Result Resolve(string resolutionSummary, Guid? resolutionCategoryId, ICurrentUser user, IClock clock);
  public Result Close(ICurrentUser user, IClock clock);
  public Result Reopen(string reason, ICurrentUser user, IClock clock);
  ```
- **`TC-301` (Backend Infrastructure/Audit):** Lắng nghe các `DomainEvent` tương ứng để ghi vào bảng `TicketAuditLogs` và đẩy thông báo thời gian thực.
- **`TC-302` (Frontend Web):** Xây dựng component `TicketLifecycleActionBar` dựa trên ma trận trạng thái: chỉ hiển thị các nút bấm hợp lệ với vai trò hiện tại của người dùng.
