# Thuật ngữ kỹ thuật dùng trong tài liệu

Trạng thái: hướng dẫn hiện hành; cập nhật 2026-10-20. Áp dụng cùng [quy chuẩn tài liệu](documentation-standard.md). Bảng này giải thích cách viết, không đổi tên lớp, vai trò hoặc dữ liệu API.

| Định danh hoặc thuật ngữ gốc | Cách diễn giải bằng tiếng Việt |
| --- | --- |
| Backend | Phần xử lý phía máy chủ; có thể giữ “backend” để phân biệt kho mã |
| Frontend | Giao diện người dùng; mã thuộc kho giao diện độc lập |
| Ticket | Phiếu yêu cầu |
| Requester | Người yêu cầu; giữ `Requester` khi nói về giá trị vai trò |
| Agent / Admin | Nhân viên hỗ trợ / quản trị viên; giữ tên vai trò trong cấu hình và mã |
| Module / feature | Mô-đun / tính năng |
| Use case | Luồng xử lý nghiệp vụ |
| Published contracts | Giao diện và kiểu dữ liệu được phép công khai giữa các mô-đun |
| Port / adapter | Cổng giao tiếp / bộ tích hợp triển khai cổng |
| Actor / claims | Người hoặc tiến trình thực hiện thao tác / thông tin danh tính trong token |
| Authentication / authorization | Xác thực danh tính / kiểm tra quyền truy cập |
| Scope | Phạm vi truy cập |
| DTO | Dữ liệu trao đổi giữa các thành phần; giữ tên kiểu DTO khi đối chiếu mã |
| Persistence / transaction | Lưu dữ liệu / giao dịch |
| Idempotency | Xử lý lặp an toàn; với tạo phiếu là chống tạo trùng và phát lại phản hồi đã lưu |
| Receipt / fingerprint | Biên nhận / dấu vân tay của yêu cầu |
| Optimistic concurrency | Phát hiện xung đột khi nhiều yêu cầu cùng ghi dữ liệu |
| Baseline / re-baseline | Mốc chuẩn / xác lập lại mốc chuẩn |
| Runbook / rollback | Hướng dẫn vận hành / khôi phục phiên bản trước |
| CI / quality gate | Tích hợp liên tục / bước kiểm tra bắt buộc |
| Smoke test | Kiểm tra nhanh các luồng chính |
| Image / registry | Ảnh đóng gói ứng dụng / kho lưu ảnh |
| NoOp / Fake | Bộ tích hợp không thực hiện thao tác thật / bộ tích hợp giả dành cho kiểm thử |
| Timeout / retry / circuit breaker | Giới hạn thời gian / thử lại / ngắt tạm kết nối khi dịch vụ liên tục lỗi |
| SLA | Cam kết mức dịch vụ |
| Human-in-the-Loop | Con người xem xét và quyết định đối với đề xuất AI |

Điểm mô hình AI và các mức Low/Medium/High là tín hiệu hỗ trợ người đánh giá, chưa phải xác suất đã hiệu chuẩn. Không diễn giải điểm 0,75 thành khẳng định “đúng 75%”. Trong ngữ cảnh dự án này, dùng “phiếu yêu cầu” cho Ticket và “biên nhận” cho receipt; tránh “vé” hoặc “hóa đơn” gây nhầm nghiệp vụ.

Các trạng thái như `Submitted`, `InProgress`, `Resolved`, `Closed`, `Cancelled` được giữ nguyên khi đối chiếu enum. Phần diễn giải lần lượt dùng “đã gửi”, “đang xử lý”, “đã giải quyết”, “đã đóng”, “đã hủy”. Giá trị số và nghĩa dữ liệu hiện hành nằm trong [hợp đồng backend](../architecture/current-backend-contract.md).

Ví dụ nên viết: “Application kiểm tra quyền của người đang đăng nhập rồi gọi cổng lưu dữ liệu.” Tên kỹ thuật vẫn được giữ trong câu cần truy vết: “`IClaimTicket.ExecuteAsync` gọi `Ticket.Claim` để chuyển trạng thái.”
