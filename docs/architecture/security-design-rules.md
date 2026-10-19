# Quy tắc thiết kế quyền và ranh giới nghiệp vụ

Trạng thái: hướng dẫn kỹ thuật hiện hành, cập nhật 2026-10-19. Áp dụng [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md); không bổ sung endpoint, role hoặc chính sách team/assignment.

## Module sở hữu quyền nghiệp vụ

Logic quyền Ticketing nằm trong `Application/Ticketing/Authorization` và các use case Ticketing. `Common` chỉ xác thực danh tính dùng chung; không đọc Ticket hoặc persistence module. `Infrastructure/Identity` quản lý tài khoản/token, không quyết định ai được sửa ticket.

Mẫu đang chạy: [GetTicketDetailUseCase](../../src/backend/ServiceDesk.Application/Ticketing/GetTicketDetail/GetTicketDetailUseCase.cs) xác thực actor, tạo [TicketReadScope](../../src/backend/ServiceDesk.Application/Ticketing/Authorization/TicketReadScope.cs), rồi gọi gateway. Gateway nhận phạm vi tường minh, không tự đọc claims hoặc coi null là AllTickets. Xuyên module chỉ dùng kiểu được cho phép trong [ModuleRules](../../tests/ServiceDesk.ArchitectureTests/ModuleRules.cs).

## Thứ tự kiểm tra cho lệnh tài nguyên tương lai

1. Xác thực actor; policy chức năng ở HTTP có thể từ chối role trước khi tải tài nguyên.
2. Kiểm tra cú pháp đầu vào không phụ thuộc dữ liệu riêng của tài nguyên.
3. Tải tài nguyên trong phạm vi actor được phép nhìn thấy; thiếu hoặc ngoài phạm vi trả 404 chung.
4. Kiểm tra quyền thực hiện hành động trên tài nguyên nhìn thấy; thiếu quyền trả 403.
5. Kiểm tra revision; chỉ caller đã được phép mới nhận lỗi stale/conflict 409.
6. Kiểm tra trạng thái, điều kiện nghiệp vụ và đối tượng đích; mã lỗi cụ thể theo hợp đồng của tính năng đã duyệt.
7. Ghi thay đổi và lịch sử nghiệp vụ nguyên tử; xử lý xung đột đồng thời, không phát sự kiện thành công trước commit.

Không dùng thứ tự “tìm ID → kiểm revision → kiểm quyền” trong sơ đồ assignment cũ. Với caller ngoài phạm vi, revision đúng/sai đều không được tiết lộ ticket có tồn tại. Thứ tự này là yêu cầu thiết kế cho tính năng tương lai; không tự thêm revision hay response 422 vào API hiện có.

## Tình huống bắt buộc khi triển khai

- Không đăng nhập/ID sai bị từ chối trước persistence.
- Hai ticket ngoài phạm vi, một tồn tại và một không tồn tại, có phản hồi che giấu tương đương.
- Ticket ngoài phạm vi với revision đúng hoặc sai đều không trả revision/409.
- Ticket nhìn thấy nhưng không có quyền hành động bị từ chối trước kiểm tra revision.
- Caller được phép nhưng revision stale không ghi dữ liệu hoặc lịch sử thành công.
- Hai request hợp lệ cạnh tranh không tạo ghi đè mất dữ liệu; lỗi lưu lịch sử rollback thay đổi nghiệp vụ.

Các trường hợp assignment/revision là tiêu chí cho lần triển khai tương ứng, chưa phải kiểm thử đã chạy. Quyền Agent hiện hành nằm trong [hợp đồng backend](current-backend-contract.md); thay chính sách theo [DEC-02](pending-business-decisions.md).
