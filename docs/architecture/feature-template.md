# Mẫu một tính năng backend

Trạng thái: hướng dẫn hiện hành cho tính năng mới và khi sửa tính năng cũ. Không yêu cầu di chuyển hoặc đổi tên mã hiện có. Quy tắc ranh giới nằm ở [ADR-015](../adr/ADR-015-module-first-clean-architecture.md) và [AGENTS.md](../../AGENTS.md); hợp đồng HTTP nằm ở [hợp đồng hiện hành](current-backend-contract.md); trách nhiệm từng module nằm ở [mô-đun và luồng xử lý](backend-modules-and-use-cases.md).

Ba module hiện viết hơi khác nhau (xem mục 8). Tính năng mới theo mẫu dưới đây, lấy Ticketing làm mẫu tham chiếu.

## 1. Một tính năng, một thư mục, một tệp

```text
Modules/<Module>/Application/<TínhNăng>/<TínhNăng>UseCase.cs
```

Tệp chứa đủ ba thứ, sau các dòng `using` và `namespace ...;`:

1. `<TínhNăng>Command` hoặc `<TínhNăng>Query`: `record` đầu vào của use case. Lệnh ghi có `uint? ExpectedRevision`.
2. `I<TínhNăng>` với đúng một phương thức `ExecuteAsync(..., CancellationToken)`, công khai.
3. `<TínhNăng>UseCase`: `internal sealed`, nhận phụ thuộc qua primary constructor.

Request body HTTP là kiểu của `Presentation`, không đưa xuống Application. DTO trả về dùng chung đặt ở `Application/Contracts`, cổng dữ liệu ở `Application/Ports`.

## 2. Thứ tự bước của lệnh ghi

Mọi lệnh ghi chạy cùng một thứ tự; không tự viết lại khung này trong use case mới.

| Bước | Việc | Lỗi khi không đạt |
| --- | --- | --- |
| 1 | `ArgumentNullException.ThrowIfNull(command)`, rồi xác định danh tính người gọi | lỗi lập trình, hoặc 403 nếu danh tính không dùng được |
| 2 | Kiểm vai trò | 403 |
| 3 | Tải thực thể trong phạm vi quyền đọc | 404, không lộ sự tồn tại |
| 4 | Điều kiện riêng của lệnh (chạy trước kiểm revision) | theo từng lệnh |
| 5 | Kiểm `expectedRevision` | 400 nếu thiếu hoặc bằng 0, 409 nếu cũ |
| 6 | Gọi phương thức của Domain | 400 hoặc 422 (`RuleViolationException`) |
| 7 | Lưu qua cổng ghi; ghi audit nếu lệnh có audit | 409 khi xung đột lúc lưu |

- Lệnh chuyển trạng thái phiếu dùng `TicketTransitions` (nhân viên: `RunAsStaffAsync`; chủ sở hữu hoặc Admin: `RunAsOwnerAsync`).
- Lệnh sửa, xuất bản, lưu trữ bài viết dùng `ArticleTransitions`. Knowledge lấy danh tính ở bước kiểm tác giả (sau khi tải bài viết) vì bước 2 chỉ đọc vai trò.
- Bước 5 luôn gọi `ExpectedRevision.Require` (SharedKernel); module chỉ truyền thông điệp 409 riêng nếu có.
- Tạo mới không có bước 3 và 5. Tạo phiếu giữ ticket và receipt trong cùng một giao dịch (xem AGENTS.md).

## 3. Lỗi nghiệp vụ

Mã nguồn ServiceDesk không ném `ArgumentException`, `InvalidOperationException`, `KeyNotFoundException` hay `UnauthorizedAccessException` cho lỗi do người dùng gây ra. Dùng các lớp con của `DomainException` (SharedKernel), mỗi lỗi có `Code` ổn định dạng snake_case:

| Tình huống | Lớp | HTTP |
| --- | --- | --- |
| Đầu vào thiếu, sai định dạng, ngoài giới hạn | `InvalidInputException` | 400 |
| Hợp lệ về hình thức nhưng vi phạm quy tắc hoặc trạng thái | `RuleViolationException` (mặc định 400; Knowledge truyền 422) | 400 hoặc 422 |
| Không có quyền | `ForbiddenException` | 403 |
| Không tồn tại hoặc ngoài phạm vi được biết | `NotFoundException` (không có body) | 404 |
| Xung đột ghi đồng thời, trùng Idempotency-Key | lớp con của `ConflictException` | 409 |
| Dịch vụ ngoài không khả dụng | `ServiceUnavailableException` | 503 |
| Đang xử lý, nên thử lại | lớp con của `RetryLaterException` (kèm `Retry-After`) | 503 |

Quy ước:

