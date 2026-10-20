# ADR-008: Docker Compose và cổng truy cập NGINX

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

Bổ sung phạm vi ngày 2026-10-19: sơ đồ Compose/NGINX áp dụng cho toàn hệ thống. Cấu hình Compose, cổng truy cập và kiểm tra toàn hệ thống thuộc [kho hạ tầng](https://github.com/supportflow-ai/supportflow-ai-infra). Backend giữ Dockerfile API/Worker/Database. Xem [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md) và [bản đồ tài liệu](../README.md).

## Bối cảnh

Nhóm năm người cần môi trường có thể dựng lại cho phát triển và kiểm thử gần với triển khai thực tế, với chi phí vận hành phù hợp.

## Quyết định

Dùng Docker Compose quản lý NGINX, giao diện, hai bản API, Worker, PostgreSQL và MinIO theo sơ đồ hệ thống ban đầu. NGINX chuyển `/` tới giao diện và `/api/` tới nhóm máy chủ API để cân bằng tải.

## Phương án đã cân nhắc

- Kubernetes: độ phức tạp và chi phí vận hành vượt quy mô dự án.
- Traefik: nhóm chưa quen, chưa cần tự phát hiện dịch vụ.
- Không có cổng chuyển tiếp: khó minh họa cân bằng tải và quản lý truy cập thống nhất.

## Hệ quả

Compose hỗ trợ dựng hệ thống từ cấu hình; NGINX tập trung định tuyến, cân bằng tải và chuyển tiếp các header. Việc định cấu hình HTTPS có thể bổ sung sau. Nhóm chấp nhận mô hình vận hành đơn giản, không có cơ chế điều phối và tự phục hồi tương đương Kubernetes.

Các header `X-Real-IP`, `X-Forwarded-For`, `X-Forwarded-Proto` được xử lý tại cấu hình cổng truy cập. Sơ đồ quyết định không tự chứng minh môi trường đã triển khai hoặc đang có đủ hai bản API; đối chiếu commit và hướng dẫn của kho hạ tầng.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-008-docker-compose-nginx.md).
