# Ma trận quyền chính thức

Trạng thái: tài liệu hiện hành, đối chiếu mã `dev@a182c9f` (2026-11-08; phân quyền chưa đổi kể từ `8036e2c`) và [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md). Thay thế vai trò tham chiếu của đề xuất cũ [02-rbac-permission-matrix.md](02-rbac-permission-matrix.md), vốn chỉ còn là nguồn truy vết. "Đã triển khai" là mô tả mã, không phải nghiệm thu; kết quả kiểm thử phải gắn với mốc chạy riêng.

## Nhãn trạng thái

| Nhãn | Ý nghĩa |
| --- | --- |
| **Đã triển khai** | Có trong mã tại mốc đối chiếu |
| **Đã duyệt – chưa triển khai** | Chủ dự án đã chốt (ADR-016, DEC-05, DEC-07), mã chưa làm |
| **Mở** | Chưa có quyết định; không suy ra từ đề xuất cũ |
| **Lịch sử** | Đề xuất cũ đã bị thay thế hoặc không còn dùng |

## Vai trò và năng lực

- Vai trò JWT hiện có: **Requester**, **Agent**, **Admin**. Chính sách HTTP: `RequireRequester` nhận cả ba, `RequireAgent` nhận Agent và Admin, `RequireAdmin` chỉ Admin.
- **Lead** (đích, đã duyệt) không phải vai trò JWT: là năng lực của thành viên active có `IsLead` trong `SupportTeamMember`, chỉ có hiệu lực trong nhóm đó.
- Quyền trên phiếu do Application kiểm theo chủ sở hữu, người phụ trách, nhóm và trạng thái, không chỉ theo policy HTTP.

## Phạm vi đọc phiếu

| Đối tượng | Hiện tại (**Đã triển khai**) | Đích |
| --- | --- | --- |
| Requester | Chỉ phiếu của mình; ngoài phạm vi trả 404 | Không đổi |
| Agent | Mọi phiếu (`AllTickets`) | **Đã duyệt – chưa triển khai**: phiếu thuộc nhóm mình là thành viên active hoặc được phân công trực tiếp |
| Admin | Mọi phiếu | Không đổi |
| Lead | Chưa có | **Đã duyệt – chưa triển khai**: đọc và điều phối trong nhóm mình phụ trách |

Chi tiết phiếu, timeline, danh sách tệp, xin URL tải xuống và hàng đợi dùng cùng quy tắc phạm vi. Đã chốt (ADR-016): Agent thuộc nhiều nhóm đọc theo hợp mọi nhóm đang hoạt động; phiếu cũ chưa kết thúc vào `MANUAL_TRIAGE`. Còn mở: lịch sử ghi chú nội bộ sau khi chuyển nhóm, hiệu lực tức thời khi thu hồi tư cách thành viên.

## Ma trận thao tác trên phiếu

| Thao tác | Quyền hiện tại | Trạng thái điều kiện | Ghi chú đích |
| --- | --- | --- | --- |
| Tạo phiếu | Mọi người dùng đã đăng nhập; chủ sở hữu là người gọi; hỗ trợ `Idempotency-Key` | Tạo ở Submitted | Draft: **Mở** (DEC-01) |
| Xem danh sách của tôi | Người gọi | | Không đổi |
| Hàng đợi | Agent, Admin; hiện thấy mọi phiếu | | Lọc theo nhóm/phân công: **Đã duyệt – chưa triển khai** |
| Cập nhật | Chủ sở hữu hoặc Admin | Chỉ Submitted | |
| Hủy | Chủ sở hữu hoặc Admin | Chỉ Submitted | |
| Nhận xử lý (claim) | Agent hoặc Admin; phiếu đã phân công cho người khác chỉ Admin nhận thay được | Submitted hoặc Assigned | Phạm vi theo nhóm cho Agent thường: **Mở** |
| Phân công / hủy phân công | Mọi Agent hoặc Admin, cho bất kỳ Agent nào | Gán không được khi Closed hoặc Cancelled; hủy phân công chỉ khi Assigned | Đích: Lead trong nhóm mình hoặc Admin (**Đã duyệt – chưa triển khai**). Cấm phân công lại khi Resolved (DEC-05): **Đã duyệt – chưa triển khai**, mã hiện vẫn cho gán khi Resolved |
| Giải quyết | Agent hoặc Admin | Chỉ InProgress | Nội dung giải quyết: **Mở** |
| Mở lại | Chủ sở hữu hoặc Admin | Resolved sang InProgress | Thời hạn mở lại: **Mở** (không dùng 168 giờ cũ) |
| Đóng | Chủ sở hữu hoặc Admin; Agent không đóng chỉ vì được phân công | Resolved sang Closed | **Đã triển khai** và khớp DEC-05 |
| Trạng thái cuối | Closed là terminal; Resolved không terminal | | Không tự động đóng trong MVP |
| Bình luận công khai | Chủ sở hữu phiếu; Agent/Admin | Không viết vào phiếu Closed/Cancelled | Phản hồi khi PendingUser: **Mở** (DEC-03) |
| Ghi chú nội bộ | Agent hoặc Admin | | Requester không bao giờ thấy |
| Timeline | Requester chỉ thấy phần công khai; staff thấy cả ghi chú nội bộ | | Lịch sử sau chuyển nhóm: **Mở** |
| Tệp đính kèm | Người xem được phiếu: xin quyền tải lên, xác nhận, liệt kê, xin URL tải xuống; không viết vào phiếu Closed/Cancelled | | Giới hạn tệp: xem bên dưới |
| Gợi ý AI (đọc, chấp nhận) | Chưa có endpoint | | Đích: Agent đọc; xác nhận định tuyến là việc của Lead (**Đã duyệt – chưa triển khai**) |

