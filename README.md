# AI IT Service Desk — Backend

Backend .NET của SupportFlow AI, tổ chức theo module trước, Clean Architecture bên trong từng module (Identity, Ticketing, Knowledge, AIAssistance). Kho này sở hữu API, nghiệp vụ, truy cập dữ liệu, tiến trình nền, migration và kiểm thử backend.

Mốc tài liệu: đối chiếu mã `dev` tại `a182c9fde556423d04764fe2a0b7ac16af773923`, cập nhật 2026-11-08, sau các PR #50–#55 (refactor module, Identity, Knowledge, giới hạn tần suất) và #59–#60 (nền dữ liệu nhóm hỗ trợ, chưa đổi phân quyền). Mốc này mô tả mã nguồn, không thay bằng chứng nghiệm thu. Xem [quy chuẩn tài liệu](docs/process/documentation-standard.md) và [bảng thuật ngữ](docs/process/technical-glossary.md).

| Thành phần | Repo |
|---|---|
| Backend | https://github.com/supportflow-ai/ai-it-service-desk |
| Giao diện React | https://github.com/supportflow-ai/ai-it-service-desk-web |
| Hợp đồng và dịch vụ AI tương lai | https://github.com/supportflow-ai/ai-service |
| Compose, NGINX và kiểm tra toàn hệ thống qua GitHub Actions | https://github.com/supportflow-ai/supportflow-ai-infra |

Bắt đầu tại [bản đồ tài liệu](docs/README.md) và [hợp đồng backend hiện hành](docs/architecture/current-backend-contract.md). Các hợp đồng lịch sử và đề xuất chưa duyệt được phân loại riêng.

[Bảng mốc mã nguồn](docs/architecture/source-baseline.md) ghi mốc đối chiếu của tài liệu hiện hành. Hợp đồng OpenAPI và công cụ Python được công bố cùng mã tương ứng; đối chiếu HEAD và kết quả kiểm thử trước khi tích hợp.

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

Đăng ký, đăng nhập và thao tác ticket không cần MinIO; chỉ luồng tệp đính kèm cần MinIO. Chỉ điền cấu hình MinIO khi có dịch vụ thật; tên `minio:9000` chỉ dùng được trong mạng Docker có dịch vụ tên đó. Quên mật khẩu gửi mã qua SMTP khi có `Email__Smtp__*`; chưa cấu hình thì chỉ ghi cảnh báo, không gửi (xem [.env.example](.env.example)). AI mặc định NoOp, không cần khóa hay dịch vụ AI. Hàng đợi gửi mã nằm trong tiến trình API; nếu cần chạy host Worker, dùng `python scripts/run_backend.py --no-build worker` ở terminal riêng.

## JWT là gì và lấy ở đâu?

JWT là chuỗi thông tin xác thực có chữ ký do backend cấp sau khi đăng nhập hoặc đăng ký thành công. Nó chứa danh tính, vai trò và thời hạn; nội dung không được mã hóa để giữ bí mật. Frontend gửi token trong header `Authorization: Bearer <token>` ở các yêu cầu cần đăng nhập.

- `Jwt__SecretKey` là **khóa ký của backend**, không phải token đăng nhập. Không gửi khóa này cho frontend hoặc đặt vào biến `VITE_...`.
- `token` trong response đăng ký/đăng nhập là **JWT của người dùng**. Không cần tạo JWT thủ công hoặc lấy từ một trang web khác.

Để lấy token trong Swagger:

