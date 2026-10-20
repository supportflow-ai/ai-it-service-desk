# ADR-004: PostgreSQL và pgvector

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Bối cảnh

Hệ thống cần cơ sở dữ liệu nghiệp vụ và định hướng lưu biểu diễn vectơ để tìm kiếm tương đồng bằng AI.

## Quyết định

Chọn PostgreSQL làm cơ sở dữ liệu chính và pgvector cho định hướng lưu vectơ.

## Phương án đã cân nhắc

- SQL Server: chi phí bản quyền và mức phù hợp với hệ thống ưu tiên Linux.
- PostgreSQL kết hợp cơ sở dữ liệu vectơ riêng như Pinecone/Weaviate: tăng số thành phần và độ phức tạp ở quy mô hiện tại.

## Hệ quả

Một cơ sở dữ liệu có thể phục vụ dữ liệu quan hệ và vectơ; PostgreSQL là nền tảng nguồn mở đã được sử dụng rộng rãi. Khả năng mở rộng khi số vectơ rất lớn là đánh đổi cần đánh giá riêng.

Phần đã triển khai được xác định trong [hợp đồng hiện hành](../architecture/current-backend-contract.md): sử dụng PostgreSQL không có nghĩa đã có luồng tạo, lưu hoặc tìm kiếm vectơ. Quyết định này không xác nhận AI thật đã được kết nối.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-004-postgresql-pgvector.md).
