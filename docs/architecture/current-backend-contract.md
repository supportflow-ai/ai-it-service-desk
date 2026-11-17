# Hợp đồng backend đang triển khai

Trạng thái: mô tả hiện hành, đối chiếu mã `dev` tại `8036e2c185dfef7c4ed9941d736388c781e85f52`, cập nhật 2026-11-08, sau PR #50–#55. Bổ sung 2026-11-16 (đối chiếu `dev@ef4390d`): phân công, hủy phân công, Impact/Urgency/Priority, nhóm hỗ trợ và các trạng thái chưa có lệnh chuyển. Bản chuẩn HTTP là [OpenAPI baseline](../../contracts/backend/openapi-baseline.json) (40 đường dẫn, 41 thao tác); tài liệu này mô tả thêm quyền, trạng thái và lỗi mà OpenAPI không nêu. Mốc nguồn có thể mới hơn nhánh local của người đọc; đối chiếu commit trước khi chạy. Cập nhật tài liệu không thay API, quyền hoặc dữ liệu và không xác nhận phiên bản VPS hay một bản phát hành. Quyết định kiến trúc: [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md). Chính sách cần chốt: [bảng quyết định](pending-business-decisions.md). Hồ sơ các đợt củng cố đầu tháng 11 là lịch sử: [giai đoạn 1](../runbooks/supportflow-hardening-phase-1.md), [giai đoạn 2–6](../runbooks/supportflow-hardening-phase-2-6.md).

## HTTP và danh tính

[Bảng mốc mã nguồn](source-baseline.md) ghi cách xác định mốc đối chiếu. Liên kết mã tương đối bên dưới mở checkout của người đọc; đối chiếu HEAD trước khi áp dụng. Cột "Chính sách" là chính sách HTTP của endpoint; Application kiểm tra thêm quyền theo phiếu (chủ sở hữu, người phụ trách, Admin).

| Nhóm | Thao tác | Đường dẫn | Chính sách | Ghi chú |
| --- | --- | --- | --- | --- |
| Xác thực | Đăng ký, đăng nhập, làm mới, quên và đặt lại mật khẩu | `POST /api/auth/register`, `/login`, `/refresh`, `/forgot-password`, `/reset-password` | Ẩn danh, có giới hạn tần suất | Chi tiết ở mục Identity bên dưới |
| Xác thực | Đăng xuất | `POST /api/auth/revoke` | Ẩn danh | Luôn 204 |
| Xác thực | Đổi mật khẩu, hồ sơ bản thân | `POST /api/auth/change-password`, `GET /api/auth/me` | Đã đăng nhập | Tài khoản hợp lệ nhưng không còn trong cơ sở dữ liệu trả 404 |
| Người dùng | Danh sách theo vai trò; gán vai trò | `GET /api/users`, `PUT /api/users/{id}/roles` | Agent hoặc Admin; Admin | |
| Phòng ban | Danh sách | `GET /api/departments` | Đã đăng nhập | Phòng ban của người gửi, không phải nhóm hỗ trợ |
| Phiếu | Tạo; danh sách cá nhân; hàng đợi; chi tiết; cập nhật | `POST /api/tickets`, `GET /api/tickets/me`, `GET /api/tickets/queue`, `GET /api/tickets/{id}`, `POST /api/tickets/{id}/update` | Requester (gồm Agent/Admin); hàng đợi chỉ Agent/Admin | Tạo: Submitted, 201 với `Location`, hỗ trợ `Idempotency-Key`; danh sách phân trang; cập nhật chỉ ở Submitted, chủ sở hữu hoặc Admin |
| Phiếu | Nhận, phân công, hủy phân công | `POST /api/tickets/{id}/claim`, `/assign`, `/unassign` | Agent hoặc Admin | Lưu người phụ trách và ghi audit; phân công/hủy phân công yêu cầu Lead của nhóm đang phụ trách phiếu hoặc Admin; phiếu đã phân công cho người khác chỉ Admin nhận thay được |
| Phiếu | Giải quyết, mở lại, đóng, hủy, tạm dừng, tiếp tục | `POST /api/tickets/{id}/resolve`, `/reopen`, `/close`, `/cancel`, `/pending`, `/resume` | Đã đăng nhập; Application kiểm vai trò và chủ sở hữu | Bảng chuyển trạng thái bên dưới; pending/resume yêu cầu Assigned Agent, Team Lead active hoặc Admin |
| Phiếu | Báo cáo phân tích khối lượng và SLA | `GET /api/tickets/analytics` | Agent hoặc Admin (`RequireAgent`) | Tham số `from`, `to` UTC tối đa 90 ngày; Requester 403 |
| Trao đổi | Bình luận; ghi chú nội bộ; timeline | `POST /api/tickets/{id}/comments`, `POST /api/tickets/{id}/internal-notes`, `GET /api/tickets/{id}/timeline` | Requester; Agent; Requester | Requester chỉ thấy phiếu của mình; ghi chú nội bộ không lộ cho Requester |
| Tệp | Xin quyền tải lên; xác nhận; danh sách; xin URL tải xuống | `POST /api/tickets/{id}/attachments/upload-url`, `/attachments/confirm`, `GET /api/tickets/{id}/attachments`, `/attachments/download-url` | Requester (theo phạm vi phiếu) | Luồng ở cuối tài liệu |
| Tri thức | Tạo bản nháp, sửa, xuất bản, lưu trữ | `POST /api/knowledge/articles`, `PUT .../{id}/draft`, `POST .../{id}/publish`, `POST .../{id}/archive` | Agent hoặc Admin | Sửa, xuất bản và lưu trữ bắt buộc `expectedRevision` |
| Tri thức | Chi tiết, tìm kiếm, danh sách | `GET /api/knowledge/articles/{id}`, `/search`, `GET /api/knowledge/articles` | Đã đăng nhập | Quyền đọc theo trạng thái bài viết |

