# ADR-001: Kiến trúc nguyên khối theo mô-đun

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20; giữ phạm vi quyết định ban đầu.

Bổ sung phạm vi ngày 2026-10-19: năm mô-đun là định hướng sản phẩm, không phải yêu cầu tạo mô-đun rỗng. Phần đã triển khai gồm Identity, Ticketing và AIAssistance; Knowledge/Analytics chưa triển khai. Xem [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md) và [bản đồ tài liệu](../README.md).

## Bối cảnh

Nhóm năm người xây dựng hệ thống hỗ trợ yêu cầu CNTT, gồm các mảng danh tính, phiếu yêu cầu, tri thức, hỗ trợ AI và phân tích. Kiến trúc cần ranh giới rõ ràng và phù hợp quy mô nhóm.

## Quyết định

Chọn kiến trúc nguyên khối theo mô-đun (Modular Monolith): các mô-đun logic cùng nằm trong một đơn vị triển khai. Định hướng ban đầu có năm mô-đun; phần bổ sung phía trên xác định phạm vi đã có.

## Phương án đã cân nhắc

- Dịch vụ nhỏ độc lập: tăng chi phí xử lý hệ thống phân tán, vượt nhu cầu của nhóm.
- Ứng dụng nguyên khối không chia mô-đun: thiếu ranh giới rõ ràng và khó bảo trì khi mở rộng.

## Hệ quả

Triển khai và tìm lỗi đơn giản hơn; có thể đánh giá thay đổi theo từng tính năng. Nhóm phải duy trì ranh giới bằng kiểm thử kiến trúc. Có thể tách mô-đun thành dịch vụ riêng khi có nhu cầu thực tế.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-001-modular-monolith.md).
