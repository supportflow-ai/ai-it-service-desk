# AI IT Service Desk — Backend

Backend .NET của SupportFlow AI, tổ chức theo Clean Architecture và các mô-đun Identity, Ticketing, AIAssistance. Kho này sở hữu API, nghiệp vụ, truy cập dữ liệu, tiến trình nền, migration và kiểm thử backend.

Mốc tài liệu: đối chiếu mã `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`, sau PR #28 và #29; cập nhật 2026-10-20. Mốc này mô tả mã nguồn, không thay bằng chứng nghiệm thu. Xem [quy chuẩn tài liệu](docs/process/documentation-standard.md) và [bảng thuật ngữ](docs/process/technical-glossary.md).

| Thành phần | Repo |
|---|---|
| Backend | https://github.com/supportflow-ai/ai-it-service-desk |
| Giao diện React | https://github.com/supportflow-ai/ai-it-service-desk-web |
| Hợp đồng và dịch vụ AI tương lai | https://github.com/supportflow-ai/ai-service |
| Compose, NGINX và kiểm tra toàn hệ thống qua GitHub Actions | https://github.com/supportflow-ai/supportflow-ai-infra |

Bắt đầu tại [bản đồ tài liệu](docs/README.md) và [hợp đồng backend hiện hành](docs/architecture/current-backend-contract.md). Các hợp đồng lịch sử và đề xuất chưa duyệt được phân loại riêng.

[Bảng mốc mã nguồn](docs/architecture/source-baseline.md) ghi phạm vi của nhánh đã đồng bộ trên dev sau PR #29. Tài liệu, hợp đồng OpenAPI và công cụ Python được công bố cùng mã tương ứng; đối chiếu HEAD và kết quả kiểm thử trước khi tích hợp.

## Khởi chạy backend trên máy phát triển

Chạy các lệnh từ thư mục gốc repo. Cần .NET SDK theo [global.json](global.json), Python theo [workflow](.github/workflows/ci.yml) và một PostgreSQL đã được khởi tạo. Dùng `python3` thay `python` trên Linux/macOS nếu cần.

1. Nếu chưa có `.env`, sao chép [.env.example](.env.example) thành `.env`. Không ghi đè file đã được người quản trị cấp và không đưa bí mật lên GitHub.
2. Điền `Database__ConnectionString` và `Jwt__SecretKey`. Với database dùng chung, nhận thông tin kết nối và khóa SSH riêng từ người phụ trách; xem [hướng dẫn kết nối VPS](docs/runbooks/shared-development-database.md). Không dùng tài khoản ứng dụng để chạy migration.
3. Nếu tạo môi trường riêng mới, tạo khóa ký JWT **một lần**, lưu vào `Jwt__SecretKey` và giữ lại cho những lần chạy sau. Có thể tạo chuỗi ngẫu nhiên tại máy bằng `python -c "import secrets; print(secrets.token_hex(32))"`. Không thay khóa mạnh đang dùng hoặc gửi khóa vào chat.
4. Giữ `DOTNET_ENVIRONMENT=Development`, `ASPNETCORE_ENVIRONMENT=Development` và `ASPNETCORE_URLS=http://localhost:5000` cho cách chạy local bên dưới. Các giá trị này là cấu hình máy phát triển, không phải cấu hình production.

Restore và build:

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
```

Nếu dùng database VPS, mở terminal riêng để giữ đường hầm SSH; bỏ qua bước này nếu dùng PostgreSQL trực tiếp tại máy:

```sh
python scripts/start_database_tunnel.py
```

Mở terminal khác, từ gốc repo khởi chạy API:

```sh
python scripts/run_backend.py --no-build api
```

API chạy tại `http://localhost:5000`; mở `http://localhost:5000/swagger` để thử API trong Development. Kiểm tra `http://localhost:5000/health/live` cho tiến trình và `http://localhost:5000/health/ready` cho kết nối PostgreSQL. Readiness không xác nhận schema/role đã được khởi tạo hoặc MinIO sẵn sàng. Dừng API/tunnel bằng Ctrl+C tại terminal tương ứng.