Tại mốc đối chiếu trên, Agent và Admin đọc chi tiết toàn hệ thống. B1-PR3 thu hẹp quyền Agent theo [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md): nhóm đang hoạt động hoặc phân công trực tiếp. Bộ lọc áp dụng cho chi tiết, hàng đợi và số lượng, timeline, tệp và lệnh của nhân viên. Phiếu ngoài phạm vi trả 404. Agent vẫn truy cập được phần công khai của phiếu mình tạo theo quyền chủ sở hữu. Admin đọc toàn hệ thống. Tạo phiếu và danh sách cá nhân luôn theo người đăng nhập.

Application từ chối actor chưa xác thực, ID không phải GUID hợp lệ hoặc GUID rỗng trước persistence. Endpoint còn có policy role; role không được cấp bị từ chối tại HTTP. Worker dùng actor anonymous, không đăng ký HttpContext. Không chỉ dựa vào middleware để bảo vệ use case.

Nguồn: [AuthEndpoints](../../src/backend/Modules/Identity/Presentation/AuthEndpoints.cs), [TicketEndpoints](../../src/backend/Modules/Ticketing/Presentation/TicketEndpoints.cs) (tách thành các tệp `TicketEndpoints.*.cs` cùng thư mục), [KnowledgeEndpoints](../../src/backend/Modules/Knowledge/Presentation/KnowledgeEndpoints.cs), [GetTicketDetail](../../src/backend/Modules/Ticketing/Application/GetTicketDetail/GetTicketDetailUseCase.cs).

## Identity sau PR #47, #48 và nhánh refactor

Cập nhật 2026-11-07, đối chiếu PR #50 đến #53 vào `dev`. Mã ở [AuthEndpoints](../../src/backend/Modules/Identity/Presentation/AuthEndpoints.cs) và [DepartmentEndpoints](../../src/backend/Modules/Identity/Presentation/DepartmentEndpoints.cs).

| Thao tác | Đường dẫn | Quyền | Hành vi |
| --- | --- | --- | --- |
| Làm mới phiên | `POST /api/auth/refresh` | Ẩn danh, có rate limit | Thu hồi token cũ bằng một lệnh cập nhật có điều kiện và lưu token mới trong cùng giao dịch. Token đã thu hồi bị gửi lại, hoặc hai request cùng token chạy song song, thì cả chuỗi phiên của token đó (mọi token sinh từ cùng một lần đăng nhập, cột `family_id`) bị thu hồi, kể cả token vừa cấp cho request thắng; phiên trên thiết bị khác giữ nguyên. |
| Đăng xuất | `POST /api/auth/revoke` | Ẩn danh | Thu hồi cả chuỗi phiên của refresh token được gửi, kể cả khi token đó đã được xoay vòng hoặc một lần làm mới đang chạy song song; token không tồn tại thì bỏ qua. Luôn trả 204. Access token đã phát vẫn hiệu lực tới khi hết hạn. |
| Đổi mật khẩu | `POST /api/auth/change-password` | Đã đăng nhập | Đổi mật khẩu và thu hồi mọi refresh token của người đó trong cùng giao dịch. |
| Quên mật khẩu | `POST /api/auth/forgot-password` | Ẩn danh, có rate limit | Chỉ đưa email vào hàng đợi trong bộ nhớ rồi trả ngay cùng một thông điệp, nên nội dung và thời gian phản hồi không phụ thuộc email có tồn tại. Xử lý nền: chỉ phát mã khi email tồn tại và chưa có 5 mã trong một giờ; đếm, vô hiệu mã cũ và thêm mã chạy tuần tự theo email (khóa tư vấn PostgreSQL) nên luôn còn đúng một mã hiệu lực. Có `Email:Smtp:Host` thì gửi qua SMTP; chưa cấu hình thì `NoOpOtpSender` chỉ ghi cảnh báo. Log chỉ ghi loại lỗi, không ghi email hay mã. Hàng đợi mất khi tiến trình dừng; người dùng gửi lại yêu cầu. |
| Đặt lại mật khẩu | `POST /api/auth/reset-password` | Ẩn danh, có rate limit | Một giao dịch khóa dòng mã (`FOR UPDATE`). Sai mã tăng `failed_attempts`; sau 5 lần sai mã bị từ chối kể cả khi đúng. Đúng mã thì đổi mật khẩu, đánh dấu đã dùng và thu hồi mọi refresh token; mật khẩu mới không hợp lệ thì rollback, mã vẫn dùng được. |
| Danh sách người dùng theo vai trò | `GET /api/users?role=` | Agent hoặc Admin (`RequireAgent`) | Mặc định vai trò Agent; trả `UserId`, `FullName`. |
| Gán vai trò | `PUT /api/users/{id}/roles` | Admin | Đổi vai trò và thu hồi refresh token của người được gán trong cùng giao dịch. Không cho gỡ Admin của quản trị viên cuối cùng; các lần gán vai trò khóa dòng vai trò Admin nên hai yêu cầu đồng thời không thể gỡ hai Admin cuối. Access token đã phát vẫn mang vai trò cũ tới khi hết hạn (`Jwt:ExpiryMinutes`). |
| Phòng ban | `GET /api/departments` | Đã đăng nhập | Danh sách phòng ban. |

