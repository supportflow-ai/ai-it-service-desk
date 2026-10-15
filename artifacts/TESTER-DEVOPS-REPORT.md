# Tester/DevOps Validation Report

## 1. Environment

| Item | Value |
|---|---|
| Date | 2026-09-17 |
| Commit | `9010486ca8706f0678c3e97b12c589f007cf6c13` |
| Commit message | `chore(TC-203): standardize pull request quality checklist` |
| Docker | 29.4.3 |
| Docker Compose | v5.1.3 |
| .NET SDK | 10.0.300 |
| OS | Windows |

---

## 2. Backend Test Results

| Test group | Project | Passed | Failed | Status |
|---|---|---:|---:|---|
| Domain (Unit) | `ServiceDesk.Domain.Tests` | 87 | 0 | ✅ PASS |
| Application (Unit) | `ServiceDesk.Application.Tests` | 13 | 0 | ✅ PASS |
| Architecture | `ServiceDesk.ArchitectureTests` | 6 | 0 | ✅ PASS |
| Integration | `ServiceDesk.IntegrationTests` | 4 | 0 | ✅ PASS |
| **TOTAL** | | **110** | **0** | ✅ **ALL PASS** |

Solution: `src/backend/ServiceDesk.sln`

TRX evidence:
- `artifacts/test-results/unit/domain-tests.trx`
- `artifacts/test-results/unit/application-tests.trx`
- `artifacts/test-results/architecture/architecture-tests.trx`
- `artifacts/test-results/integration/integration-tests.trx`

### Integration test notes
- Integration tests use **Testcontainers** (PostgreSQL via Docker)
- 4 tests cover: DB connectivity + migration, health check endpoint, Swagger endpoint, API info endpoint
- All 4 pass with real PostgreSQL container spun up per test run
- Fix applied: `Program.cs` health check now uses `IOptions<DatabaseOptions>` factory (lazy) so `WebApplicationFactory` can override `Database:ConnectionString` via `ConfigureAppConfiguration`

---

## 3. Runtime Validation

| Component | Image | Status | Notes |
|---|---|---|---|
| api-1 | `ai-it-service-desk-api-1:latest` (100 MB) | ✅ Up | port 5000→8080 |
| api-2 | `ai-it-service-desk-api-2:latest` (100 MB) | ✅ Up | port 5001→8080 |
| frontend | `ai-it-service-desk-frontend:latest` (28.9 MB) | ✅ Up | behind Nginx |
| postgres | `pgvector/pgvector:pg17` (158 MB) | ✅ Healthy | port 5432, pgvector extension |
| minio | `quay.io/minio/minio:latest` (62.2 MB) | ✅ Healthy | ports 9000–9001 |
| nginx | `nginx:alpine` (28.8 MB) | ✅ Up | port 80→frontend, /api/→upstream |
| worker | `ai-it-service-desk-worker:latest` (86.4 MB) | ✅ Up | background processor |

### HTTP Smoke Tests (all 200 OK)

| Endpoint | Method | Status |
|---|---|---|
| `http://localhost` | GET | ✅ 200 |
| `http://localhost/health` | GET | ✅ 200 Healthy |
| `http://localhost/api` | GET | ✅ 200 |
| `http://localhost:5000/swagger/index.html` | GET | ✅ 200 |
| `http://localhost:5001/swagger/index.html` | GET | ✅ 200 |

Health check response:
```json
{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy","description":null,"duration":8.4338}],"totalDuration":8.7443}
```

---

## 4. Persistence Test (G7)

| Step | Result |
|---|---|
| `docker compose restart postgres` | ✅ Container restarted |
| postgres status after 10s | ✅ Up (healthy) |
| `GET /health` after postgres restart | ✅ `{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy"}]}` |
| `docker compose restart minio` | ✅ Container restarted |
| minio status after 10s | ✅ Up (healthy) |
| Volume `postgres_data` | ✅ Survives restart |
| Volume `minio_data` | ✅ Survives restart |
| Data-level persistence (ticket/file) | ⚠️ DEFERRED — no business endpoints yet |

