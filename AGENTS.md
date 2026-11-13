# AGENTS.md — Backend (`ai-it-service-desk`)

Dành cho mọi người làm việc trong kho này, gồm lập trình viên, người kiểm thử, người vận hành và trợ lý AI. Quy tắc chung của cả dự án (quy trình nhánh, commit, PR, giới hạn an toàn, hoàn thành) nằm ở [cẩm nang chung](docs/team-handbook.md); file này chỉ ghi phần riêng của backend và không thay đặc tả kiến trúc.

## 0. Tám điều cần nhớ trước khi sửa

1. **Module trước, Clean Architecture bên trong module.** Đặt mã đúng chỗ theo bảng §3.
2. **Giữ tương thích** API, enum đã lưu và migration đã phát hành. Đổi hợp đồng chỉ khi nhiệm vụ yêu cầu.
3. **Không sửa test, fixture hay baseline để làm CI xanh.**
4. **Chạy `ServiceDesk.ArchitectureTests`** sau mọi thay đổi chạm `using` hoặc tham chiếu project.
5. **Application tự xác thực actor và phân quyền**, không chỉ dựa vào middleware HTTP.
6. **Persistence, transaction, đồng thời cần kiểm thử PostgreSQL thật**, không thay bằng SQLite hay EF InMemory.
7. **Không thao tác database, staging, production** khi chưa được yêu cầu rõ lệnh và đích (§6).
8. **Đổi API, hợp đồng AI, Dockerfile hoặc migration thì báo kho khác** (§8).

## 1. Phạm vi và tài liệu