Giới hạn tần suất hai lớp. Theo IP: đăng ký, đăng nhập, quên và đặt lại mật khẩu tối đa 60 request mỗi 60 giây (`auth-ip`); `/refresh` có ngân sách riêng 120 request mỗi 60 giây (`auth-refresh`) nên không tiêu hết lượt đăng nhập. Theo email: email đã chuẩn hóa (cắt khoảng trắng, chữ thường) cộng loại thao tác (đăng nhập, quên mật khẩu, đặt lại mật khẩu) tối đa 10 request mỗi 900 giây, độc lập với IP và giữa các thao tác. Vượt ngưỡng trả 429 cùng nội dung `{error}` cho email tồn tại lẫn không tồn tại, kèm header `Retry-After` (đã khai báo 429 trong OpenAPI). Cấu hình ở mục `RateLimiting:Auth` (`Enabled`, `PermitLimit`, `WindowSeconds`, `RefreshPermitLimit`, `EmailPermitLimit`, `EmailWindowSeconds`). Request không có địa chỉ IP không bị giới hạn theo IP. Sau reverse proxy, địa chỉ lấy từ Forwarded Headers. Các ngưỡng 60/60 giây, 10/900 giây, 5 lần sai và 5 mã mỗi giờ là giá trị khởi điểm chưa được nhóm chốt; cần đo sau NGINX trên staging.

Mọi thao tác tạo hoặc thu hồi refresh token (đăng nhập, làm mới phiên, đăng xuất, đổi/đặt lại mật khẩu, đổi vai trò, thu hồi chuỗi phiên khi phát hiện token bị dùng lại) lấy khóa dòng người dùng (`AspNetUsers ... FOR UPDATE`) trước trong giao dịch của mình. Lệnh thu hồi vì vậy luôn chạy sau khi token mới đã commit; đăng nhập kiểm mật khẩu và lưu refresh token trong cùng một giao dịch. Đổi vai trò cũng đổi security stamp.

Bộ lọc lỗi `ApiErrorFilter` giữ mã 400/403/404/409/503. Thông điệp của `ArgumentException` và `UnauthorizedAccessException` chỉ được trả khi mã ServiceDesk ném ra; lỗi cùng loại từ .NET hoặc thư viện trả thông điệp chung ("Yêu cầu không hợp lệ.", "Bạn không có quyền thực hiện thao tác này.") và chỉ ghi chi tiết vào log. Knowledge chỉ trả 422 kèm thông điệp cho `InvalidOperationException` ném từ `ServiceDesk.Modules.Knowledge.Domain`; ngoại lệ cùng loại từ adapter hay thư viện thành lỗi 500 chung.

## Thao tác vòng đời phiếu

Các thao tác đều dùng POST. Chỉ `update` nhận nội dung JSON; các thao tác còn lại nhận ID trên đường dẫn. Điều kiện chuyển trạng thái nằm trong `Ticket`, quyền nằm trong từng luồng Application. Agent đọc được một phiếu không tự có quyền cập nhật, mở lại, đóng hoặc hủy phiếu của người khác.

