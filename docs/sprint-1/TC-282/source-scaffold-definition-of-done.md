# 08 — Scaffold Scope & Definition of Done

## Trong scope

Repository skeleton; solution/projects; git/editor/docker env files; backend DI/config/logging/ProblemDetails/Swagger/health; EF Core/Npgsql; Identity/JWT foundation; MinIO adapter foundation; AI abstraction + Fake/NoOp; module folders; React/Vite app shell/router/query/Axios; architecture tests; PostgreSQL Testcontainers; Dockerfiles/Compose/NGINX; Jenkinsfile; README/ADR/runbook.

## Out of scope

Ticket entity/use cases/API/UI; state machine; priority; triage/assignment; comments/internal notes; SLA; Knowledge Article feature; semantic search thật; AI classification/suggestion thật; attachment feature; admin/analytics; notifications; production credentials/deploy automation.

## Definition of Done

### Build
- [ ] `dotnet restore` pass.
- [ ] `dotnet build` pass.
- [ ] backend tests pass.
- [ ] architecture tests pass.
- [ ] frontend install/build pass.

### Runtime
- [ ] API image build.
- [ ] Worker image/strategy documented.
- [ ] Frontend image/static build.
- [ ] `docker compose up --build` khởi động foundation stack.
- [ ] PostgreSQL healthy.
- [ ] MinIO running.
- [ ] `/health` responds.
- [ ] NGINX routes `/` and `/api/`.
- [ ] API runtime non-root.

### Architecture
- [ ] Domain không reference framework/infrastructure.
- [ ] Application không reference Infrastructure.
- [ ] Không business endpoint.
- [ ] 5 logical modules represented without microservices.
- [ ] Không speculative GenericRepository/BaseService.

### Security/config
- [ ] Không hard-coded secret.
- [ ] `.env.example` chỉ placeholder.
- [ ] JWT/DB/MinIO/AI config qua env/options.

### Documentation
- [ ] README đủ cho thành viên mới chạy project.
- [ ] dependency graph documented.
- [ ] ADR foundation có Context/Decision/Alternatives/Consequences.
- [ ] intentionally deferred items documented.

Khi DoD đạt thì **dừng**, không tự implement Sprint 1.
