# Database phát triển dùng chung trên VPS

Trạng thái: hướng dẫn kết nối kèm hồ sơ thiết lập môi trường phát triển ngày 2026-10-19. Các số lượng, phiên bản, đường dẫn triển khai và bằng chứng bên dưới thuộc mốc bàn giao đó; đợt rà soát tài liệu ngày 2026-10-20 không kết nối VPS để xác minh lại. Đây không phải production hoặc ủy quyền cho thao tác vận hành mới.

## Địa chỉ và dữ liệu

| Thành phần | Giá trị |
| --- | --- |
| VPS | `103.130.214.110`, Ubuntu 24.04 |
| PostgreSQL | 17.11, container `supportflow-postgres-dev` |
| Database | `servicedesk_dev` |
| Tài khoản ứng dụng | `servicedesk_app`, không superuser hoặc tạo bảng |
| Tài khoản migration | `servicedesk_owner`, tách khỏi ứng dụng |
| Cổng trên VPS | `127.0.0.1:5432`, không publish ra địa chỉ công khai |
| Địa chỉ trên máy phát triển | `127.0.0.1:15432` qua SSH tunnel |
| Volume | `supportflow_shared_dev_pgdata` |
| Cấu hình triển khai thực tế | `/opt/supportflow/shared-db/compose.shared-db.yml` trên VPS |
| Bản sao lưu ban đầu | `/opt/supportflow/shared-db/backups/servicedesk_dev-initial.dump` |

Image PostgreSQL đã được ghim digest trong cấu hình trên VPS. Container tự khởi động lại; volume giữ dữ liệu riêng. Cấu hình giới hạn RAM 384 MiB và 30 kết nối; chuỗi kết nối local giới hạn pool 5 kết nối mỗi tiến trình, phù hợp VPS khoảng 1 GB cho mục đích phát triển. Cấu hình chạy hệ thống thuộc phạm vi hạ tầng; không đưa mật khẩu hoặc tệp Compose có credential vào kho backend.

Schema được khởi tạo bằng công cụ `ServiceDesk.Database` từ mã backend `334485d`, giữ nguyên migration. Tại thời điểm bàn giao: 5 migration, 10 bảng, 3 role Requester/Agent/Admin, 0 user và 0 ticket. Không chạy seed-demo hoặc bootstrap-admin. Tài khoản ứng dụng không có quyền trên bảng lịch sử migration.

## Chạy trên máy phát triển

Máy được thiết lập ở mốc bàn giao đã có `.env` chứa chuỗi kết nối và metadata `DatabaseTunnel__*`. Mỗi thành viên hoặc checkout mới phải nhận cấu hình qua kênh được cấp, không giả định Git cung cấp sẵn `.env`. Mật khẩu không được in hoặc đưa vào Git. Khóa SSH nằm trên máy của thành viên; mỗi máy cần đường dẫn khóa và quyền SSH phù hợp.

Mở terminal ở gốc backend để duy trì tunnel:

```sh
python scripts/start_database_tunnel.py
```

Terminal khác chạy API, sau khi build Release thành công:

```sh
python scripts/run_backend.py --no-build api
```

Linux/macOS có thể cần `python3`. Không mở thêm tunnel nếu cổng 15432 đã được tiến trình tunnel của mình sử dụng. Công cụ không tự bỏ qua xác minh host key; máy mới phải xác minh khóa máy chủ qua nguồn tin cậy trước khi kết nối.

Nếu SSH bị ngắt do mạng, công cụ thử kết nối lại tối đa 5 lần liên tiếp với thời gian chờ tăng dần. Sau phiên ổn định ít nhất một phút, bộ đếm lỗi liên tiếp được đặt lại. Lỗi quyền, host key hoặc cổng đã bị sử dụng phải được xử lý trực tiếp; không tự bỏ qua kiểm tra bảo mật. Ctrl+C dừng tunnel. Khi tunnel mất và chưa khôi phục, các thao tác cần database có thể trả lỗi máy chủ dù JWT vẫn hợp lệ.

Worker chạy tương tự với thành phần `worker`. Với DBeaver/pgAdmin trên máy đã mở tunnel, dùng host `127.0.0.1`, cổng `15432`, database `servicedesk_dev`, user `servicedesk_app` và mật khẩu từ cấu hình local được cấp. Không dùng địa chỉ công khai của VPS làm host PostgreSQL trực tiếp.

## Migration và sao lưu

Không dùng tài khoản ứng dụng để migration. Chỉ người được giao vận hành chạy công cụ bằng credential owner trên VPS, sau khi xác minh đích, phiên bản và bản sao lưu. Cả nhóm thống nhất migration trước khi áp dụng; không tự chạy các migration thử nghiệm của nhánh cá nhân lên database chung.

Công cụ migration Linux tự chứa runtime đã được build trên máy local và đặt tại `/opt/supportflow/shared-db/database-tool-linux/`. Không cài .NET toàn máy; image runtime Microsoft tải không thành công do kết nối bị ngắt. Credential owner/admin được giữ trong tệp chỉ root đọc tại thư mục triển khai, không truyền cho API/Worker.

Bản sao lưu ban đầu đã kiểm tra đọc danh mục bằng `pg_restore --list`, chưa xác nhận phục hồi toàn bộ. Chưa thiết lập lịch sao lưu định kỳ hoặc bản sao ngoài VPS; cần bổ sung trước khi coi dữ liệu dùng chung là dữ liệu phải lưu giữ dài hạn. Không xóa volume hoặc hạ migration để khắc phục lỗi ứng dụng.

## Bằng chứng kiểm tra ban đầu

- PostgreSQL healthy, listener chỉ ở loopback; schema/role đúng số lượng nêu trên.
- Kết nối .NET/Npgsql từ máy phát triển qua tunnel thành công bằng user ứng dụng; không có quyền CREATE ở schema public và không superuser.
- API khởi động với cấu hình nạp từ `.env`; `/health/ready` trả HTTP 200, sau đó tiến trình API kiểm thử đã được dừng.
- `.env` và bản sao cấu hình cũ trong `.cache/vps/` bị Git ignore. Các công cụ chạy local chỉ thay môi trường tiến trình con.

Rà soát bổ sung ngày 2026-10-19: đã tái hiện SSH bị reset gây đăng nhập trả 500, khôi phục tunnel và bổ sung thử kết nối lại. Sau khôi phục, 12 kiểm tra đạt: readiness 200; token hợp lệ 200; thiếu/sai chữ ký/hết hạn/sai audience 401; role không đủ 403; role đúng 200; đăng nhập tài khoản không tồn tại trả 401. Token kiểm thử chỉ giữ trong bộ nhớ; không tạo user/ticket. Đây chưa phải kiểm chứng đăng nhập thành công với tài khoản thực.

Đây là kiểm tra triển khai mới, không thay bộ integration test/CI của backend hoặc nghiệm thu cả hệ thống qua gateway. Xem thêm [môi trường](environment-runbook.md) và [quy trình rollout](backend-refactor-rollout.md).
