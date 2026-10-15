# Persistence Test Results

Date: 2026-09-17

## PostgreSQL

| Step | Command | Result |
|---|---|---|
| Restart container | `docker compose restart postgres` | ✅ Restarted |
| Container status after 10s | `docker compose ps postgres` | ✅ Up 10 seconds (healthy) |
| Health check after restart | `GET http://localhost/health` | ✅ `{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy","duration":16.6993}]}` |

**Volume:** `ai-it-service-desk_postgres_data` (local driver) — confirmed present before and after restart.

## MinIO

| Step | Command | Result |
|---|---|---|
| Restart container | `docker compose restart minio` | ✅ Restarted |
| Container status after 10s | `docker compose ps minio` | ✅ Up 10 seconds (healthy) |

**Volume:** `ai-it-service-desk_minio_data` (local driver) — confirmed present before and after restart.

## Limitation

> **Data-level persistence test was NOT performed** because no ticket or file upload business API endpoints exist yet
> (Identity/Ticketing folders are placeholder `.gitkeep` only).
>
> The test confirms **infrastructure-level persistence** (volumes survive restart, services recover healthy),
> but **application-level data persistence** (create ticket → restart → retrieve ticket) must be re-run
> after business endpoints are implemented.

## Conclusion

| Check | Status |
|---|---|
| PostgreSQL survives restart | ✅ PASS |
| PostgreSQL health check passes after restart | ✅ PASS |
| MinIO survives restart | ✅ PASS |
| Data-level persistence (ticket/file) | ⚠️ DEFERRED — no business endpoints yet |
