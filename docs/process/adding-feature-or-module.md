# Thêm tính năng hoặc mô-đun backend

Trạng thái: hướng dẫn hiện hành, ngày 2026-10-23. Giữ kiến trúc và các quyết định trong [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md).

## Trước khi viết mã

1. Xác định mô-đun sở hữu nghiệp vụ, dữ liệu và quyền; chỉ tạo mô-đun khi có nghiệp vụ thực sự. Knowledge/Analytics hiện là phần dự kiến, các `.gitkeep` không phải chức năng đã triển khai.
2. Đối chiếu [hợp đồng hiện hành](../architecture/current-backend-contract.md), [chính sách bảo mật](../architecture/security-design-rules.md) và [quyết định còn mở](../architecture/pending-business-decisions.md). Chốt lựa chọn nghiệp vụ chưa rõ trước phần phụ thuộc.
3. Xác định use case, actor, phạm vi nhìn thấy, quyền hành động, chuyển trạng thái, lỗi, API/data contract và các kiểm thử cần bổ sung.

## Vị trí và quyền phụ thuộc

- Giữ từng use case trong `Application/<Module>/<UseCase>`; giao diện công khai có `ExecuteAsync`, implementation `internal sealed`. Input validation cần được bảo vệ khi gọi trực tiếp Application/Domain, không chỉ tại HTTP.
- Quy tắc trạng thái/bất biến thuộc Domain; Application kiểm quyền và điều phối; port chuyên biệt do Application sở hữu; adapter thực hiện thao tác kỹ thuật.
- Không thêm mediator, Generic Repository, framework mô-đun hoặc dự án riêng chỉ để thống nhất hình thức.
- Khi thêm module thật, đăng ký phân loại trong `ModuleRules` và cấp đúng cặp bên gọi/bên sở hữu/contract trong `Rules/PublishedContracts`. Không tự cấp quyền vì type là `public` hoặc được API sử dụng. Kiểu chưa đăng ký phải tiếp tục làm kiểm thử thất bại.
- Mapping và adapter nằm trong mô-đun Infrastructure sở hữu dữ liệu. Giữ một `ApplicationDbContext`; kiểm tra cả truy cập entity qua generic và bảng đích của SQL dạng chuỗi. Kiểm thử phụ thuộc tĩnh không chứng minh ownership của mọi câu SQL.
- API chỉ gọi contract Application. Worker giữ khả năng đăng ký từng phần (`AddTicketingApplication` và hạ tầng cần thiết), không dùng quyền người dùng ngầm định. Không cố định tổng số project, use case hay interface.

## Kiểm chứng và tài liệu

- Thêm trường hợp thành công và bị từ chối; kiểm tra authorization trước persistence đối với quyền chức năng và trước xung đột đối với tài nguyên.
- Thay đổi persistence/đồng thời cần PostgreSQL thật; giữ nguyên giao dịch tạo phiếu và receipt. `xmin` không thay revision từ client; không tự thử lại lệnh có thể đổi nghiệp vụ.
- Đối chiếu status/header/JSON và OpenAPI. Chỉ cập nhật bản chuẩn khi có thay đổi hợp đồng được chủ đích chấp nhận, xem từng mục chênh lệch rồi chạy xác nhận với `EXPORT_API_BASELINE` tắt.
- Chạy các kiểm tra tương ứng trong [runbook](../runbooks/backend-hardening-verification.md), báo số test thực chạy. Kiểm thử không chạy hoặc bị chặn phải ghi rõ, không lấy build thành công thay nghiệm thu.
- Cập nhật tài liệu hiện hành và quyết định ngay cùng thay đổi, giữ mốc lịch sử. Không thêm migration để che lệch mapping và không sửa migration đã dùng chung.
