# ADR-014: Luồng xử lý theo tính năng và giao diện công khai của mô-đun

## Trạng thái và mốc quyết định

Đã chấp nhận ngày 2026-10-19. Mốc nguồn của quyết định gốc: `9dd0746450dca4348d6d0fa8a09cfc2a48860020`. Bản diễn giải tiếng Việt ngày 2026-10-20 giữ ngày, phạm vi và số liệu gốc; phần phát triển sau đó được ghi riêng ở cuối.

## Quyết định tại mốc tái cấu trúc

Giữ sáu dự án backend và ba mô-đun Identity, Ticketing, AIAssistance. Application công khai sáu giao diện nghiệp vụ tại mốc quyết định: RegisterUser, LoginUser, GetCurrentUser, CreateTicket, GetMyTickets và GetTicketDetail. Mỗi lớp triển khai là `internal`, thực hiện một thao tác và được đăng ký rõ ràng, không qua bộ điều phối trung gian.

Application sở hữu kiểm tra danh tính và phạm vi truy cập. Requester chỉ đọc chi tiết phiếu của mình; Agent/Admin giữ phạm vi toàn hệ thống. Đợt này chưa triển khai cách ly theo nhóm, phân công hoặc vòng đời phiếu mở rộng. Tạo phiếu vẫn là Submitted; đăng ký chỉ cấp Requester. FullName chỉ thuộc phản hồi cho đến đợt bổ sung hồ sơ riêng. Tiêu đề đầu vào tối đa 100 ký tự; cột dữ liệu hiện có vẫn giữ 200 ký tự.

Giao diện nghiệp vụ và DTO được công khai khác với cổng dành cho bộ tích hợp. Phụ thuộc xuyên mô-đun phải nằm trong danh sách kiểu cho phép rõ ràng. Mã dùng chung không trở thành đường đi vòng tới thực thể mô-đun; ngoại lệ kiến trúc chỉ rõ từng kiểu kỹ thuật. Giữ một `DbContext`, ánh xạ do mô-đun sở hữu và các cổng dữ liệu chuyên biệt.

Identity tuần tự hóa kiểm tra mật khẩu và ghi số lần sai/đặt lại theo từng tài khoản bằng khóa dòng và giao dịch PostgreSQL. Lỗi ghi kỹ thuật không được âm thầm bỏ trạng thái bảo mật hoặc cấp token. Tạo phiếu giữ cơ chế chống tạo trùng nguyên tử và phản hồi phát lại nguyên trạng.

DTO/ánh xạ JSON của hợp đồng AI thuộc Infrastructure. Kết quả Application bảo đảm điều kiện đầu ra của bộ tích hợp; Domain sở hữu bất biến đề xuất. Mã băm đầu vào v2 dùng các trường UTF-8 có tiền tố độ dài; dịch vụ AI phản hồi nguyên trạng mã băm trên giao thức.

## Bằng chứng tại mốc gốc

Biên dịch thành công, 202 kiểm thử không cần cơ sở dữ liệu đạt; CI của mốc gốc cũng đạt 54 kiểm thử PostgreSQL thật và tạo ba ảnh ứng dụng. Đây là số liệu của mốc nguồn ghi trên, không phải số kiểm thử tại HEAD hôm nay. Nghiệm thu cuối phải kiểm tra lại đúng phiên bản triển khai. Quyết định không chủ đích thay cấu trúc dữ liệu.

## Bổ sung phạm vi sau PR #28

Ngày 2026-10-20, mã `dev` tại `260f37b5c415d0aa69818dc306411ab5a4f467f5` đã có thêm UpdateTicket, ClaimTicket, ResolveTicket, ReopenTicket, CloseTicket và CancelTicket, đưa tổng số luồng lên 12.

Đây là phần phát triển sau mốc quyết định, không sửa nghĩa “sáu luồng” hoặc số liệu kiểm thử gốc. Các ranh giới tầng/mô-đun tiếp tục được áp dụng; chức năng và điểm chưa đạt được mô tả tại [mô-đun hiện hành](../architecture/backend-modules-and-use-cases.md), [hợp đồng backend](../architecture/current-backend-contract.md) và [quy tắc bảo mật](../architecture/security-design-rules.md).

Nhận xử lý chưa có phân công; mở lại chưa có giới hạn thời gian hoặc tác vụ tự đóng. Các lựa chọn còn mở theo [bảng quyết định](../architecture/pending-business-decisions.md).

Đối chiếu bổ sung ngày 2026-10-20 tại `dev@47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #29: vẫn có 12 luồng. PR #29 bổ sung tiếng Việt trong mã và một số chuỗi hiển thị, chưa hoàn tất các khoảng cách quyền/ghi đồng thời. [Bảng mốc nguồn](../architecture/source-baseline.md) phân biệt phần đã triển khai với bản local trước PR #28.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-014-feature-use-cases-and-published-contracts.md).

## Củng cố ranh giới ngày 2026-10-23

Trên nền `47da281` (cùng cây nội dung với `e2d1ad4`), tách danh sách contract gọi từ HTTP khỏi ma trận contract theo bên sử dụng và mô-đun sở hữu; giữ kiểm tra từ chối kiểu chưa phân loại. Bổ sung ownership đối với kiểu Identity của framework và đường gọi generic. Không đổi mô hình dữ liệu, không cố định tổng số use case; kiểm thử vẫn yêu cầu implementation nội bộ và contract `ExecuteAsync` cùng tính năng.

Chủ dự án chọn Agent/Admin đều được claim; quyết định một phần DEC-02 và tác động ở [bảng quyết định](../architecture/pending-business-decisions.md). Quyền resolve được kiểm tra trước persistence. Xung đột ghi lạc quan được chuyển từ EF sang lỗi Application rồi HTTP 409 `{ error }`; đây là bổ sung có chủ đích cho sáu lệnh vòng đời, không chuyển toàn bộ lỗi HTTP sang một mô hình mới. Các thay đổi chưa commit khi ghi bổ sung; bằng chứng chạy phải ghi riêng theo bản làm việc, không kế thừa số liệu tại mốc gốc.