| Thao tác | Trạng thái trước | Trạng thái sau | Giới hạn hiện tại |
| --- | --- | --- | --- |
| Cập nhật | Submitted | Submitted | Quy tắc tiêu đề, mô tả và danh mục như khi tạo |
| Nhận xử lý | Submitted hoặc Assigned | InProgress | Agent hoặc Admin; lưu người phụ trách và ghi audit. Phiếu đã phân công cho người khác chỉ Admin nhận thay được. Chưa có nhóm |
| Phân công | Mọi trạng thái trừ Closed, Cancelled và Resolved (DEC-05) | Assigned nếu đang Submitted; trạng thái khác giữ nguyên | Lead thuộc nhóm active đang phụ trách phiếu (gán cho Agent cùng nhóm) hoặc Admin (gán cho Agent bất kỳ); ngoài phạm vi trả 404, Agent thường/Lead ngoài nhóm trả 403, Resolved trả 400 |
| Hủy phân công | Assigned | Submitted | Chỉ khi đang Assigned; Lead của nhóm phụ trách phiếu hoặc Admin (Agent thường trả 403); InProgress không hủy phân công được |
| Giải quyết | InProgress | Resolved | Chưa có nội dung giải quyết, thời điểm giải quyết riêng hoặc lịch sử nghiệp vụ |
| Mở lại | Resolved | InProgress | Chưa có cửa sổ thời gian, kiểm tra người phụ trách hoặc tác vụ tự đóng |
| Đóng | Resolved | Closed | Chưa có tác vụ đóng tự động |
| Hủy | Submitted | Cancelled | Chưa nhận lý do hủy hoặc lưu lịch sử nghiệp vụ |
| Tạm dừng | InProgress | PendingUser hoặc PendingExternal | Assigned Agent, Team Lead active của nhóm sở hữu, hoặc Admin; body JSON `{ status, expectedRevision }` |
| Tiếp tục | PendingUser hoặc PendingExternal | InProgress | Assigned Agent, Team Lead active của nhóm sở hữu, hoặc Admin; query param `expectedRevision` |

Mọi lệnh ghi phiếu (cập nhật, nhận xử lý, phân công, hủy phân công, giải quyết, mở lại, đóng, hủy, tạm dừng, tiếp tục) bắt buộc `expectedRevision`: thiếu hoặc bằng 0 trả 400, khác revision hiện tại (`xmin`) trả 409. Trạng thái khác bị Domain từ chối; `TicketCommandGuard` chuyển lỗi trạng thái thành `ArgumentException`, bộ lọc HTTP trả 400 với `{ error }`. Phiếu thiếu hoặc ngoài quyền chủ sở hữu của các lệnh `update/reopen/close/cancel` trả 404. Các lệnh mới chưa có cơ chế khóa chống tạo trùng hoặc phát lại phản hồi như thao tác tạo phiếu. Yêu cầu đã thực hiện thành công rồi gửi lại có thể nhận lỗi trạng thái.

## Các điểm đã xử lý sau refactor

Đối chiếu `dev@314f5b9`. Các vấn đề từng ghi ở mục này tại mốc PR #29 đã được sửa trong mã:

| Vấn đề cũ | Hành vi hiện tại |
| --- | --- |
| Quyền nhận xử lý chưa thống nhất | `ClaimTicketUseCase` nhận Agent hoặc Admin, khớp chính sách `RequireAgent`; phiếu đã phân công cho người khác chỉ Admin nhận thay được (403 với người khác). |
| Giải quyết tải phiếu trước khi kiểm vai trò | `ResolveTicketUseCase` kiểm Agent/Admin trước khi tải phiếu; Requester luôn nhận 403, không phân biệt được phiếu có tồn tại hay không. |
| Ghi đồng thời chưa có phản hồi ổn định | Lệnh ghi kiểm `expectedRevision` với `xmin` trước khi lưu; `DbUpdateConcurrencyException` khi lưu được chuyển thành `ConcurrencyConflictException` và trả 409. |
| Phân trang Knowledge tràn offset | `(page - 1) * pageSize` vượt `int` trả 400, cùng hợp đồng với danh sách phiếu (trước đây phép nhân tràn số và trả nhầm trang đầu). |
| Bình luận/tệp ghi vào phiếu vừa đóng | Bình luận công khai và xác nhận tệp đọc lại trạng thái phiếu với `FOR SHARE` trong giao dịch ghi (`TicketRowLock`); người ngoài phạm vi nhận 404 trước khi xét trạng thái. |

`GetForUpdateAsync` vẫn đọc phiếu bằng `SingleOrDefaultAsync` (không khóa dòng); xung đột được phát hiện bằng revision. Xem [quy tắc bảo mật](security-design-rules.md) và [hướng dẫn kiểm chứng](../runbooks/backend-hardening-verification.md).

