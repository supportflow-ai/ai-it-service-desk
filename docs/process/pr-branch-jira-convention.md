# Quy ước nhánh, commit, PR và Jira

Trạng thái: hướng dẫn hiện hành cho backend, cập nhật 2026-10-19. Áp dụng cùng [AGENTS.md](../../AGENTS.md) và [DoR/DoD](definition-of-ready-done.md). Chỉ dẫn trực tiếp của người dùng trong nhiệm vụ được ưu tiên khi có ngoại lệ rõ ràng; không sửa lịch sử Git để áp quy ước mới.

## Nhánh

Mẫu thông thường: `<type>/TC-<id>-<mô-tả-ngắn-không-dấu>`. Loại nhánh: `feature`, `feat`, `bugfix`, `fix`, `chore`, `refactor`, `hotfix`, `docs`, `test`; công việc bằng Codex dùng tiền tố `codex/`. Nhánh phát hành dùng `release/<phiên-bản>` theo nhiệm vụ phát hành riêng.

`TC-<id>` phải là mã Jira có thật. Không tự bịa mã hoặc suy ra quyền tạo công việc Jira. Nếu người dùng yêu cầu tiếp tục một nhánh cụ thể, giữ nhánh đó kể cả tên không khớp mẫu. Nhánh hiện tại `codex/backend-clean-architecture` thuộc trường hợp này. Ngoại lệ tên nhánh không tự miễn mã Jira của commit/PR.

[Workflow](../../.github/workflows/ci.yml) kiểm tra mọi PR; push chỉ kích hoạt trên main/dev/staging và `codex/**`. Nhánh feature khác chưa mở PR không tự có kiểm tra push. Không tự mở rộng trigger, push hoặc tạo PR nếu chưa được giao.

## Commit và ngôn ngữ

Tài liệu mới, mô tả commit và PR dùng tiếng Việt có dấu. Giữ định danh/câu lệnh kỹ thuật, tránh xen tiếng Anh khi có cách nói tiếng Việt rõ ràng.

```text
<type>(<scope>): [TC-<id>] <mô tả tiếng Việt>
```

Ví dụ về cấu trúc, phải thay mã bằng công việc thực tế:

```text
docs(context): [TC-<id>] thống nhất nguồn tài liệu backend
fix(ticketing): [TC-<id>] chặn truy cập phiếu ngoài phạm vi
test(identity): [TC-<id>] kiểm tra khóa tài khoản khi đăng nhập đồng thời
```

Thiếu mã Jira bắt buộc và chưa có ngoại lệ được người dùng cho phép: hoàn tất phần chỉnh sửa/kiểm tra độc lập, hỏi trước khi commit. Không tạo commit rỗng để giả lập truy vết. Kiểm tra nhánh/HEAD/thay đổi trước khi đưa tệp vào commit; chỉ chọn đúng phạm vi. Không tự commit cho nhiệm vụ chỉ review/lập kế hoạch; không tự push, hợp nhất hoặc triển khai.

## PR và bằng chứng

Tiêu đề: `[TC-<id>] <type>(<scope>): <mô tả tiếng Việt>`. Theo [mẫu PR](../../.github/pull_request_template.md), ghi:

- Mã Jira thực tế và URL lấy từ công việc tương ứng; không chép tên miền giả hoặc URL ví dụ.
- Mã truy vết `TC-<id>` hoặc `PR-<số>`; nếu có ngoại lệ Jira đã được cho phép, ghi căn cứ ngoại lệ.
- Vấn đề, hành vi sau thay đổi, phạm vi API/Worker/Database/nghiệp vụ/kiểm thử/tài liệu.
- SHA và thay đổi chưa commit tại lúc kiểm tra; lệnh thực chạy, số test, kết quả, TRX hoặc CI, phần chưa chạy và lý do.
- Tương thích API/dữ liệu và cách rollback khi có tác động; smoke hệ thống dẫn tới repo infra với đúng SHA backend.

Không chỉ đánh dấu ô “đạt”. Kiểm tra cho tài liệu có thể là liên kết/cấu hình/validator; không bắt buộc dựng database để có nhật ký test không liên quan. CI của PR vẫn phải đạt các kiểm tra bắt buộc theo cấu hình hiện tại. Không tuyên bố branch protection, approval hoặc trạng thái Jira đã thay đổi khi chưa có bằng chứng.
