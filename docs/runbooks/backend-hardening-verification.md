# Kiểm chứng backend

Trạng thái: quy trình hiện hành, cập nhật 2026-10-19. Đợt củng cố bắt đầu từ `9dd0746450dca4348d6d0fa8a09cfc2a48860020`, trên nhánh `codex/backend-clean-architecture`, PR #26 đích dev tại thời điểm lập hồ sơ. Đây không phải xác nhận trạng thái PR hôm nay.

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

Có test trong mã không chứng minh test đã chạy. Các test offline cũng không chứng minh transaction/lock hoạt động trên PostgreSQL.

## Tương thích, bàn giao và rollback

[Hợp đồng hiện hành](../architecture/current-backend-contract.md) xác định hành vi được giữ: JSON/status số, tạo Submitted, đăng ký Requester, FullName chỉ phản hồi, receipt và lỗi/header. Không sửa migration đã phát hành hoặc xuất lại baseline HTTP để che khác biệt. Refactor không chủ đích đổi schema phải kiểm tra model drift; cách làm ở [runbook môi trường](environment-runbook.md#phát-hiện-lệch-mapping-khi-refactor).

Ghi SHA, thay đổi chưa commit, lệnh và số test/kết quả thực tế. Trước merge cần CI tại head mới và review phù hợp. Smoke infra cần ghim đúng SHA backend/web trong thay đổi infra riêng; smoke cũ không kiểm chứng backend mới.

Đợt củng cố không có migration mới nên rollback bằng image ứng dụng trước đó, không chạy Down trên dữ liệu thật. Với bản triển khai cũ hơn còn seed tự động, tuân [runbook rollout](backend-refactor-rollout.md). Các chính sách nghiệp vụ chưa chốt theo [bảng quyết định](../architecture/pending-business-decisions.md).
