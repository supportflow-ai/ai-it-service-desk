# Mã băm đầu vào và liên kết yêu cầu phân loại AI v1

Hướng dẫn hợp đồng hiện hành, đối chiếu Backend `dev@3640c33` ngày 21/11/2026. Thuật toán và giá trị kiểm chứng giữ từ mốc `47634bc`. OpenAPI, fixture và phiên bản giao thức vẫn theo bản ghim v1.

Backend tính `inputHash`. Nhà cung cấp AI coi đây là giá trị cần phản hồi nguyên trạng, cùng `requestId`, `ticketId`, `ticketRevision`; không tự suy ra quyền hoặc tính lại bằng cách chuẩn hóa riêng.

## Thuật toán chuẩn ai-input-v2

1. Mở đầu bằng các byte UTF-8 của `ai-input-v2`, tiếp theo là một byte 0.
2. Loại khoảng trắng ở hai đầu tiêu đề; mã hóa UTF-8; nối độ dài byte bằng số nguyên 32 bit theo thứ tự byte lớn trước, rồi nối nội dung byte.
3. Mô tả null được coi là chuỗi rỗng; loại khoảng trắng hai đầu và nối độ dài/nội dung như bước trước.
4. Tính SHA-256 trên toàn bộ dãy byte, biểu diễn bằng 64 ký tự thập lục phân viết thường.

Không đổi chữ hoa/thường, chuẩn hóa Unicode hoặc thay ký tự xuống dòng.

Giá trị kiểm chứng: tiêu đề `VPN issue`, mô tả `Cannot connect` tạo mã:

```text
e737e044b7ef976a6f2f2e97f1f2aaee633a2205cc16d051316bf2139d5dc02c
```

Tiền tố độ dài ngăn nhầm ranh giới giữa các trường. Thuật toán này độc lập với dấu vân tay chống tạo trùng phiếu; phần tạo phiếu giữ cách mã hóa riêng.

## Phạm vi triển khai

Backend có adapter HTTP chọn bằng `Ai:Enabled` và kiểm bốn trường tương quan. Tại mốc rà còn thiếu caller từ Ticketing, lưu phân tích và luồng duyệt. [Adapter hiện hành](../../../docs/adr/ADR-012-ai-contract-and-future-transport.md#adapter-http-tại-mốc-đối-chiếu) · [Kiểm tra hợp đồng](../../../docs/runbooks/environment-runbook.md#python-venv-và-hợp-đồng-ai)

Trong bộ checkout liên kho, xem sai khác giữa bản ghim và runtime ở `ai-service/docs/backend-alignment.md` mục “Đối chiếu phản hồi HTTP phân loại”. Khi cập nhật hợp đồng có chủ đích, phối hợp cùng bản ghim AI Service và kiểm lại trên ứng viên.