## Điểm còn lệch giữa chính sách và mã

Cập nhật ngày 2026-11-16: Khoảng lệch DEC-05 (cấm phân công phiếu Resolved) đã được xử lý hoàn toàn trong mã Domain, Application và kiểm thử HTTP/Integration. Chính sách quyền Lead/Admin (DEC-09 và ADR-016) đã được thực hiện. Hiện không còn khoảng lệch giữa chính sách đã chốt và mã đối với các mục này.

## Dữ liệu phiếu và mã trạng thái

| Trường | Ràng buộc hiện tại |
| --- | --- |
| `title` | Trim, không rỗng, tối đa 100 ký tự; cột DB vẫn tối đa 200 để giữ dữ liệu cũ |
| `description` | Trim, tối thiểu 10 ký tự; không tự thêm giới hạn 4000 từ đề xuất cũ |
| `categoryId` | Mã chuỗi thuộc danh sách được cho phép, không phải GUID |
| `requesterId` | Lấy từ actor trong Application, không nhận từ command |
| `ticketNumber` | Sinh bằng sequence DB, `IT-YYYY-…`, độ rộng tối thiểu bốn chữ số; không reset theo năm |
| `requesterDepartmentId` | Nullable; chưa có luồng snapshot hồ sơ/phòng ban hoàn chỉnh |
| `status` | Số nguyên trong JSON; khi tạo là Submitted (1) |
| `impact`, `urgency`, `priority` | Khi tạo là Medium, Medium, P3. Chưa có endpoint hay use case ghi các trường này (`Ticket.SetTriage` có trong Domain nhưng chưa được gọi), nên mọi phiếu hiện mang P3; bộ lọc và sắp xếp theo `priority` của hàng đợi chạy trên giá trị này |
| `supportTeamId` | Phiếu mới vào `MANUAL_TRIAGE`. Chưa có `RoutingRule` hay API chuyển nhóm ([ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md)) |
| `sla` | `SlaSummaryDto` chứa kết quả đánh giá SLA tức thời của vé (`isApplicable`, `firstResponseDeadline`, `isFirstResponseBreached`, `firstResponseState`, `resolutionDeadline`, `isResolutionBreached`, `resolutionState`, `resolutionRemainingMinutes`). Vé legacy có `isApplicable: false`, các cờ false/null, state `"NotApplicable"`. Việc đọc vé và trả summary là thuần túy, không mutate thực thể và không làm thay đổi `xmin`. |
| Bộ lọc SLA hàng đợi | Endpoint `GET /api/tickets/queue` hỗ trợ query parameter `slaStatus` với các giá trị: `All`, `OnTrack`, `AtRisk`, `Breached` (không phân biệt hoa thường). Giá trị không hợp lệ trả về 400 Bad Request. Lọc SLA diễn ra trực tiếp tại SQL predicate trong PostgreSQL trước khi tính `TotalCount` và thực hiện phân trang (`Skip`/`Take`). |
| Analytics API | Endpoint `GET /api/tickets/analytics` nhận `from` và `to` (ISO-8601 có múi giờ tường minh Z hoặc offset, `from < to`, khoảng cách tối đa đúng 90 ngày). Quyền: `RequireAgent` (Agent/Admin; Requester nhận 403; chưa đăng nhập nhận 401). Phân tích trên cohort nửa mở `CreatedAt >= from AND CreatedAt < to`. Dữ liệu tổng hợp gồm `volume`, `statusDistribution`, `priorityDistribution`, `firstResponseCompliance`, `resolutionCompliance`, `averageResolutionMinutes`, `resolutionEligibleCount`. Tỷ lệ compliance tính bằng % (`Achieved / (Achieved + Breached) * 100`, mẫu số 0 trả null). `averageResolutionMinutes` tính bằng phút làm tròn 2 chữ số thập phân trên các vé áp dụng SLA hiện ở Resolved hoặc Closed (mẫu số 0 trả null); không gọi elapsed là giờ lao động thực tế hoặc KPI cá nhân. Chạy bằng 1 câu lệnh SQL aggregation duy nhất trên snapshot database, không tải ticket về RAM. |
| Phân trang | Mặc định page=1, pageSize=10; page ≥ 1, pageSize từ 1 đến 100; kiểm tra tràn offset ở Application |

| Giá trị đã định nghĩa | Tên |
| ---: | --- |
| 0 | Draft |
| 1 | Submitted |
| 2 | InProgress |
| 3 | PendingUser |
| 4 | Resolved |
| 5 | Closed |
| 6 | Cancelled |
| 7 | Triaged |
| 8 | Assigned |
| 9 | PendingExternal |

