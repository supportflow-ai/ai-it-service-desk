# Mốc mã nguồn và phạm vi tài liệu

Trạng thái: chỉ mục mốc nguồn hiện hành; cập nhật ngày 2026-10-20 (Asia/Bangkok).

## Mốc áp dụng

Nhánh `codex/docs-vi-rebaseline` được chuẩn bị trên `dev@47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #28 và #29. Mã nguồn, tài liệu, công cụ Python và bản chuẩn OpenAPI trong thay đổi này được đối chiếu trên cùng bản làm việc.

| Thành phần | Phạm vi |
| --- | --- |
| Mã backend kế thừa từ dev | 12 luồng nghiệp vụ; sáu thao tác vòng đời phiếu được thêm ở PR #28 |
| Tài liệu hiện hành | Tiếng Việt, phân biệt hành vi đang có, đề xuất, quyết định và bằng chứng lịch sử |
| Hợp đồng OpenAPI | 16 đường dẫn, gồm sáu thao tác mới và `UpdateTicketRequest`; mô tả Swagger theo PR #29 |
| Kiểm thử hợp đồng | So sánh toàn bộ tài liệu OpenAPI, giữ kiểm tra chi tiết từng đường dẫn/lược đồ đã công bố |
| Công cụ phát triển | Hai công cụ Python, kiểm thử và hướng dẫn được công bố cùng nhau |

[Hợp đồng backend](current-backend-contract.md) và [mô-đun/luồng xử lý](backend-modules-and-use-cases.md) mô tả mã tại mốc này. Liên kết mã tương đối mở checkout của người đọc; kiểm tra HEAD trước khi áp dụng. Kết quả kiểm thử của một commit cũ không xác nhận thay đổi mới.

Bản local `codex/backend-clean-architecture@334485d` được dùng trong lần rà soát ban đầu chỉ có sáu luồng; khác biệt này và cách khắc phục được lưu trong [báo cáo rà soát](../reviews/documentation-audit-20261020.md). Dùng nhánh đã đồng bộ để kiểm chứng phần vòng đời mới.

## PR #29 có thay đổi gì liên quan tài liệu

PR #29 bổ sung/Việt hóa chú thích mã và đổi một số chuỗi thông báo lỗi, lý do AI không khả dụng, mô tả Swagger và thông báo vận hành. Ba khoảng cách về quyền nhận xử lý, thứ tự kiểm tra ở giải quyết phiếu và ánh xạ lỗi ghi đồng thời vẫn được ghi trong hợp đồng hiện hành.

Giữ định danh JSON/mã HTTP không có nghĩa giữ nguyên nội dung từng thông báo lỗi. Bên sử dụng cần dựa vào hợp đồng đã công bố khi xử lý phản hồi.

## Đồng bộ hợp đồng API

`contracts/backend/openapi-baseline.json` được xuất từ ứng dụng trên mốc dev tương ứng và rà soát phần chênh lệch. `PublishedApiMatchesBaseline` so sánh cả đường dẫn, lược đồ, mô tả, cấu hình bảo mật và các phần OpenAPI còn lại. Việc thêm API hoặc schema mà quên cập nhật bản chuẩn sẽ làm kiểm thử thất bại.

Xuất bản chuẩn chỉ là thao tác cập nhật có chủ đích; lần kiểm thử xác nhận phải tắt `EXPORT_API_BASELINE`. OpenAPI không thay kiểm thử hành vi phân quyền, dữ liệu và ghi đồng thời. Xem [quy chuẩn tài liệu](../process/documentation-standard.md).

## Công cụ local và quy tắc Git

`scripts/run_backend.py`, `scripts/start_database_tunnel.py` và `scripts/tests/test_run_backend.py` thuộc cùng thay đổi với hướng dẫn. Hai quy tắc bỏ qua công cụ được gỡ khỏi `.gitignore` kế thừa từ dev. Cấu hình `.env`, khóa riêng, bộ nhớ đệm và kết quả build vẫn phải nằm ngoài tập công bố.

Checkout mới có đủ công cụ để làm theo [hướng dẫn môi trường](../runbooks/environment-runbook.md); mỗi máy tự chuẩn bị cấu hình và quyền kết nối cần thiết. Công cụ không tự cấp quyền thao tác vào cơ sở dữ liệu dùng chung.

## Kiểm chứng theo đúng mốc

- Kết quả khắc phục và kiểm tra thay đổi này nằm trong [báo cáo rà soát](../reviews/documentation-audit-20261020.md).
- [CI dev tại commit nền](https://github.com/supportflow-ai/ai-it-service-desk/actions/runs/39424511333) chỉ chứng minh mốc dev đó; trước hợp nhất cần CI tại HEAD của thay đổi mới.
- [Kiểm chứng khởi chạy cũ](../runbooks/local-startup-verification.md) là bằng chứng ngày 2026-10-19, không xác nhận trạng thái VPS hôm nay.
- Kho hạ tầng tại nhánh `codex/infra-handoff` ghim backend `9dd0746450dca4348d6d0fa8a09cfc2a48860020` trong `sources.lock.json` lúc rà soát. Kiểm tra hệ thống theo khóa đó không chứng nhận backend mới.
- Quyết định kiến trúc và kết quả sprint giữ ngày/commit gốc; bản dịch hoặc chú thích mới không tạo lại nghiệm thu.

## Đợt củng cố ngày 2026-10-23

Mã nền local: `codex/backend-clean-architecture@47da28103cc46500aac0847accfbaa2783f88db2`, cùng cây nội dung với commit `e2d1ad406337dbd1a9a63ac6b522b005dc502674`. Các thay đổi mô tả dưới đây chưa commit khi cập nhật; không dùng kết quả tại mã nền để nghiệm thu bản làm việc mới.

- Claim cho phép Agent/Admin theo quyết định trực tiếp của chủ dự án; resolve kiểm tra quyền chức năng trước persistence.
- Sáu lệnh vòng đời bổ sung 409 `{ error }` cho xung đột ghi `xmin`; không đổi route, DTO, status enum, schema dữ liệu hoặc cơ chế tạo phiếu/receipt.
- Contract gọi từ HTTP được tách khỏi ma trận cấp quyền xuyên mô-đun; ownership kiểu dữ liệu Identity được kiểm tra cả qua generic; không cố định số use case.
- Có thêm hồi quy use case, HTTP và PostgreSQL hai host; phải đọc số test/kết quả thực chạy tại đúng bản làm việc. Kiểm thử SQL dạng chuỗi và smoke toàn hệ thống vẫn cần đánh giá riêng theo phạm vi.
- Cách thêm chức năng theo [hướng dẫn phát triển](../process/adding-feature-or-module.md); tài liệu và quyết định được cập nhật cùng mã.
