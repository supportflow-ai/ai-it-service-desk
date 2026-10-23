# Phạm vi tài liệu đánh giá backend

Trạng thái: chỉ mục hiện hành; cập nhật 2026-10-20. Chú thích tiếng Việt nằm ngoài phần bản gốc; ngày, commit và kết luận của bằng chứng không được thay bằng kết quả mới.

| Hồ sơ | Nội dung tra cứu |
| --- | --- |
| [Rà soát tài liệu và phạm vi kho ngày 2026-10-20](documentation-audit-20261020.md) | Mâu thuẫn, ba điểm đã khắc phục, kết quả kiểm tra và ứng viên dọn tệp/thư mục |
| [Rà soát kho mã](repository-audit.md), [nợ kỹ thuật](technical-debt-register.md) | Phát hiện và điểm chặn tại mốc gốc |
| [Kế hoạch tích hợp](branch-integration-plan.md), [đợt tích hợp dev](consolidated-dev-integration.md) | Nguồn nhánh và phạm vi các đợt hợp nhất cũ |
| [Chống tạo trùng phiếu](p0-idempotency-remediation.md) | Phương án và giới hạn kiểm chứng ở thời điểm xử lý |
| [Mốc tái cấu trúc](refactor-baseline.md), [kiểm chứng tái cấu trúc](refactor-verification.md) | Mã nguồn và kết quả của đợt tái cấu trúc |
| [Khoảng cách hợp đồng phiếu](ticket-contract-gap-analysis.md), [bảo mật thao tác phiếu](ticket-security-findings.md) | Phân tích theo mốc nguồn, cần đối chiếu lại khi phát triển |
| [Rà soát ngữ cảnh](project-context-audit.md), [khắc phục ngữ cảnh](project-context-remediation.md) | Đợt chuẩn hóa nguồn tài liệu ngày 2026-10-19 |

Các tài liệu trong thư mục này ghi nhận đánh giá backend hoặc bằng chứng tại một
commit cụ thể. Những báo cáo trước khi tách kho có thể nhắc đến cấu trúc cũ gồm cả
giao diện và hạ tầng; không dùng các lệnh hoặc đường dẫn cũ làm hướng dẫn chạy hiện tại.

Tài liệu và bản vá chỉ phục vụ giao diện đã được loại khỏi phiên bản hiện tại của
kho backend. Có thể tra cứu nguyên bản tại commit
`508c6367d7109f796108868bb8ac65b83169ced7` trong lịch sử Git.

Nguồn tham chiếu hiện tại:

- [Kiến trúc và luồng xử lý backend](../architecture/backend-modules-and-use-cases.md).
- [Quy chuẩn tài liệu và cách giữ bằng chứng](../process/documentation-standard.md).
- [Kiểm chứng backend](../runbooks/backend-hardening-verification.md).
- [Hợp đồng HTTP được bảo vệ bằng kiểm thử](../../contracts/backend/openapi-baseline.json).
- [Kho giao diện độc lập](https://github.com/supportflow-ai/ai-it-service-desk-web).
- [Kho hạ tầng và kiểm tra toàn hệ thống](https://github.com/supportflow-ai/supportflow-ai-infra).

Đợt đồng bộ ngữ cảnh: [báo cáo trước chỉnh sửa](project-context-audit.md) và [kết quả xử lý](project-context-remediation.md). Báo cáo trước chỉnh sửa giữ nguyên phát hiện và số dòng tại mốc đã ghi; dùng kết quả xử lý để biết phần đã được cập nhật.

- [Củng cố backend 2026-10-23](backend-hardening-20261023.md): thay đổi chưa commit, 291 test offline; PostgreSQL chưa nghiệm thu.
