# Bản đồ tài liệu dự án

Trạng thái: hiện hành cho kho backend. Cập nhật: 2026-10-20. Phần mô tả chức năng đã đối chiếu `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #28 và #29; kết quả kiểm thử phải gắn với mốc chạy riêng.

[Quy chuẩn tài liệu](process/documentation-standard.md) quy định cách viết tiếng Việt, ghi mốc nguồn và cập nhật theo thay đổi. [Bảng thuật ngữ](process/technical-glossary.md) thống nhất cách gọi giữa các tài liệu.

[Báo cáo rà soát toàn bộ tài liệu](reviews/documentation-audit-20261020.md) ghi các nguồn xung đột, phần đã xử lý và các điểm cần đồng bộ trước khi chốt.

Đọc [bảng mốc mã nguồn](architecture/source-baseline.md) trước khi chạy: nhánh chuẩn hóa được chuẩn bị trên dev sau PR #29, gồm mã 12 luồng, bản chuẩn API và công cụ Python tương ứng. Kết quả kiểm thử cần gắn với đúng HEAD.

Đọc theo thứ tự: [AGENTS.md](../AGENTS.md) → [kiến trúc hiện hành](architecture/backend-modules-and-use-cases.md) → [hợp đồng đang triển khai](architecture/current-backend-contract.md) → tài liệu của nhiệm vụ. [Hướng dẫn môi trường](runbooks/environment-runbook.md) là nguồn lệnh chạy local và thao tác database.

## Chọn đúng loại nguồn

| Loại | Nguồn | Cách sử dụng |
| --- | --- | --- |
| Cách làm việc | AGENTS, [Git/Jira](process/pr-branch-jira-convention.md), [DoR/DoD](process/definition-of-ready-done.md) | Phạm vi thao tác, ngôn ngữ, kiểm chứng và bàn giao |
| Quyết định kiến trúc | [Danh mục ADR](adr/README.md), đặc biệt ADR-011–014 | Ranh giới và quyết định còn hiệu lực; đọc cả phần bổ sung/thay thế của ADR cũ |
| Hành vi đang triển khai | [Hợp đồng backend](architecture/current-backend-contract.md), mã nguồn, [baseline HTTP](../contracts/backend/openapi-baseline.json) và kiểm thử | Đối chiếu tương thích tại cùng SHA; mã hiện tại không tự chứng minh mọi yêu cầu sản phẩm đã đúng |
| Nghiệp vụ tương lai | [Bảng quyết định còn mở](architecture/pending-business-decisions.md), các đề xuất trong [chỉ mục kiến trúc](architecture/README.md) | Đầu vào cho quyết định tiếp theo; không tự mở rộng nhiệm vụ |
| Vận hành | [Môi trường](runbooks/environment-runbook.md), [kiểm chứng](runbooks/backend-hardening-verification.md), [triển khai](runbooks/backend-refactor-rollout.md), [CI](runbooks/github-actions.md) | Chạy đúng môi trường và phạm vi được giao |
| Database nhóm | [VPS phát triển dùng chung](runbooks/shared-development-database.md) | Địa chỉ, SSH tunnel, tài khoản ứng dụng và giới hạn vận hành |
| Bằng chứng khởi chạy tại mốc cũ | [Kết quả nạp cấu hình và kết nối giao diện/backend](runbooks/local-startup-verification.md) | Chỉ phản ánh lần chạy ngày 2026-10-19; không mô tả trạng thái môi trường hôm nay |
| Bằng chứng lịch sử | [Reviews](reviews/README.md), [Sprint 1](sprint-1/README.md), [artifacts](../artifacts/README.md) | Chỉ chứng minh phạm vi/ngày/SHA ghi trong từng hồ sơ |

Không có quy tắc “mọi tệp trong architecture luôn thắng OpenAPI”. Khi nguồn xung đột, xác định nhiệm vụ được giao, phạm vi, trạng thái và quan hệ thay thế trước. Nếu vẫn chưa có quyết định, ghi rõ hai nguồn và tác động, hỏi phần cần quyết định; tiếp tục phần độc lập. Không sửa mã hoặc baseline chỉ để khớp một nhãn APPROVED cũ.

## Quản lý ngữ cảnh

- Tài liệu đang áp dụng có nguồn sở hữu rõ; cập nhật cùng thay đổi làm nó mất hiệu lực. Mốc SHA mô tả nguồn được đối chiếu, không thay bằng chứng chạy kiểm thử.
- Đề xuất phải dẫn tới quyết định còn mở; chỉ chuyển thành yêu cầu triển khai sau khi được chấp thuận cho phạm vi cụ thể.
- Bản ghi lịch sử giữ nội dung gốc và ngày/SHA. Chú thích mới nằm riêng phía trên, không ký duyệt hoặc sửa số liệu thay tác giả.
- Bộ `ai-it-service-desk-claude-context-pack/` chỉ còn là hồ sơ dựng khung local, bị Git bỏ qua. Không copy prompt/CLAUDE.md cũ ra root. Những bản nguồn cần phục vụ truy vết đã được lưu trong hồ sơ Sprint 1; repo không phụ thuộc bộ local để build hoặc đọc hướng dẫn hiện hành.
- Không sao chép toàn bộ tài liệu thành một gói ngữ cảnh mới. Dùng chỉ mục này và liên kết để tránh các bản nội dung lệch nhau.

## Kiểm tra tài liệu

Chạy từ gốc repo, dùng Python theo workflow (Linux/macOS có thể cần `python3`):

```sh
python scripts/validate_project_docs.py
python -m unittest discover -s scripts/tests -p "test_project_docs.py"
```

Công cụ chỉ dùng thư viện chuẩn, không cần venv hoặc kết nối mạng. Kiểm tra tệp được quản lý trong Git và tệp mới không bị ignore, phân loại nguồn theo [danh mục](context-sources.json), liên kết Markdown tới tệp cục bộ, chú thích lịch sử/đề xuất và nguồn thay thế. Lệnh không kiểm tra URL mạng, anchor hoặc lệnh vận hành bằng cách thực thi chúng. Không coi kiểm tra tĩnh là nghiệm thu ứng dụng.
