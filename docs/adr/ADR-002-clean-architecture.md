# ADR-002: Phân tầng theo Clean Architecture

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20; giữ quyết định phân tầng ban đầu.

Bổ sung phạm vi ngày 2026-10-19: bốn dự án dưới đây mô tả bốn tầng ban đầu. Worker và Database là chương trình/công cụ bổ sung; Application được dùng các giao diện hỗ trợ đăng ký phụ thuộc. Cấu trúc sáu dự án, thư mục theo tính năng và giao diện công khai được làm rõ trong [ADR-011](ADR-011-backend-module-boundaries.md), [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md).

## Bối cảnh

Cần tách nghiệp vụ khỏi nền tảng kỹ thuật để dễ kiểm thử, bảo trì và thay thế bộ tích hợp.

## Quyết định

Áp dụng Clean Architecture:

| Tầng | Trách nhiệm và phụ thuộc |
| --- | --- |
| Domain | Thực thể, kiểu giá trị, quy tắc nghiệp vụ; độc lập với nền tảng kỹ thuật |
| Application | Luồng xử lý và cổng giao tiếp; phụ thuộc Domain |
| Infrastructure | Triển khai các cổng bằng EF Core, MinIO và các bộ tích hợp khác |
| API | HTTP và lắp ghép dịch vụ; tham chiếu Application/Infrastructure |

Worker và Database sử dụng lại các tầng trên theo trách nhiệm riêng, không tạo thêm tầng nghiệp vụ.

## Phương án đã cân nhắc

- Phân tầng Controller → Service → Repository theo thông lệ: không tự bảo đảm đảo chiều phụ thuộc; lớp dịch vụ dễ ôm quá nhiều trách nhiệm.
- Chỉ chia theo tính năng: nhóm khó duy trì ranh giới mô-đun nếu thiếu quy tắc phụ thuộc giữa các tầng.

## Hệ quả

Domain dễ kiểm thử và bộ tích hợp có thể thay thế. Đổi lại, nhóm phải hiểu chiều phụ thuộc và quản lý nhiều dự án hơn. Kiểm thử kiến trúc bằng NetArchTest cùng các kiểm tra ranh giới hiện hành giúp phát hiện vi phạm.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-002-clean-architecture.md).
