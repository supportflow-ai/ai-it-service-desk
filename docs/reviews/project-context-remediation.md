# Kết quả xử lý tài liệu ngữ cảnh

Ngày: 2026-10-19. Nhánh `codex/backend-clean-architecture`, SHA nền `8244cc1649eb2f8db28c8dcaf30b0d540eab1c58` cộng các thay đổi chưa commit. Đối chiếu [báo cáo trước chỉnh sửa](project-context-audit.md).

## Phạm vi hoàn tất

Đã xử lý chín phát hiện trong phạm vi tài liệu và công cụ kiểm tra tài liệu. Không thay mã backend, hợp đồng OpenAPI/AI, migration, chính sách vai trò hoặc repo khác. Quyết định nghiệp vụ tương lai được ghi rõ là chưa chọn, không trình bày thành tính năng đã nghiệm thu.

| Phát hiện | Cách xử lý | Nguồn sau chỉnh sửa |
| --- | --- | --- |
| CTX-01 | Thay chỉ mục tự mâu thuẫn bằng danh mục hiện hành/đề xuất/lịch sử; phân biệt nghiệm thu theo mốc nguồn | [Bản đồ tài liệu](../README.md), [chỉ mục kiến trúc](../architecture/README.md), [Sprint 1](../sprint-1/README.md) |
| CTX-02 | Ghi đủ mười mã trạng thái, JSON số, Submitted, route/category/title/number hiện hành; cảnh báo tại tệp cũ | [Hợp đồng backend](../architecture/current-backend-contract.md) |
| CTX-03 | Đính chính thứ tự quyền trước revision tại sơ đồ cũ và tạo hướng dẫn kỹ thuật chung | [Quy tắc bảo mật](../architecture/security-design-rules.md), [chú thích assignment](../architecture/06-ticket-assignment-policy.md) |
| CTX-04 | Giao policy cho Application/Ticketing; phân biệt quyền Agent hiện tại với team/assignment/Lead tương lai | [Quy tắc bảo mật](../architecture/security-design-rules.md), [chú thích RBAC](../architecture/01-rbac-and-api-permission-matrix.md) |
| CTX-05 | Ghi từng lựa chọn chưa chốt, nguồn khác nhau và điều kiện triển khai; không tự lựa chọn chính sách | [DEC-01–08](../architecture/pending-business-decisions.md) |
| CTX-06 | Đánh dấu cả 15 Markdown local và manifest là hồ sơ scaffold; giữ nội dung gốc, không copy CLAUDE.md ra root | Bộ local bị ignore; quy tắc dùng nguồn ở [bản đồ tài liệu](../README.md) |
| CTX-07 | README dẫn về runbook môi trường; validator AI dùng venv; giữ chốt chặn database và EF local | [README](../../README.md), [môi trường](../runbooks/environment-runbook.md), [kiểm chứng](../runbooks/backend-hardening-verification.md) |
| CTX-08 | Thống nhất tiếng Việt, ngoại lệ nhánh được chỉ định, mã Jira thật và kiểm tra theo phạm vi; mô tả đúng trigger CI | [Git/Jira](../process/pr-branch-jira-convention.md), [DoR/DoD](../process/definition-of-ready-done.md), [AGENTS](../../AGENTS.md) |
| CTX-09 | Bổ sung quan hệ ADR-001/002/008 với ADR-011/014 và nguồn infra; giữ Jenkins ở phần lịch sử | [Danh mục ADR](../adr/README.md) |

Thêm [danh mục nguồn](../context-sources.json), [validator](../../scripts/validate_project_docs.py) và sáu tình huống hồi quy. GitHub Actions chạy hai lệnh này trước bước .NET; các bước .NET/PostgreSQL/AI/image, trigger và quyền workflow giữ nguyên.

## Kiểm chứng

- `python scripts/validate_project_docs.py`: đạt, 91 tài liệu được quản lý và 360 liên kết cục bộ; nguồn/chú thích hợp lệ.
- `python -m unittest discover -s scripts/tests -p "test_project_docs.py"`: 6/6 đạt, không bỏ qua; dùng thư mục tạm local như ghi bên dưới.
- Phân tích YAML và so sánh cấu trúc với HEAD: chỉ thêm đúng hai bước tài liệu. Cả hai tệp Python biên dịch được; `git diff --check` đạt.
- So sánh phần sau `original-record` của 14 hợp đồng/đề xuất với Git HEAD: nội dung gốc giống nhau sau chuẩn hóa CRLF/LF. Chỉ thêm chú thích, không viết lại nhãn phê duyệt hoặc kết quả cũ.
- Cả 15 Markdown trong context pack local có chú thích hết hiệu lực. Những thay đổi này bị Git ignore theo thiết kế, không tự đi cùng commit; bản clone mới không có pack nên không phụ thuộc chúng.
- Không có thay đổi trong `src/`, `tests/` .NET hoặc `contracts/`. Không chạy lệnh database, seed, bootstrap hoặc dịch vụ ứng dụng.
- Kiểm tra cấu trúc workflow và so sánh với HEAD để xác nhận chỉ thêm hai bước tài liệu; không gọi GitHub để chạy CI.

Lần chạy test đầu bị Windows sandbox chặn ghi/xóa trong thư mục tạm hệ thống. Chạy lại với TEMP/TMP của tiến trình trỏ vào `.cache/doc-test-temp` đã qua kiểm thử; không sửa test để bỏ qua lỗi. Thư mục này đã được Git ignore, không thay biến môi trường toàn máy.

Không chạy lại build/test .NET, PostgreSQL hoặc build image vì không thay mã, dependency, Dockerfile hoặc cấu hình chạy ứng dụng. CI từ xa chưa chạy cho các thay đổi chưa commit. Kiểm tra tài liệu không xác minh anchor, URL ngoài repo, trạng thái Jira/PR/approval hay tính năng tương lai.

## Bàn giao

Điểm bắt đầu mới là [docs/README.md](../README.md). Các bản ghi cũ giữ nguyên và được tách khỏi chỉ dẫn hiện hành. Chín phát hiện tài liệu đã có xử lý; DEC-01–08 vẫn là công việc quyết định sản phẩm/nghiệm thu về sau, không phải lỗi tài liệu bị bỏ qua.

AGENTS.md, runbook môi trường từ lượt trước và các tệp mới cần đi cùng thay đổi khi commit để bản clone có đủ hướng dẫn. Chưa commit/push trong lần bàn giao này; không cập nhật PR, merge, phát hành image hoặc deploy. Các số dòng trong báo cáo trước chỉnh sửa thuộc mốc trước khi thêm chú thích.
