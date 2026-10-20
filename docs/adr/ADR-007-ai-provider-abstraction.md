# ADR-007: Cổng giao tiếp với nhà cung cấp AI

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Bối cảnh

AI hỗ trợ phân loại phiếu và định hướng tìm kiếm kho tri thức. Cần giảm phụ thuộc vào một nhà cung cấp và duy trì nghiệp vụ khi AI không sẵn sàng.

## Quyết định

Application sở hữu `ITicketClassificationService` và `IEmbeddingService`. Infrastructure cung cấp các bộ tích hợp NoOp cho giai đoạn khởi tạo. Nhà cung cấp thật được bổ sung sau bằng bộ tích hợp triển khai các cổng này.

## Phương án đã cân nhắc

- Gọi trực tiếp SDK trong Application: làm nghiệp vụ phụ thuộc nhà cung cấp, khó kiểm thử và thay thế.
- Một giao diện AI lớn cho mọi khả năng: gom nhiều trách nhiệm, khó bảo trì khi thêm chức năng.
- Gắn cứng NoOp, không có cổng: thiếu ranh giới chuẩn bị cho tích hợp sau.

## Hệ quả

Có thể thay bộ tích hợp và dùng NoOp/bộ giả để kiểm thử độc lập; nghiệp vụ chính có thể tiếp tục khi AI không khả dụng. Mỗi nhà cung cấp cần bộ tích hợp riêng. Kiểm thử tự động không phụ thuộc dịch vụ AI thật.

Hợp đồng v1 và yêu cầu của bộ tích hợp tương lai được bổ sung trong [ADR-012](ADR-012-ai-contract-and-future-transport.md). Hiện chưa kết nối AI thật.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-007-ai-provider-abstraction.md).
