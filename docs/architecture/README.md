# Chỉ mục kiến trúc backend

Trạng thái: hiện hành cho kho backend. Đối chiếu `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #28 và #29; cập nhật 2026-10-20. Quy tắc chọn nguồn nằm trong [bản đồ tài liệu](../README.md).

## Nguồn đang áp dụng

| Nguồn | Phạm vi |
| --- | --- |
| [Mốc mã nguồn](source-baseline.md) | Phân biệt mã local, thay đổi chưa commit và dev sau PR #29; khoảng cách hợp đồng/công cụ cần xử lý trước khi tích hợp |
| [Mô-đun và luồng xử lý](backend-modules-and-use-cases.md) | Clean Architecture, 12 luồng nghiệp vụ, giao diện công khai và giao dịch |
| [Hợp đồng backend](current-backend-contract.md) | API, mười mã trạng thái, dữ liệu và quyền đã triển khai tại SHA được ghi |
| [Quy tắc thiết kế bảo mật](security-design-rules.md) | Quyền trước revision, vị trí policy Ticketing, tình huống phủ định |
| [Danh mục ADR](../adr/README.md) | Quan hệ giữa quyết định nền tảng và ADR-011–014 |
| [Quyết định còn mở](pending-business-decisions.md) | Các lựa chọn nghiệp vụ chưa cấp phép triển khai |

Tạo ticket hiện tại vẫn là Submitted; JSON status là số. Không đổi sang Draft, bảng enum 0–8 hoặc `/api/v1` chỉ vì gặp yêu cầu đó trong hồ sơ cũ. API/DB đang có được bảo vệ bằng baseline và kiểm thử; thay hợp đồng cần nhiệm vụ và phương án chuyển đổi rõ ràng.

## Các đề xuất cần quyết định trước khi triển khai

| Tài liệu | Mốc/phạm vi | Quyết định liên quan |
| --- | --- | --- |
| [Actor và use case](01-actor-use-cases.md) | Review ngày 2026-10-09, nguồn `6b124e73…`; mô tả thực tế chỉ đúng tại mốc nguồn | DEC-02 |
| [RBAC](02-rbac-permission-matrix.md) | Review cùng mốc; các security findings lịch sử không tự mô tả HEAD | DEC-02, DEC-08 |
| [Domain ticket](03-ticket-domain-contract.md) | Đề xuất domain/tương thích, nguồn ghi trong tài liệu | DEC-01 |
| [Vòng đời phiếu](04-ticket-lifecycle-policy.md) | Đề xuất ngày 2026-10-10; sáu thao tác đã có sau PR #28 được mô tả riêng trong hợp đồng hiện hành | DEC-01, DEC-03–05 |
| [Assignment/comment](05-assignment-and-comment-policy.md) | Đề xuất; chưa có tính năng tương ứng | DEC-02–05 |
| [Attachment](06-attachment-security.md) | Đề xuất; chưa duyệt phương thức tải và giới hạn | DEC-06–07 |

Các đề xuất này là đầu vào thảo luận. Không suy ra quyền triển khai từ nhãn READY FOR REVIEW, số Jira hoặc sự tồn tại của tệp.

## Hồ sơ hợp đồng lịch sử

Các tệp sau giữ bản ghi ngày 2026-09-29. Nhãn APPROVED và chỉ dẫn trong bản gốc không thay thế hợp đồng hiện hành hay phê duyệt nhiệm vụ mới. Chú thích đầu từng tệp chỉ rõ nguồn thay thế và điều còn mở; bản gốc được giữ để truy vết.

| Hồ sơ | Cách tra cứu hiện tại |
| --- | --- |
| [RBAC/API cũ](01-rbac-and-api-permission-matrix.md) | Hợp đồng hiện hành, quy tắc bảo mật, DEC-02 |
| [Fields cũ](02-ticket-domain-and-field-contract.md) | Hợp đồng hiện hành, DEC-01 |
| [Lifecycle/visibility cũ](03-ticket-lifecycle-and-visibility-baseline.md) | Hợp đồng hiện hành; không remap enum |
| [Transition/reopen cũ](04-ticket-state-transition-and-reopen-matrix.md) | DEC-03–05 |
| [Comment/privacy cũ](05-comment-and-internal-note-privacy-contract.md) | Quy tắc bảo mật, DEC-03 |
| [Assignment cũ](06-ticket-assignment-policy.md) | Quy tắc quyền trước revision, DEC-02 |
| [Attachment cũ](07-attachment-security-limits-and-policy.md) | DEC-06–07 |
| [Governance/nghiệm thu cũ](08-sprint-acceptance-and-governance-record.md) | [Hồ sơ Sprint 1](../sprint-1/README.md), DEC-08 |

Không dùng một thứ bậc tuyệt đối cho toàn bộ thư mục này. Xung đột về sản phẩm cần người quyết định; lỗi trong chỉ dẫn kỹ thuật đã được đính chính tại phần hiện hành. Nhãn lịch sử không chứng minh feature đã có hoặc CI/QA đã đạt trên commit mới.