Nguồn: [TicketStatus](../../src/backend/Modules/Ticketing/Domain/TicketStatus.cs), [Ticket](../../src/backend/Modules/Ticketing/Domain/Ticket.cs), [TicketDto](../../src/backend/Modules/Ticketing/Application/Contracts/TicketDto.cs), [mapping](../../src/backend/Modules/Ticketing/Infrastructure/Configurations/TicketConfiguration.cs). Có enum không có nghĩa đã triển khai lệnh chuyển trạng thái: chưa lệnh nào tạo hoặc chuyển tới Draft (0) và Triaged (7); theo DEC-09 (chốt 2026-11-16), phân loại chỉ là dữ liệu và không thêm chuyển trạng thái tới Triaged (7), giữ enum để tương thích dữ liệu đã lưu; Draft (0) chờ DEC-01. PendingUser (3) và PendingExternal (9) đã có quyết định nghiệp vụ theo DEC-03, DEC-04 và chính sách SLA Sprint 4 (chốt 2026-11-16 và 2026-11-17). Không đổi nghĩa 0–6, loại Cancelled hoặc đổi JSON sang chuỗi để khớp bảng 0–8 trong tài liệu lịch sử.

## Chính sách Cam kết Mức Dịch vụ (SLA) và Báo cáo Phân tích (Analytics)

Nguồn thẩm quyền: Được chủ dự án phê duyệt ngày 2026-11-16 và 2026-11-17 cho Sprint 4 MVP/demo (ghi nhận tại Mục §3 của cẩm nang kế hoạch bàn giao). Tách biệt hoàn toàn khỏi các đề xuất chưa duyệt.

### 1. Thời lượng Cam kết và Lịch làm việc
- **Lịch tính giờ:** Calendar 24/7 UTC liên tục qua `IClock`, không trừ ban đêm, cuối tuần hoặc ngày lễ trong MVP/demo.
- **Mốc bắt đầu:** Tính từ `Ticket.CreatedAt` (do backend xác lập bằng `IClock`), không nhận từ client. Thời gian chờ triage hoặc phân công vẫn tính vào cả hai đồng hồ. Idempotent replay giữ nguyên CreatedAt gốc.
- **Target Snapshot theo Priority lúc tạo:**
  + P1: First Response 60 phút; Resolution 240 phút.
  + P2: First Response 120 phút; Resolution 480 phút.
  + P3: First Response 240 phút; Resolution 1.440 phút.
  + P4: First Response 480 phút; Resolution 4.320 phút.

### 2. Đồng hồ Phản hồi Đầu tiên (First Response Clock)
- **Sự kiện kích hoạt:** Sự kiện sớm nhất giữa bình luận công khai hợp lệ của nhân viên hỗ trợ (`IsInternal = false`, người gửi có role Agent hoặc Admin và có quyền trên phiếu) HOẶC phiếu được `Resolve` thành công.
- **Loại trừ:** Bình luận của Requester sở hữu phiếu (kể cả có role Agent/Admin), ghi chú nội bộ, audit hệ thống, thao tác nhận/phân công/hủy phân công, hoặc phản hồi tự động.
- **Tính chất:** Không tạm dừng trong `PendingUser` hoặc `PendingExternal`. Đã xác lập `FirstRespondedAt` thì giữ nguyên vĩnh viễn, không chạy lại khi Reopen hoặc đổi Priority.

### 3. Đồng hồ Giải quyết (Resolution Clock) và Vòng đời Trạng thái
- **Tạm dừng (Pause / Resume):** Tạm dừng khi chuyển sang `PendingUser` hoặc `PendingExternal`; tiếp tục tính phần thời gian còn lại khi trở lại `InProgress`.
- **Quyền chuyển Pending/Resume thủ công:** Áp dụng cho 4 chuyển đổi giữa `InProgress` và `PendingUser`/`PendingExternal`: Assigned Agent, Team Lead active của nhóm đang sở hữu phiếu, hoặc Admin. Thành viên nhóm chỉ có quyền đọc bị từ chối 403; ngoài scope trả 404; bắt buộc `expectedRevision`.
- **Auto-Resume (DEC-03):** Khi Requester sở hữu phiếu gửi bình luận công khai trong `PendingUser`, phiếu tự động chuyển sang `InProgress` và tiếp tục Resolution Clock; bình luận này không tính là First Response.
- **Resolve trực tiếp từ PendingExternal (DEC-04):** Cho phép Resolve từ `InProgress` và `PendingExternal`. Khi Resolve từ `PendingExternal`, lấy timestamp UTC chốt pause một lần duy nhất, chuyển sang `Resolved`, dừng đồng hồ Resolution và ghi nhận First Response nếu trước đó chưa có.
- **Reopen Carry-over:** Reopen từ `Resolved` sang `InProgress` tiếp tục tích lũy trên tổng thời gian đã làm việc trước đó; thời gian nằm ở `Resolved` chờ nghiệm thu hoàn toàn không tính vào duration; vi phạm cũ giữ nguyên.
- **Thay đổi Priority:** Cập nhật target cho các đồng hồ chưa hoàn thành. Luôn đánh giá vi phạm theo target cũ tại cùng timestamp trước khi áp dụng target mới; leo thang có thể gây vi phạm ngay; hạ Priority nới target nhưng không xóa dấu vi phạm cũ. First Response đã phản hồi thì giữ nguyên target snapshot cũ.

