# Mã băm đầu vào và liên kết yêu cầu phân loại AI v1

Trạng thái: hướng dẫn hợp đồng hiện hành; đối chiếu mã `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`, cập nhật 2026-10-20. Đợt Việt hóa không đổi OpenAPI, dữ liệu mẫu, mã băm hoặc phiên bản giao thức.

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

Chưa lưu bản ghi AI hoặc kết nối nhà cung cấp thật. Phạm vi bộ tích hợp tương lai nằm trong [ADR-012](../../../docs/adr/ADR-012-ai-contract-and-future-transport.md). Cách chạy công cụ kiểm tra hợp đồng nằm trong [hướng dẫn môi trường](../../../docs/runbooks/environment-runbook.md#python-venv-và-hợp-đồng-ai).
