# Danh mục quyết định kiến trúc

Trạng thái: chỉ mục hiện hành, cập nhật 2026-10-20. ADR giữ lý do quyết định tại thời điểm viết; bổ sung/thay thế phải được dẫn rõ. Bản diễn giải tiếng Việt giữ ngày và phạm vi quyết định, dẫn tới nguồn trước khi dịch. Nhãn Accepted không chứng minh tính năng đã triển khai.

| ADR | Hiệu lực và quan hệ |
| --- | --- |
| [001 — Modular Monolith](ADR-001-modular-monolith.md) | Còn hiệu lực; mục tiêu năm module được phân biệt với ba module đã triển khai ở ADR-014 |
| [002 — Clean Architecture](ADR-002-clean-architecture.md) | Còn hiệu lực; bốn tầng ban đầu được bổ sung host Worker/Database và tổ chức feature ở ADR-011/014 |
| [004 — PostgreSQL/pgvector](ADR-004-postgresql-pgvector.md) | PostgreSQL hiện dùng; vector là hướng cho tìm kiếm tương lai, không xác nhận đã có luồng embeddings |
| [005 — Identity/JWT](ADR-005-identity-jwt.md) | Còn hiệu lực; transaction, lockout và phạm vi hiện hành theo ADR-011/014 |
| [006 — MinIO](ADR-006-minio-object-storage.md) | Còn hiệu lực về adapter; upload/download nghiệp vụ còn chờ quyết định |
| [007 — AI port](ADR-007-ai-provider-abstraction.md) | Còn hiệu lực, bổ sung hợp đồng v1 và NoOp theo ADR-012/014 |
| [008 — Compose/NGINX](ADR-008-docker-compose-nginx.md) | Topology hệ thống; nguồn cấu hình thuộc repo infra |
| [009 — Jenkins](ADR-009-jenkins-ci-cd.md) | Đã bị ADR-013 thay thế; chỉ giữ lịch sử |
| [010 — PostgreSQL Testcontainers](ADR-010-testcontainers-postgresql.md) | Còn hiệu lực cho kiểm thử persistence thật |
| [011 — Ranh giới backend](ADR-011-backend-module-boundaries.md) | Hiện hành, được làm rõ bởi ADR-014 |
| [012 — Hợp đồng AI](ADR-012-ai-contract-and-future-transport.md) | Hợp đồng được áp dụng; HTTP adapter thật và chính sách retry/circuit mới là thiết kế tương lai |
| [013 — GitHub Actions](ADR-013-github-actions.md) | Hiện hành, thay Jenkins; test/build image/smoke, chưa phát hành image/deploy |
| [014 — Luồng xử lý/giao diện công khai](ADR-014-feature-use-cases-and-published-contracts.md) | Sáu luồng tại mốc quyết định gốc; phần bổ sung sau PR #28 dẫn tới 12 luồng hiện hành, quyền và giới hạn còn lại |

ADR-003 về frontend đã được chuyển khỏi phiên bản repo backend; không đánh lại số ADR để lấp khoảng trống. Tra cứu lịch sử Git nếu cần nguồn cũ. [Hợp đồng đang triển khai](../architecture/current-backend-contract.md) và [quyết định còn mở](../architecture/pending-business-decisions.md) phân biệt hành vi hiện tại với phát triển tương lai.
