# AI IT Service Desk — Backend

Backend .NET 10 của SupportFlow AI, tổ chức theo Clean Architecture và các module Identity, Ticketing, AIAssistance. Repo này sở hữu API, nghiệp vụ, persistence, Worker, migration và kiểm thử backend.

| Thành phần | Repo |
|---|---|
| Backend | https://github.com/supportflow-ai/ai-it-service-desk |
| Frontend React | https://github.com/supportflow-ai/ai-it-service-desk-web |
| AI contract và service tương lai | https://github.com/supportflow-ai/ai-service |
| Compose, NGINX, Jenkins và smoke hệ thống | https://github.com/supportflow-ai/supportflow-ai-infra |

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

Domain độc lập framework; Application phụ thuộc Domain. Infrastructure triển khai interface do Application sở hữu. API/Worker lắp ghép các module cần dùng. Endpoint không truy cập DbContext; Worker không sử dụng HttpContext hoặc danh tính người dùng ngầm định. Xem [ADR-011](docs/adr/ADR-011-backend-module-boundaries.md).

## Build và test

Cần SDK đúng phiên bản trong `global.json` (10.0.300). Dependency được ghim bằng phiên bản cụ thể và NuGet lockfile.

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

Integration tests dùng PostgreSQL 17 Testcontainers và cần Docker đang hoạt động. Application unit tests không cần EF Core/Infrastructure/database.

## Chạy phát triển

Khởi động PostgreSQL/MinIO bằng repo infra hoặc cung cấp dịch vụ tương đương. Thiết lập trong terminal PowerShell, sử dụng credential local của bạn:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Database__ConnectionString = 'Host=localhost;Port=5432;Database=servicedesk;Username=servicedesk;Password=YOUR_LOCAL_PASSWORD'
$env:ASPNETCORE_URLS = 'http://localhost:5000'
dotnet run --project src/backend/ServiceDesk.Database -- migrate
dotnet run --project src/backend/ServiceDesk.Database -- seed-roles
dotnet run --project src/backend/ServiceDesk.Api --no-launch-profile
```

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

```sh
python -m pip install -r contracts/requirements.txt
python scripts/validate_ai_contract.py
```

Xem [ADR-012](docs/adr/ADR-012-ai-contract-and-future-transport.md) cho thiết kế xác thực, timeout/retry/circuit breaker của adapter tương lai. Chưa triển khai SLA, vòng đời ticket đầy đủ, receipt cleanup hoặc live AI trong đợt refactor này.

## Tương thích và lịch sử

Giữ API hiện tại, status enum, scope/replay idempotency và dữ liệu đã lưu. Migration bổ sung sửa độ rộng mã ticket sau 9.999; không reset sequence hoặc đổi số cũ. Không rewrite lịch sử Git. Tài liệu sprint/audit cũ được giữ làm bằng chứng lịch sử; kiến trúc triển khai hiện tại được mô tả bởi ADR-011/012 và runbook trên.
