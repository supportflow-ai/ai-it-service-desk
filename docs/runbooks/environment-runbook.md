# Runbook — Clean Integration Environment Deployment

**Target:** Local Development & Continuous Integration Staging  
**Orchestrator:** Docker Compose  
**Rule:** Zero data loss — NEVER execute `docker compose down -v` without explicit written confirmation.  

---

## 1. Prerequisites
* Docker Engine 24+ & Docker Compose v2.20+
* Port availability on host:
  * `80` (NGINX Reverse Proxy / Frontend / Public API Gateway)
  * `5432` (PostgreSQL 17)
  * `9000`, `9001` (MinIO API & Console)
  * `5000`, `5001` (Direct API-1 and API-2 debugging ports via override)

---

## 2. Environment Configuration
Ensure `.env` exists in repository root:
```bash
# Copy template if .env does not exist
cp .env.example .env
```
Default parameters configured in `.env.example`:
* `POSTGRES_USER=servicedesk`
* `POSTGRES_DB=servicedesk`
* `JWT_ISSUER=ServiceDesk`
* `JWT_AUDIENCE=ServiceDeskClient`
* `MINIO_BUCKET=servicedesk`

---

## 3. Clean Startup Procedure

### Step 3.1: Graceful Stop (Without Volume Removal)
```bash
docker compose down
```
> [!CAUTION]
> Do NOT append `-v` or `--volumes`. Persistent volumes `postgres_data` and `minio_data` must be preserved.

### Step 3.2: Build and Start Services
```bash
docker compose up -d --build
```

### Step 3.3: Verify Container Health & Status
```bash
docker compose ps
```
Expected output:
* `ai-it-service-desk-postgres-1` : `Up (healthy)`
* `ai-it-service-desk-minio-1`    : `Up (healthy)`
* `ai-it-service-desk-api-1-1`     : `Up`
* `ai-it-service-desk-api-2-1`     : `Up`
* `ai-it-service-desk-worker-1`    : `Up`
* `ai-it-service-desk-frontend-1`  : `Up`
* `ai-it-service-desk-nginx-1`     : `Up`

---

## 4. Verification & Health Checks

### Step 4.1: Health Endpoint Check
```bash
curl -i http://localhost/health
```
Expected response:
```json
HTTP/1.1 200 OK
{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy","description":null,"duration":...}],"totalDuration":...}
```

### Step 4.2: Frontend Availability Check
```bash
curl -i http://localhost/
```
Expected response:
```text
HTTP/1.1 200 OK
Content-Type: text/html
<!doctype html>...
```

### Step 4.3: API Gateway & Swagger Check
```bash
# Load balancer passthrough:
curl -s http://localhost/api/
# Direct instance Swagger checks:
curl -I http://localhost:5000/swagger/index.html
curl -I http://localhost:5001/swagger/index.html
```

### Step 4.4: Automated Smoke Hook
```powershell
powershell -File deploy/smoke/smoke-test.ps1
```
or
```bash
bash deploy/smoke/smoke-test.sh
```

---

## 5. Troubleshooting & Restart Procedures

### Service Log Inspection
```bash
docker compose logs -f api-1
docker compose logs -f postgres
docker compose logs -f nginx
```

### Single Service Restart
```bash
docker compose restart api-1 api-2
```
