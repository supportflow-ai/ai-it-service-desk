# Danh mục quyết định kiến trúc

Chỉ mục hiện hành cập nhật ngày 21/11/2026, đối chiếu Backend `dev@b3cc19a`. Mỗi ADR giữ mốc, phạm vi quyết định và lý do tại thời điểm viết. Kiểm tra tình trạng triển khai qua [mốc nguồn](../architecture/source-baseline.md) và tài liệu sở hữu từng luồng.

| ADR | Hiệu lực và quan hệ |
| --- | --- |
| [001 — Modular Monolith](ADR-001-modular-monolith.md) | Còn hiệu lực; mục tiêu năm module được phân biệt với ba module đã triển khai ở ADR-014 |
| [002 — Clean Architecture](ADR-002-clean-architecture.md) | Còn hiệu lực; bốn tầng ban đầu được bổ sung host Worker/Database và tổ chức feature ở ADR-011/014 |
| [004 — PostgreSQL/pgvector](ADR-004-postgresql-pgvector.md) | PostgreSQL hiện dùng; vector là hướng cho tìm kiếm tương lai, không xác nhận đã có luồng embeddings |
| [005 — Identity/JWT](ADR-005-identity-jwt.md) | Còn hiệu lực; transaction, lockout và phạm vi hiện hành theo ADR-011/014 |
| [006 — MinIO](ADR-006-minio-object-storage.md) | Bị ADR-017 thay thế từ 2026-11-16; mã còn dùng MinIO tới khi PR R2 hoàn tất, không mở rộng |
| [007 — AI port](ADR-007-ai-provider-abstraction.md) | Còn hiệu lực, bổ sung hợp đồng v1 và NoOp theo ADR-012/014 |
| [008 — Compose/NGINX](ADR-008-docker-compose-nginx.md) | Topology hệ thống; nguồn cấu hình thuộc repo infra |
| [009 — Jenkins](ADR-009-jenkins-ci-cd.md) | Đã bị ADR-013 thay thế; chỉ giữ lịch sử |
| [010 — PostgreSQL Testcontainers](ADR-010-testcontainers-postgresql.md) | Còn hiệu lực cho kiểm thử persistence thật |
| [011 — Ranh giới backend](ADR-011-backend-module-boundaries.md) | Hiện hành về luật phụ thuộc và giao dịch, được làm rõ bởi ADR-014; phần tổ chức project đã được ADR-015 thay thế |
| [012 — Hợp đồng AI](ADR-012-ai-contract-and-future-transport.md) | Hợp đồng v1 và adapter HTTP đã có trong mã. Mặc định dùng NoOp. Luồng từ Ticketing đến duyệt và lưu gợi ý còn chờ tích hợp theo AI-02 |
| [013 — GitHub Actions](ADR-013-github-actions.md) | Thay Jenkins và chạy CI theo kho. Hiện trạng có phát hành GHCR theo yêu cầu, triển khai theo runbook Infra |
| [014 — Luồng xử lý/giao diện công khai](ADR-014-feature-use-cases-and-published-contracts.md) | Sáu luồng tại mốc quyết định gốc; phần bổ sung sau PR #28 dẫn tới 12 luồng hiện hành, quyền và giới hạn còn lại |
| [015 — Module trước, Clean bên trong](ADR-015-module-first-clean-architecture.md) | Đã chấp nhận 2026-11-13; mã đã hợp nhất vào `dev` (PR #50–#53) và CI đạt; cấu trúc thư mục tinh gọn thêm (`Hosts`, `Platform`) ghi trong ADR |
| [016 — Nhóm hỗ trợ và phạm vi đọc của Agent](ADR-016-support-team-routing-and-agent-scope.md) | Hướng thiết kế đã duyệt 2026-11-08. Đã có nền dữ liệu và phạm vi đọc cùng chặn ghi ngoài phạm vi (B1, PR #64). Lead/Admin có quyền phân công và hủy phân công từ PR #82. Chuyển nhóm và `RoutingRule` còn chờ |
| [017 — Cloudflare R2 thay MinIO](ADR-017-cloudflare-r2-object-storage.md) | Đã chấp nhận 2026-11-16, thay ADR-006; không dùng MinIO ở mọi môi trường. Mã chưa đổi; triển khai theo [kế hoạch](../architecture/r2-storage-plan.md) |

ADR-003 về frontend đã được chuyển khỏi phiên bản repo backend; không đánh lại số ADR để lấp khoảng trống. Tra cứu lịch sử Git nếu cần nguồn cũ. [Hợp đồng đang triển khai](../architecture/current-backend-contract.md) và [quyết định còn mở](../architecture/pending-business-decisions.md) phân biệt hành vi hiện tại với phát triển tương lai.