1. Nếu chưa có tài khoản, gọi `POST /api/auth/register` với email, mật khẩu và `fullName` tùy chọn. Mật khẩu cần ít nhất 8 ký tự, có chữ hoa, chữ thường và chữ số. Server chỉ cấp vai trò Requester, bất kể role client gửi lên.
2. Hoặc gọi `POST /api/auth/login` bằng email/mật khẩu của tài khoản đã có.
3. Sao chép trường `token` từ response, nhấn **Authorize** trong Swagger và dán token. Không dán `Jwt__SecretKey`.
4. Gọi `GET /api/auth/me` để kiểm tra phiên. Access token hết hạn thì gọi `POST /api/auth/refresh` với `refreshToken` nhận lúc đăng nhập (mỗi lần làm mới cấp token mới; dùng lại token cũ thu hồi cả chuỗi phiên); đăng xuất bằng `POST /api/auth/revoke`. Chi tiết trong [hợp đồng hiện hành](docs/architecture/current-backend-contract.md#identity-sau-pr-47-48-và-nhánh-refactor).

Frontend tự nhận và gửi token khi người dùng đăng nhập. Không chia sẻ JWT cá nhân cho các thành viên khác. Đăng nhập sai 5 lần sẽ bị khóa 15 phút; API vẫn trả 401 chung. Các endpoint xác thực còn bị giới hạn tần suất theo IP và theo email (trả 429 kèm `Retry-After`).

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
  BuildingBlocks/
    ServiceDesk.SharedKernel/      # Vai trò, actor hiện tại, đồng hồ, cổng lưu trữ, ngoại lệ nghiệp vụ dùng chung
    ServiceDesk.SharedKernel.Web/  # Thành phần HTTP dùng chung cho endpoint (bộ lọc lỗi)
  Modules/
    <Module>/                      # Identity, Ticketing, Knowledge, AIAssistance
      ServiceDesk.Modules.<Module>.csproj
      <Module>Module.cs
      Domain/  Application/  Infrastructure/  Presentation/
  ServiceDesk.Infrastructure/      # Hạ tầng dùng chung: ApplicationDbContext, migration, bảo trì DB, MinIO, đồng hồ
  ServiceDesk.Api/                 # Host HTTP: ghép module, middleware, health
  ServiceDesk.Worker/              # Chương trình xử lý nền
  ServiceDesk.Database/            # Công cụ migration và khởi tạo có chủ đích
tests/                             # Domain, Application, Architecture, Integration
contracts/                         # Baseline HTTP và AI classification v1
```

Mỗi thư mục trong `Modules/` chứa một project. Tên assembly và namespace gốc giữ dạng `ServiceDesk.Modules.<Module>`. Host API đọc người dùng từ HTTP qua `ServiceDesk.Api/Authentication/`. Module Identity sở hữu cấu hình JWT, tài khoản và phiên.

Module là ranh giới ngoài cùng. Bên trong module, Presentation và Infrastructure phụ thuộc Application, còn Application phụ thuộc Domain. Domain và Application độc lập với EF Core, Npgsql và ASP.NET Core. Mỗi module đăng ký dịch vụ qua lớp `<Module>Module` và đặt endpoint trong `Presentation` nếu có API HTTP. API và Worker ghép module qua các điểm đăng ký. AIAssistance dùng ba kiểu phân loại đã công bố của Ticketing. Xem [ADR-015](docs/adr/ADR-015-module-first-clean-architecture.md), [ADR-011](docs/adr/ADR-011-backend-module-boundaries.md) và [ADR-014](docs/adr/ADR-014-feature-use-cases-and-published-contracts.md).

Application tổ chức theo mô-đun và tính năng, mỗi tính năng một thư mục có giao diện `I<TínhNăng>` và lớp `<TínhNăng>UseCase`: Identity 11 luồng (đăng ký, đăng nhập, hồ sơ, làm mới/thu hồi phiên, đổi/quên/đặt lại mật khẩu, gán vai trò, danh sách người dùng, phòng ban), Ticketing 19 luồng (tạo, danh sách cá nhân và hàng đợi, chi tiết, cập nhật, nhận/phân công/hủy phân công, giải quyết/mở lại/đóng/hủy, bình luận, ghi chú nội bộ, timeline, tệp đính kèm), Knowledge 6 luồng (bản nháp, xuất bản, lưu trữ, chi tiết, tìm kiếm). Hàm xử lý HTTP gọi giao diện nghiệp vụ; lớp triển khai là `internal`. Các giao diện công khai và cổng tích hợp được kiểm tra riêng bằng kiểm thử kiến trúc. [Tài liệu mô-đun và luồng xử lý](docs/architecture/backend-modules-and-use-cases.md) mô tả trách nhiệm, giao dịch và phạm vi quyền hiện tại.

Quyền, điều kiện chuyển trạng thái, `expectedRevision` và mã lỗi của từng thao tác ticket nằm trong [hợp đồng hiện hành](docs/architecture/current-backend-contract.md). Requester chỉ đọc phiếu của mình; Agent/Admin đọc toàn hệ thống vì chưa có cách ly theo nhóm hỗ trợ (quyết định còn mở DEC-02). Identity khóa dòng người dùng trong PostgreSQL khi tạo hoặc thu hồi phiên để các bản API chạy song song vẫn nhất quán.

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

Integration tests dùng PostgreSQL 17 Testcontainers và cần Docker đang hoạt động. Application unit tests không cần database: chúng tham chiếu project module nhưng thay cổng dữ liệu bằng mock.

Đợt củng cố Identity thêm hai migration (`AddOtpFailedAttempts`, `AddRefreshTokenFamily`) và một migration tương thích triển khai cuốn chiếu (`DefaultRefreshTokenFamilyForLegacyWriters`); thứ tự áp dụng và quay lui nằm trong runbook. Xem [runbook kiểm chứng](docs/runbooks/backend-hardening-verification.md) để kiểm tra đúng commit và phân biệt test offline với PostgreSQL thật.

## CI bằng GitHub Actions

[Backend quality gates](.github/workflows/ci.yml) chạy khi mở/cập nhật PR, push vào main/dev/staging hoặc nhánh `codex/**`, và khi chạy thủ công trong tab Actions. Pipeline restore khóa phiên bản, build, unit/architecture/compatibility tests, integration PostgreSQL thật, kiểm tra AI contract và build ba image API/Worker/Database. Kết quả test TRX và AI contract được lưu thành workflow artifacts.

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

Xem [ADR-012](docs/adr/ADR-012-ai-contract-and-future-transport.md) cho thiết kế xác thực, giới hạn thời gian, thử lại và ngắt tạm kết nối của bộ tích hợp tương lai. Chưa triển khai: nhóm hỗ trợ, SLA, tự đóng phiếu, dọn biên nhận và AI thật.

## Tương thích và lịch sử

Giữ API hiện tại, status enum, scope/replay idempotency và dữ liệu đã lưu. Migration bổ sung sửa độ rộng mã ticket sau 9.999; không reset sequence hoặc đổi số cũ. Không rewrite lịch sử Git. Tài liệu sprint/audit cũ được giữ làm bằng chứng lịch sử; kiến trúc triển khai hiện tại được mô tả bởi ADR-011–014, hợp đồng hiện hành và runbook trên.