## Người dùng, vai trò, tri thức

| Thao tác | Quyền hiện tại |
| --- | --- |
| Đăng ký | Ẩn danh; chỉ cấp Requester |
| Gán vai trò | Chỉ Admin; thu hồi refresh token của người được gán; không gỡ Admin cuối cùng |
| Danh sách người dùng theo vai trò | Agent hoặc Admin |
| Phòng ban | Mọi người dùng đã đăng nhập |
| Tri thức: tạo, sửa bản nháp, xuất bản, lưu trữ | Agent hoặc Admin; tác giả hoặc Admin được thay đổi bài; `expectedRevision` bắt buộc |
| Tri thức: đọc | Admin đọc mọi bài; Agent đọc bài đã xuất bản và bài của mình; người khác chỉ bài đã xuất bản |
| Phân tích, báo cáo | Chưa có module; **Mở** |

Giới hạn: access token đã phát giữ vai trò cũ tới khi hết hạn (`Jwt:ExpiryMinutes`), nên thu hồi quyền chưa có hiệu lực tức thời (quyết định riêng trong DEC-02).

## Tệp đính kèm (DEC-07)

Chính sách mục tiêu **đã được chủ dự án chốt**; tình trạng mã khác nhau từng điều:

| Giới hạn | Chính sách | Trong mã tại mốc đối chiếu |
| --- | --- | --- |
| Dung lượng mỗi tệp | Tối đa 10 MiB | **Đã triển khai** |
| Định dạng | `.pdf`, `.png`, `.jpg`, `.jpeg`, `.docx`, `.txt`, `.zip` | **Đã triển khai** (danh sách đuôi tệp) |
| Số tệp mỗi phiếu | Tối đa 10 | **Chưa được kiểm tra trong mã** |
| Tổng dung lượng mỗi phiếu | Tối đa 50 MiB | **Chưa được kiểm tra trong mã** |
| Tin metadata của client | Không; backend tự kiểm giới hạn và loại tệp | Xác nhận đối chiếu dung lượng và loại tệp thực tế khi hoàn tất tải lên; chưa kiểm tra chữ ký nội dung |
| Quét mã độc | Chưa có | Chưa có |
| Lưu giữ, xóa | Chưa chốt | Chưa có chức năng xóa |

Hạn mức 10 tệp và 50 MiB mỗi phiếu là chính sách đã duyệt nhưng chưa có triển khai và kiểm thử; không ghi là "đã triển khai".

## Quy tắc chung

- Kiểm quyền và phạm vi trước khi lộ trạng thái hoặc revision; phiếu ngoài phạm vi trả 404, hành động không được phép trên phiếu thấy được trả 403.
- Mọi lệnh ghi phiếu cần `expectedRevision` (thiếu trả 400, lệch trả 409).
- AI không tăng quyền của người dùng, không chọn Agent, không chuyển trạng thái, không chốt độ ưu tiên cuối (`TriagePolicy`).
- Không suy quyền từ `Department`, claim phía client hay dữ liệu frontend.
- Kiểm thử bắt buộc cho thay đổi quyền: trường hợp bị từ chối, người nhiều vai trò, IDOR giữa hai Requester, thu hồi quyền.
