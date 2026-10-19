# Điều kiện bắt đầu, hoàn thành và đánh giá công việc

Trạng thái: hướng dẫn hiện hành cho backend, cập nhật 2026-10-19. Quy ước nhánh/Jira/ngôn ngữ theo [tài liệu Git](pr-branch-jira-convention.md); phạm vi nguồn theo [bản đồ tài liệu](../README.md).

## Điều kiện bắt đầu (DoR)

Công việc cần có mục tiêu, người phụ trách, phạm vi và tiêu chí kiểm chứng rõ. Xác định hợp đồng/phiên bản áp dụng, phụ thuộc, tác động bảo mật/dữ liệu và phương án kiểm thử. Lựa chọn nghiệp vụ còn mở phải có người quyết định; không tự chọn trong lúc sửa lỗi kỹ thuật.

Nhiệm vụ review/tài liệu có thể bắt đầu khi đã có nguồn và câu hỏi đủ rõ; không đợi toàn bộ chức năng tương lai được triển khai. Trợ lý không tự chuyển trạng thái Jira hoặc giao việc cho người khác.

## Điều kiện hoàn thành (DoD)

- Hoàn tất phần được giao; mô tả rõ giới hạn, hợp đồng và rủi ro còn lại.
- Các kiểm tra phù hợp với thay đổi đạt, có bằng chứng theo đúng SHA; phần chưa chạy phải nêu lý do và không được báo đạt thay.
- Không có bí mật trong mã/tài liệu/nhật ký; lỗi nghiêm trọng ngoài phạm vi được báo rõ.
- Tài liệu chịu ảnh hưởng được cập nhật bằng tiếng Việt; baseline không bị sửa để che regression.
- Khi bàn giao PR để merge: CI bắt buộc đạt trên head mới, có ít nhất một người khác đánh giá/chấp thuận, các yêu cầu sửa đã được xử lý. PR hạ tầng/Docker/CI/migration cần thêm xác nhận QA hoặc DevOps phù hợp.
- Hồ sơ Jira/PR có truy vết theo quy ước; cập nhật hệ thống bên ngoài chỉ khi được giao. Bàn giao chỉnh sửa local không đồng nghĩa PR đã được nghiệm thu hoặc đã merge.

## Kiểm tra theo loại thay đổi

Chạy hợp các dòng bị ảnh hưởng. Lệnh chi tiết ở [AGENTS.md](../../AGENTS.md), [môi trường](../runbooks/environment-runbook.md) và [kiểm chứng backend](../runbooks/backend-hardening-verification.md).

| Thay đổi | Kiểm tra |
| --- | --- |
| Chỉ tài liệu | Validator tài liệu, liên kết và cấu hình/lệnh tham chiếu; không bắt buộc chạy lại toàn bộ .NET/PostgreSQL/image tại local |
| Công cụ kiểm tra tài liệu | Các tình huống lỗi của validator và chạy trên tài liệu repo; nếu sửa workflow, kiểm tra cấu trúc và lệnh mới |
| Nghiệp vụ/quyền | Unit/use case và hồi quy HTTP liên quan, gồm trường hợp từ chối |
| Endpoint/DTO/response | OpenAPI và hành vi JSON/status/header/caller |
| Ranh giới tầng/module | ArchitectureTests |
| Persistence/migration/transaction/đồng thời | PostgreSQL thật; không thay bằng SQLite hoặc EF InMemory |
| Contract/mapper/fixture AI | Validator AI và unit/adapter tương ứng, không cần AI thật |
| Dockerfile/dependency/cấu hình chạy | Locked restore, build, test liên quan và image chịu ảnh hưởng |

Workflow vẫn chạy đầy đủ các bước đã cấu hình khi PR kích hoạt, kể cả PR chỉ tài liệu. Ma trận này tránh ép chạy local không liên quan; không cho phép bỏ qua CI bắt buộc. Thay đổi chức năng phải đối chiếu đủ tiêu chí nghiệm thu và workflow. Smoke toàn hệ thống thuộc infra, phải dùng đúng SHA; kết quả cũ không chứng nhận phiên bản mới.

## Đánh giá và nghiệm thu

Reviewer kiểm phạm vi, tương thích, quyền/dữ liệu, kiểm thử thực chạy và giới hạn. Không tự phê duyệt PR của mình. Không hạ cảnh báo, bỏ assertion hoặc sửa baseline để làm CI xanh. Trạng thái Done/APPROVED trong một bản ghi cũ không thay phê duyệt và bằng chứng hiện tại.

Quy định trong tài liệu không chứng minh GitHub đã bật branch protection. Chỉ thay quy tắc bảo vệ repo, hợp nhất PR hoặc triển khai khi có nhiệm vụ riêng.
