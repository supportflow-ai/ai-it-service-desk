# AI IT Service Desk — Backend

Backend .NET 10 của SupportFlow AI, tổ chức theo Clean Architecture và các module Identity, Ticketing, AIAssistance. Repo này sở hữu API, nghiệp vụ, persistence, Worker, migration và kiểm thử backend.

| Thành phần | Repo |
|---|---|
| Backend | https://github.com/supportflow-ai/ai-it-service-desk |
| Frontend React | https://github.com/supportflow-ai/ai-it-service-desk-web |
| AI contract và service tương lai | https://github.com/supportflow-ai/ai-service |
| Compose, NGINX và smoke hệ thống qua GitHub Actions | https://github.com/supportflow-ai/supportflow-ai-infra |

Bắt đầu tại [bản đồ tài liệu](docs/README.md) và [hợp đồng backend hiện hành](docs/architecture/current-backend-contract.md). Các hợp đồng lịch sử và đề xuất chưa duyệt được phân loại riêng.

## Kiến trúc

```text
src/backend/
  ServiceDesk.Domain/          # Entity, value type, business policy
  ServiceDesk.Application/     # Use case, authorization, DTO, interfaces
  ServiceDesk.Infrastructure/  # EF/PostgreSQL, Identity, MinIO, NoOp AI
  ServiceDesk.Api/             # HTTP, JWT, claims adapter và composition root
  ServiceDesk.Worker/          # Composition root cho xử lý nền
  ServiceDesk.Database/        # Công cụ migration và khởi tạo có chủ đích
tests/                        # Domain, Application, Architecture, Integration
contracts/                    # Baseline HTTP và AI classification v1
```

Domain độc lập framework; Application phụ thuộc Domain. Infrastructure triển khai interface do Application sở hữu. API/Worker lắp ghép các module cần dùng. Endpoint không truy cập DbContext; Worker không sử dụng HttpContext hoặc danh tính người dùng ngầm định. Xem [ADR-011](docs/adr/ADR-011-backend-module-boundaries.md) và [ADR-014](docs/adr/ADR-014-feature-use-cases-and-published-contracts.md).

Application tổ chức theo module và feature: Identity có RegisterUser/LoginUser/GetCurrentUser; Ticketing có CreateTicket/GetMyTickets/GetTicketDetail. Endpoint gọi contract của từng use case; implementation là internal. Published Contracts và adapter ports được kiểm tra riêng bằng architecture tests. [Tài liệu module và luồng xử lý](docs/architecture/backend-modules-and-use-cases.md) mô tả ownership, transaction và phạm vi quyền hiện tại.

Requester chỉ đọc chi tiết ticket của mình; Agent/Admin hiện đọc chi tiết toàn hệ thống. Danh sách và tạo ticket luôn dùng actor hiện tại. Hệ thống chưa thực thi cách ly theo team hoặc assignment. Identity đọc trạng thái tài khoản dưới row lock PostgreSQL để giữ lockout nhất quán giữa các API replica.

## Build và test

Cần SDK đúng phiên bản trong [global.json](global.json). Dependency được ghim bằng phiên bản cụ thể và NuGet lockfile.

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

Integration tests dùng PostgreSQL 17 Testcontainers và cần Docker đang hoạt động. Application unit tests không cần EF Core/Infrastructure/database.

Các kiểm tra snapshot xác nhận đợt củng cố không cần migration mới. Xem [runbook kiểm chứng](docs/runbooks/backend-hardening-verification.md) để kiểm tra đúng commit và phân biệt test offline với PostgreSQL thật.

## CI bằng GitHub Actions

[Backend quality gates](.github/workflows/ci.yml) chạy khi mở/cập nhật PR, push vào main/dev/staging hoặc nhánh codex, và khi chạy thủ công trong tab Actions. Pipeline restore khóa phiên bản, build, unit/architecture/compatibility tests, integration PostgreSQL thật, kiểm tra AI contract và build ba image API/Worker/Database. Kết quả test TRX và AI contract được lưu thành workflow artifacts.