**.NET không tự đọc `.env`.** Công cụ Python nạp file vào tiến trình con, rồi gọi `dotnet run` với `Release` và không dùng launch profile. Biến môi trường đã có ưu tiên hơn file; vì vậy biến cũ trong terminal có thể làm ứng dụng kết nối nhầm đích. Công cụ từ chối hai tên môi trường không khớp nhau. Xem [quy tắc nạp cấu hình](docs/runbooks/environment-runbook.md#nạp-cấu-hình-và-khởi-chạy).

Database mới cần được người phụ trách chạy `migrate` và `seed-roles` bằng công cụ Database sau khi xác minh đích. API/Worker không tự thực hiện các bước này. Database dùng chung đã khởi tạo thì thành viên chỉ cần kết nối, không chạy migration lại theo nhánh cá nhân.

Các luồng đăng ký, đăng nhập và ticket hiện chưa dùng lưu tệp, nên chưa cần MinIO để kiểm chứng chúng. Chỉ điền cấu hình MinIO khi có dịch vụ thật; tên `minio:9000` chỉ dùng được trong mạng Docker có dịch vụ tên đó. AI mặc định NoOp, không cần khóa hay dịch vụ AI. Worker chưa có tác vụ nền mới; nếu cần chạy host Worker, dùng `python scripts/run_backend.py --no-build worker` ở terminal riêng.

## JWT là gì và lấy ở đâu?

JWT là chuỗi thông tin xác thực có chữ ký do backend cấp sau khi đăng nhập hoặc đăng ký thành công. Nó chứa danh tính, vai trò và thời hạn; nội dung không được mã hóa để giữ bí mật. Frontend gửi token trong header `Authorization: Bearer <token>` ở các yêu cầu cần đăng nhập.

- `Jwt__SecretKey` là **khóa ký của backend**, không phải token đăng nhập. Không gửi khóa này cho frontend hoặc đặt vào biến `VITE_...`.
- `token` trong response đăng ký/đăng nhập là **JWT của người dùng**. Không cần tạo JWT thủ công hoặc lấy từ một trang web khác.

Để lấy token trong Swagger:

1. Nếu chưa có tài khoản, gọi `POST /api/auth/register` với email, mật khẩu và `fullName` tùy chọn. Mật khẩu cần ít nhất 8 ký tự, có chữ hoa, chữ thường và chữ số. Server chỉ cấp vai trò Requester, bất kể role client gửi lên.
2. Hoặc gọi `POST /api/auth/login` bằng email/mật khẩu của tài khoản đã có.
3. Sao chép trường `token` từ response, nhấn **Authorize** trong Swagger và dán token. Không dán `Jwt__SecretKey`.
4. Gọi `GET /api/auth/me` để kiểm tra phiên. Token hết hạn thì đăng nhập lại; API hiện chưa có luồng refresh token.

Frontend tự nhận và gửi token khi người dùng đăng nhập. Không chia sẻ JWT cá nhân cho các thành viên khác. Đăng nhập sai 5 lần sẽ bị khóa 15 phút; API vẫn trả 401 chung.

## Chạy frontend cùng backend

Frontend ở repo riêng `ai-it-service-desk-web`. Trong checkout frontend, dùng Node theo `.nvmrc` rồi chạy:

```sh
npm ci
npm run dev
```

Mở `http://localhost:5173`. Máy chủ phát triển Vite chuyển `/api` sang `http://localhost:5000`, nên giữ API chạy ở terminal backend. Với cấu hình mặc định, không cần đưa JWT hoặc mật khẩu database vào frontend. Nếu đổi địa chỉ API hoặc gọi khác origin, xem cấu hình `VITE_API_BASE_URL` và CORS của hai repo.

## Repo infra có tác dụng gì?

Repo `supportflow-ai-infra` ghép các dịch vụ thành môi trường chạy chung: Compose khai báo container và mạng/volume, NGINX định tuyến frontend và `/api`, cấu hình môi trường xác định các dịch vụ kết nối với nhau, smoke test kiểm tra hệ thống qua gateway. Đây là nơi quản lý cách triển khai và khởi động các thành phần cùng nhau; nghiệp vụ vẫn thuộc backend.

Có thể phát triển backend trực tiếp trên máy với PostgreSQL được cấp mà không chạy cả repo infra. Muốn kiểm chứng đầy đủ hệ thống qua gateway hoặc triển khai các image độc lập thì cần cấu hình infra. Việc có repo infra không có nghĩa các dịch vụ đã được triển khai; môi trường VPS được ghi riêng trong runbook. Phạm vi GitHub Actions hiện tại là test, build image và smoke, chưa phát hành image hoặc tự deploy production.

## Kiến trúc

```text
src/backend/
  ServiceDesk.Domain/          # Thực thể, kiểu giá trị, quy tắc nghiệp vụ
  ServiceDesk.Application/     # Luồng xử lý, quyền, DTO, cổng giao tiếp
  ServiceDesk.Infrastructure/  # EF/PostgreSQL, Identity, MinIO, AI không gọi dịch vụ thật
  ServiceDesk.Api/             # HTTP, JWT, chuyển đổi danh tính và lắp ghép dịch vụ
  ServiceDesk.Worker/          # Chương trình xử lý nền
  ServiceDesk.Database/        # Công cụ migration và khởi tạo có chủ đích
tests/                        # Domain, Application, Architecture, Integration
contracts/                    # Baseline HTTP và AI classification v1
```

Domain độc lập với nền tảng kỹ thuật; Application phụ thuộc Domain. Infrastructure triển khai giao diện do Application sở hữu. API/Worker lắp ghép các mô-đun cần dùng. Hàm xử lý HTTP không truy cập DbContext; Worker không sử dụng HttpContext hoặc danh tính người dùng ngầm định. Xem [ADR-011](docs/adr/ADR-011-backend-module-boundaries.md) và [ADR-014](docs/adr/ADR-014-feature-use-cases-and-published-contracts.md).

Application tổ chức theo mô-đun và tính năng. Hiện có 12 luồng xử lý: Identity có RegisterUser/LoginUser/GetCurrentUser; Ticketing có CreateTicket/GetMyTickets/GetTicketDetail/UpdateTicket/ClaimTicket/ResolveTicket/ReopenTicket/CloseTicket/CancelTicket. Hàm xử lý HTTP gọi giao diện nghiệp vụ; lớp triển khai là `internal`. Các giao diện công khai và cổng tích hợp được kiểm tra riêng bằng kiểm thử kiến trúc. [Tài liệu mô-đun và luồng xử lý](docs/architecture/backend-modules-and-use-cases.md) mô tả trách nhiệm, giao dịch và phạm vi quyền hiện tại.

Nhận xử lý hiện chỉ chuyển `Submitted → InProgress`, chưa lưu người phụ trách. Mở lại hiện chuyển `Resolved → InProgress`, chưa có giới hạn thời gian. Quyền và giới hạn của sáu thao tác mới được ghi trong [hợp đồng hiện hành](docs/architecture/current-backend-contract.md), gồm các điểm chưa thống nhất cần xử lý.

Requester chỉ đọc chi tiết phiếu của mình; Agent/Admin hiện đọc chi tiết toàn hệ thống. Danh sách và tạo phiếu luôn theo người đang đăng nhập. Hệ thống chưa cách ly theo nhóm hoặc phân công. Identity đọc trạng thái tài khoản dưới khóa dòng PostgreSQL để giữ trạng thái khóa đăng nhập nhất quán giữa các bản API.

## Build và test

Cần SDK đúng phiên bản trong [global.json](global.json). Dependency được ghim bằng phiên bản cụ thể và NuGet lockfile.

`packages.lock.json` ghi lại các gói NuGet đã được giải quyết, gồm cả phụ thuộc gián tiếp. `restore --locked-mode` yêu cầu cấu hình dependency khớp lockfile và dừng nếu cần thay đổi lockfile; không tự cập nhật để làm build đạt. Khi chủ động nâng dependency, cập nhật và review lockfile trong cùng thay đổi.

`-c Release` chọn cấu hình biên dịch có tối ưu; nó **không** đặt môi trường thành Production. Backend vẫn chạy Development nếu biến môi trường đặt như trên. `--no-restore` dùng kết quả restore trước đó; `--no-build` dùng kết quả build Release đã có, không tự biên dịch mã mới. Sau khi sửa C#, build lại trước khi chạy với `--no-build`. Có thể bỏ `--no-build` ở công cụ Python để nó tự build khi chạy.

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

## Database và triển khai

Production bắt buộc khóa JWT riêng ít nhất 32 byte UTF-8; khóa mẫu/thiếu bị từ chối. Giữ khóa mạnh, issuer và audience đang dùng để duy trì token hợp lệ. Tài khoản demo chỉ được tạo bằng lệnh riêng `seed-demo` trong Development/Test khi được bật rõ ràng, không được seed lúc API/Worker khởi động.

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

Xem [ADR-012](docs/adr/ADR-012-ai-contract-and-future-transport.md) cho thiết kế xác thực, giới hạn thời gian, thử lại và ngắt tạm kết nối của bộ tích hợp tương lai. Sau PR #28 đã có một phần thao tác vòng đời phiếu; phân công, giới hạn mở lại, tự đóng, SLA, dọn biên nhận và AI thật vẫn chưa triển khai.

## Tương thích và lịch sử

Giữ API hiện tại, status enum, scope/replay idempotency và dữ liệu đã lưu. Migration bổ sung sửa độ rộng mã ticket sau 9.999; không reset sequence hoặc đổi số cũ. Không rewrite lịch sử Git. Tài liệu sprint/audit cũ được giữ làm bằng chứng lịch sử; kiến trúc triển khai hiện tại được mô tả bởi ADR-011–014, hợp đồng hiện hành và runbook trên.
