# Mốc mã nguồn và phạm vi tài liệu

Trạng thái: chỉ mục mốc nguồn hiện hành; cập nhật ngày 2026-11-13 (Asia/Bangkok).

## Mốc áp dụng

Hai mốc được ghi riêng:

- **Mốc mã:** `dev@07ce07a9dd50bcd143690c7ba1a19cc0048b9508` (2026-11-13). Từ mốc `a182c9f` (2026-11-08), mã đổi qua PR #59–#60 (nền dữ liệu nhóm hỗ trợ), #64 (B1-PR3: phạm vi đọc và lệnh ghi của Agent theo nhóm hỗ trợ hoặc phân công trực tiếp, [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md)) và #65 (B3: hạn mức tệp đính kèm theo phiếu, xử lý lock timeout khi xác nhận). PR #61, #63, #66 đổi tài liệu và kiểm thử. Các commit #68, #70 và `07ce07a` chỉ đổi cấu trúc thư mục và tên project, không đổi API hay dữ liệu. Mốc mã cuối cùng đổi hợp đồng hay hành vi người dùng thấy được là `dcd104e` (PR #65).
- **Tài liệu:** cập nhật tới mốc `07ce07a` (2026-11-13).

Các mốc này mô tả mã, không phải bằng chứng nghiệm thu; kết quả CI gắn với SHA của từng PR.

| Thành phần | Phạm vi |
| --- | --- |
| Mã backend | Bốn module (Identity, Ticketing, Knowledge, AIAssistance); 36 luồng nghiệp vụ |
| Hợp đồng OpenAPI | 40 đường dẫn, 41 thao tác trong `contracts/backend/openapi-baseline.json` |
| Kiểm thử hợp đồng | `PublishedApiMatchesBaseline` so sánh toàn bộ tài liệu OpenAPI |
| Tài liệu hiện hành | Tiếng Việt, phân biệt hành vi đang có, đề xuất, quyết định và bằng chứng lịch sử |

[Hợp đồng backend](current-backend-contract.md) và [mô-đun/luồng xử lý](backend-modules-and-use-cases.md) mô tả mã tại mốc này. Liên kết mã tương đối mở checkout của người đọc; kiểm tra HEAD trước khi áp dụng. Kết quả kiểm thử của một commit cũ không xác nhận thay đổi mới. Mốc cũ `dev@47634bc` (sau PR #28 và #29) và bản local `codex/backend-clean-architecture@334485d` chỉ còn trong [báo cáo rà soát](../reviews/documentation-audit-20261020.md) để truy vết.

## Nhánh tái tổ chức theo module (2026-11-07)

Nhánh `refactor/backend-module-first` dựa trên `dev@0f189b5` chuyển mã sang `src/backend/Modules/<Module>` và `src/backend/BuildingBlocks` theo [ADR-015](../adr/ADR-015-module-first-clean-architecture.md). Việc di chuyển không đổi API, dữ liệu hay hành vi nghiệp vụ; các sửa bảo mật Identity đi trước ở commit riêng. Kết quả kiểm thử gắn với SHA của lần chạy CI ghi trong PR #50. Liên kết tương đối trong tài liệu hiện hành trỏ tới vị trí mới; tài liệu bằng chứng giữ đường dẫn của mốc cũ.

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
