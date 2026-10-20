# Hợp đồng backend đang triển khai

Trạng thái: mô tả hiện hành, đối chiếu mã `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #28 và #29; cập nhật 2026-10-20. Mốc nguồn có thể mới hơn nhánh local của người đọc; đối chiếu commit trước khi chạy. Cập nhật tài liệu không thay API, quyền hoặc dữ liệu, không xác nhận đã nghiệm thu các hành vi còn lỗi. Quyết định kiến trúc: [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md). Chính sách cần chốt: [bảng quyết định](pending-business-decisions.md).

## HTTP và danh tính

[Bảng mốc mã nguồn](source-baseline.md) ghi nhánh chuẩn hóa trên dev sau PR #29 và phạm vi hợp đồng đã đồng bộ. Liên kết mã tương đối bên dưới mở checkout của người đọc; đối chiếu HEAD trước khi áp dụng.

| Thao tác | Đường dẫn | Hành vi |
| --- | --- | --- |
| Đăng ký | `POST /api/auth/register` | Máy chủ chỉ cấp Requester, bỏ qua yêu cầu tự chọn vai trò |
| Đăng nhập | `POST /api/auth/login` | Sai mật khẩu, không có tài khoản hoặc bị khóa đều trả 401 chung |
| Hồ sơ bản thân | `GET /api/auth/me` | Application lấy ID từ người đang đăng nhập; tài khoản hợp lệ nhưng không còn trong cơ sở dữ liệu trả 404 |
| Tạo phiếu | `POST /api/tickets` | Chủ sở hữu là người đang đăng nhập; tạo Submitted; 201 với header Location `/api/tickets/{id}` |
| Danh sách cá nhân | `GET /api/tickets/me` | Chỉ phiếu của người đang đăng nhập; kết quả phân trang, không phải mảng trần |
| Chi tiết | `GET /api/tickets/{id}` | Requester chỉ đọc phiếu của mình; Agent/Admin đọc toàn hệ thống; thiếu/ngoài phạm vi trả 404 |
| Cập nhật phiếu | `POST /api/tickets/{id}/update` | Chủ sở hữu hoặc Admin; chỉ ở Submitted; nhận `title`, `description`, `categoryId`; trả 200 với `TicketDto` |
| Nhận xử lý | `POST /api/tickets/{id}/claim` | Application hiện yêu cầu Agent; Submitted sang InProgress; trả 200 với `TicketDto`; xem điểm lệch quyền bên dưới |
| Giải quyết | `POST /api/tickets/{id}/resolve` | Agent/Admin; InProgress sang Resolved; trả 200 với `TicketDto` |
| Mở lại | `POST /api/tickets/{id}/reopen` | Chủ sở hữu hoặc Admin; Resolved sang InProgress; trả 200 với `TicketDto` |
| Đóng | `POST /api/tickets/{id}/close` | Chủ sở hữu hoặc Admin; Resolved sang Closed; trả 200 với `TicketDto` |
| Hủy | `POST /api/tickets/{id}/cancel` | Chủ sở hữu hoặc Admin; Submitted sang Cancelled; trả 200 với `TicketDto` |

Agent là nhân viên hỗ trợ; Admin là quản trị viên; Requester là người yêu cầu. Chưa có phân quyền theo nhóm, phân công hoặc vai trò Lead. Người có nhiều vai trò vẫn được cấp phạm vi đọc chi tiết rộng hơn nếu có Agent/Admin. Tạo phiếu và danh sách cá nhân luôn theo người đang đăng nhập, kể cả nhân viên hỗ trợ.

Application từ chối actor chưa xác thực, ID không phải GUID hợp lệ hoặc GUID rỗng trước persistence. Endpoint còn có policy role; role không được cấp bị từ chối tại HTTP. Worker dùng actor anonymous, không đăng ký HttpContext. Không chỉ dựa vào middleware để bảo vệ use case.

Nguồn: [AuthEndpoints](../../src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs), [TicketEndpoints](../../src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs), [GetTicketDetail](../../src/backend/ServiceDesk.Application/Ticketing/GetTicketDetail/GetTicketDetailUseCase.cs), [GetCurrentUser](../../src/backend/ServiceDesk.Application/Identity/GetCurrentUser/GetCurrentUserUseCase.cs).

## Các thao tác vòng đời sau PR #28

Sáu thao tác mới đều dùng POST. Chỉ `update` nhận nội dung JSON; các thao tác còn lại nhận ID trên đường dẫn. Điều kiện chuyển trạng thái nằm trong `Ticket`, quyền nằm trong từng luồng Application. Agent đọc được một phiếu không tự có quyền cập nhật, mở lại, đóng hoặc hủy phiếu của người khác.

| Thao tác | Trạng thái trước | Trạng thái sau | Giới hạn hiện tại |
| --- | --- | --- | --- |
| Cập nhật | Submitted | Submitted | Quy tắc tiêu đề, mô tả và danh mục như khi tạo |
| Nhận xử lý | Submitted | InProgress | Chưa lưu người phụ trách, nhóm hoặc lịch sử nhận xử lý |
| Giải quyết | InProgress | Resolved | Chưa có nội dung giải quyết, thời điểm giải quyết riêng hoặc lịch sử nghiệp vụ |
| Mở lại | Resolved | InProgress | Chưa có cửa sổ thời gian, kiểm tra người phụ trách hoặc tác vụ tự đóng |
| Đóng | Resolved | Closed | Chưa có tác vụ đóng tự động |
| Hủy | Submitted | Cancelled | Chưa nhận lý do hủy hoặc lưu lịch sử nghiệp vụ |

Trạng thái khác bị Domain từ chối; Application chuyển lỗi trạng thái thành `ArgumentException`, bộ lọc HTTP hiện trả 400 với `{ error }`. Phiếu thiếu hoặc ngoài quyền chủ sở hữu của các lệnh `update/reopen/close/cancel` trả 404. Các lệnh mới chưa có cơ chế khóa chống tạo trùng hoặc phát lại phản hồi như thao tác tạo phiếu. Yêu cầu đã thực hiện thành công rồi gửi lại có thể nhận lỗi trạng thái.

Nguồn được ghim: [các đường dẫn HTTP tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/47634bc8162c13bd6ae024233ec24a0076738c51/src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs), [quy tắc trên Ticket](https://github.com/supportflow-ai/ai-it-service-desk/blob/47634bc8162c13bd6ae024233ec24a0076738c51/src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs), [các luồng Ticketing](https://github.com/supportflow-ai/ai-it-service-desk/tree/47634bc8162c13bd6ae024233ec24a0076738c51/src/backend/ServiceDesk.Application/Ticketing).

## Các điểm chưa đạt cần xử lý

Các phát hiện dưới đây từ đối chiếu mã nguồn tại mốc ghi đầu tài liệu; chưa phải kết quả tái hiện HTTP mới trong đợt sửa tài liệu.

| Vấn đề | Hành vi trong mã | Yêu cầu theo dõi |
| --- | --- | --- |
| Quyền nhận xử lý chưa thống nhất | `RequireAgent` ở HTTP nhận Agent hoặc Admin, nhưng `ClaimTicketUseCase` chỉ nhận Agent; Admin chỉ có vai trò Admin bị bộ lọc trả 401 | Chốt phạm vi vai trò theo DEC-02; kiểm thử Agent, Admin và người nhiều vai trò; sửa phản hồi từ chối theo chính sách được chọn |
| Giải quyết tải tài nguyên trước kiểm tra vai trò | `ResolveTicketUseCase` tìm ID rồi mới kiểm tra Agent/Admin; Requester đã đăng nhập có thể nhận 404 khi thiếu phiếu và 401 khi phiếu tồn tại | Áp dụng thứ tự kiểm tra quyền và che giấu tài nguyên trong quy tắc bảo mật; bổ sung kiểm thử từ chối tương ứng |
| Phản hồi ghi đồng thời chưa hoàn thiện | `xmin` là dấu phát hiện xung đột; bộ ghi mới chưa ánh xạ `DbUpdateConcurrencyException` thành lỗi nghiệp vụ ổn định | Kiểm thử hai yêu cầu cạnh tranh trên PostgreSQL thật; xử lý xung đột ở bộ tích hợp mà không đưa kiểu EF vào Application |

`GetForUpdateAsync` hiện dùng `SingleOrDefaultAsync`, không thực hiện `SELECT ... FOR UPDATE`. Không suy ra có khóa dòng từ tên phương thức hoặc coi CI cũ là bằng chứng đã kiểm tra các tình huống trên. Xem [quy tắc bảo mật](security-design-rules.md), [DEC-02/DEC-05](pending-business-decisions.md) và [hướng dẫn kiểm chứng](../runbooks/backend-hardening-verification.md).

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

Nguồn: [TicketStatus](../../src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs), [Ticket](../../src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs), [TicketDto](../../src/backend/ServiceDesk.Application/Ticketing/Contracts/TicketDto.cs), [mapping](../../src/backend/ServiceDesk.Infrastructure/Ticketing/Configurations/TicketConfiguration.cs). Có enum không có nghĩa đã triển khai lệnh chuyển trạng thái. Không đổi nghĩa 0–6, loại Cancelled hoặc đổi JSON sang chuỗi để khớp bảng 0–8 trong tài liệu lịch sử.

## Lỗi và idempotency

- Giữ hình dạng phản hồi lỗi hiện hành, không chuyển đồng loạt sang ProblemDetails. [LegacyErrorFilter](../../src/backend/ServiceDesk.Api/Endpoints/LegacyErrorFilter.cs) giữ `{ error }` cho lỗi đầu vào/chống tạo trùng; 404 chi tiết không dựng một DTO lỗi mới.
- PR #29 đã đổi một số chuỗi thông báo sang tiếng Việt, gồm lỗi khóa chống tạo trùng. Hình dạng JSON và mã HTTP được giữ không đồng nghĩa nội dung thông báo còn giống bản local trước PR #29. Chi tiết phiên bản nằm trong bảng mốc nguồn.
- Tạo ticket hỗ trợ `Idempotency-Key` tùy chọn. Receipt nằm trong phạm vi requester + method + resource + key hash; khác payload cùng key trả 409.
- Advisory lock và transaction PostgreSQL giữ ticket/receipt nguyên tử. Receipt dùng giờ DB, hiệu lực 10 phút; lock timeout 5 giây; timeout trả 503 và `Retry-After: 1`.
- Replay trả response đã lưu sau kiểm tra quyền sở hữu, không dựng lại từ ticket có thể thay đổi. Không dùng cache trong tiến trình thay cơ chế này.
- Dấu vân tay tạo phiếu giữ thuật toán riêng; không thay bằng mã băm AI `ai-input-v2`.

## Kiểm chứng và giới hạn

[OpenAPI baseline](../../contracts/backend/openapi-baseline.json) cùng [ApiContractTests](../../tests/ServiceDesk.IntegrationTests/ApiContractTests.cs), [TicketAccessScopeTests](../../tests/ServiceDesk.IntegrationTests/TicketAccessScopeTests.cs), [TicketIdempotencyPostgresTests](../../tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresTests.cs) bảo vệ các phần tương ứng. Baseline OpenAPI không thay kiểm thử hành vi, dữ liệu và quyền.

Bản chuẩn OpenAPI đã được đồng bộ trên mã sau PR #29: 16 đường dẫn, gồm sáu thao tác mới và `UpdateTicketRequest`. Kiểm thử `PublishedApiMatchesBaseline` so sánh toàn bộ tài liệu, phát hiện cả mục mới chưa công bố và thay đổi mô tả/bảo mật. Quy trình cập nhật có chủ đích theo [quy chuẩn tài liệu](../process/documentation-standard.md); kết quả này không xác nhận các khoảng cách hành vi ở trên đã được sửa.

PR #29 cũng Việt hóa mô tả API và hướng dẫn Bearer trong Swagger, trong khi bản chuẩn vẫn giữ mô tả tiếng Anh. Kiểm thử hiện không so sánh toàn bộ phần `info`/mô tả bảo mật, nên việc CI đạt chưa chứng minh những phần mô tả này đã đồng bộ. Các điểm quyền nhận xử lý, thứ tự tải phiếu ở giải quyết và ghi đồng thời trong bảng trên vẫn còn tại mốc PR #29.

Chưa có luồng Draft/Submit riêng, phân công, nhóm, SLA, bình luận/tệp đính kèm, AI thật hoặc thu hồi tức thời quyền trong JWT đã phát. FullName chưa có dữ liệu hồ sơ riêng được lưu. Các phần còn thiếu thuộc đợt phát triển được quyết định riêng.
