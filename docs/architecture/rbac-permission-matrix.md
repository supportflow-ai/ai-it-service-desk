# Ma trận quyền chính thức

Trạng thái: hiện hành, đối chiếu mã `dev@b3cc19a` ngày 21/11/2026. B1-PR3 đã áp dụng phạm vi đọc và lệnh nhân viên. [Phương án SupportTeam đã chốt](../adr/ADR-016-support-team-routing-and-agent-scope.md#phương-án-supportteam-đã-chốt) mô tả định tuyến và chuyển đội còn chờ triển khai. [Ma trận lịch sử](02-rbac-permission-matrix.md)

## Nhãn trạng thái

| Nhãn | Ý nghĩa |
| --- | --- |
| **Đã triển khai** | Có trong mã tại mốc đối chiếu |
| **Đã duyệt – chưa triển khai** | Chủ dự án đã chốt phần tương ứng và mã còn chờ cập nhật |
| **Mở** | Chưa có quyết định; không suy ra từ đề xuất cũ |
| **Lịch sử** | Đề xuất cũ đã bị thay thế hoặc không còn dùng |

## Vai trò và năng lực

- Vai trò JWT hiện có: **Requester**, **Agent**, **Admin**. Chính sách HTTP: `RequireRequester` nhận cả ba, `RequireAgent` nhận Agent và Admin, `RequireAdmin` chỉ Admin.
- **Lead** không phải vai trò JWT: là năng lực của thành viên active có `IsLead` trong `SupportTeamMember`, chỉ có hiệu lực trong nhóm đó.
- Quyền trên phiếu do Application kiểm theo chủ sở hữu, người phụ trách, nhóm và trạng thái, không chỉ theo policy HTTP.

## Phạm vi đọc phiếu

| Đối tượng | Phạm vi hiện hành |
| --- | --- |
| Requester | Chi tiết, timeline công khai và tệp của phiếu mình tạo |
| Agent có quyền xử lý phiếu | Phiếu thuộc nhóm đang hoạt động mà Agent là thành viên hoặc phiếu được gán trực tiếp cho Agent. Timeline có ghi chú nội bộ |
| Agent chỉ có quyền chủ phiếu | Chi tiết, bình luận và timeline công khai, cùng tệp của phiếu mình tạo |
| Admin | Mọi phiếu, kể cả ghi chú nội bộ |
| Thành viên có cờ `IsLead` | Đọc theo phạm vi Agent hoặc Admin tương ứng. Phân công và hủy phân công cho phiếu thuộc nhóm phụ trách |

Phiếu ngoài phạm vi của thao tác trả `404`. Hàng đợi và lệnh của nhân viên dùng phạm vi nhóm hoặc phân công. Tư cách thành viên được đọc từ cơ sở dữ liệu. Thay đổi đã commit có hiệu lực từ request kế tiếp. Quyền từ phân công trực tiếp vẫn có hiệu lực khi Agent còn được gán.

Backend kiểm quyền của người gọi và điều kiện của người nhận riêng. Mã cho gán tới tài khoản có vai trò Agent hoặc Admin. Lead phải gán thành viên cùng nhóm. Admin được gán ngoài nhóm. Phân công trực tiếp cấp cho Agent nhận phiếu quyền xử lý và xem ghi chú nội bộ. Yêu cầu đang xử lý đồng thời với thay đổi thành viên có thể hoàn tất theo quyền đã đọc trước khi thay đổi được ghi. [Kiểm vai trò người nhận](../../src/backend/Modules/Ticketing/Infrastructure/AgentDirectory.cs)

## Ma trận thao tác trên phiếu

| Thao tác | Quyền hiện tại | Trạng thái điều kiện | Ghi chú đích |
| --- | --- | --- | --- |
| Tạo phiếu | Mọi người dùng đã đăng nhập; chủ sở hữu là người gọi; hỗ trợ `Idempotency-Key` | Tạo ở Submitted | Draft: **Mở** (DEC-01) |
| Xem danh sách của tôi | Người gọi | | Không đổi |
| Hàng đợi | Agent theo nhóm hoặc phân công, Admin toàn hệ thống | Lọc phạm vi trước khi đếm và phân trang | Agent trực cần được cấp thành viên `MANUAL_TRIAGE` để thấy phiếu mới chưa gán |
| Cập nhật | Chủ sở hữu hoặc Admin | Chỉ Submitted | |
| Hủy | Chủ sở hữu hoặc Admin | Chỉ Submitted | |
| Nhận xử lý (claim) | Agent trong phạm vi xử lý hoặc Admin. Chỉ Admin có thể nhận thay người đang được gán | Submitted hoặc Assigned | |
| Phân công | Lead của nhóm phụ trách phiếu hoặc Admin. Người nhận có vai trò Agent hoặc Admin | Chặn Closed, Cancelled và Resolved | **Đã triển khai.** Lead chỉ gán cùng nhóm. Thẩm quyền bàn giao khi chuyển đội chờ TEAM-05/06 |
| Hủy phân công | Lead của nhóm phụ trách phiếu hoặc Admin | Chỉ Assigned khi đã có người phụ trách. Trả về Submitted | **Đã triển khai** |
| Tạm dừng | Agent trong phạm vi xử lý hoặc Admin | InProgress sang PendingUser hoặc PendingExternal | |
| Tiếp tục | Agent trong phạm vi xử lý hoặc Admin | PendingUser hoặc PendingExternal sang InProgress | |
| Giải quyết | Agent trong phạm vi xử lý hoặc Admin | InProgress hoặc PendingExternal | Nội dung giải quyết còn mở |
| Mở lại | Chủ sở hữu hoặc Admin | Resolved sang InProgress | Thời hạn mở lại: **Mở** (không dùng 168 giờ cũ) |
| Đóng | Chủ sở hữu hoặc Admin; Agent không đóng chỉ vì được phân công | Resolved sang Closed | **Đã triển khai** và khớp DEC-05 |
| Trạng thái cuối | Closed là terminal; Resolved không terminal | | Không tự động đóng trong MVP |
| Bình luận công khai | Chủ phiếu, Agent trong phạm vi xử lý hoặc Admin | Không viết vào phiếu Closed/Cancelled | Bình luận của chủ phiếu ở PendingUser đưa phiếu về InProgress theo DEC-03 |
| Ghi chú nội bộ | Agent trong phạm vi xử lý hoặc Admin | | |
| Timeline | Chủ phiếu chỉ thấy phần công khai nếu chỉ có quyền chủ sở hữu. Agent trong phạm vi xử lý và Admin thấy cả ghi chú nội bộ | | Khi có chuyển đội, ghi chú đi cùng phiếu và quyền được xét lại theo [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md#phương-án-supportteam-đã-chốt) |
| Tệp đính kèm | Người xem được phiếu: xin quyền tải lên, xác nhận, liệt kê, xin URL tải xuống; không viết vào phiếu Closed/Cancelled | | Giới hạn tệp: xem bên dưới |
| Gợi ý AI (đọc, duyệt) | Chưa có endpoint | | Quyền xem và duyệt còn mở theo AI-01. Xác nhận đội nhận thuộc quy trình SupportTeam |

`Triaged = 7` là giá trị enum chưa có lệnh chuyển vào. Việc lưu triage như dữ liệu hay dùng làm trạng thái chờ DEC-09. [Hợp đồng vòng đời](current-backend-contract.md#thao-tác-vòng-đời-phiếu)

## Người dùng, vai trò, tri thức

| Thao tác | Quyền hiện tại |
| --- | --- |
| Đăng ký | Ẩn danh; chỉ cấp Requester |
| Gán vai trò | Chỉ Admin; thu hồi refresh token của người được gán; không gỡ Admin cuối cùng |
| Danh sách người dùng theo vai trò | Agent hoặc Admin |
| Phòng ban | Mọi người dùng đã đăng nhập |
| Tri thức: tạo, sửa bản nháp, xuất bản, lưu trữ | Agent hoặc Admin tạo bài. Agent là tác giả hoặc Admin được sửa bản nháp, xuất bản và lưu trữ theo trạng thái bài với `expectedRevision` |
| Tri thức: đọc | Admin đọc mọi bài; Agent đọc bài đã xuất bản và bài của mình; người khác chỉ bài đã xuất bản |
| Phân tích, báo cáo | Chưa có module; **Mở** |

Access token đã phát giữ vai trò JWT toàn hệ thống tới khi hết hạn (`Jwt:ExpiryMinutes`). Cơ sở dữ liệu xác định tư cách thành viên hiện hành và là nguồn để kiểm quyền Lead khi phân công hoặc hủy phân công.

## Tệp đính kèm (DEC-07)

Chính sách mục tiêu **đã được chủ dự án chốt**; tình trạng mã khác nhau từng điều:

| Giới hạn | Chính sách | Trong mã tại mốc đối chiếu |
| --- | --- | --- |
| Dung lượng mỗi tệp | Tối đa 10 MiB | **Đã triển khai** |
| Định dạng | `.pdf`, `.png`, `.jpg`, `.jpeg`, `.docx`, `.txt`, `.zip` | **Đã triển khai** (danh sách đuôi tệp) |
| Số tệp mỗi phiếu | Tối đa 10 | Kiểm khi xác nhận tệp |
| Tổng dung lượng mỗi phiếu | Tối đa 50 MiB | Kiểm khi xác nhận tệp |
| Tin metadata của client | Không; backend tự kiểm giới hạn và loại tệp | Xác nhận đối chiếu dung lượng và loại tệp thực tế khi hoàn tất tải lên; chưa kiểm tra chữ ký nội dung |
| Quét mã độc | Chưa có | Chưa có |
| Lưu giữ, xóa | Chưa chốt | Chưa có chức năng xóa |

Backend khóa phiếu khi ghi xác nhận để hai giao dịch không cùng dùng phần hạn mức còn lại. Hạn mức này tính theo metadata tệp đã xác nhận.

## Quy tắc chung

- Kiểm quyền và phạm vi trước khi trả trạng thái hoặc revision. Phiếu ngoài phạm vi của từng thao tác trả `404`. Một Agent đọc phiếu qua nhóm vẫn có thể nhận `404` khi đóng phiếu nếu thiếu quyền chủ sở hữu. Thiếu vai trò ở chính sách HTTP hoặc nhận thay người đã được gán khi không có quyền nhận thay trả `403` cho người gọi đã xác thực. [Hợp đồng hiện hành](current-backend-contract.md)
- Các lệnh cập nhật và vòng đời phiếu cần `expectedRevision`. Thiếu trả `400`, lệch trả `409`. [Vị trí revision trong request](current-backend-contract.md#thao-tác-vòng-đời-phiếu)
- AI không tăng quyền của người dùng, không chọn Agent, không chuyển trạng thái, không chốt độ ưu tiên cuối (`TriagePolicy`).
- Không suy quyền từ `Department`, claim phía client hay dữ liệu frontend.
- Kiểm thử bắt buộc cho thay đổi quyền: trường hợp bị từ chối, người nhiều vai trò, IDOR giữa hai Requester, thu hồi quyền.
