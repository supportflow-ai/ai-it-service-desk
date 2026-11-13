# Quy ước nhánh, commit và PR

Trạng thái: hướng dẫn hiện hành cho backend, cập nhật 2026-11-13. Áp dụng cùng [AGENTS.md](../../AGENTS.md), [cẩm nang chung](../team-handbook.md) và [DoR/DoD](definition-of-ready-done.md). Chỉ dẫn trực tiếp của người giao việc được ưu tiên khi có ngoại lệ rõ ràng; không sửa lịch sử Git để áp quy ước mới.

**Mã công việc (Jira hoặc công cụ khác) không bắt buộc** trong tên nhánh, commit hay tiêu đề PR. Có mã hoặc liên kết thì ghi vào mô tả PR để truy vết; không tự bịa mã. Commit và nhánh cũ có `TC-<id>` giữ nguyên.

## Nhánh

Mẫu: `<loại>/<mô-tả-ngắn-không-dấu>`. Loại nhánh: `feature`, `feat`, `bugfix`, `fix`, `chore`, `refactor`, `hotfix`, `docs`, `test`; công việc bằng Codex dùng tiền tố `codex/`. Nhánh phát hành dùng `release/<phiên-bản>` theo nhiệm vụ phát hành riêng.

Tạo nhánh từ `dev` và mở PR vào `dev`; `dev` được đưa vào `main` bằng PR tổng hợp riêng. Nếu người giao việc yêu cầu tiếp tục một nhánh cụ thể, giữ nhánh đó kể cả tên không khớp mẫu. Tên nhánh hiện tại phải lấy từ Git, không lấy từ ví dụ hoặc hồ sơ cũ.

[Workflow](../../.github/workflows/ci.yml) kiểm tra mọi PR; push chỉ kích hoạt trên `main`, `dev`, `staging` và `codex/**`, và có thể chạy thủ công. Nhánh khác chưa mở PR không tự có kiểm tra push. Không tự mở rộng trigger, push hay tạo PR nếu chưa được giao.

## Commit và ngôn ngữ

Tài liệu mới, mô tả commit và PR dùng tiếng Việt có dấu. Giữ định danh và câu lệnh kỹ thuật; tránh xen tiếng Anh khi có cách nói tiếng Việt rõ ràng. Áp dụng [quy chuẩn tài liệu](documentation-standard.md) và [bảng thuật ngữ](technical-glossary.md).

```text
<loại>(<phạm vi>): <mô tả tiếng Việt>
```

Ví dụ:

```text
docs(context): thống nhất nguồn tài liệu backend
fix(ticketing): chặn truy cập phiếu ngoài phạm vi
test(identity): kiểm tra khóa tài khoản khi đăng nhập đồng thời
```

Kiểm tra nhánh, HEAD và thay đổi chưa commit trước khi đưa tệp vào commit; chỉ chọn đúng phạm vi, không dùng `git add -A` khi chưa xác minh toàn bộ thay đổi thuộc nhiệm vụ. Không tự commit cho nhiệm vụ chỉ đánh giá hoặc lập kế hoạch; không tự push, hợp nhất hay triển khai.

## PR và bằng chứng

Tiêu đề cùng dạng với commit: `<loại>(<phạm vi>): <mô tả tiếng Việt>`. Theo [mẫu PR](../../.github/pull_request_template.md), ghi:

- Mã công việc hoặc liên kết, nếu có.
- Vấn đề, hành vi sau thay đổi, phạm vi API, Worker, Migrator, nghiệp vụ, kiểm thử và tài liệu.
- SHA và thay đổi chưa commit tại lúc kiểm tra; lệnh thực chạy, số test, kết quả, TRX hoặc CI, phần chưa chạy và lý do.
- Tương thích API và dữ liệu, cách rollback khi có tác động; ảnh hưởng tới web, ai-service hoặc infra theo [cẩm nang chung](../team-handbook.md#4-thay-đổi-ảnh-hưởng-kho-khác). Smoke toàn hệ thống dẫn tới kho infra với đúng SHA backend.

Không chỉ đánh dấu ô “đạt”. Kiểm tra cho tài liệu có thể là liên kết, cấu hình hoặc validator; không bắt buộc dựng database để có nhật ký test không liên quan. CI của PR vẫn phải đạt các kiểm tra bắt buộc theo cấu hình hiện tại.

Quy tắc duyệt (ít nhất một người khác, không tự phê duyệt) là thỏa thuận nhóm: GitHub hiện chưa bật bảo vệ nhánh hay CODEOWNERS cho kho này. Không tuyên bố bảo vệ nhánh, phê duyệt hay trạng thái công việc đã thay đổi khi chưa có bằng chứng.