### 4. Ranh giới Đánh giá và Quy tắc Sticky Breach
- So sánh thời gian tiêu hao $E$ và target $T$ theo độ chính xác thời gian:
  + $E < 0.75 \times T$: `OnTrack`.
  + $0.75 \times T \le E \le T$: `AtRisk` khi đồng hồ chưa hoàn thành (tại đúng $E = T$ đồng hồ chưa hoàn thành thuộc `AtRisk`).
  + Hoàn thành tại $E \le T$ và chưa từng breach: `Achieved`.
  + $E > T$ hoặc đã từng breach trước đó: `Breached`.
- **Sticky Breach:** Cờ vi phạm có ưu tiên tuyệt đối cao hơn `OnTrack`/`AtRisk`/`Achieved`. Đã vi phạm thì vĩnh viễn không trở lại trạng thái đạt dù có hạ Priority, tạm dừng, Resolve hay Reopen.

### 5. Xử lý Dữ liệu Lịch sử (Legacy Tickets)
- Toàn bộ ticket tạo trước thời điểm kích hoạt SLA mang cờ `IsSlaApplicable = false`, hiển thị `NotApplicable`.
- Không hồi tố, không backfill. Worker và bộ lọc hàng đợi bỏ qua các ticket này. Ticket lịch sử vẫn tính trong tổng Volume và Distribution của Analytics nhưng bị loại hoàn toàn khỏi mẫu số của Compliance Rate và Average Resolution.

### 6. Báo cáo Phân tích (Analytics API)
- **Đường dẫn:** `GET /api/tickets/analytics?from={ISO8601}&to={ISO8601}`.
- **Tham số:** `from` và `to` bắt buộc có múi giờ tường minh (Z hoặc offset được URL-encode), khoảng cách `to - from` tối đa đúng 90 ngày UTC ($7.776.000$ giây). Lọc trên cohort nửa mở `CreatedAt >= fromUtc AND CreatedAt < toUtc`.
- **Phân quyền Phạm vi Dữ liệu (Scope):**
  + Admin: Thống kê trên toàn bộ phiếu thuộc cohort trong hệ thống.
  + Agent / Lead: Thống kê trên hợp của các nhóm active mà actor là thành viên + các phiếu được phân công trực tiếp (`AssignedToId == actorId`). Overlap chỉ đếm một lần; phiếu do Agent tạo với tư cách Requester không tự được cộng vào scope.
  + Requester: Từ chối 403 Forbidden. Chưa đăng nhập: 401 Unauthorized.
- **Chỉ số & Công thức:**
  + Phân bổ Status và Priority phản ánh snapshot tại thời điểm truy vấn. Tổng của mỗi distribution luôn bằng đúng `Volume` (có bucket `"Other"` và `"Unassigned"` cho dữ liệu dị biệt).
  + Tỷ lệ tuân thủ First Response và Resolution: $\text{Rate} = \frac{\text{Achieved}}{\text{Achieved} + \text{Breached}}$. Mẫu số bằng 0 trả `null`.
  + Thời gian giải quyết trung bình (MTTR): $\text{Average} = \frac{\sum \text{Duration các vé đủ điều kiện}}{\text{Số lượng vé đủ điều kiện}}$ tính trên các vé áp dụng SLA hiện ở `Resolved` hoặc `Closed`. Loại bỏ vé Reopen đang xử lý dở dang. Không có vé đủ điều kiện trả `null`.

