# ADR-012: Hợp đồng phân loại AI v1 và bộ tích hợp tương lai

## Trạng thái

Nền tảng hợp đồng đã được chấp nhận; kết nối dịch vụ thật được hoãn. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Hợp đồng và trách nhiệm

Nguồn chuẩn là `contracts/ai/v1/openapi.yaml`, dùng OpenAPI 3.1 và YAML tương thích JSON. Backend công bố bản hợp đồng có phiên bản, sở hữu ngữ nghĩa; kho `ai-service` dùng bản sao ghim commit/checksum. Thay đổi phá vỡ tương thích cần phiên bản chính mới; thay đổi tương thích vẫn phải cập nhật dữ liệu mẫu, công cụ kiểm tra và hai kho cùng nhau.

API AI tương lai `POST /v1/ticket-classifications` chỉ nhận ảnh chụp dữ liệu đã được backend kiểm tra quyền: mã yêu cầu, ID phiếu, phiên bản nghiệp vụ, mã băm xác định của tiêu đề/mô tả và nội dung cần phân loại. Phản hồi giữ nguyên các giá trị liên kết và có tối đa một đề xuất cho từng trường danh mục, mức ảnh hưởng, mức khẩn cấp.

Chỉ chấp nhận tên giá trị trong danh mục cho phép và điểm hữu hạn từ 0 đến 1. Điểm null thể hiện chưa có bằng chứng, không được tự tạo độ tin cậy. Từ chối trường lạ, trường đặc quyền, enum dạng số, đề xuất trùng và kết quả cũ hoặc sai yêu cầu. Không gửi token người dùng, bí mật, ghi chú nội bộ hoặc dữ liệu không liên quan.

Backend tính mức ưu tiên/độ tin cậy, kiểm tra quyền đọc, sở hữu mọi chuyển trạng thái và quyết định chấp nhận/điều chỉnh/từ chối của con người. AI không được cấp thông tin truy cập ghi cơ sở dữ liệu nghiệp vụ và phản hồi AI không trực tiếp thực hiện hành động.

`xmin` là dấu phiên bản của cơ sở dữ liệu, không tự thay thế phiên bản nghiệp vụ kiểu `long`. Chỉ thêm cột phiên bản hoặc dữ liệu AI sau khi có triển khai riêng xác định vòng đời và trách nhiệm sở hữu.

## Yêu cầu bộ tích hợp HTTP tương lai

Các yêu cầu sau chưa được kết nối trong giai đoạn hiện tại:

- Dùng HTTPS và thông tin xác thực dịch vụ riêng, có phạm vi hẹp, lấy từ cấu hình bí mật. Không chuyển tiếp JWT của người dùng; thay khóa ngoài mã nguồn.
- Giới hạn mỗi lần thử 3 giây; tổng thời gian thao tác tối đa 5 giây, gồm cả thời gian chờ.
- Thử lại tối đa một lần khi lỗi truyền tải hoặc 502/503/504. Chỉ thử lại 429 khi `Retry-After` nằm trong thời gian còn lại; nếu không thì dùng phương án dự phòng ngay.
- Không thử lại lỗi kiểm tra đầu vào, 401/403 hoặc yêu cầu hủy từ bên gọi. Giữ cùng mã yêu cầu giữa các lần thử; phân loại chỉ đọc nhưng chi phí nhà cung cấp có thể lặp.
- Ngắt tạm kết nối sau năm lỗi truyền tải/khả dụng liên tiếp, chờ 30 giây rồi cho phép một lần thăm dò. Thành công thì đóng trạng thái ngắt; thất bại thì mở lại.
- Chuyển lỗi nhà cung cấp thành kết quả được kiểm soát `Unavailable`, `Timeout`, `InvalidResponse`; giữ nghiệp vụ phiếu hoạt động và không lộ lỗi thô, nội dung hoặc thông tin xác thực.
- Luôn dùng bộ chuyển đổi hợp đồng trước khi trả kết quả Application; giới hạn kích thước phản hồi, che thông tin xác thực và truyền yêu cầu hủy.

Cấu hình chạy thật hiện chỉ đăng ký NoOp. Bộ giả nằm trong kiểm thử. CI hiện kiểm tra lược đồ, dữ liệu mẫu, ánh xạ và NoOp; bộ tích hợp thật cần kiểm thử truyền tải/cơ chế ngắt kết nối trước khi được đưa vào.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-012-ai-contract-and-future-transport.md).
