# ADR-010: Kiểm thử tích hợp PostgreSQL bằng Testcontainers

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Bối cảnh

Cần PostgreSQL thật để kiểm tra migration EF Core, bảng Identity, ràng buộc và giao dịch. Bộ lưu dữ liệu trong bộ nhớ không thể xác nhận các hành vi đặc thù PostgreSQL.

## Quyết định

Dùng Testcontainers for .NET để tạo PostgreSQL tạm cho kiểm thử. Bộ kiểm thử khởi tạo cơ sở dữ liệu, chạy các tình huống rồi thu dọn. `WebApplicationFactory` thay chuỗi kết nối bằng đích kiểm thử.

## Phương án đã cân nhắc

- EF Core InMemory: không kiểm chứng đầy đủ ràng buộc, giao dịch hoặc các phần riêng của PostgreSQL.
- SQLite trong bộ nhớ: gần SQL hơn nhưng khác phương ngữ và không có các khả năng tương ứng như pgvector.
- PostgreSQL phát triển dùng chung: trạng thái giữa các lần chạy dễ ảnh hưởng nhau và khó dựng lại độc lập.

## Hệ quả

Kiểm thử được cách ly và phát hiện sớm lỗi migration/hành vi dữ liệu thật. Máy phát triển hoặc môi trường CI cần Docker; khởi tạo container làm tăng thời gian chạy. MinIO và nhà cung cấp AI bên ngoài được thay bằng bộ giả phù hợp với phạm vi kiểm thử.

Từng kết quả phải ghi số kiểm thử thực chạy và commit. Xem [hướng dẫn kiểm chứng](../runbooks/backend-hardening-verification.md).

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-010-testcontainers-postgresql.md).
