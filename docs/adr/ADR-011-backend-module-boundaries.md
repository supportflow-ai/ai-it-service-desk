# ADR-011: Luồng xử lý do Application sở hữu và ranh giới mô-đun

## Trạng thái

Đã chấp nhận cho đợt tái cấu trúc backend. Bản diễn giải tiếng Việt cập nhật 2026-10-20; các phần bổ sung sau quyết định gốc được ghi riêng.

## Quyết định về phụ thuộc

Giữ Domain, Application, Infrastructure, API và Worker trong một kho backend. Identity, Ticketing và AIAssistance là các mô-đun nằm trong các tầng này. Domain/Application không phụ thuộc EF Core, PostgreSQL, ASP.NET Core hoặc Infrastructure. Application sở hữu giao diện nghiệp vụ, kiểm tra quyền và điều phối; bộ tích hợp thực hiện các thao tác kỹ thuật.

Ticketing dùng `ITicketQueries` và `ITicketWriter`, không dùng trực tiếp `DbContext` hoặc `IQueryable`. Bộ ghi giữ toàn bộ giao dịch tạo phiếu, khóa PostgreSQL và biên nhận chống tạo trùng. Chuyển riêng một phần giao dịch lên Application sẽ làm yếu tính nguyên tử.

Giữ tương thích mã băm biên nhận, phạm vi, hiệu lực 10 phút, thời gian chờ khóa 5 giây và phản hồi phát lại nguyên trạng. Giờ cơ sở dữ liệu quyết định hết hạn biên nhận; `IClock` cung cấp thời gian nghiệp vụ.

## Danh tính và truy cập xuyên mô-đun

Identity điều phối các cổng tài khoản/token do Application sở hữu. Tạo tài khoản và cấp vai trò Requester được thực hiện nguyên tử trong bộ tích hợp; bỏ qua vai trò do bên gọi tự chọn.

API chuyển thông tin danh tính HTTP sang giao diện Application. Worker dùng danh tính ẩn danh, không ngầm thực hiện thao tác được cấp quyền theo người dùng.

Truy cập xuyên mô-đun thông qua giao diện và kiểu dữ liệu được công khai rõ ràng. AI có thể dùng kiểu danh mục, mức ảnh hưởng và mức khẩn cấp của Ticketing; không tham chiếu thực thể Ticket, Identity hoặc lớp lưu dữ liệu của mô-đun khác. Giữ một `DbContext` chung, chia cấu hình ánh xạ theo mô-đun và bảo vệ ranh giới bằng kiểm thử kiến trúc.

Quyết định không đưa thêm kho dữ liệu tổng quát, MediatR, môi giới thông điệp hoặc một dự án riêng cho mỗi mô-đun. Vòng đời phiếu và chức năng con người quyết định đề xuất AI tiếp tục thuộc backend.

## Khởi tạo có chủ đích

API/Worker không tự sửa cấu trúc dữ liệu hoặc tạo tài khoản khi khởi động. Công cụ Database riêng sở hữu migration, tạo vai trò, tài khoản mẫu được bật rõ ràng và quản trị viên mới.

Cấu hình JWT production được kiểm tra trước khi nhận lưu lượng. Identity lưu trạng thái khóa sau năm lần đăng nhập sai trong 15 phút; API giữ phản hồi 401 chung.

## Phần phát triển tiếp và các bổ sung

Quyết định gốc tách AI thật, vòng đời phiếu đầy đủ/SLA, dữ liệu hồ sơ, dọn biên nhận, lịch sử nghiệp vụ và theo dõi vận hành thành các đợt phát triển riêng.

[ADR-014](ADR-014-feature-use-cases-and-published-contracts.md) làm rõ sáu luồng tại mốc tái cấu trúc ban đầu, phạm vi đọc phiếu, kiểm tra kiểu công khai và giao dịch đăng nhập theo tài khoản. Agent/Admin đọc chi tiết toàn hệ thống; cách ly theo nhóm còn được hoãn.

Bổ sung ngày 2026-10-20: PR #28 thêm sáu luồng Ticketing, đưa tổng số lên 12. Các phần đã có và giới hạn còn lại nằm trong [mô tả hiện hành](../architecture/backend-modules-and-use-cases.md). Bảo đảm nguyên tử của tạo phiếu không tự áp dụng cho mọi lệnh ghi mới.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-011-backend-module-boundaries.md).
