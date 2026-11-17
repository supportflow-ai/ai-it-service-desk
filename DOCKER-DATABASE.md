# PostgreSQL Docker local cho thành viên

Trạng thái: hướng dẫn phát triển local, ngày 16/11/2026. Không dùng cấu hình này để triển khai VPS hoặc production.

## Phạm vi

Mỗi thành viên có database riêng, tạo từ migration của checkout backend và seed ba tài khoản demo. Không tải dữ liệu từ VPS hoặc máy của thành viên khác. Dữ liệu tự tạo không đồng bộ giữa các máy. Volume giữ dữ liệu khi dừng container.

Cần Docker với Linux containers và Docker Compose v2. Không cần cài PostgreSQL hoặc .NET trên máy để tạo database. Lần build đầu cần Internet để tải image và NuGet. Chạy mọi lệnh bên dưới từ gốc repo `ai-it-service-desk`.

## 1. Chuẩn bị cấu hình

PowerShell:

```powershell
Copy-Item .env.local-db.example .env.local-db
```

Linux/macOS:

```bash
cp .env.local-db.example .env.local-db
```

Chỉ sao chép khi chưa có `.env.local-db`, không ghi đè cấu hình cá nhân đã tồn tại. Mở file, đổi `LOCAL_DB_PASSWORD` và `LOCAL_DEMO_PASSWORD`. Mật khẩu demo cần đáp ứng chính sách Identity: ít nhất 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt. Không dùng mật khẩu VPS/production. Nên dùng chữ, số và ký tự `!@_-` để tránh ký tự phân cách connection string hoặc nội suy Compose. File `.env.local-db` bị Git ignore; không commit hoặc chia sẻ bí mật.

Cổng mặc định là `15434`, khác tunnel VPS `15432` và database thủ công `15433` đã dùng trước đó. Nếu cổng bận, đổi `LOCAL_DB_PORT` và cập nhật connection string tương ứng. Database chỉ lắng nghe trên `127.0.0.1` của máy thành viên.

## 2. Tạo database và khởi tạo schema

```sh
docker compose --env-file .env.local-db -f compose.local-db.yml -p supportflow-local-db up -d --wait postgres
docker compose --env-file .env.local-db -f compose.local-db.yml -p supportflow-local-db run --build --rm init
```

Chỉ tiếp tục khi cả hai lệnh thành công. Service `init` build Dockerfile Migrator hiện có, rồi lần lượt chạy `migrate`, `seed-roles`, `seed-demo`; gặp lỗi sẽ dừng. Schema lấy từ migration, không từ SQL viết tay. Lệnh seed chỉ tạo tài khoản chưa tồn tại, không đặt lại mật khẩu hoặc đổi quyền tài khoản cũ.

| Thuộc tính | Giá trị |
| --- | --- |
| Host từ máy phát triển | `127.0.0.1` |
| Port mặc định | `15434` |
| Database | `servicedesk_dev` |
| User local | `servicedesk_local` |
| Mật khẩu DB | `LOCAL_DB_PASSWORD` trong file cá nhân |
| Tài khoản demo | `admin@test.com`, `agent@test.com`, `requester@test.com` |
| Mật khẩu demo | `LOCAL_DEMO_PASSWORD` tại lần tạo tài khoản đầu tiên |

Tài khoản DB local có quyền khởi tạo schema, chỉ phục vụ môi trường riêng. Không áp dụng mô hình quyền này cho database dùng chung hoặc production.

## 3. Kết nối backend và công cụ quản lý DB

Trong `.env` backend cá nhân, giữ cấu hình JWT và dịch vụ khác, thay riêng connection string bằng giá trị local. Điền mật khẩu thật từ `.env.local-db`, không giữ nguyên placeholder:

```dotenv
Database__ConnectionString=Host=127.0.0.1;Port=15434;Database=servicedesk_dev;Username=servicedesk_local;Password=<LOCAL_DB_PASSWORD>
SD_DB_HOST=127.0.0.1
SD_DB_PORT=15434
SD_DB_NAME=servicedesk_dev
```

Biến môi trường đã đặt trong terminal được ưu tiên hơn `.env`; bỏ cấu hình connection string VPS cũ khỏi terminal trước khi khởi chạy. Khởi động lại API/Worker để dùng DB mới, theo [hướng dẫn môi trường](docs/runbooks/environment-runbook.md). Không cần SSH tunnel cho DB local. Frontend vẫn gọi backend, không kết nối PostgreSQL trực tiếp.

DBeaver/pgAdmin dùng cùng host, port, database, user và mật khẩu local ở trên.

## 4. Kiểm tra

```sh
docker compose --env-file .env.local-db -f compose.local-db.yml -p supportflow-local-db ps
docker compose --env-file .env.local-db -f compose.local-db.yml -p supportflow-local-db exec -T postgres psql -X -U servicedesk_local -d servicedesk_dev -v ON_ERROR_STOP=1 -c 'SELECT COUNT(*) FROM public."__EFMigrationsHistory";'
docker compose --env-file .env.local-db -f compose.local-db.yml -p supportflow-local-db exec -T postgres psql -X -U servicedesk_local -d servicedesk_dev -v ON_ERROR_STOP=1 -c 'SELECT COUNT(*) FROM public."AspNetUsers";'
```

Trên database mới, có 3 tài khoản demo sau seed. Số migration thay đổi theo phiên bản backend, không ghim số lượng trong quy trình. Kiểm tra thêm `/health` và đăng nhập bằng tài khoản demo sau khi API đã trỏ DB local. Health đạt không thay thế kiểm thử nghiệp vụ.

## 5. Cập nhật và dừng

Sau khi pull phiên bản backend có migration mới, chạy lại lệnh `run --build --rm init` ở mục 2. Migration chỉ áp dụng phần chưa có; seed không ghi đè tài khoản cũ. Sao lưu trước nếu dữ liệu local cần giữ. Không chạy hai phiên bản backend khác nhau trên cùng database khi chưa kiểm tra tương thích.

Dừng container, giữ volume:

```sh
docker compose --env-file .env.local-db -f compose.local-db.yml -p supportflow-local-db down
```

Chạy lại hai lệnh mục 2 để tiếp tục. Không dùng `down -v` nếu cần giữ dữ liệu: tùy chọn đó xóa volume database. Đổi mật khẩu trong file không tự thay mật khẩu PostgreSQL hoặc tài khoản demo đã tồn tại; cần quy trình đổi mật khẩu phù hợp, không xóa volume để né lỗi.

## Giới hạn

Đã kiểm tra ngày 16/11/2026: build image Migrator thành công; khởi tạo database mới và chạy lại `init` đều đạt, giữ 15 migration, 3 user, 3 role và cột `refresh_tokens.family_id`. Runtime hiện in cảnh báo thiếu `libgssapi_krb5.so.2` khi kết nối; trong kiểm tra này cả ba lệnh vẫn hoàn thành với exit code 0. Chưa kiểm chứng Linux/macOS host hoặc kiến trúc ARM.

Đây là database local, không khởi chạy API, Worker, AI hoặc kho đối tượng. Image `postgres:17` giữ phiên bản major nhưng có thể cập nhật bản vá. Không đóng gói dữ liệu cá nhân vào image; việc chuyển dữ liệu lên VPS phải dùng backup/restore và quy trình vận hành riêng.
