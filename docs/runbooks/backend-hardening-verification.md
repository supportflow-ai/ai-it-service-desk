# Kiểm chứng backend

Trạng thái: quy trình hiện hành, cập nhật 2026-10-20. Mô tả chức năng đã đối chiếu `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #28. Đợt củng cố ban đầu bắt đầu từ `9dd0746450dca4348d6d0fa8a09cfc2a48860020`, PR #26 đích dev; đây là bối cảnh lịch sử, không phải kết quả chạy lại hôm nay.

## Chọn kiểm tra theo phạm vi

Thay đổi chỉ tài liệu theo [DoR/DoD](../process/definition-of-ready-done.md); kiểm tra [bản đồ nguồn](../README.md) bằng validator tài liệu. Với thay đổi chức năng, từ gốc repo chạy:

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

Chỉ dùng `--no-build` sau build thành công cùng mã/cấu hình. Kiểm tra số test thực chạy lớn hơn 0 khi lọc; exit code thành công với không test không là bằng chứng đạt. PostgreSQL dùng Testcontainers với Docker local/runner được giao, không thay bằng SQLite/EF InMemory. Nếu không chạy được tại local, ghi rõ và chờ CI đúng commit; không dùng kết quả cũ làm nghiệm thu.

Validator hợp đồng AI dùng interpreter trong venv theo [hướng dẫn môi trường](environment-runbook.md#python-venv-và-hợp-đồng-ai), không cài package vào Python global trên máy phát triển. Kiểm tra tệp ngữ cảnh dùng thư viện chuẩn:

```sh
python scripts/validate_project_docs.py
python -m unittest discover -s scripts/tests -p "test_project_docs.py"
```

Linux/macOS có thể dùng `python3` đúng phiên bản theo workflow. CI còn kiểm tra AI contract/fixtures và build ba image API/Worker/Database; TRX và AI contract là artifacts. Không phát hành image hoặc tự triển khai production.

## Bằng chứng cần đối chiếu khi thay đổi chức năng

- `IdentityConcurrencyTests`: đăng nhập sai qua hai host, rollback khi lỗi ghi/cancellation, tài khoản độc lập và đúng/sai xen kẽ.
- `IdentityPersistenceTests`: lockout 15 phút, hết hạn/reset, tạo tài khoản và role nguyên tử, scope đã theo dõi user.
- `TicketAccessScopeTests`: Requester đọc chéo trả 404; Agent/Admin giữ quyền hiện tại; danh sách theo actor.
- `PersistenceContractTests`: model khớp snapshot và scope null bị từ chối.
- ArchitectureTests: phân loại tầng/module, Published Contracts và các mẫu helper/generic/API cố tình vượt ranh giới.
- Domain/adapter: parent/enum/value/terminal state, hash, bất biến kết quả AI, correlation, trường lạ và cancellation NoOp/Fake.
- Baseline API, migration bảo toàn dữ liệu và idempotency hai host: replay gốc, khác payload, expiry, timeout, rollback.

Với mã đã chứa PR #28, kiểm tra thêm sáu thao tác cập nhật, nhận xử lý, giải quyết, mở lại, đóng và hủy phiếu. Bao phủ trạng thái được phép/bị cấm, chủ sở hữu/người ngoài phạm vi, Agent, Admin và người nhiều vai trò. Đối chiếu 401/403/404 theo chính sách được chọn, nhất là khác biệt quyền nhận xử lý và thứ tự kiểm tra ở thao tác giải quyết.

Ghi đồng thời cần tình huống hai yêu cầu cạnh tranh trên PostgreSQL thật và phản hồi xung đột được kiểm soát. Tên `GetForUpdateAsync` không chứng minh có khóa dòng. Các kiểm tra trên là phần cần hoàn thiện, chưa được đợt sửa tài liệu này báo đạt; xem [hợp đồng hiện hành](../architecture/current-backend-contract.md#các-điểm-chưa-đạt-cần-xử-lý).

Bản chuẩn OpenAPI hiện có 16 đường dẫn, gồm sáu thao tác mới và `UpdateTicketRequest`. `PublishedApiMatchesBaseline` so sánh toàn bộ tài liệu; chạy với `EXPORT_API_BASELINE` tắt để xác nhận. Chỉ bật biến xuất khi chủ đích cập nhật hợp đồng, xem chênh lệch từng mục rồi chạy lại kiểm thử theo [quy chuẩn tài liệu](../process/documentation-standard.md).

Có test trong mã không chứng minh test đã chạy. Các test offline cũng không chứng minh transaction/lock hoạt động trên PostgreSQL.

## Tương thích, bàn giao và rollback

[Hợp đồng hiện hành](../architecture/current-backend-contract.md) xác định hành vi được giữ: JSON/status số, tạo Submitted, đăng ký Requester, FullName chỉ phản hồi, receipt và lỗi/header. Không sửa migration đã phát hành hoặc xuất lại baseline HTTP để che khác biệt. Refactor không chủ đích đổi schema phải kiểm tra model drift; cách làm ở [runbook môi trường](environment-runbook.md#phát-hiện-lệch-mapping-khi-refactor).

Ghi SHA, thay đổi chưa commit, lệnh và số test/kết quả thực tế. Trước merge cần CI tại head mới và review phù hợp. Smoke infra cần ghim đúng SHA backend/web trong thay đổi infra riêng; smoke cũ không kiểm chứng backend mới.

Đợt refactor 2026-11 (PR #50–#54) thêm ba migration: `20261107090000_AddOtpFailedAttempts` (cột `otp_codes.failed_attempts`, mặc định 0), `20261107160000_AddRefreshTokenFamily` (cột `refresh_tokens.family_id`, backfill `family_id = id`, `NOT NULL`, index) và `20261108010000_DefaultRefreshTokenFamilyForLegacyWriters` (trigger điền `family_id = id` khi câu INSERT không gửi cột này). Thứ tự triển khai:

1. Review trên SHA ứng viên, rồi chạy trên DB staging riêng được khôi phục từ bản sao và đã xác nhận restore được.
2. Ghi lại `__EFMigrationsHistory` trước; `ServiceDesk.Database migrate` áp **mọi** migration đang chờ, không chỉ ba migration này. Đo thời gian chờ khóa và thời gian backfill `family_id` trên dữ liệu thật.
3. Nhờ trigger, bản API cũ vẫn ghi được refresh token sau migration nên có thể triển khai cuốn chiếu; vẫn nên chạy migration trước rồi mới khởi động API mới (API mới đọc hai cột mới).
4. Ghi lại `__EFMigrationsHistory` sau; smoke đăng nhập, làm mới phiên, đăng xuất, quên mật khẩu.

Rollback ưu tiên dùng image ứng dụng trước đó và giữ schema mới (cột mới không cản bản cũ). Chỉ chạy `Down` khi bắt buộc và đã thử trên staging: `Down` xóa `failed_attempts` (mất bộ đếm nhập sai OTP) và `family_id` (mất liên kết chuỗi phiên). [Kiểm thử nâng cấp](../../tests/ServiceDesk.IntegrationTests/RefreshTokenFamilyMigrationTests.cs) kiểm backfill, bản ghi của API cũ sau migration và quay lui rồi nâng cấp lại. Với bản triển khai cũ hơn còn seed tự động, tuân [runbook rollout](backend-refactor-rollout.md). Các chính sách nghiệp vụ chưa chốt theo [bảng quyết định](../architecture/pending-business-decisions.md).

### Nhóm hỗ trợ (ADR-016, B1)

Hai migration cộng thêm: `20261108120000_AddSupportTeams` (bảng `support_teams`, `support_team_members`, cột nullable `tickets.support_team_id`, hàng đợi `MANUAL_TRIAGE`) và `20261108130000_BackfillLegacyTicketsToManualTriage` (đặt `support_team_id` cho phiếu chưa kết thúc còn null; Closed/Cancelled giữ null). Lưu ý khi triển khai:

- Backfill chỉ ghi `support_team_id` nhưng UPDATE làm `xmin` (revision) của các dòng bị ảnh hưởng đổi một lần: client đang giữ revision cũ nhận 409 rồi tải lại. Đo thời gian và số dòng trên staging.
- Sau khi mọi bản API cũ đã dừng ghi và trước khi bật phạm vi đọc của B1-PR3, chạy lại đúng câu `UPDATE` trong [migration backfill](../../src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20261108130000_BackfillLegacyTicketsToManualTriage.cs). Kiểm tra `SELECT count(*) FROM public.tickets WHERE support_team_id IS NULL AND status NOT IN (5, 6);` trả về 0. Lệnh `migrate` bỏ qua migration đã ghi trong lịch sử. Kiểm tra nhóm hoạt động và thành viên đã được cấp đúng trước khi thử hàng đợi Agent trên staging.
- Rollback: `Down` của `BackfillLegacyTicketsToManualTriage` không làm gì (không phân biệt được dòng nào vốn là null); `Down` của `AddSupportTeams` xóa cột `support_team_id` và hai bảng, mất dữ liệu nhóm. Ưu tiên giữ schema và quay lại image trước.