- `Code` không đổi khi sửa câu chữ; thông điệp viết tiếng Việt có dấu, nói rõ người dùng cần làm gì. Ngoại lệ: thông điệp của Identity giữ tiếng Anh vì frontend so khớp bằng biểu thức chính quy.
- Lỗi lặp lại gom vào danh mục của module (`TicketErrors`, `KnowledgeErrors`); lỗi chỉ xuất hiện một lần thì viết tại chỗ.
- Domain ném thẳng lỗi nghiệp vụ; Application không bọc lại hay đổi kiểu.
- Ngoại lệ thuần của .NET hoặc thư viện chỉ biểu thị lỗi lập trình hoặc lỗi hạ tầng: `ApiErrorFilter` ẩn thông điệp và ghi log; `InvalidOperationException` thành 500 chung.
- Identity dùng kết quả (`AuthResult`, `IdentityOperationResult`) cho luồng xác thực để không lộ sự tồn tại của tài khoản; không áp dụng cho module khác.
- Khi cần bắt lỗi quanh lời gọi hạ tầng, để lỗi nghiệp vụ đi qua nguyên vẹn (`ex is not DomainException`) và chỉ đổi lỗi hạ tầng thành `ServiceUnavailableException`.

Body lỗi do `ApiErrorFilter` dựng: `{ "error": "...", "code": "...", "type": "urn:servicedesk:error:<code>", "title": "..." }`. Không thêm trường khác, không đổi kiểu của `errors`.

## 4. Domain

- Quy tắc của một trường (độ dài, định dạng, chuẩn hóa) viết một lần trong value object `internal` (ví dụ `TicketTitle`) và dùng ở cả tạo mới lẫn cập nhật. Chữ ký công khai của thực thể vẫn nhận kiểu nguyên thủy.
- Phương thức chuyển trạng thái tự kiểm trạng thái nguồn và ném `RuleViolationException` có mã mô tả quy tắc, ví dụ `ticket_cannot_resolve`, `article_not_draft`.
- Giá trị enum đã lưu không đổi nghĩa; giá trị chưa dùng được ghi chú bằng XML comment, không dùng `[Obsolete]` vì build coi cảnh báo là lỗi.

## 5. Presentation

- `app.MapGroup("/api/...").AddEndpointFilter<ApiErrorFilter>()`; endpoint chỉ gắn kết dữ liệu, gọi `I<TínhNăng>` và trả kết quả.
- Mỗi endpoint có `RequireAuthorization(...)`, tên (`WithName`) và `Produces` cho các mã trả về.
- Không bắt lỗi theo kiểu riêng của module trong endpoint; mã HTTP đến từ `DomainException.StatusCode`.
- Thêm hoặc đổi endpoint thì cập nhật [OpenAPI baseline](../../contracts/backend/openapi-baseline.json) có chủ đích.

## 6. Đăng ký

Đăng ký use case trong `Add<Module>Application` và adapter trong `Add<Module>Infrastructure` của tệp `<Module>Module.cs`. Host chỉ gọi `Add<Module>Module` và `Map...Endpoints`.

## 7. Kiểm thử

| Loại | Kiểm gì | Ở đâu |
| --- | --- | --- |
| Domain | Mỗi quy tắc: lớp lỗi, `Code`, biên giá trị | `tests/ServiceDesk.Domain.Tests` |
| Application | Mỗi use case: quyền (403, 404), revision, lớp lỗi và `Code`; mock cổng | `tests/ServiceDesk.Application.Tests` |
| Integration | Lưu trữ, đồng thời, hợp đồng HTTP trên PostgreSQL thật | `tests/ServiceDesk.IntegrationTests` |
| Kiến trúc | Chạy sau mọi thay đổi chạm `using` hoặc tham chiếu | `tests/ServiceDesk.ArchitectureTests` |

Khẳng định mã HTTP và `Code`, không chỉ kiểu exception. Số kiểm thử chạy thực tế phải lớn hơn 0.

## 8. Khác biệt hiện có giữa các module

| Khía cạnh | Ticketing (mẫu) | Knowledge | Identity |
| --- | --- | --- | --- |
| Bố cục một tính năng | Một tệp | Ba tệp (`I...`, `...Command`, `...UseCase`) | Một tệp |
| Đầu vào | `Command`, `Query` | `Command`, `Query` | Request DTO từ `Application/Contracts` |
| Thứ tự `using` và `namespace` | `using` trước | `namespace` trước ở nhiều use case | `using` trước |
| Báo lỗi | `DomainException` | `DomainException` (422 cho quy tắc trạng thái) | `DomainException` cho quyền và đầu vào; kết quả (Result) cho xác thực |

Không di chuyển tệp chỉ để đồng nhất bố cục; áp dụng mẫu khi tạo tính năng mới hoặc khi đã phải sửa nhiều tệp của tính năng cũ.