Kho này sở hữu API, nghiệp vụ, phân quyền, truy cập dữ liệu, tiến trình nền, schema và migration cùng công cụ Migrator, hợp đồng HTTP và hợp đồng AI v1, và kiểm thử backend. Giao diện ([`ai-it-service-desk-web`](https://github.com/supportflow-ai/ai-it-service-desk-web)), hạ tầng toàn hệ thống ([`supportflow-ai-infra`](https://github.com/supportflow-ai/supportflow-ai-infra)) và dịch vụ AI ([`ai-service`](https://github.com/supportflow-ai/ai-service)) thuộc các kho riêng.

| Đường dẫn | Nội dung |
| --- | --- |
| `src/backend/Hosts/Api` | Host HTTP: ghép module, middleware, health, JWT |
| `src/backend/Hosts/Worker` | Host tiến trình nền, không phụ thuộc HTTP |
| `src/backend/Hosts/Migrator` | CLI vận hành: `migrate`, `seed-roles`, `seed-demo`, `bootstrap-admin`, `check-production` |
| `src/backend/Platform` | `ApplicationDbContext`, migration, bảo trì DB, MinIO, đồng hồ hệ thống; không chứa nghiệp vụ |
| `src/backend/BuildingBlocks/SharedKernel`, `SharedKernel.Web` | Kiểu dùng chung nhiều module; thành phần HTTP dùng chung |
| `src/backend/Modules/<Module>` | Identity, Ticketing, Knowledge, AIAssistance; mỗi module một project `ServiceDesk.Modules.<Module>` |
| `tests/` | `Domain.Tests`, `Application.Tests`, `ArchitectureTests`, `IntegrationTests` (PostgreSQL thật) |
| `contracts/` | `backend/openapi-baseline.json` (hợp đồng HTTP), `ai/v1/openapi.yaml` (hợp đồng AI) |
| `scripts/` | Chạy backend với `.env`, đường hầm database, validator tài liệu và hợp đồng AI |

| Nguồn | Tra cứu |
| --- | --- |
| [README](README.md), [môi trường](docs/runbooks/environment-runbook.md) | Dịch vụ phụ thuộc, chạy local, Python venv, EF CLI |
| [Cẩm nang chung](docs/team-handbook.md), [nhánh, commit, PR](docs/process/branch-commit-pr-convention.md), [DoR/DoD](docs/process/definition-of-ready-done.md) | Cách làm việc chung của dự án và của kho |
| [Bản đồ tài liệu](docs/README.md), [hợp đồng hiện hành](docs/architecture/current-backend-contract.md) | Nghiệp vụ, quyền, trạng thái phê duyệt của từng tài liệu |
| [ADR](docs/adr/), [module và use case](docs/architecture/backend-modules-and-use-cases.md) | Kiến trúc mục tiêu và phần đã triển khai |
| [Kiểm chứng](docs/runbooks/backend-hardening-verification.md), [triển khai](docs/runbooks/backend-refactor-rollout.md), [CI](.github/workflows/ci.yml) | Kiểm thử, migration, rollback |
| [Quy chuẩn tài liệu](docs/process/documentation-standard.md), [thuật ngữ](docs/process/technical-glossary.md) | Cách viết và cập nhật tài liệu |

## 2. Lệnh

Chạy từ gốc repo, không tương tác. SDK theo [global.json](global.json); Python theo workflow; EF CLI khớp `Microsoft.EntityFrameworkCore.Design` trong [project API](src/backend/Hosts/Api/ServiceDesk.Api.csproj). Không chép lại số phiên bản vào tài liệu khác.

Build trước khi dùng `--no-build`, cùng mã và cấu hình `Release`:

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

Chạy riêng một project, kiểm thử kiến trúc hoặc lọc theo tên (thay `TEN_TEST_DAY_DU` bằng tên thực tế):

```sh
dotnet test tests/ServiceDesk.Application.Tests -c Release --no-build
dotnet test tests/ServiceDesk.ArchitectureTests -c Release --no-build
dotnet test tests/ServiceDesk.Application.Tests -c Release --no-build --filter "FullyQualifiedName=TEN_TEST_DAY_DU"
```

Số kiểm thử thực sự chạy phải lớn hơn 0; lệnh kết thúc thành công nhưng không khớp test thì không phải bằng chứng đạt. Kiểm thử tích hợp PostgreSQL cần Docker đang chạy; kiểm thử kiến trúc chạy offline, không cần database.

Chạy API hoặc Worker local bằng công cụ nạp `.env` (cần cấu hình theo [README](README.md)); kết thúc phải dừng tiến trình do mình tạo:

```sh
python scripts/run_backend.py --no-build api
python scripts/run_backend.py --no-build worker
```

Kiểm tra tài liệu và hợp đồng AI. Validator tài liệu chỉ dùng thư viện chuẩn. Hợp đồng AI cần Python venv; chưa có venv thì tạo theo tài liệu môi trường, không cài package vào Python global:

```sh
python scripts/validate_project_docs.py
python -m unittest discover -s scripts/tests -p "test_project_docs.py"
./.venv/Scripts/python.exe -m pip install -r contracts/requirements.txt
./.venv/Scripts/python.exe scripts/validate_ai_contract.py
```

Linux/macOS dùng `./.venv/bin/python` thay interpreter trên; lệnh tạo venv có thể cần `python3`.

Chưa có `.cache/dotnet-tools/dotnet-ef` thì dừng lệnh EF và cài **cục bộ** theo tài liệu môi trường, không tự cài hay cập nhật tool global. Migration nằm ở `Platform`, startup project là API, context là `ApplicationDbContext`:

```sh
./.cache/dotnet-tools/dotnet-ef migrations add TEN_MIGRATION --project src/backend/Platform --startup-project src/backend/Hosts/Api --context ApplicationDbContext --output-dir Persistence/Migrations --configuration Release
./.cache/dotnet-tools/dotnet-ef migrations remove --project src/backend/Platform --startup-project src/backend/Hosts/Api --context ApplicationDbContext --configuration Release
./.cache/dotnet-tools/dotnet-ef migrations has-pending-model-changes --project src/backend/Platform --startup-project src/backend/Hosts/Api --context ApplicationDbContext --configuration Release --no-build
```

Các lệnh EF là lựa chọn theo nhiệm vụ, không chạy nối tiếp. `remove` có thể kết nối database nên chịu cùng chốt chặn ở §6; chỉ gỡ migration cuối chưa merge, chưa phát hành, chưa dùng chung và đã xác minh không còn được áp dụng. Đã thử áp dụng ở local thì theo quy trình gỡ trong tài liệu môi trường, không xóa tay hoặc dùng `--force`.

Sau refactor không chủ đích đổi schema, dùng `has-pending-model-changes` để phát hiện lệch ánh xạ, không tạo migration để che lỗi. Nếu `add` sinh thay đổi ngoài dự kiến, dừng và báo. Kiểm tra `Up`/`Down` không có `DROP` hay mất dữ liệu ngoài ý muốn, snapshot chỉ đổi phần được giao; build lại sau khi sinh hoặc gỡ migration.

## 3. Kiến trúc và ranh giới module

Modular Monolith, tổ chức **module trước, Clean Architecture bên trong module** ([ADR-015](docs/adr/ADR-015-module-first-clean-architecture.md); ADR đã hợp nhất vào `dev`, chờ nhóm duyệt). Tài liệu bằng chứng và đề xuất cũ (`docs/reviews`, `docs/sprint-1`, `artifacts`, `docs/architecture/0[1-6]-*.md`) còn ghi đường dẫn `ServiceDesk.Domain/Application/...` của mốc cũ: chỉ dùng để truy vết, không dùng làm chỗ đặt mã mới.

| Cần viết | Đặt ở |
| --- | --- |
| Thực thể, giá trị, quy tắc | `src/backend/Modules/<Module>/Domain/` |
| Luồng xử lý | `.../Application/<TínhNăng>/`: interface `I<TínhNăng>` có `ExecuteAsync`, lệnh/truy vấn, lớp `<TínhNăng>UseCase` `internal sealed`; DTO dùng chung ở `Application/Contracts`, cổng ở `Application/Ports` |
| Adapter PostgreSQL/HTTP, ánh xạ EF, options | `.../Infrastructure/` (ánh xạ EF ở `Infrastructure/Configurations`) |
| Endpoint và request body | `.../Presentation/` |
| Đăng ký DI của module | `<Module>Module.cs`: `Add<Module>Application`, `Add<Module>Infrastructure`, `Add<Module>Module` |
| Kiểu dùng chung nhiều module (vai trò, actor, đồng hồ, cổng lưu trữ, ngoại lệ nghiệp vụ) | `src/backend/BuildingBlocks/SharedKernel/`; thành phần HTTP dùng chung ở `src/backend/BuildingBlocks/SharedKernel.Web/` |
| `ApplicationDbContext`, migration, bảo trì DB, MinIO, đồng hồ hệ thống | `src/backend/Platform/` (không đặt nghiệp vụ ở đây) |
| Ghép module, middleware | `src/backend/Hosts/Api/Program.cs`, `src/backend/Hosts/Worker/WorkerServices.cs`; Identity đăng ký JWT qua `AddIdentityAuthentication` |

### Quy tắc nào do máy kiểm

| Quy tắc | Bắt bởi |
| --- | --- |
| Domain không dùng EF Core, Npgsql, ASP.NET Core hay tầng khác; Application không dùng Infrastructure, Presentation, EF Core, Npgsql, HTTP context; Presentation không chạm persistence; SharedKernel không tham chiếu module | `CleanArchitectureTests`, `BoundaryTests` |
| Endpoint không truy cập Infrastructure hay EF; Worker không phụ thuộc HTTP hay API; chỉ Identity đọc bảng ASP.NET Core Identity; miền AI không dùng thực thể Ticket hay Identity | `BoundaryTests` |
| Xuyên module chỉ dùng kiểu trong `Published` của [ModuleRules](tests/ServiceDesk.ArchitectureTests/ModuleRules.cs); use case `internal`; mỗi kiểu nằm đúng assembly module | `ModuleEncapsulationTests`, `ModuleRuleNegativeTests` |
| Hợp đồng HTTP khớp baseline OpenAPI | `PublishedApiMatchesBaseline` (`ApiContractTests`) |
| Liên kết và phân loại tài liệu | `scripts/validate_project_docs.py` (chạy trong CI) |

Các quy tắc dưới đây **không có máy kiểm**, người viết và người duyệt tự giữ:

- Trong module, phụ thuộc chỉ đi vào trong: Presentation → Application → Domain; Infrastructure → Application → Domain. Các tầng cùng nằm trong một project nên vi phạm chỉ bị chặn khi chạy kiểm thử kiến trúc.
- Application xác thực actor và phân quyền nghiệp vụ. Mẫu: [CreateTicketUseCase](src/backend/Modules/Ticketing/Application/CreateTicket/CreateTicketUseCase.cs). Tách ghi và truy vấn; không tự thêm mediator hoặc Generic Repository.
- Dùng gateway chuyên biệt, không đưa `DbSet`/`IQueryable`/transaction EF qua cổng Application. Adapter persistence nhận `DbContext` dùng chung (bí danh của `ApplicationDbContext` theo scope) và dùng `Set<T>()`; không tham chiếu `ApplicationDbContext` từ module. API không truy cập persistence để xử lý nghiệp vụ.
- Hiện chỉ AIAssistance tham chiếu Ticketing và chỉ dùng `TicketCategory`, `Impact`, `Urgency`. Không đi vòng qua SharedKernel, helper hay generic; SharedKernel không chứa thực thể hay quy tắc của một module.
- Module khác cần vai trò hoặc tên hiển thị thì dùng `IUserDirectory` (SharedKernel, Identity cài đặt); host nào ghép module đó phải gọi `AddIdentityUserDirectory`.
- Lệnh ghi Ticketing dùng `TicketCommandGuard` (revision, lỗi quy tắc → 400, audit hệ thống) và `TicketPermissions`; Knowledge dùng `KnowledgePermissions`; không chép lại khung tải, kiểm, lưu trong use case mới.
- Thay đổi Identity có thu hồi phiên (đổi vai trò, đổi hoặc đặt lại mật khẩu, xoay vòng refresh token) phải ghi và thu hồi trong cùng một giao dịch, kèm kiểm thử PostgreSQL cho trường hợp chạy đồng thời.
- Ngoại lệ nghiệp vụ cần mã HTTP chung thì kế thừa lớp cơ sở trong SharedKernel (`ConflictException` → 409, `RetryLaterException` → 503 kèm `Retry-After`); không thêm `catch` riêng của module vào `ApiErrorFilter`.
- Khi sửa luồng tạo phiếu, giữ ticket và receipt trong cùng transaction; kiểm tra rollback, replay và đồng thời theo bảng §9.
- API và Worker không tự migration hay seed khi khởi động. Không tự kết nối AI thật ngoài nhiệm vụ.

### Thêm tính năng hoặc module

- **Thêm tính năng:** tạo thư mục trong `Application`, đăng ký trong `<Module>Module.cs`, map endpoint trong `Presentation`, rồi cập nhật [baseline OpenAPI](contracts/backend/openapi-baseline.json) có chủ đích. Khi có hợp đồng xuyên module mới, cập nhật `Published` và chỉ rõ module tiêu thụ trong `ModuleRules`.
- **Thêm module** (chỉ khi nhiệm vụ yêu cầu, không tạo module rỗng): project `ServiceDesk.Modules.<Module>` theo mẫu module hiện có; thêm vào `ServiceDesk.sln`, `Modules` và `ProductionAssemblies` trong ArchitectureTests; tham chiếu từ host và Dockerfile của API, Worker, Migrator. Nếu module có bảng: thêm assembly vào `ModuleModelAssemblies` và `DbSet` trong `ApplicationDbContext`, tham chiếu project từ `ServiceDesk.Platform`, rồi tạo migration. Project mới cần `packages.lock.json` sinh bằng `dotnet restore`; review diff trước khi commit.

## 4. Ngôn ngữ và Git

Quy ước đầy đủ: [nhánh, commit, PR](docs/process/branch-commit-pr-convention.md) và [cẩm nang chung](docs/team-handbook.md). Điểm riêng cần nhớ:

- Trao đổi, tài liệu mới, mô tả commit và PR bằng tiếng Việt có dấu; giữ nguyên định danh, câu lệnh và hợp đồng kỹ thuật.
- **Mã công việc (Jira hoặc công cụ khác) không bắt buộc.** Commit dạng `<loại>(<phạm vi>): <mô tả tiếng Việt>`; có mã thì ghi vào mô tả PR.
- Kiểm tra nhánh, HEAD và thay đổi chưa commit trước khi sửa. Chỉ đưa phần thuộc nhiệm vụ vào commit; không dùng `git add -A` khi chưa xác minh toàn bộ thay đổi thuộc nhiệm vụ.
- Không tự commit khi chỉ được yêu cầu đánh giá hay lập kế hoạch; không tự đổi nhánh đã được chỉ định, và không đổi tên nhánh đó để khớp mẫu.
- Chỉ sửa AGENTS.md khi nhiệm vụ yêu cầu. Ngoại lệ: đổi cấu hình được ghim hoặc lệnh vận hành thì cập nhật hướng dẫn bị ảnh hưởng trong cùng thay đổi.

## 5. Việc phải hỏi trước

- Khi hai nguồn xung đột mà chưa có căn cứ giải quyết, nêu cả hai kèm phiên bản, trạng thái, tác động và quyết định cần chốt; hỏi trước khi sửa phần phụ thuộc. Tiếp tục phần độc lập đã được giao.
- Trong nhiệm vụ sửa bảo mật, được từ chối yêu cầu trái phép theo chính sách đã rõ và thêm kiểm thử hồi quy. Hỏi khi cần chọn chính sách mới, như quyền của Agent theo chủ sở hữu, nhóm hay phân công. Lỗ hổng không phải hành vi cần giữ.
- Báo lỗi nghiêm trọng ngoài phạm vi kèm bằng chứng và ảnh hưởng; không âm thầm bỏ qua và không mở rộng nhiệm vụ.
- Mặc định giữ tương thích API và dữ liệu. Chỉ đổi hợp đồng, đặc tả hay tài liệu quy trình khi nhiệm vụ yêu cầu; không sửa kiểm thử, fixture hoặc baseline để hợp thức hóa lỗi hay làm CI đạt.
- Khi di chuyển hoặc đổi tên tệp, cập nhật liên kết trong tài liệu loại `current` và `decision` theo [danh mục nguồn](docs/context-sources.json). Tài liệu `evidence`, `proposal`, `historical` giữ nguyên nội dung, đường dẫn và số dòng gốc; chỉ thay liên kết tương đối bị gãy bằng URL GitHub ghim SHA trước thay đổi. Ngoại lệ lưu trữ của tám tệp `historical` trong `docs/architecture/` ghi ở [quy chuẩn tài liệu](docs/process/documentation-standard.md#ngoại-lệ-lưu-trữ).

## 6. Database và môi trường

- Không sửa, xóa hay đổi thứ tự migration đã phát hành **hoặc đã áp dụng trong môi trường dùng chung**; không đổi nghĩa enum đã lưu. Schema mới cần migration và phương án tương thích. Không dùng `EnsureCreated` thay `Migrate` để xác minh nâng cấp.
- Không chạy lệnh chờ nhập tương tác. Cấu hình phải có qua môi trường hoặc kho bí mật; không yêu cầu gửi bí mật trong chat, không giả định biến môi trường tồn tại giữa các lần gọi công cụ.
- Trước `migrate`, `seed-roles` hoặc EF truy cập database, đối chiếu `SD_DB_HOST`, `SD_DB_PORT`, `SD_DB_NAME` không bí mật với đích EF thực tế bằng phép kiểm tra chỉ in host, cổng, tên database trong tài liệu môi trường. Không in chuỗi kết nối hay đầu ra EF thô khi lỗi.
- Xác minh thêm container hoặc tiến trình PostgreSQL local nghe đúng cổng; `docker ps` phải dùng Docker context local. Không có bằng chứng, hoặc chỉ thấy tunnel/proxy chưa rõ đích: dừng thao tác database và hỏi. Nhãn Development hay `localhost` riêng lẻ không đủ.
- Database dev local không mặc nhiên là dữ liệu dùng một lần. Chỉ reset hay hạ migration khi có ủy quyền cho đúng đích và tác động mất dữ liệu; nếu chưa được phép, giữ migration và đề xuất migration sửa bổ sung.
- Không tự chạy `seed-demo`, `bootstrap-admin`, `check-production` hay thao tác vào staging và production; cần yêu cầu rõ lệnh và đích. `check-production` không được coi là chỉ đọc; xem tài liệu môi trường.
- API và Worker chỉ khởi chạy khi nhiệm vụ cần: chạy nền bằng công cụ của môi trường, lưu PID hoặc ID phiên công cụ, dừng tiến trình do mình tạo khi xong, không chờ vô hạn.

## 7. Thao tác mất dữ liệu hoặc tác động bên ngoài

- Không tự ghi đè thay đổi của người khác, `reset --hard`, `clean`, force-push, xóa volume hay chạy migration `Down` trên dữ liệu thật. `checkout` hoặc `restore` bị chặn khi ghi đè tệp có thay đổi chưa commit, và không cấm tạo nhánh. Cần ủy quyền rõ cho thao tác có thể mất dữ liệu; database kiểm thử đã xác nhận dùng một lần thì được xử lý trong phạm vi test.
- Không tự push, hợp nhất PR hay triển khai production khi chưa có ủy quyền tương ứng. Không đưa bí mật vào Git hay nhật ký, và không nới lỏng hướng dẫn để né giới hạn.

## 8. Thay đổi ảnh hưởng kho khác

Bảng đầy đủ ở [cẩm nang chung](docs/team-handbook.md#4-thay-đổi-ảnh-hưởng-kho-khác). Với backend:

| Bạn đổi | Phải báo hoặc sửa |
| --- | --- |
| Đường dẫn, tham số, mã phản hồi, DTO của API (và `contracts/backend/openapi-baseline.json`) | **web** cập nhật snapshot OpenAPI và chạy lại `contract:generate`, `contract:check` |
| `contracts/ai/v1/openapi.yaml` | **ai-service** cập nhật bản sao và `contract.lock.json`; CI của ai-service so từng byte |
| Đường dẫn hoặc nội dung Dockerfile của Api, Worker, Migrator; tên image; biến môi trường | **infra** cập nhật Compose và workflow |
| Migration, lệnh Migrator | **infra** kiểm chứng bằng smoke migrate, seed, khởi động API và Worker |
| Địa chỉ hay cổng lắng nghe, CORS | **web** (proxy `/api`) và **infra** (NGINX) |

Smoke toàn hệ thống thuộc infra, phải dùng đúng SHA backend.

## 9. Kiểm chứng và bàn giao

Chạy hợp các nhóm kiểm tra tương ứng với thay đổi:

| Thay đổi | Kiểm tra cần chạy |
| --- | --- |
| Công cụ kiểm tra tài liệu hoặc workflow tài liệu | Validator và các tình huống hồi quy của công cụ; kiểm tra lệnh và cấu trúc workflow, không tự chạy lại image không chịu ảnh hưởng |
| Chỉ tài liệu | `python scripts/validate_project_docs.py`, đối chiếu câu lệnh và cấu hình; không bắt buộc toàn bộ suite |
| Nghiệp vụ, phân quyền | Unit và use case, kiểm thử hồi quy HTTP liên quan; quyền cần trường hợp bị từ chối |
| Endpoint, DTO, response HTTP | Kiểm thử hợp đồng HTTP và OpenAPI, JSON, status và header, tương thích caller liên quan |
| Ranh giới tầng và module | Project ArchitectureTests |
| Persistence, migration, transaction, đồng thời | Integration PostgreSQL thật; không thay bằng SQLite hay EF InMemory |
| Contract, fixture, mapper AI hoặc validator | Unit và adapter tương ứng, cùng `scripts/validate_ai_contract.py` bằng Python venv |
| Dockerfile, build, dependency, cấu hình | Restore khóa, build, test liên quan và build image Api, Worker, Migrator chịu ảnh hưởng theo workflow |

Khi bàn giao thay đổi chức năng, đối chiếu đủ các bước workflow và tiêu chí nghiệm thu; không tự thêm phát hành image hay triển khai. Gắn kết quả với SHA và thay đổi chưa commit nếu có; không dùng báo cáo của commit cũ để xác nhận HEAD mới. Smoke toàn hệ thống thuộc kho infra.

Bàn giao ngắn: tệp và phạm vi chính; cách sửa và hợp đồng chịu ảnh hưởng; lệnh cùng số test thực chạy và kết quả; kiểm tra chưa chạy và lý do; giới hạn; nhánh, SHA và trạng thái push hay PR. CI đạt không tự chứng minh đã nghiệm thu đầy đủ; không tuyên bố hoàn thành khi thiếu bằng chứng.

### Lỗi đã biết khi kiểm thử

Hai test từng đỏ ngẫu nhiên khi máy đang bận (quan sát 2026-11-12 và 2026-11-13, thường ngay sau build), đạt khi chạy riêng và khi chạy lại:

- `HttpClassificationAdapterTests.RejectsMismatchedOrExtendedClosedContract`: nhận `Timeout` thay vì `InvalidResponse` do timeout 3 giây của adapter.
- `MinioAttachmentUploadTests.SignedPolicyEnforcesSizeAndCopyIsNotWritableByOldUploadForm`: `NullReferenceException` trong thư viện Minio khi container MinIO chưa sẵn sàng.

Khi gặp, chạy riêng lớp test (`--filter "FullyQualifiedName~<Lớp>"`) ba lần và chạy lại cả bước; chỉ coi là lỗi thật nếu đỏ ổn định. Nêu rõ trong báo cáo, không giấu và không "sửa" mã không liên quan.
