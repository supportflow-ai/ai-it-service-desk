# Hướng dẫn làm việc trong kho backend

## 1. Phạm vi và tài liệu

Kho này sở hữu backend, migration, công cụ Database và kiểm thử. Giao diện, hạ tầng toàn hệ thống và dịch vụ AI độc lập thuộc các kho riêng. AGENTS.md quy định cách làm việc, không thay đặc tả kiến trúc.

| Nguồn | Tra cứu |
| --- | --- |
| [README](README.md), [môi trường](docs/runbooks/environment-runbook.md) | Dịch vụ phụ thuộc, chạy local cho người dùng, Python venv và EF CLI |
| [ADR](docs/adr/), [module/use case](docs/architecture/backend-modules-and-use-cases.md) | Kiến trúc mục tiêu và phần đã triển khai |
| [Bản đồ tài liệu](docs/README.md), [hợp đồng hiện hành](docs/architecture/current-backend-contract.md), [Git/Jira](docs/process/pr-branch-jira-convention.md) | Nghiệp vụ, quyền, quy trình; đối chiếu trạng thái phê duyệt |
| [Kiểm chứng](docs/runbooks/backend-hardening-verification.md), [triển khai](docs/runbooks/backend-refactor-rollout.md), [CI](.github/workflows/ci.yml) | Kiểm thử, migration và rollback |

## 2. Lệnh cho trợ lý

Chạy từ gốc repo, không tương tác. SDK theo [global.json](global.json); Python theo workflow; EF CLI khớp `Microsoft.EntityFrameworkCore.Design` trong [project API](src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj). Không chép lại các số phiên bản ở đây.

Build trước khi dùng `--no-build`, cùng mã và cấu hình `Release`:

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

Chạy riêng project, kiểm thử kiến trúc hoặc lọc theo tên (thay `TEN_TEST_DAY_DU` bằng tên thực tế):

```sh
dotnet test tests/ServiceDesk.Application.Tests -c Release --no-build
dotnet test tests/ServiceDesk.ArchitectureTests -c Release --no-build
dotnet test tests/ServiceDesk.Application.Tests -c Release --no-build --filter "FullyQualifiedName=TEN_TEST_DAY_DU"
```

Số kiểm thử thực sự chạy phải lớn hơn 0; lệnh kết thúc thành công nhưng không khớp test không phải bằng chứng đạt. PostgreSQL integration tests cần Docker; kiểm thử kiến trúc chạy offline, không cần database.

Kiểm tra hợp đồng AI bằng interpreter venv, cài dependency trước khi chạy. Chưa có venv thì tạo theo tài liệu môi trường, không cài package vào Python global:

```powershell
./.venv/Scripts/python.exe -m pip install -r contracts/requirements.txt
./.venv/Scripts/python.exe scripts/validate_ai_contract.py
```

Linux/macOS dùng `./.venv/bin/python` thay interpreter trên; lệnh tạo venv có thể cần `python3`.

Chưa có `.cache/dotnet-tools/dotnet-ef` thì dừng lệnh EF và cài **cục bộ** theo tài liệu môi trường, không tự cài/cập nhật tool global. Migration ở Infrastructure, startup project là API, context là `ApplicationDbContext`:

```sh
./.cache/dotnet-tools/dotnet-ef migrations add TEN_MIGRATION --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --context ApplicationDbContext --output-dir Persistence/Migrations --configuration Release
./.cache/dotnet-tools/dotnet-ef migrations remove --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --context ApplicationDbContext --configuration Release
./.cache/dotnet-tools/dotnet-ef migrations has-pending-model-changes --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --context ApplicationDbContext --configuration Release --no-build
```

Các lệnh là lựa chọn theo nhiệm vụ, không chạy nối tiếp. `remove` có thể kết nối database nên chịu cùng chốt chặn ở §5; chỉ gỡ migration cuối chưa merge/phát hành/dùng chung và đã xác minh không còn được áp dụng. Đã thử áp dụng ở local: theo quy trình gỡ trong tài liệu môi trường, không xóa tay hoặc dùng `--force`.

Sau refactor không chủ đích đổi schema, dùng `has-pending-model-changes` để phát hiện lệch mapping, không tạo migration để che lỗi. Nếu `add` sinh thay đổi ngoài dự kiến, dừng và báo. Kiểm tra `Up`/`Down` không có `DROP` hay mất dữ liệu ngoài ý muốn, snapshot chỉ đổi phần được giao; build lại sau sinh/gỡ migration.

## 3. Kiến trúc và ranh giới module

