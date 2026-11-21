# Mốc mã nguồn và phạm vi tài liệu

Chỉ mục mốc nguồn hiện hành cập nhật ngày 21/11/2026 (Asia/Bangkok). [Bản đồ tài liệu Backend](../README.md)

## Mốc áp dụng

Hai mốc được ghi riêng.

- **Mốc mã** `dev@3640c33eb83e049b9cc8488608c18a155b225292`, được rà ngày 21/11/2026. Từ `dev@07ce07a9dd50bcd143690c7ba1a19cc0048b9508` (13/11), thay đổi duy nhất dưới `src`, `tests` và `contracts` là chú thích của [`AIAnalysis.TicketRevision`](../../src/backend/Modules/AIAssistance/Domain/AIAnalysis.cs) về điều kiện hết hạn theo `InputHash`.
- **Mốc tài liệu** 21/11/2026, bổ sung phạm vi `AI-02`, phương án SupportTeam và đường đến kế hoạch lưu trữ sau khi ADR-017 được chấp nhận. Xem [chỉ mục kiến trúc](README.md) và [danh mục ADR](../adr/README.md).

Kết quả CI gắn với SHA của từng lượt chạy. Kết quả nghiệm thu gắn với cấu hình ứng viên được ghi trong `DEC-08`.

| Thành phần | Phạm vi |
| --- | --- |
| Mã backend | Bốn module (Identity, Ticketing, Knowledge, AIAssistance); 36 luồng nghiệp vụ |
| [OpenAPI HTTP Backend](../../contracts/backend/openapi-baseline.json) | 40 đường dẫn, 41 thao tác. SHA-256 trên byte Git là `c60fa717629e075adde1b13c5480b0f067fda755d46c3fb9c5c16edb2b10fa38` |
| [OpenAPI phân loại AI v1](../../contracts/ai/v1/openapi.yaml) | SHA-256 trên byte Git là `ad1d93230158b9ee66c6e3d0843edafcadde97b01bd2adef62265e4cf7f16428`. AI Service dùng bản ghim cùng hash |
| Kiểm thử hợp đồng | `PublishedApiMatchesBaseline` so sánh toàn bộ tài liệu OpenAPI |
| Tài liệu hiện hành | Tiếng Việt, phân biệt hành vi đang có, đề xuất, quyết định và bằng chứng lịch sử |

[Hợp đồng Backend](current-backend-contract.md) và [mô-đun/luồng xử lý](backend-modules-and-use-cases.md) ghi mốc rà riêng. Kiểm tra HEAD trước khi dùng liên kết mã tương đối. Snapshot Web còn ghim Backend `0f189b5`, SHA-256 `3884d37d…`. Soát chênh lệch sáu thao tác theo `ai-it-service-desk-web/docs/architecture/api-baseline.md` trước khi ghép phiên bản.

## Mốc đã đối chiếu ngày 13/11/2026

Từ `a182c9f` đến `07ce07a`, PR #59–#60 bổ sung nền dữ liệu nhóm hỗ trợ, #64 giới hạn phạm vi Agent theo nhóm hoặc phân công trực tiếp và #65 thêm hạn mức tệp theo phiếu cùng xử lý lock timeout. Các mốc #68, #70 và `07ce07a` đổi cấu trúc thư mục và tên project. Mốc cuối trong khoảng này thay hợp đồng hoặc hành vi người dùng thấy được là `dcd104e` (PR #65). [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md)

Mốc `dev@47634bc` (sau PR #28 và #29) và bản local `codex/backend-clean-architecture@334485d` được lưu để truy vết. [Báo cáo rà soát](../reviews/documentation-audit-20261020.md)

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
