# Củng cố quyền bình luận và ghi ticket–timeline nguyên tử

Hồ sơ đợt củng cố (lịch sử), đã hợp nhất vào `dev` qua PR #50–#53; nội dung giữ nguyên để truy vết. Hướng dẫn hiện hành: [kiểm chứng backend](backend-hardening-verification.md). Trạng thái "chưa commit/working tree" bên dưới là của thời điểm viết, không mô tả `dev` hôm nay.

Trạng thái: thay đổi triển khai đang kiểm chứng, chưa nghiệm thu trên PostgreSQL. Cập nhật: 2026-11-03. Mốc bắt đầu: nhánh `dev`, `44ba0b4b4fc1ad6f515f8485b83736eccb127491`; các thay đổi mô tả ở đây nằm trong working tree sau mốc này, chưa commit.

## Phạm vi

- [AddCommentUseCase](https://github.com/supportflow-ai/ai-it-service-desk/blob/1e272b91d3a6d31aefe3547c2b6cf3947e26dcb2/src/backend/Modules/Ticketing/ServiceDesk.Modules.Ticketing/Application/AddComment/AddCommentUseCase.cs) kiểm tra quyền trên ticket trước tra cứu tác giả và ghi bình luận. Requester chỉ bình luận trên ticket của mình; Agent/Admin giữ phạm vi ticket toàn hệ thống. Không đổi quyền ghi chú nội bộ, team hoặc assignment.
- Ticket thiếu và ticket ngoài quyền Requester đều trả 404 qua bộ lọc HTTP hiện có; payload phản hồi bình luận thành công giữ nguyên.
- [ITicketWriter](https://github.com/supportflow-ai/ai-it-service-desk/blob/1e272b91d3a6d31aefe3547c2b6cf3947e26dcb2/src/backend/Modules/Ticketing/ServiceDesk.Modules.Ticketing/Application/Ports/ITicketWriter.cs) có phương thức lưu ticket và timeline của cùng ticket một cách nguyên tử.
- [PostgresTicketWriter](https://github.com/supportflow-ai/ai-it-service-desk/blob/1e272b91d3a6d31aefe3547c2b6cf3947e26dcb2/src/backend/Modules/Ticketing/ServiceDesk.Modules.Ticketing/Infrastructure/PostgresTicketWriter.cs) thêm timeline vào cùng DbContext rồi gọi một SaveChangesAsync. EF Core/Npgsql dùng transaction tự động cho các lệnh ghi trong lần lưu; lỗi commit hoặc concurrency không được để lại cập nhật/audit một phần.
- Bảy use case Assign, Unassign, Claim, Resolve, Reopen, Close, Cancel dùng phương thức nguyên tử; bỏ hai lần lưu độc lập. Luồng UpdateTicket và tạo ticket/idempotency giữ nguyên.

Không thay schema, migration, enum, đường dẫn HTTP hoặc chính sách vai trò hiện có ngoài việc đóng lỗ hổng bình luận xuyên quyền.

## Kiểm thử cần chạy

| Phạm vi | Nguồn kiểm thử | Điều cần chứng minh |
| --- | --- | --- |
| Quyền tại Application | [CommentAuthorizationTests](../../tests/ServiceDesk.Application.Tests/Ticketing/CommentAuthorizationTests.cs) | Chủ sở hữu và staff được phép; Requester khác/actor sai bị từ chối trước side effect |
| Quyền HTTP | [TicketCommentAuthorizationEndpointTests](../../tests/ServiceDesk.IntegrationTests/TicketCommentAuthorizationEndpointTests.cs) | Hai tài khoản thật, 404 với ticket ngoài quyền, timeline không bị sửa, chủ sở hữu vẫn nhận 201 |
| Điều phối audit | [TicketAuditUseCaseTests](../../tests/ServiceDesk.Application.Tests/Ticketing/TicketAuditUseCaseTests.cs) | Bảy use case chuyển ticket và audit cho cùng đơn vị ghi, không che lỗi persistence |
| Transaction PostgreSQL | [TicketAuditPersistenceTests](../../tests/ServiceDesk.IntegrationTests/TicketAuditPersistenceTests.cs) | Lưu cả hai; lỗi deferred trigger lúc commit rollback cả hai; revision cũ không sinh audit mồ côi; audit khác ticket bị từ chối |
| Hồi quy | AssignmentUseCaseTests, TicketLifecycleUseCaseTests và bộ architecture | Giữ quy tắc chuyển trạng thái, actor và chiều phụ thuộc |

Chạy từ gốc repository:

```powershell
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore -m:1 -nr:false
dotnet test tests/ServiceDesk.Application.Tests -c Release --no-build
dotnet test tests/ServiceDesk.ArchitectureTests -c Release --no-build
dotnet test tests/ServiceDesk.IntegrationTests -c Release --no-build --filter "FullyQualifiedName~TicketCommentAuthorizationEndpointTests|FullyQualifiedName~TicketAuditPersistenceTests"
python scripts/validate_project_docs.py
git diff --check
```

Các integration tests chỉ dùng PostgreSQL do Testcontainers tạo. Deferred trigger kiểm tra lỗi commit chỉ được tạo/xóa trong database dùng một lần của fixture. Không chạy lệnh DDL này trên database local dùng chung hoặc VPS.

## Điều kiện bàn giao

Restore khóa, build và test phù hợp phải được ghi kết quả trên đúng working tree. Nếu môi trường không chạy được testhost/Docker, ghi rõ kiểm thử bị chặn và không đánh dấu PostgreSQL atomicity hoặc HTTP authorization đã nghiệm thu. Không dùng SQLite/EF InMemory để thay bằng chứng transaction PostgreSQL. CI và full-stack smoke sẽ xác nhận trước khi phát hành.

## Kết quả lần triển khai 03/11/2026

- `restore --locked-mode` và build Release toàn solution đạt, 0 warning/error; không đổi dependency.
- Application 101, Domain 118, Architecture 26 kiểm thử: tổng 245 đạt, 0 thất bại, 0 bỏ qua; chạy bằng XunitFrontController trực tiếp từ các assembly đã build và runner xUnit đi kèm dự án. Có 16 trường hợp unit mới về quyền bình luận và ghi ticket–audit.
- Lệnh `dotnet test` chuẩn bị hủy vì VSTest không kết nối được testhost sau 15 giây. Kết quả trực tiếp nêu trên không thay yêu cầu CI chạy quy trình chuẩn.
- Năm integration tests mới đã biên dịch nhưng chưa chạy vì Docker engine không hoạt động. HTTP authorization và rollback PostgreSQL vẫn chờ nghiệm thu.
- Validator tài liệu đạt 98 tài liệu/529 liên kết; kiểm tra whitespace đạt.

Kết quả chỉ áp dụng cho working tree sau mốc ghi đầu tài liệu. Thay đổi tiếp theo cần chạy lại các nhóm bị ảnh hưởng.