- Duy trì Modular Monolith, Clean Architecture, Layered + Feature Folders; không tạo module rỗng hoặc cố định số module/project theo một đợt refactor.
- Domain không phụ thuộc EF Core, ASP.NET Core hoặc Infrastructure. Application không phụ thuộc Infrastructure, EF Core, Npgsql hoặc HTTP context.
- Application xác thực actor và phân quyền nghiệp vụ, không chỉ dựa vào middleware HTTP. Infrastructure triển khai các cổng giao tiếp.
- Mẫu: [CreateTicketUseCase](src/backend/ServiceDesk.Application/Ticketing/CreateTicket/CreateTicketUseCase.cs), interface có phương thức `ExecuteAsync`, implementation `internal`. Tách ghi/truy vấn; không tự thêm mediator hoặc Generic Repository.
- Dùng gateway chuyên biệt, không đưa `DbSet`/`IQueryable`/transaction EF qua cổng Application. API không truy cập persistence để xử lý nghiệp vụ; Worker không phụ thuộc HTTP context.
- Xuyên module chỉ dùng Published Contracts trong [ModuleRules](tests/ServiceDesk.ArchitectureTests/ModuleRules.cs); `public` không tự cấp quyền. Không đi vòng qua helper, generic hoặc `Common`.
- API/Worker không tự migration/seed khi khởi động.
- Khi sửa luồng tạo phiếu, giữ ticket và receipt trong cùng transaction; kiểm tra rollback, replay và đồng thời theo bảng §6.
- Không tự kết nối AI thật ngoài nhiệm vụ.

## 4. Ngôn ngữ và Git

- Trao đổi, tài liệu mới, mô tả commit và PR bằng tiếng Việt có dấu; hạn chế xen tiếng Anh. Giữ nguyên định danh, câu lệnh và hợp đồng kỹ thuật.
- Conventional Commits: `<type>(<scope>): [TC-<id>] <mô tả tiếng Việt>`. `TC-<id>` là mã Jira thực tế, không tự sinh. Đối chiếu quy ước Git/Jira và chỉ dẫn trực tiếp còn hiệu lực của người dùng.
- Kiểm tra nhánh, HEAD và thay đổi chưa commit trước khi sửa. Chỉ đưa phần thuộc nhiệm vụ vào commit; không dùng `git add -A` khi chưa xác minh và được phép đưa toàn bộ thay đổi vào commit.
- Không tự commit khi chỉ được yêu cầu đánh giá/lập kế hoạch; không tự đổi nhánh đã chỉ định. Nhánh được người dùng chỉ định là ngoại lệ với mẫu đặt tên; không đổi tên để làm đúng mẫu. Ngoại lệ tên nhánh không tự miễn mã Jira cho commit.
- Thiếu mã Jira bắt buộc và chưa có ngoại lệ: hoàn tất chỉnh sửa/kiểm tra rồi hỏi trước khi commit.
- Chỉ sửa AGENTS.md khi nhiệm vụ yêu cầu; ngoại lệ: đổi cấu hình được ghim hoặc lệnh vận hành thì cập nhật hướng dẫn bị ảnh hưởng trong cùng thay đổi.

## 5. Việc phải hỏi trước và giới hạn hành động

### Phạm vi, chính sách và xung đột

- Khi hai nguồn xung đột chưa có căn cứ giải quyết, nêu cả hai, phiên bản/trạng thái, tác động và quyết định cần chốt; hỏi trước khi sửa phần phụ thuộc. Tiếp tục phần độc lập đã được giao.
- Trong nhiệm vụ sửa bảo mật, được từ chối yêu cầu trái phép theo chính sách đã rõ và thêm kiểm thử hồi quy. Hỏi khi cần chọn chính sách mới, như quyền vai trò Agent (nhân viên hỗ trợ) theo chủ sở hữu, team hay assignment. Lỗ hổng không phải hành vi cần giữ.
- Báo lỗi nghiêm trọng ngoài phạm vi kèm bằng chứng và ảnh hưởng; không âm thầm bỏ qua hoặc mở rộng nhiệm vụ.
- Mặc định giữ tương thích API/dữ liệu. Chỉ đổi hợp đồng/đặc tả/tài liệu quy trình khi nhiệm vụ yêu cầu; không sửa kiểm thử, fixture hoặc baseline để hợp thức hóa lỗi hay làm CI đạt.

### Database và môi trường