> Infrastructure-level persistence confirmed. Data-level test (create → restart → retrieve) must be re-run after ticket/auth endpoints are implemented.

Evidence: `artifacts/smoke/persistence.md`

---

## 5. Docker Hardening

### Container Security
| Check | api-1 | api-2 | Status |
|---|---|---|---|
| Non-root user | `uid=999(appuser)` | `uid=999(appuser)` | ✅ PASS |

### Nginx
- Config syntax test: ✅ `syntax is ok` / `test is successful`
- Upstream: `api-1:8080`, `api-2:8080` (load balanced)
- Frontend proxy: `frontend:80`

### Volumes (persistence)
| Volume | Driver | Status |
|---|---|---|
| `ai-it-service-desk_postgres_data` | local | ✅ Exists |
| `ai-it-service-desk_minio_data` | local | ✅ Exists |

### Dockerfiles found
- `src/backend/ServiceDesk.Api/Dockerfile`
- `src/backend/ServiceDesk.Worker/Dockerfile`
- `src/frontend/service-desk-web/Dockerfile`

Docker Compose config exported to: `artifacts/docker/compose-config.yml`

---

## 5. Known Gaps

| # | Gap | Severity | Notes |
|---|---|---|---|
| G-1 | API authentication/authorization manual testing NOT performed | Medium | Swagger UI available at `:5000/swagger` and `:5001/swagger`. Requires manual testing session. |
| G-2 | IDOR negative tests NOT performed | High | Requires two test accounts, manual curl/Swagger workflow |
| G-3 | Internal Note authorization test NOT performed | High | No test users created yet |
| G-4 | Attachment authorization test NOT performed | Medium | Requires file upload flow |
| G-5 | AI fallback tests NOT performed | Medium | Need to identify NoOp AI provider in codebase first |
| G-6 | Persistence test — infrastructure level DONE, data-level DEFERRED | Low | Container restart + health verified ✅; ticket/file data persistence pending business endpoints |
| G-7 | Jenkins pipeline partially configured — missing stages | Medium | `Jenkinsfile` exists but missing: Architecture Test stage, Integration Test stage (currently excluded), Health Check stage, Smoke Test stage. Jenkins server not installed/running. |
| G-8 | Dockerfile multi-stage audit incomplete | Low | Dockerfiles found, detailed review (USER directive, secrets, tags) pending |
| G-9 | `npm run type-check` script missing in `package.json` | Low | `tsc -b` in `npm run build` covers type checking |
| G-10 | `minio` image changed from `minio/minio:latest` to `quay.io/minio/minio:latest` | Info | User-initiated change, more reliable registry |

---

## 6. Release Blockers

| # | Blocker | Status |
|---|---|---|
| B-1 | Backend tests: 110/110 PASS | ✅ Resolved |
| B-2 | Integration test health check fix | ✅ Resolved (IOptions<DatabaseOptions> lazy factory) |
| B-3 | Frontend build fix (`@types/node`, ESM `__dirname`) | ✅ Resolved |
| B-4 | Manual API auth/IDOR tests pending | ⚠️ Blocked — requires manual test session |
| B-5 | Jenkins pipeline missing Architecture/Integration/Health/Smoke stages | ⚠️ GAP — Jenkinsfile exists but incomplete; Jenkins server not running |

---

## 7. Evidence

| Evidence | Path |
|---|---|
| Domain unit tests TRX | `artifacts/test-results/unit/domain-tests.trx` |
| Application unit tests TRX | `artifacts/test-results/unit/application-tests.trx` |
| Architecture tests TRX | `artifacts/test-results/architecture/architecture-tests.trx` |
| Integration tests TRX | `artifacts/test-results/integration/integration-tests.trx` |
| Docker Compose config | `artifacts/docker/compose-config.yml` |
| Smoke test script | `artifacts/smoke/health-check.ps1` |
| Smoke test result | `artifacts/smoke/health-check-result.txt` |
| Persistence test evidence | `artifacts/smoke/persistence.md` |
| This report | `artifacts/TESTER-DEVOPS-REPORT.md` |
