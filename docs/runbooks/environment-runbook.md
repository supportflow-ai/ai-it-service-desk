# Môi trường phát triển và công cụ backend

## Phạm vi

Compose, PostgreSQL/MinIO local, gateway và smoke toàn hệ thống thuộc kho [supportflow-ai-infra](https://github.com/supportflow-ai/supportflow-ai-infra). Dùng cấu hình local ở kho đó hoặc dịch vụ tương đương; không lấy báo cáo sprint cũ làm hướng dẫn chạy hiện tại.

Phiên bản công cụ lấy trực tiếp từ nguồn được ghim, không duy trì thêm một bản số phiên bản tại đây:

- .NET SDK: [global.json](../../global.json).
- Python: `python-version` trong [workflow](../../.github/workflows/ci.yml).
- EF CLI: `Microsoft.EntityFrameworkCore.Design` trong [project API](../../src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj).

Cần build `Release` thành công từ gốc repo trước các lệnh dùng `--no-build`. Lệnh build/test đầy đủ ở [AGENTS.md](../../AGENTS.md).

## Nạp cấu hình và khởi chạy

Các công cụ Python, kiểm thử và hướng dẫn trong phần này được công bố cùng nhau; hai quy tắc bỏ qua công cụ đã được gỡ khỏi `.gitignore` kế thừa từ dev. Đối chiếu [bảng mốc nguồn](../architecture/source-baseline.md#công-cụ-local-và-quy-tắc-git) và chuẩn bị cấu hình riêng cho mỗi máy.

Các lệnh sau chạy từ gốc repo. Hướng dẫn bắt đầu, cách lấy JWT và giải thích Release/lockfile nằm trong [README](../../README.md). Trợ lý không chạy lệnh chờ nhập tương tác hoặc giả định biến môi trường tồn tại qua các lần gọi công cụ.

### Cấu hình bằng .env

Nếu chưa có `.env`, sao chép [.env.example](../../.env.example) thành `.env`, điền kết nối PostgreSQL và khóa ký JWT được cấp. Không ghi đè file của môi trường đang dùng. File `.env` bị Git ignore; không gửi nội dung vào chat hoặc đưa vào log/commit. Các project chưa khai báo `UserSecretsId` và .NET không tự nạp `.env`.

[Công cụ chạy backend](../../scripts/run_backend.py) hỗ trợ:

- Nạp `.env` ở gốc repo nếu file tồn tại, hoặc `--env-file DUONG_DAN` được chỉ định. Chỉ định file không tồn tại là lỗi; không chỉ định file và không có `.env` thì dùng biến môi trường của tiến trình.
- Cú pháp `KEY=value`, dòng trống, dòng chú thích bắt đầu bằng `#`, dấu nháy bao quanh giá trị và UTF-8 có/không BOM. Không thực thi shell, nội suy biến, xử lý cú pháp `export` hoặc chú thích cuối dòng. Đặt chú thích trên dòng riêng.
- Ưu tiên: khóa .NET chuẩn thắng tên cũ trong cùng nguồn; môi trường tiến trình thắng file, kể cả khi môi trường dùng tên cũ. Không thay biến môi trường toàn máy.
- Tên cũ được ánh xạ: `DATABASE_CONNECTION_STRING` → `Database__ConnectionString`; `JWT_SECRET_KEY`, `JWT_ISSUER`, `JWT_AUDIENCE`, `JWT_EXPIRY_MINUTES` → các khóa `Jwt__...`; `MINIO_ENDPOINT`, `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `MINIO_BUCKET`, `MINIO_USE_SSL` → các khóa `Minio__...`. Dùng tên chuẩn cho cấu hình mới.
- API bắt buộc chuỗi kết nối và khóa ký JWT ít nhất 32 byte UTF-8, không để giá trị mẫu. Worker và lệnh Database thông thường cần chuỗi kết nối. Kiểm tra này không chứng minh credential đúng hoặc dịch vụ sẵn sàng; validator .NET tiếp tục kiểm tra cấu hình JWT khi API khởi động.
- API đăng nhập/ticket hiện chưa gọi lưu tệp, nên MinIO chưa bắt buộc cho các luồng này. Khi dùng lưu tệp phải có endpoint và credential MinIO thật; readiness hiện chỉ kiểm tra PostgreSQL.
- Mặc định Development nếu cả hai biến môi trường đều vắng. Khi chỉ có một trong `DOTNET_ENVIRONMENT`/`ASPNETCORE_ENVIRONMENT`, công cụ đặt biến còn lại cùng giá trị; khi cả hai có nhưng khác nhau, dừng với mã 2. Không tự đổi Production đã được cấu hình thành Development.
- API mặc định `ASPNETCORE_URLS=http://localhost:5000` nếu chưa có. URL này ưu tiên hơn `ASPNETCORE_HTTP_PORTS`; đặt URL rõ ràng khi muốn cổng khác.
- Gọi `dotnet run -c Release --no-launch-profile`; không nạp ngầm `launchSettings.json`. Tuỳ chọn của công cụ phải nằm trước tên thành phần; phần sau tên thành phần được chuyển cho ứng dụng.

Sau locked restore và build Release thành công, chạy ở các terminal riêng:

```sh
python scripts/run_backend.py --no-build api
python scripts/run_backend.py --no-build worker
```

Đổi file cấu hình hoặc cho phép build lúc chạy:

```sh
python scripts/run_backend.py --env-file .env --no-build api
python scripts/run_backend.py api
```

Linux/macOS có thể cần `python3`. Dừng bằng Ctrl+C. Nếu nhiệm vụ cần trợ lý chạy host, chạy nền bằng công cụ môi trường, lưu PID/ID phiên, kiểm tra hữu hạn rồi dừng tiến trình do mình tạo.

Đọc `.env` không tự mở tunnel hoặc chạy migration/seed. Database dùng chung có quy trình riêng tại [runbook VPS](shared-development-database.md). Mở tunnel bằng `python scripts/start_database_tunnel.py` ở terminal riêng trước API. Với tài khoản chỉ mở tunnel, không thử mở shell trên VPS. Loopback của tunnel vẫn là database từ xa, không phải database local dùng một lần.

Thành phần `database` chuyển tham số sang công cụ Database, ví dụ `python scripts/run_backend.py --no-build database migrate`; chỉ chạy khi nhiệm vụ và đích được xác minh theo phần dưới. Lệnh `seed-demo`/`bootstrap-admin` cần cấu hình riêng theo quy trình triển khai, không truyền credential provisioning sang API/Worker.

### Không dùng Python

Người dùng có thể cấp biến môi trường cho tiến trình bằng công cụ quản lý bí mật của mình rồi gọi trực tiếp .NET. Phải dùng tên cấu hình chuẩn, ví dụ `Database__ConnectionString`, `Jwt__SecretKey`; tên cũ viết hoa chỉ được công cụ Python chuyển đổi. Mở terminal mới phải nạp lại cấu hình. Các lệnh .NET trực tiếp phía dưới không tự nạp `.env`.

### Trước khi ghi vào database

Người dùng cung cấp riêng metadata **không bí mật** `SD_DB_HOST`, `SD_DB_PORT`, `SD_DB_NAME` của đích được phép. Đây là thông tin để đối chiếu, không thay thế cấu hình `Database__ConnectionString` và không tự cấp quyền ghi.

Ví dụ metadata cho local; điều chỉnh theo đích được giao:

```powershell
$env:SD_DB_HOST = '127.0.0.1'
$env:SD_DB_PORT = '5432'
$env:SD_DB_NAME = 'servicedesk'
```

Bash dùng `export SD_DB_HOST=127.0.0.1 SD_DB_PORT=5432 SD_DB_NAME=servicedesk`. Các biến này và cấu hình ứng dụng phải có trong cùng lần gọi công cụ kiểm tra/thao tác.

Sau build và cài EF CLI cục bộ, đối chiếu đích thực tế mà không in chuỗi kết nối. Khối sau dùng Python stdlib, chạy được trên Linux/macOS; PowerShell thay `python3` bằng `python` đúng phiên bản. Nội dung chuỗi Python dùng chung cho hai shell:

```sh
python3 -c '
import json, os, subprocess, sys
from pathlib import Path
from urllib.parse import urlsplit
try:
    tool = Path(".cache/dotnet-tools/dotnet-ef" + (".exe" if os.name == "nt" else ""))
    result = subprocess.run([str(tool), "dbcontext", "info",
        "--project", "src/backend/ServiceDesk.Infrastructure",
        "--startup-project", "src/backend/ServiceDesk.Api",
        "--context", "ApplicationDbContext", "--configuration", "Release",
        "--no-build", "--json"], stdin=subprocess.DEVNULL,
        capture_output=True, text=True, timeout=30)
    if result.returncode != 0:
        raise ValueError()
    info = json.loads(result.stdout)
    endpoint = urlsplit(info["dataSource"])
    host = endpoint.hostname
    expected_host = os.environ["SD_DB_HOST"].strip("[]")
    expected_port = int(os.environ["SD_DB_PORT"])
    expected_name = os.environ["SD_DB_NAME"]
    if (endpoint.scheme != "tcp" or endpoint.username or endpoint.password
        or not host or not expected_host or not expected_name
        or not 1 <= expected_port <= 65535
        or host.casefold() != expected_host.casefold()
        or endpoint.port != expected_port or info["databaseName"] != expected_name):
        raise ValueError()
    print(json.dumps({"host": host, "port": endpoint.port, "database": info["databaseName"]}))
except Exception:
    print("db_target_unverified", file=sys.stderr)
    sys.exit(1)
'
```

Đầu ra chỉ có host/cổng/tên database. Lỗi CLI, JSON không hợp lệ hoặc metadata không khớp đều trả mã lỗi `db_target_unverified` và dừng; không in stdout/stderr thô của EF. Phép kiểm tra này đọc cấu hình qua context của repo hiện tại, không mở kết nối database; nếu sửa bootstrap có tác dụng phụ, phải đánh giá lại trước khi dùng.

Với PostgreSQL riêng trên máy phát triển, tiếp tục kiểm tra tài nguyên local như bên dưới. Trường hợp kết nối cơ sở dữ liệu dùng chung qua SSH theo [hướng dẫn VPS](shared-development-database.md) chỉ cấp đường truyền cho ứng dụng; không áp dụng các lệnh migration của mục local cho đích dùng chung. Thao tác quản trị đích đó đi theo người phụ trách, tài khoản owner và quy trình riêng trong hướng dẫn VPS.

Không suy ra đích là local chỉ từ metadata khớp:

```sh
docker context show
docker context inspect --format "{{.Endpoints.docker.Host}}"
docker ps --format "{{.ID}}\t{{.Names}}\t{{.Ports}}"
```

Docker context phải trỏ tới Docker local, không phải daemon từ xa. Đối chiếu container PostgreSQL và cổng publish với metadata; không dùng `docker inspect` toàn bộ vì có thể lộ biến môi trường.

Nếu dùng PostgreSQL cài trực tiếp, kiểm tra listener và **tên/PID** tiến trình, không in command line chứa credential. PowerShell dùng `Get-NetTCPConnection -State Listen -LocalPort <CONG>` rồi `Get-Process -Id <PID>`; Linux dùng `ss -ltnp`, macOS dùng `lsof -nP -iTCP:<CONG> -sTCP:LISTEN`, thay placeholder bằng cổng/PID thực tế.

Chỉ chạy `migrate`, `seed-roles`, `migrations remove` hoặc EF khác cần truy cập database khi nhiệm vụ cần và đã xác nhận:

- Host là `localhost`, `127.0.0.1`, `::1` hoặc container local có bằng chứng từ cấu hình/kiểm tra Docker.
- Tên database, chủ sở hữu và mục đích đúng môi trường phát triển/kiểm thử được giao.
- Không qua tunnel/proxy/port-forward tới staging, production hoặc database dùng chung. Host loopback hay nhãn Development riêng lẻ không chứng minh database là local.

Host local mà không xác minh được container hoặc tiến trình PostgreSQL nghe đúng cổng thì coi là chưa rõ đích và dừng. Nếu listener là SSH/kubectl/proxy, phải làm rõ đích trước; việc có listener không tự chứng minh an toàn.

Không rõ một điều kiện thì dừng thao tác database và hỏi người dùng; tiếp tục việc độc lập. Đích dùng chung cần ủy quyền riêng theo [quy trình triển khai](backend-refactor-rollout.md). Không in chuỗi kết nối đầy đủ để báo cáo bằng chứng.

Sau khi xác nhận đích và build thành công:

```sh
dotnet run --project src/backend/ServiceDesk.Database -c Release --no-build -- migrate
dotnet run --project src/backend/ServiceDesk.Database -c Release --no-build -- seed-roles
```

API/Worker không tự migration hoặc seed. Khởi chạy từng tiến trình trong terminal riêng đã nạp cấu hình:

```sh
dotnet run --project src/backend/ServiceDesk.Api -c Release --no-build --no-launch-profile
dotnet run --project src/backend/ServiceDesk.Worker -c Release --no-build
```

Hai lệnh trên chạy liên tục: người dùng dừng bằng Ctrl+C. Nếu nhiệm vụ yêu cầu trợ lý khởi chạy, chạy nền bằng công cụ của môi trường, lưu PID hoặc ID phiên công cụ, kiểm tra đầu ra với thời gian chờ hữu hạn và dừng tiến trình do mình tạo khi hoàn tất.

## Python venv và hợp đồng AI

Không cần kích hoạt venv, gọi thẳng interpreter để tránh phụ thuộc terminal hiện tại. `.venv/` và `.cache/` đã được bỏ qua trong [.gitignore](../../.gitignore).

PowerShell, chọn `python` đúng phiên bản theo workflow:

```powershell
python -m venv .venv
./.venv/Scripts/python.exe -m pip install -r contracts/requirements.txt
./.venv/Scripts/python.exe scripts/validate_ai_contract.py
```

Linux/macOS thường dùng `python3`; nếu trỏ sai phiên bản, chọn interpreter tương ứng trước khi tạo venv:

```sh
python3 -m venv .venv
./.venv/bin/python -m pip install -r contracts/requirements.txt
./.venv/bin/python scripts/validate_ai_contract.py
```

Chạy validator khi thay contract/schema/fixtures/mapper AI hoặc chính validator; ghép với unit/adapter tests liên quan theo workflow. Không gọi dịch vụ AI thật để kiểm chứng contract offline.

## EF CLI và migration

Cài EF CLI trong `.cache/dotnet-tools`, phiên bản lấy từ project API; không cập nhật tool global chỉ để làm công việc này.

PowerShell:

```powershell
$apiProject = [xml](Get-Content -LiteralPath 'src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj' -Raw)
$efVersion = [string]($apiProject.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Microsoft.EntityFrameworkCore.Design').Version
dotnet tool install dotnet-ef --tool-path .cache/dotnet-tools --version $efVersion
```

Bash, dùng interpreter venv vừa tạo để đọc XML:

```bash
ef_version=$(./.venv/bin/python -c 'import xml.etree.ElementTree as E; p=E.parse("src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj"); print(next(n.attrib["Version"] for n in p.iter("PackageReference") if n.attrib.get("Include")=="Microsoft.EntityFrameworkCore.Design"))')
dotnet tool install dotnet-ef --tool-path .cache/dotnet-tools --version "$ef_version"
```

Nếu tool đã tồn tại, thay `install` bằng `update` với cùng đường dẫn/phiên bản. Nếu chưa cài được tool cục bộ, dừng lệnh EF, báo thiếu công cụ; không thay bằng cài/cập nhật global. Migration ở Infrastructure; API là startup project, context là `ApplicationDbContext`; lệnh thêm/gỡ/kiểm tra model ở AGENTS.md.

### Phát hiện lệch mapping khi refactor

`migrations has-pending-model-changes` hoạt động trên repo với EF CLI được ghim: so model với snapshot, không cập nhật database. Dùng sau build; với kiểm tra offline có thể dùng cấu hình Development giả ở tiến trình riêng, không tái sử dụng cấu hình giả cho thao tác database.

Nếu refactor không chủ đích đổi schema mà lệnh phát hiện chênh lệch, dừng và báo phần mapping bị lệch; không sinh migration để che lỗi. Nếu nhiệm vụ đổi schema có chủ đích, chỉ tạo migration đúng phạm vi. Khi `migrations add` sinh thay đổi ngoài dự kiến, không áp dụng; báo và xử lý nguyên nhân trước.

Kiểm tra `Up`/`Down` không có `DROP` hoặc mất dữ liệu ngoài ý muốn; snapshot chỉ đổi đúng phần được giao. Build lại sau khi sinh/gỡ migration. Không sửa migration đã phát hành hoặc đã áp dụng ở môi trường dùng chung.

### Gỡ migration mới chưa phát hành

`migrations remove` mặc định có thể kết nối database để kiểm tra lịch sử; phải qua cùng bước xác minh đích như `migrate`. Không dùng `--force` hoặc xóa file/snapshot bằng tay.

- Migration cuối chưa merge/phát hành/dùng chung, chưa áp dụng: xác minh trạng thái rồi dùng `migrations remove`.
- Đã áp dụng ở môi trường dùng chung: giữ nguyên, tạo migration sửa bổ sung theo nhiệm vụ; không gỡ.
- Đã áp dụng **chỉ ở local riêng**: local dev không mặc nhiên là dùng một lần. Mặc định giữ nguyên và đề xuất migration sửa bổ sung. Chỉ hạ/gỡ khi người dùng đã cho phép đúng đích và ảnh hưởng dữ liệu.

Nếu được phép hạ local: dừng ứng dụng liên quan, sao lưu và xác minh phục hồi nếu có dữ liệu cần giữ, xem kỹ `Down`, xác định chính xác migration trước đó rồi chạy:

```sh
./.cache/dotnet-tools/dotnet-ef database update MIGRATION_TRUOC --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --context ApplicationDbContext --configuration Release
```

`MIGRATION_TRUOC` là ID thực tế của migration liền trước, không tự dùng `0`. Chỉ sau khi xác minh hạ thành công và migration không còn trong lịch sử local mới chạy `migrations remove`. Nếu hạ bị lỗi, dừng; không cưỡng ép gỡ. Sau đó build và kiểm tra model lại.

Nếu database được **xác nhận rõ là dùng một lần** và được phép reset, có thể tái tạo trong phạm vi đã cho phép; không suy ra quyền xóa database/volume khác. Quy trình này không cho phép tự hạ migration hoặc reset database dev có dữ liệu cần giữ.

## Kiểm thử kiến trúc đang chạy thế nào

[Project ArchitectureTests](../../tests/ServiceDesk.ArchitectureTests/ServiceDesk.ArchitectureTests.csproj) dùng xUnit qua `dotnet test`, NetArchTest và Mono.Cecil để đọc assembly; không cần database, Docker hoặc API đang chạy.

```sh
dotnet build tests/ServiceDesk.ArchitectureTests -c Release --no-restore
dotnet test tests/ServiceDesk.ArchitectureTests -c Release --no-build
```

Cần locked restore trước build nếu chưa restore. Bộ kiểm thử kiểm tra chiều phụ thuộc tầng, use case đúng assembly, API/Worker không vượt ranh giới, danh sách Published Contracts và ownership. Các mẫu cố tình vi phạm chứng minh rule bắt được helper/API bypass, generic `Set<T>` và namespace chưa đăng ký. Xem [ModuleRules](../../tests/ServiceDesk.ArchitectureTests/ModuleRules.cs) và [negative tests](../../tests/ServiceDesk.ArchitectureTests/ModuleRuleNegativeTests.cs).

Khi lọc riêng kiểm thử, thay placeholder bằng tên thực tế và kiểm tra tổng số test chạy lớn hơn 0; exit code thành công với 0 test không chứng minh đạt.

## Các lệnh tài khoản và check-production

Trợ lý chỉ chạy `seed-demo`, `bootstrap-admin`, `check-production` khi được giao rõ lệnh và đích; không suy ra quyền chạy từ tên lệnh hoặc từ việc đang viết tài liệu. Thông tin bootstrap chỉ cấp cho tiến trình một lần, không truyền sang API/Worker. Điều kiện `SeedDemo__*` và `BootstrapAdmin__*` nằm trong quy trình triển khai.

Hành vi hiện có của `check-production`, đối chiếu [DatabaseMaintenance](../../src/backend/ServiceDesk.Infrastructure/Persistence/DatabaseMaintenance.cs) và [Database Program](../../src/backend/ServiceDesk.Database/Program.cs):

- Chỉ tìm ba email trong `DemoEmails`: `admin@test.com`, `agent@test.com`, `requester@test.com`.
- Với tài khoản tồn tại, gọi `UserManager.CheckPasswordAsync` bằng mật khẩu mẫu đã có trong mã; nếu còn khớp, dừng với `demo_default_password_present` và exit code 1.
- Nếu không phát hiện mật khẩu mẫu, exit code 0; tài khoản không tồn tại được bỏ qua. Lỗi khác trả 1, cách gọi không hợp lệ trả 2.
- Không kiểm tra đầy đủ role, JWT, migration, lockout hoặc mọi tài khoản; exit code 0 không phải chứng nhận an toàn production.
- Không coi là chỉ đọc: Identity có thể ghi lại hash khi cần nâng cấp định dạng/chi phí băm. Hành vi này được xác nhận trong [mã nguồn UserManager của ASP.NET Core](https://github.com/dotnet/aspnetcore/blob/v10.0.13/src/Identity/Extensions.Core/src/UserManager.cs#L701-L738); đối chiếu lại khi nâng dependency.

Khi lệnh báo tài khoản mẫu, dừng nghiệm thu và xử lý mật khẩu/role theo quy trình quản trị. Không tự xóa hoặc nâng quyền tài khoản. Không chạy lệnh này trong đợt chỉ chỉnh tài liệu.