- Không sửa/xóa/đổi thứ tự migration đã phát hành **hoặc đã áp dụng trong môi trường dùng chung**; không đổi nghĩa enum đã lưu. Schema mới cần migration và phương án tương thích. Không dùng `EnsureCreated` thay `Migrate` để xác minh nâng cấp.
- Trợ lý không chạy lệnh chờ nhập tương tác. Cấu hình phải có qua môi trường/kho bí mật; không yêu cầu gửi bí mật trong chat hoặc giả định biến môi trường tồn tại qua các lần gọi công cụ.
- Trước `migrate`, `seed-roles` hoặc EF truy cập database, đối chiếu `SD_DB_HOST`/`SD_DB_PORT`/`SD_DB_NAME` không bí mật với đích EF thực tế bằng phép kiểm tra chỉ in host/cổng/tên database trong tài liệu môi trường. Không in chuỗi kết nối hay đầu ra EF thô khi lỗi.
- Xác minh thêm container/tiến trình PostgreSQL local nghe đúng cổng; `docker ps` phải dùng Docker context local. Không có bằng chứng, hoặc chỉ thấy tunnel/proxy chưa xác định đích: dừng thao tác database và hỏi. Nhãn Development hay `localhost` riêng lẻ không đủ.
- Database dev local không mặc nhiên là dữ liệu dùng một lần. Chỉ reset hoặc hạ migration khi có ủy quyền cho đúng đích và tác động mất dữ liệu; nếu chưa được phép, giữ migration và đề xuất migration sửa bổ sung.
- Không tự chạy `seed-demo`, `bootstrap-admin`, `check-production` hoặc thao tác vào staging/production; cần yêu cầu rõ lệnh và đích. `check-production` không được coi là chỉ đọc; xem tài liệu môi trường.
- API/Worker chỉ khởi chạy khi nhiệm vụ cần: chạy nền bằng công cụ của môi trường, lưu PID hoặc ID phiên công cụ, dừng tiến trình do mình tạo khi xong; không chờ vô hạn.

### Thao tác làm mất dữ liệu hoặc thay đổi bên ngoài

- Không tự ghi đè thay đổi người khác, `reset --hard`, `clean`, force-push, xóa volume hoặc migration `Down` trên dữ liệu thật. `checkout`/`restore` bị chặn khi ghi đè tệp có thay đổi chưa commit, không cấm tạo nhánh. Cần ủy quyền rõ cho thao tác có thể mất dữ liệu; database kiểm thử được xác nhận dùng một lần được xử lý trong phạm vi test.
- Không tự push, hợp nhất PR hoặc triển khai production khi chưa có ủy quyền tương ứng. Không đưa bí mật vào Git/nhật ký hoặc nới lỏng hướng dẫn để né giới hạn.

## 6. Kiểm chứng và bàn giao

Thay đổi chạm nhiều dòng phải chạy hợp các nhóm kiểm tra tương ứng:

| Thay đổi | Kiểm tra cần chạy |
| --- | --- |
| Công cụ kiểm tra tài liệu/workflow tài liệu | Validator và các tình huống hồi quy của công cụ; kiểm tra lệnh/cấu trúc workflow, không tự chạy lại image không chịu ảnh hưởng |
| Chỉ tài liệu | `python scripts/validate_project_docs.py`, đối chiếu câu lệnh/cấu hình; không bắt buộc toàn bộ suite |
| Nghiệp vụ/phân quyền | Unit/use case và kiểm thử hồi quy HTTP liên quan; quyền cần trường hợp bị từ chối |
| Endpoint/DTO/response HTTP | Kiểm thử hợp đồng HTTP/OpenAPI, JSON, status/header và tương thích caller liên quan |
| Ranh giới tầng/module | Project ArchitectureTests |
| Persistence/migration/transaction/đồng thời | Integration PostgreSQL thật; không thay bằng SQLite hoặc EF InMemory |
| Contract/fixture/mapper AI hoặc validator | Unit/adapter tương ứng và `scripts/validate_ai_contract.py` bằng Python venv theo tài liệu môi trường |
| Dockerfile/build/dependency/configuration | Restore khóa, build, test liên quan và build image API/Worker/Database chịu ảnh hưởng theo workflow |

Khi bàn giao thay đổi chức năng, đối chiếu đủ các bước workflow và tiêu chí nghiệm thu; không tự thêm phát hành image/deploy.

Gắn kết quả với SHA và thay đổi chưa commit nếu có. Không dùng báo cáo commit cũ để xác nhận HEAD mới; nhãn `APPROVED`/`Proposed` không chứng minh đã triển khai. Smoke toàn hệ thống thuộc kho hạ tầng, phải dùng đúng SHA backend.

Bàn giao ngắn: tệp/phạm vi chính, cách sửa/hợp đồng chịu ảnh hưởng, lệnh và số test thực chạy/kết quả, kiểm tra chưa chạy/lý do, giới hạn, nhánh/SHA và trạng thái push/PR. CI đạt không tự chứng minh đã nghiệm thu đầy đủ; không tuyên bố hoàn thành khi thiếu bằng chứng.
