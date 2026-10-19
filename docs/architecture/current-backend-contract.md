# Hợp đồng backend đang triển khai

Trạng thái: mô tả hiện hành, đối chiếu nguồn `8244cc1649eb2f8db28c8dcaf30b0d540eab1c58` ngày 2026-10-19. Cập nhật tài liệu không thay API, quyền hoặc dữ liệu. Quyết định kiến trúc: [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md). Nghiệp vụ chưa triển khai: [bảng quyết định](pending-business-decisions.md).

## HTTP và danh tính

| Thao tác | Đường dẫn | Hành vi |
| --- | --- | --- |
| Đăng ký | `POST /api/auth/register` | Server chỉ cấp Requester, bỏ qua yêu cầu tự chọn role |
| Đăng nhập | `POST /api/auth/login` | Sai mật khẩu, không có tài khoản hoặc bị khóa đều trả 401 chung |
| Hồ sơ bản thân | `GET /api/auth/me` | Application lấy ID từ actor; tài khoản hợp lệ nhưng không còn trong DB trả 404 |
| Tạo ticket | `POST /api/tickets` | Owner là actor hiện tại; tạo Submitted; 201 với Location `/api/tickets/{id}` |
| Danh sách cá nhân | `GET /api/tickets/me` | Chỉ ticket của actor; kết quả phân trang, không phải mảng trần |
| Chi tiết | `GET /api/tickets/{id}` | Requester chỉ đọc ticket của mình; Agent/Admin đọc toàn hệ thống; thiếu/ngoài phạm vi trả 404 |

Agent trong tài liệu này là vai trò nhân viên hỗ trợ. Chưa có phân quyền theo team, assignment hoặc role Lead. Người có nhiều role vẫn được cấp phạm vi chi tiết rộng hơn nếu có Agent/Admin. Tạo và list luôn theo actor hiện tại, kể cả nhân viên hỗ trợ.

Application từ chối actor chưa xác thực, ID không phải GUID hợp lệ hoặc GUID rỗng trước persistence. Endpoint còn có policy role; role không được cấp bị từ chối tại HTTP. Worker dùng actor anonymous, không đăng ký HttpContext. Không chỉ dựa vào middleware để bảo vệ use case.

Nguồn: [AuthEndpoints](../../src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs), [TicketEndpoints](../../src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs), [GetTicketDetail](../../src/backend/ServiceDesk.Application/Ticketing/GetTicketDetail/GetTicketDetailUseCase.cs), [GetCurrentUser](../../src/backend/ServiceDesk.Application/Identity/GetCurrentUser/GetCurrentUserUseCase.cs).

## Dữ liệu ticket và mã trạng thái

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

- Giữ response lỗi hiện tại, không chuyển đồng loạt sang ProblemDetails. [LegacyErrorFilter](../../src/backend/ServiceDesk.Api/Endpoints/LegacyErrorFilter.cs) giữ `{ error }` cho lỗi đầu vào/idempotency; 404 chi tiết không dựng một DTO lỗi mới.
- Tạo ticket hỗ trợ `Idempotency-Key` tùy chọn. Receipt nằm trong phạm vi requester + method + resource + key hash; khác payload cùng key trả 409.
- Advisory lock và transaction PostgreSQL giữ ticket/receipt nguyên tử. Receipt dùng giờ DB, hiệu lực 10 phút; lock timeout 5 giây; timeout trả 503 và `Retry-After: 1`.
- Replay trả response đã lưu sau kiểm tra quyền sở hữu, không dựng lại từ ticket có thể thay đổi. Không dùng cache trong tiến trình thay cơ chế này.
- Fingerprint tạo ticket giữ thuật toán riêng; không thay bằng hash AI `ai-input-v2`.

## Kiểm chứng và giới hạn

[OpenAPI baseline](../../contracts/backend/openapi-baseline.json) cùng [ApiContractTests](../../tests/ServiceDesk.IntegrationTests/ApiContractTests.cs), [TicketAccessScopeTests](../../tests/ServiceDesk.IntegrationTests/TicketAccessScopeTests.cs), [TicketIdempotencyPostgresTests](../../tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresTests.cs) bảo vệ các phần tương ứng. Baseline OpenAPI không thay kiểm thử hành vi, dữ liệu và quyền.

Chưa có Draft/Submit riêng, assignment, team, SLA, comment/attachment, AI thật hoặc thu hồi tức thời quyền trong JWT đã phát. FullName chưa có persistence hồ sơ riêng. Các thiếu hụt này thuộc đợt phát triển được quyết định riêng, không tự trở thành việc cần thêm khi chỉnh tài liệu.
