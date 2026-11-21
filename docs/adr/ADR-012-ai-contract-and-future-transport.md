# ADR-012: Hợp đồng phân loại AI v1 và bộ tích hợp tương lai

## Trạng thái

Quyết định ban đầu chấp nhận nền tảng hợp đồng và hoãn kết nối dịch vụ thật. Bản diễn giải tiếng Việt cập nhật ngày 20/10/2026. Đối chiếu ngày 21/11/2026 tại `dev@3640c33` xác nhận đã có adapter HTTP. Luồng nghiệp vụ gọi từ Ticketing và lưu quyết định duyệt còn chờ tích hợp.

## Hợp đồng và trách nhiệm

Nguồn chuẩn là `contracts/ai/v1/openapi.yaml`, dùng OpenAPI 3.1 và YAML tương thích JSON. Backend công bố bản hợp đồng có phiên bản, sở hữu ngữ nghĩa; kho `ai-service` dùng bản sao ghim commit/checksum. Thay đổi phá vỡ tương thích cần phiên bản chính mới; thay đổi tương thích vẫn phải cập nhật dữ liệu mẫu, công cụ kiểm tra và hai kho cùng nhau.

`POST /v1/ticket-classifications` nhận ảnh chụp nội dung phiếu từ Backend qua Bearer dịch vụ. Bên gọi có trách nhiệm kiểm quyền lấy dữ liệu. Request gồm `requestId`, `ticketId`, `ticketRevision`, `inputHash`, `title` và `description`. Phản hồi trả lại bốn trường tương quan và mảng `suggestions`. Mỗi phần tử gồm `field`, `value`, `score`. Tối đa một gợi ý cho mỗi trong ba trường `category`, `impact`, `urgency`. Mảng rỗng biểu thị không có gợi ý. [OpenAPI và fixture v1](../../contracts/ai/v1/README.md)

Chỉ chấp nhận tên giá trị trong danh mục cho phép và điểm hữu hạn từ 0 đến 1. Điểm null thể hiện chưa có bằng chứng, không được tự tạo độ tin cậy. Từ chối trường lạ, trường đặc quyền, enum dạng số, đề xuất trùng và kết quả cũ hoặc sai yêu cầu. Không gửi token người dùng, bí mật, ghi chú nội bộ hoặc dữ liệu không liên quan.

Backend tính mức ưu tiên/độ tin cậy, kiểm tra quyền đọc, sở hữu mọi chuyển trạng thái và quyết định chấp nhận/điều chỉnh/từ chối của con người. AI không được cấp thông tin truy cập ghi cơ sở dữ liệu nghiệp vụ và phản hồi AI không trực tiếp thực hiện hành động.

`xmin` là dấu phiên bản của cơ sở dữ liệu, không tự thay thế phiên bản nghiệp vụ kiểu `long`. Chỉ thêm cột phiên bản hoặc dữ liệu AI sau khi có triển khai riêng xác định vòng đời và trách nhiệm sở hữu.

## Yêu cầu bộ tích hợp HTTP tương lai

Các yêu cầu sau giữ nguyên thiết kế trong bản diễn giải ngày 20/10/2026. So sánh với phần adapter hiện hành ở dưới khi lập cấu hình hoặc kiểm thử.

- Dùng HTTPS và thông tin xác thực dịch vụ riêng, có phạm vi hẹp, lấy từ cấu hình bí mật. Không chuyển tiếp JWT của người dùng; thay khóa ngoài mã nguồn.
- Giới hạn mỗi lần thử 3 giây; tổng thời gian thao tác tối đa 5 giây, gồm cả thời gian chờ.
- Thử lại tối đa một lần khi lỗi truyền tải hoặc 502/503/504. Chỉ thử lại 429 khi `Retry-After` nằm trong thời gian còn lại; nếu không thì dùng phương án dự phòng ngay.
- Không thử lại lỗi kiểm tra đầu vào, 401/403 hoặc yêu cầu hủy từ bên gọi. Giữ cùng mã yêu cầu giữa các lần thử; phân loại chỉ đọc nhưng chi phí nhà cung cấp có thể lặp.
- Ngắt tạm kết nối sau năm lỗi truyền tải/khả dụng liên tiếp, chờ 30 giây rồi cho phép một lần thăm dò. Thành công thì đóng trạng thái ngắt; thất bại thì mở lại.
- Chuyển lỗi nhà cung cấp thành kết quả được kiểm soát `Unavailable`, `Timeout`, `InvalidResponse`; giữ nghiệp vụ phiếu hoạt động và không lộ lỗi thô, nội dung hoặc thông tin xác thực.
- Luôn dùng bộ chuyển đổi hợp đồng trước khi trả kết quả Application; giới hạn kích thước phản hồi, che thông tin xác thực và truyền yêu cầu hủy.

## Adapter HTTP tại mốc đối chiếu

| Hạng mục | Hành vi trong mã tại `dev@3640c33` |
| --- | --- |
| Đăng ký và kết nối | `Ai:Enabled` mặc định tắt và chọn NoOp. Khi bật, adapter dùng origin HTTP(S) ở `Ai:Endpoint`, Bearer dịch vụ ở `Ai:BearerToken` và tắt chuyển hướng tự động. Việc dùng HTTPS là yêu cầu thiết kế ban đầu, còn validator chấp nhận HTTP và HTTPS |
| Ngân sách thời gian | `Ai:TimeoutSeconds` mặc định 5, từ 1 đến 30. Cùng ngân sách cho cả hai lần thử, chờ giữa các lần và đọc phản hồi. Chưa đặt giới hạn 3 giây riêng cho mỗi lần |
| Thử lại | Tối đa một lần khi nhận 502, 503 hoặc 504. Chờ 100 ms và dùng lại request gồm `requestId`. 429 và lỗi truyền tải trả về kết quả lỗi có kiểm soát ở lần gọi đó. Hủy từ bên gọi được truyền tiếp |
| Ngắt mạch | Sau ba kết quả lỗi liên tiếp, ngắt trong 30 giây. Mạch dùng chung trong một tiến trình. Một kết quả thành công đặt lại bộ đếm. Mã hiện chưa giới hạn một lần thăm dò duy nhất khi hết thời gian ngắt |
| Phản hồi | Đọc JSON tối đa 64 KiB. Kiểm tra tương quan, tập giá trị, điểm, trường lạ và gợi ý trùng. Các kết quả `Unavailable`, `Timeout` và `InvalidResponse` thuộc adapter. Xử lý thủ công khi suy giảm phải được kiểm ở luồng nghiệp vụ tương lai |

Trong thiết kế bước duyệt, `inputHash` kiểm độ mới của nội dung còn `expectedRevision` kiểm ghi đồng thời khi lưu. `ticketRevision` trong hợp đồng v1 là revision của ảnh chụp và được trả lại để đối chiếu. Việc bật adapter chỉ chọn cách gọi AI. Luồng Ticketing, quyền duyệt, lưu kết quả và audit được triển khai theo kế hoạch riêng.

[Đăng ký adapter](../../src/backend/Modules/AIAssistance/AIAssistanceModule.cs) · [Cấu hình AiOptions](../../src/backend/Modules/AIAssistance/Infrastructure/Options/AiOptions.cs) · [Adapter HTTP](../../src/backend/Modules/AIAssistance/Infrastructure/HttpTicketClassificationService.cs) · [Ngắt mạch](../../src/backend/Modules/AIAssistance/Infrastructure/ClassificationCircuit.cs) · [Kế hoạch tích hợp và AI-02](../architecture/ai-integration-plan.md)

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-012-ai-contract-and-future-transport.md).
