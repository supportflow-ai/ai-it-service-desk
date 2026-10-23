# Bằng chứng củng cố backend ngày 2026-10-23

Trạng thái: bằng chứng cho bản làm việc chưa commit, chưa nghiệm thu PostgreSQL hoặc toàn hệ thống. Nhánh `codex/backend-clean-architecture`, HEAD nền `47da28103cc46500aac0847accfbaa2783f88db2`; cây nội dung nền trùng `e2d1ad406337dbd1a9a63ac6b522b005dc502674`. Không có commit/push/PR mới trong đợt này.

## Phạm vi và quyết định

- Chủ dự án chọn Agent/Admin đều được claim. Đồng bộ use case với policy HTTP; kiểm tra vai trò resolve trước persistence để không suy ra sự tồn tại của phiếu.
- Chuyển `DbUpdateConcurrencyException` trong ghi vòng đời thành lỗi Application, ánh xạ 409 `{ error }`. Chỉ thêm sáu phản hồi 409 trong OpenAPI; giữ 400 cho trạng thái sai và các dạng lỗi hiện hành còn lại. Không thay route, DTO, enum, mapping hoặc migration.
- Tách contract HTTP khỏi quyền sử dụng giữa mô-đun; thêm hồi quy phụ thuộc bị cấm/được cấp, generic Identity và kiểm tra use case không phụ thuộc tổng số 12.
- Giữ đăng ký DI từng phần cho Worker, một DbContext và giao dịch tạo phiếu/receipt. Chỉ ghi rõ cách thêm chức năng trong [hướng dẫn](../process/adding-feature-or-module.md), không thêm framework hay module rỗng.

## Kết quả kiểm chứng thực chạy

| Kiểm tra | Kết quả |
| --- | --- |
| Restore khóa | `dotnet restore src/backend/ServiceDesk.sln --locked-mode --disable-parallel -m:1 -nr:false`: đạt |
| Build Release | `dotnet build src/backend/ServiceDesk.sln -c Release --no-restore -m:1 -nr:false`: đạt, 0 warning, 0 error |
| Domain | 118 test đạt, 0 lỗi, 0 bỏ qua |
| Application | 76 test đạt, 0 lỗi, 0 bỏ qua |
| Architecture | 34 test đạt, 0 lỗi, 0 bỏ qua |
| HTTP/hợp đồng offline | 63 test đạt, 0 lỗi, 0 bỏ qua; gồm 22 test mới trong `TicketLifecycleBoundaryTests`, OpenAPI toàn bộ và model/snapshot |
| Tổng offline | 291 test đạt qua xUnit v2 chạy trong cùng tiến trình; loại `Category=Integration` |
| Kiểm tra tài liệu | `python scripts/validate_project_docs.py`: đạt, 99 tài liệu và 532 liên kết cục bộ |
| Hồi quy công cụ tài liệu | `python -m unittest discover -s scripts/tests -p "test_project_docs.py"`: 6 test đạt; đặt TMP/TEMP của tiến trình vào `.cache/docs-test-tmp` do thư mục tạm mặc định bị chặn ghi |
| Khoảng trắng | `git diff --check`: đạt |

VSTest tiêu chuẩn bị chặn socket giữa runner và testhost (`SocketException 10013`). Đã thử ArchitectureTests tại mã mới với thời gian kết nối 15 giây: aborted, không coi là test đạt. Để có bằng chứng offline, runner tạm trong `.cache/offline-xunit` dùng `XunitFrontController`, chờ discovery/execution, từ chối tập rỗng và báo số test thật. Runner không được đưa vào sản phẩm hoặc thay pipeline CI.

Lệnh của lần chạy tạm có dạng `dotnet tests/<Project>/bin/Release/net10.0/OfflineXunit.dll tests/<Project>/bin/Release/net10.0/<Project>.dll`. Với project HTTP, đặt `ASPNETCORE_TEST_CONTENTROOT_SERVICEDESK_API` bằng đường dẫn tuyệt đối của project API và tắt `EXPORT_API_BASELINE`. Đây là cấu hình runner local; không sửa fixture/baseline để làm kiểm thử đạt. Lần thử ban đầu chưa đặt content root không đạt do WebApplicationFactory không tìm thấy solution; lần xác nhận cuối 63/63 đã có cấu hình đúng.

## Các kiểm tra chưa hoàn tất

- Docker context `desktop-linux` trỏ named pipe local `dockerDesktopLinuxEngine`; truy cập Docker API bị từ chối. Không chạy hoặc kết nối cơ sở dữ liệu dùng chung để thay thế.
- Tám trường hợp `TicketConcurrencyPostgresTests` mới đã build nhưng chưa chạy: sáu lệnh với hai context cạnh tranh, xóa phiếu đã tải và hai API host cùng cập nhật. Cần chạy nhóm `Category=Integration` theo workflow tại đúng mã trước nghiệm thu persistence.
- EF CLI cục bộ chưa có; đã thử cài đúng phiên bản `Microsoft.EntityFrameworkCore.Design` vào `.cache/dotnet-tools`, lệnh kết thúc mã 1 và không tạo được tool. Chưa chạy CLI `has-pending-model-changes`; phép so model/snapshot bằng API trong `PersistenceContractTests` đã đạt. Không cài tool global, không sinh migration.
- Không chạy image build vì không sửa Dockerfile/dependency/configuration; Docker local cũng không truy cập được. Không chạy smoke ở kho hạ tầng, không triển khai.

## Giới hạn và bước bàn giao

Phân quyền theo team/assignment, revision do client gửi, lịch sử nghiệp vụ và chính sách reopen/SLA còn thuộc đợt riêng. Kiểm tra phụ thuộc tĩnh không phân tích ownership của mọi bảng được nhắc trong SQL dạng chuỗi.

Trước hợp nhất, chạy lại các lệnh chuẩn và PostgreSQL trên môi trường có Docker, đối chiếu đúng bản làm việc/commit mới. Bản kê [dấu vân tay mã kiểm chứng](backend-hardening-20261023-code.json) ghi SHA-256 các tệp mã, kiểm thử và OpenAPI của lần chạy; kết quả không tự áp dụng cho mã thay đổi sau đó.