Frontend tự lint/test/build image trong repo web. Repo infra ghim SHA backend/web và chạy Compose, migration, smoke qua gateway. Repo AI kiểm tra contract/checksum/fixtures. Các workflow chỉ cần quyền đọc mã nguồn, không cần Jenkins hoặc secrets registry; không push image hay tự deploy production. Xem [ADR-013](docs/adr/ADR-013-github-actions.md) và [hướng dẫn chuyển CI](docs/runbooks/github-actions.md).

## Chạy phát triển

Thực hiện theo [hướng dẫn môi trường](docs/runbooks/environment-runbook.md): cấu hình an toàn, xác minh đúng database local, chạy công cụ Database và khởi động API/Worker. Đây là nguồn lệnh local duy nhất; không chạy migration chỉ để thử tài liệu.

API/Worker không tự migrate hoặc seed khi startup. Muốn tạo demo user phải chạy riêng `seed-demo` với môi trường Development/Test, `SeedDemo__Enabled=true` và `SeedDemo__Password` được cung cấp rõ ràng.

Production bắt buộc khóa JWT riêng ít nhất 32 byte UTF-8; khóa mẫu/thiếu bị từ chối. Giữ khóa mạnh, issuer và audience đang dùng để duy trì token hợp lệ. Self-registration chỉ cấp Requester. Identity khóa đăng nhập sau 5 lần sai trong 15 phút; response vẫn là 401 chung.

## Database và triển khai

Công cụ `ServiceDesk.Database` hỗ trợ `migrate`, `seed-roles`, `seed-demo`, `bootstrap-admin`, `check-production`. Bootstrap Admin chỉ tạo tài khoản mới, nhận credential qua biến môi trường trên tiến trình một lần. Mã lỗi an toàn gồm `demo_seed_forbidden`, `bootstrap_existing_account`, `demo_default_password_present`.

Dockerfile API, Worker và Database nằm cạnh từng project, build từ gốc repo:

```sh
docker build -f src/backend/ServiceDesk.Api/Dockerfile -t servicedesk-api:local .
docker build -f src/backend/ServiceDesk.Worker/Dockerfile -t servicedesk-worker:local .
docker build -f src/backend/ServiceDesk.Database/Dockerfile -t servicedesk-database:local .
```

Gateway và cách ghép hệ thống nằm trong repo infra. `/health/live` kiểm tra tiến trình, `/health/ready` kiểm tra PostgreSQL; `/health` giữ response tương thích. AI không hoạt động không làm readiness thất bại. Quy trình nâng cấp, bảo toàn volume/data và rollback nằm trong [runbook](docs/runbooks/backend-refactor-rollout.md).

## AI contract

Backend mặc định dùng NoOp, chưa kết nối model hoặc AI service. Contract chuẩn là `contracts/ai/v1/openapi.yaml`; repo AI ghim commit/checksum của bản phát hành. Backend sở hữu authorization, priority, confidence và mọi quyết định nghiệp vụ/Human-in-the-Loop. AI chỉ trả suggestions; mapper từ chối enum, score, correlation sai và trường lạ.

Chạy validator bằng Python venv theo [hướng dẫn hợp đồng AI](docs/runbooks/environment-runbook.md#python-venv-và-hợp-đồng-ai).

Xem [ADR-012](docs/adr/ADR-012-ai-contract-and-future-transport.md) cho thiết kế xác thực, timeout/retry/circuit breaker của adapter tương lai. Chưa triển khai SLA, vòng đời ticket đầy đủ, receipt cleanup hoặc live AI trong đợt refactor này.

## Tương thích và lịch sử

Giữ API hiện tại, status enum, scope/replay idempotency và dữ liệu đã lưu. Migration bổ sung sửa độ rộng mã ticket sau 9.999; không reset sequence hoặc đổi số cũ. Không rewrite lịch sử Git. Tài liệu sprint/audit cũ được giữ làm bằng chứng lịch sử; kiến trúc triển khai hiện tại được mô tả bởi ADR-011–014, hợp đồng hiện hành và runbook trên.
