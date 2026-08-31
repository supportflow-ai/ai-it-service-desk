# ADR-008: Docker Compose + NGINX Reverse Proxy

## Status
Accepted

## Context
Cần runtime topology đơn giản, reproducible cho cả development lẫn staging/production-like environment. Team 5 người, không cần orchestration phức tạp.

## Decision
Dùng **Docker Compose** để quản lý tất cả services (NGINX, Frontend, API ×2, Worker, PostgreSQL, MinIO). **NGINX** làm reverse proxy: `/` → Frontend, `/api/` → API upstream (load balance 2 instances).

## Alternatives Considered
- **Kubernetes**: Quá phức tạp cho team size và scope dự án, overhead vận hành lớn.
- **Traefik thay NGINX**: Ít phổ biến trong curriculum, team chưa quen, auto-discovery không cần thiết.
- **Không dùng reverse proxy**: Không thể demo load balancing, CORS phức tạp hơn, không production-like.

## Consequences
- **Positive**: `docker compose up --build` khởi động toàn bộ stack, NGINX xử lý routing/LB/headers, dễ thêm SSL termination sau.
- **Negative**: Docker Compose không có auto-restart/self-healing như Kubernetes (chấp nhận cho scope dự án).
- **Neutral**: Forwarded headers (X-Real-IP, X-Forwarded-For, X-Forwarded-Proto) được NGINX forward đúng.
