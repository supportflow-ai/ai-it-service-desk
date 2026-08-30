# AI IT Service Desk

**Hệ thống quản lý và hỗ trợ xử lý yêu cầu IT nội bộ bằng AI**
*AI-Assisted Internal IT Service Request Management System*

## Architecture

**Modular Monolith** with **Clean Architecture** — 5 logical modules in one deployment unit.

```
Modules: Identity | Ticketing | Knowledge | AI Assistance | Analytics
```

### Assembly Dependency Graph

```
Domain (zero deps)
  ↑
Application (owns port interfaces)
  ↑              ↑
Api (composition root)    Infrastructure (implements ports)
  ↑              ↑
  └──────────────┘
         ↑
Worker (background processing)
```

### Runtime Topology

```
Browser → NGINX → /      → Frontend (React SPA)
                → /api/  → API upstream (api-1, api-2)
                → /health

API/Worker → PostgreSQL + pgvector
           → MinIO (S3-compatible)
           → AI Provider (NoOp in foundation)
```

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core, EF Core, Npgsql |
| Database | PostgreSQL + pgvector |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Frontend | React, TypeScript, Vite, Ant Design |
| Storage | MinIO (S3-compatible) |
| Proxy | NGINX |
| CI/CD | Jenkins |
| Container | Docker, Docker Compose |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Node.js 22+](https://nodejs.org/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)

### Option 1: Docker Compose (recommended)

```bash
# Copy env file and edit secrets
cp .env.example .env

# Start all services
docker compose up --build
```

Services:
- Frontend: http://localhost (via NGINX)
- API: http://localhost/api/
- Health: http://localhost/health
- Swagger: http://localhost:5000/swagger (direct API access)
- MinIO Console: http://localhost:9001
- PostgreSQL: localhost:5432

### Option 2: Local development

```bash
# Start infrastructure
docker compose up postgres minio -d

# Backend
cd src/backend
dotnet restore
dotnet build
dotnet run --project ServiceDesk.Api

# Frontend (separate terminal)
cd src/frontend/service-desk-web
npm install
npm run dev
```

### Running Tests

```bash
# Backend tests (requires Docker for Testcontainers)
cd src/backend
dotnet test

# Frontend
cd src/frontend/service-desk-web
npm run build   # TypeScript check + production build
npm run lint    # ESLint
```

## Project Structure

```
ai-it-service-desk/
├── src/
│   ├── backend/
│   │   ├── ServiceDesk.Api/            # Composition root, endpoints
│   │   ├── ServiceDesk.Application/    # Use cases, port interfaces
│   │   ├── ServiceDesk.Domain/         # Business logic (zero deps)
│   │   ├── ServiceDesk.Infrastructure/ # EF Core, MinIO, AI adapters
│   │   └── ServiceDesk.Worker/         # Background processing
│   └── frontend/service-desk-web/      # React SPA
├── tests/
│   ├── ServiceDesk.ArchitectureTests/  # Dependency rule enforcement
│   ├── ServiceDesk.Domain.Tests/
│   ├── ServiceDesk.Application.Tests/
│   └── ServiceDesk.IntegrationTests/   # Testcontainers + PostgreSQL
├── docs/adr/                           # Architecture Decision Records
├── deploy/nginx/                       # NGINX configuration
├── docker-compose.yml                  # 7 services
└── Jenkinsfile                         # CI/CD pipeline
```

## Branch Strategy

```
feature/* → dev → staging → main
```

## Documentation

- [ADR-001: Modular Monolith](docs/adr/ADR-001-modular-monolith.md)
- [ADR-002: Clean Architecture](docs/adr/ADR-002-clean-architecture.md)
- [ADR-003: React + TypeScript + Vite](docs/adr/ADR-003-react-typescript-vite.md)
- [ADR-004: PostgreSQL + pgvector](docs/adr/ADR-004-postgresql-pgvector.md)
- [ADR-005: ASP.NET Core Identity + JWT](docs/adr/ADR-005-identity-jwt.md)

## Current Status: Foundation Scaffold

This is the **foundation scaffold only**. Business features (tickets, knowledge articles, AI classification, etc.) are **intentionally not implemented** and will be added in future sprints.

### What's included
- Clean Architecture project structure with 5 logical modules
- EF Core + PostgreSQL + pgvector foundation
- ASP.NET Core Identity + JWT authentication foundation
- MinIO object storage adapter skeleton
- AI provider abstraction with NoOp implementation
- Docker Compose stack (7 services)
- NGINX reverse proxy
- Architecture tests enforcing dependency rules
- CI/CD pipeline foundation (Jenkins)

### What's intentionally deferred
- Ticket CRUD/state machine/triage/assignment
- Knowledge Base features
- AI classification/suggestion
- Attachment upload/download
- Admin/Analytics dashboards
- Production deploy automation
