# Kiểm chứng nạp cấu hình và khởi chạy local

> **Bằng chứng tại mốc cũ.** Các kết quả dưới đây chỉ thuộc lần chạy ngày 2026-10-19 và mã/thay đổi đã ghi, không xác nhận HEAD hoặc trạng thái môi trường hiện tại. Hướng dẫn đang áp dụng: [quy trình kiểm chứng backend](backend-hardening-verification.md).

Ngày kiểm chứng: 2026-10-19. Mã backend: nhánh `codex/backend-clean-architecture`, HEAD `334485ddf46e19812c55b77812d16da20695eafd` **cùng thay đổi chưa commit** về công cụ nạp cấu hình, hướng dẫn và workflow. Không dùng kết quả dưới đây để nghiệm thu một SHA mới chưa chạy lại.

Frontend được kiểm tra tại checkout riêng, HEAD `7d768d8cf09a9bb9c2fae71e97f218d730cbb645`; không sửa mã frontend. Node trên máy là 24.13.1, trong khi `.nvmrc` frontend ghim 22; cần xác nhận lại trên Node 22 trong CI trước khi coi là kiểm chứng đúng phiên bản ghim.

## Thay đổi được kiểm chứng

- Công cụ Python nạp `.env` hoặc môi trường tiến trình; tên cấu hình chuẩn, tên cũ, thứ tự ưu tiên và lỗi đầu vào được bảo vệ bằng hồi quy.
- API bắt buộc chuỗi kết nối và khóa ký JWT; thông tin lưu tệp không bắt buộc cho các luồng HTTP hiện chưa sử dụng MinIO.
- File mẫu không chứa bí mật; README hướng dẫn cấu hình, khởi chạy, lấy JWT, lockfile/Release, frontend và vai trò repo infra. Chi tiết tại [hướng dẫn môi trường](environment-runbook.md).
- `.env` local được đặt Development và `http://localhost:5000`; giữ credential database và khóa JWT hiện có. File vẫn bị Git ignore.

## Kết quả

| Kiểm tra | Kết quả |
| --- | --- |
| `dotnet restore src/backend/ServiceDesk.sln --locked-mode` | Đạt, không cập nhật lockfile |
| `dotnet build src/backend/ServiceDesk.sln -c Release --no-restore` | Đạt, 0 cảnh báo và 0 lỗi |
| `dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"` | Đạt 241: Domain 112, Application 62, Architecture 26, adapter/tương thích offline 41 |
| `python -m unittest discover -s scripts/tests -p "test_*.py"` | Đạt 28 kiểm thử công cụ tài liệu và nạp cấu hình |
| `python scripts/validate_project_docs.py` | Đạt kiểm tra nguồn và liên kết tài liệu |
| Frontend `npm run lint` | Đạt ESLint và ranh giới kiến trúc |
| Frontend `npm run typecheck` | Đạt |
| Frontend `npm test` | Đạt 66 kiểm thử trong 9 tệp |
| Frontend `npm run build` | Đạt; có cảnh báo kích thước chunk trên 500 kB |
| Frontend `npm run test:e2e` | Đạt 11 kiểm thử Chromium dùng API fixture; không phải backend thật |

## Kiểm tra nối frontend với backend thật

Khởi chạy tạm API bằng `scripts/run_backend.py --no-build api`, Vite tại cổng 5173 và Chromium. Dùng database PostgreSQL đã được cấp qua tunnel local 15432. Không chạy migration/seed, không tạo tài khoản hoặc ticket, không ghi log token/mật khẩu.

- `/health/ready` trả 200 với PostgreSQL thật; API kiểm thử vẫn khởi động khi credential MinIO rỗng.
- `/swagger/v1/swagger.json` trả 200 trong Development và có contract đăng nhập.
- Trình duyệt gửi email không tồn tại và mật khẩu ngẫu nhiên qua frontend → Vite proxy → API → PostgreSQL, nhận 401 và hiển thị thông báo đăng nhập không hợp lệ.
- Đăng nhập thất bại không tạo token trong localStorage; gọi `/api/auth/me` không có token qua proxy trả 401.
- Dừng API và Vite tạm sau kiểm tra; giữ đường hầm database có sẵn.

Đây là kiểm chứng kết nối thật cho luồng bị từ chối. Chưa xác nhận đăng ký/đăng nhập thành công, phát JWT từ tài khoản thật hoặc tạo/xem ticket bằng trình duyệt với backend thật trong lần này.

## Giới hạn chưa nghiệm thu

Đã thử `dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"`: 62 kiểm thử lỗi khi khởi tạo fixture với `DockerUnavailableException`, Docker API trả 500; không có kiểm thử tích hợp nào đạt. Lỗi xảy ra trước bước kiểm chứng nghiệp vụ, không được tính là đã nghiệm thu transaction/concurrency/PostgreSQL migration.

Docker Desktop trên máy chưa cung cấp daemon hoạt động; không chạy được kiểm tra image API/Worker/Database. Không tự cài WSL, reset Docker, thay đổi database dùng chung hoặc bỏ kiểm thử để làm kết quả xanh. Khi Docker hoạt động, chạy lại nhóm PostgreSQL và các bước build image trong workflow.

Chưa chạy smoke qua NGINX/Compose của repo infra; checkout infra trên máy chưa có README hoặc cấu hình có thể kiểm chứng. Không sửa repo infra/AI trong nhiệm vụ này và không triển khai production.

Các kết quả local không thay thế CI cho thay đổi chưa commit. Quy trình kiểm chứng đầy đủ tại [runbook backend](backend-hardening-verification.md); hướng dẫn PostgreSQL dùng chung tại [runbook VPS](shared-development-database.md).