### 7. Hướng dẫn Vận hành Maintenance Cutover
1. **Dừng Writer cũ:** Chuyển hệ thống sang cửa sổ bảo trì. Chặn lưu lượng ghi mới tại API Gateway/Reverse Proxy và đảm bảo toàn bộ các instance writer cũ (API cũ) đã dừng hoàn toàn hoặc bị cô lập khỏi đường ghi, không tự khởi động lại.
2. **Kiểm tra Giao dịch Ghi cũ:** Xác minh không còn transaction ghi nào đang hoạt động trên cơ sở dữ liệu.
3. **Chạy Migration:** Chạy migration `20261116180130_AddTicketSlaSupport` bằng công cụ `ServiceDesk.Migrator` với metadata đích đã được xác minh (`SD_DB_HOST`, `SD_DB_PORT`, `SD_DB_NAME`) và được ủy quyền.
4. **Khởi động Phiên bản Mới:** Khởi động các instance API và Worker phiên bản mới trong môi trường bảo trì.
5. **Smoke Test & Mở Lưu lượng:** Kiểm tra tạo phiếu mới có SLA nguyên tử, kiểm tra hàng đợi và Analytics, kiểm tra vé legacy không bị ảnh hưởng. Sau khi đạt, mở lại lưu lượng cho người dùng.
6. **Điều kiện Rollback:** Nếu migration hoặc smoke test thất bại, dừng API/Worker mới, khôi phục database về bản sao lưu trước bảo trì; tuyệt đối không bật lại writer cũ trên schema mới khi chưa có kiểm chứng tương thích hai chiều.

## Lỗi và idempotency

- Giữ hình dạng phản hồi lỗi hiện hành, không chuyển đồng loạt sang ProblemDetails. [ApiErrorFilter](../../src/backend/BuildingBlocks/SharedKernel.Web/ApiErrorFilter.cs) giữ `{ error }` cho lỗi đầu vào/chống tạo trùng; 404 chi tiết không dựng một DTO lỗi mới.
- Tạo ticket hỗ trợ `Idempotency-Key` tùy chọn. Receipt nằm trong phạm vi requester + method + resource + key hash; khác payload cùng key trả 409.
- Advisory lock và transaction PostgreSQL giữ ticket/receipt nguyên tử. Receipt dùng giờ DB, hiệu lực 10 phút; lock timeout 5 giây; timeout trả 503 và `Retry-After: 1`.
- Replay trả response đã lưu sau kiểm tra quyền sở hữu, không dựng lại từ ticket có thể thay đổi. Không dùng cache trong tiến trình thay cơ chế này.
- Dấu vân tay tạo phiếu giữ thuật toán riêng; không thay bằng mã băm AI `ai-input-v2`.

## Kiểm chứng và giới hạn

[OpenAPI baseline](../../contracts/backend/openapi-baseline.json) cùng [ApiContractTests](../../tests/ServiceDesk.IntegrationTests/ApiContractTests.cs), [TicketAccessScopeTests](../../tests/ServiceDesk.IntegrationTests/TicketAccessScopeTests.cs), [TicketIdempotencyPostgresTests](../../tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresTests.cs) bảo vệ các phần tương ứng. Baseline OpenAPI không thay kiểm thử hành vi, dữ liệu và quyền.

Kiểm thử `PublishedApiMatchesBaseline` so sánh toàn bộ tài liệu OpenAPI với bản chuẩn, phát hiện cả mục mới chưa công bố và thay đổi mô tả/bảo mật. Cập nhật bản chuẩn có chủ đích theo [quy chuẩn tài liệu](../process/documentation-standard.md).

Chưa có luồng Draft/Submit riêng, API quản trị nhóm hỗ trợ (dữ liệu và phạm vi đọc theo nhóm đã có), AI thật hoặc thu hồi tức thời quyền trong access token đã phát. Chính sách SLA và Analytics Sprint 4 đã được duyệt đầy đủ ngày 2026-11-16 và 2026-11-17, đang được triển khai theo từng lát mỏng. Luồng tệp đính kèm: xin quyền tải lên nhận form multipart POST đã ký (giới hạn 10 MiB, gắn actor/ticket/tên/dung lượng/MIME/key/hết hạn) → xác nhận đối chiếu metadata object, sao chép sang key cuối bất biến rồi mới ghi metadata vào DB → tải xuống bằng URL ký ngắn hạn. Grant PUT cũ còn hạn vẫn được xác nhận theo cùng cách (xem [runbook](../runbooks/supportflow-hardening-phase-2-6.md)). Còn mở: đối soát object mồ côi khi lưu trữ thành công nhưng kết quả giao dịch DB không xác định. FullName chưa có dữ liệu hồ sơ riêng được lưu. Các phần còn thiếu thuộc đợt phát triển được quyết định riêng.

Ứng viên B3 áp dụng [DEC-07](pending-business-decisions.md) cho tệp đã xác nhận. Mỗi phiếu có tối đa 10 tệp với tổng dung lượng tối đa 50 MiB. Khi xác nhận tệp mới, backend kiểm tra hạn mức và lưu metadata trong cùng giao dịch khóa theo phiếu. Nếu vượt hạn mức, API trả 400. Nếu hết thời gian chờ khóa, API trả 503 với `Retry-After` và giữ grant để gửi lại. Gửi lại yêu cầu xác nhận thành công trả về tệp đã ghi mà không tính thêm dung lượng. Grant đang chờ xác nhận chưa chiếm hạn mức tệp đã xác nhận.
