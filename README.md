# 🚀 AI-Powered IT Service Desk

<div align="center">

**Hệ thống Quản lý và Tự động hóa Xử lý Yêu cầu IT Nội bộ Ứng dụng AI**  
*Enterprise-Grade AI-Assisted Internal IT Service Request & Knowledge Management Platform*

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19.0-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.7-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Vite](https://img.shields.io/badge/Vite-6.0-646CFF?logo=vite&logoColor=white)](https://vitejs.dev/)
[![PostgreSQL 17](https://img.shields.io/badge/PostgreSQL-17%20%2B%20pgvector-4169E1?logo=postgresql&logoColor=white)](https://github.com/pgvector/pgvector)
[![MinIO](https://img.shields.io/badge/MinIO-Object%20Storage-C72C48?logo=minio&logoColor=white)](https://min.io/)
[![Docker](https://img.shields.io/badge/Docker-Compose%20v2-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Jenkins](https://img.shields.io/badge/Jenkins-CI%2FCD-D24939?logo=jenkins&logoColor=white)](https://www.jenkins.io/)
[![Architecture](https://img.shields.io/badge/Architecture-Modular%20Monolith-orange)](docs/adr/ADR-001-modular-monolith.md)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

[Key Highlights](#-key-highlights) •
[Architecture](#-architecture) •
[Tech Stack](#-technology-stack) •
[Quick Start](#-quick-start) •
[Services & Endpoints](#-service-matrix--default-endpoints) •
[Testing](#-testing-strategy) •
[ADRs](#-architecture-decision-records-adrs) •
[Project Structure](#-project-structure) •
[Roadmap](#-development-roadmap)

</div>

---

## 📌 Overview

**AI IT Service Desk** is an enterprise-grade internal IT support system engineered to streamline ticket processing, automate triage, and empower support agents with semantic knowledge search and intelligent AI suggestions.

Built with modern software engineering paradigms, the platform enforces **Modular Monolith** architecture combined with **Clean Architecture** boundaries, strict dependency rules, containerized deployment, and comprehensive automated test suites.

---

## ✨ Key Highlights

- 🏢 **Modular Monolith Design**: 5 high-cohesion, loosely-coupled logical modules sharing a single unified deployment unit.
- 📐 **Clean Architecture & Zero-Dependency Core**: Pure Domain layer with no external package coupling, validated by automated NetArchTest rules.
- 🤖 **AI-Ready Core (RAG & Triage)**: Modular AI Provider abstraction with PostgreSQL `pgvector` for semantic knowledge retrieval and automated ticket classification.
- 🔐 **Secure Identity & Access Management**: ASP.NET Core Identity with JWT Bearer authentication and Role-Based Access Control (RBAC).
- 🗄️ **S3-Compatible Storage**: High-performance MinIO Object Storage adapter for incident attachments and knowledge assets.
- 🐳 **Full Containerization & High Availability**: Multi-instance API backend behind an NGINX reverse proxy with load balancing and integrated health checks.
- 🧪 **Enterprise Test Coverage**: Unit tests, Architecture compliance tests, and real-world database Integration tests powered by Docker Testcontainers.
- 🔄 **Production-Grade CI/CD**: End-to-end automated Jenkins pipeline with multi-stage build, test reporting, and Docker artifact verification.

---

## 🏛 Architecture

### 1. Logical Modules

The system is structured around 5 core functional modules:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        AI IT SERVICE DESK                              │
├─────────────┬─────────────┬──────────────┬──────────────┬──────────────┤
│  Identity   │  Ticketing  │  Knowledge   │ AI Assistant │  Analytics   │
│   & Auth    │  Lifecycle  │  Base & RAG  │  & Triage    │   & SLA      │
└─────────────┴─────────────┴──────────────┴──────────────┴──────────────┘
```

### 2. Clean Architecture Assembly Dependency Graph

Strict dependency direction flowing inward toward the Domain layer:

```
              ┌────────────────────────┐
              │   ServiceDesk.Domain   │  ◄── (Zero external dependencies)
              └───────────▲────────────┘
                          │
              ┌───────────┴────────────┐
              │ ServiceDesk.Application│  ◄── (Use cases, DTOs, Port interfaces)
              └─────▲────────────▲─────┘
                    │            │
         ┌──────────┴────┐  ┌────┴──────────────────────┐
         │ServiceDesk.Api│  │ServiceDesk.Infrastructure │  ◄── (EF Core, MinIO, AI)
         └────────▲──────┘  └────▲──────────────────────┘
                  │              │
                  └───────┬──────┘
                          │
              ┌───────────┴────────────┐
              │   ServiceDesk.Worker   │  ◄── (Background processing & queue)
              └────────────────────────┘
```

### 3. Container Runtime Topology

```
                                 [ Client Browser ]
                                         │
                                         │ HTTP :80
                                         ▼
                            ┌─────────────────────────┐
                            │      NGINX Gateway      │
                            │ (Reverse Proxy & LB)    │
                            └────────────┬────────────┘
                                         │
                   ┌─────────────────────┼─────────────────────┐
                   │ /                   │ /api/               │ /health
                   ▼                     ▼                     ▼
        ┌────────────────────┐ ┌───────────────────┐ ┌───────────────────┐
        │   Frontend SPA     │ │   api-1 / api-2   │ │   Health Checks   │
        │(React 19 + Vite UI)│ │ (.NET 10 Web API) │ └───────────────────┘
        └────────────────────┘ └─────────┬─────────┘
                                         │
                                         │  ┌─────────────────────────┐
                                         ├──┤   ServiceDesk.Worker    │
                                         │  │ (Background Processor)  │
                                         │  └────────────┬────────────┘
                                         │               │
                 ┌───────────────────────┼───────────────┘
                 │                       │
                 ▼                       ▼
      ┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────────────┐
      │ PostgreSQL 17       │ │ MinIO S3 Store      │ │ AI Provider         │
      │ (+ pgvector Ext.)   │ │ (Console :9001)     │ │ (Ollama/OpenAI/NoOp)│
      └─────────────────────┘ └─────────────────────┘ └─────────────────────┘
```

---

## 🛠 Technology Stack

| Layer / Category | Technology | Purpose & Description |
|---|---|---|
| **Backend Core** | [.NET 10](https://dotnet.microsoft.com/) / C# 13 | High-performance enterprise backend runtime |
| **Framework** | [ASP.NET Core](https://learn.microsoft.com/aspnet/core) | RESTful API, Health Checks, Swagger/OpenAPI |
| **ORM & Data** | [EF Core 10](https://learn.microsoft.com/ef/core/) + [Npgsql](https://www.npgsql.org/) | Object-relational mapping with PostgreSQL provider |
| **Database** | [PostgreSQL 17](https://www.postgresql.org/) + [pgvector](https://github.com/pgvector/pgvector) | Relational store + vector embeddings for RAG search |
| **Security & Auth** | ASP.NET Core Identity + JWT Bearer | Token-based auth, password hashing, claims-based RBAC |
| **Object Storage** | [MinIO](https://min.io/) | High-throughput S3-compatible asset and attachment store |
| **Frontend Framework** | [React 19](https://react.dev/) + [TypeScript 5.7](https://www.typescriptlang.org/) | Type-safe single page application (SPA) |
| **Build Tooling** | [Vite 6](https://vitejs.dev/) | Ultra-fast frontend bundling and HMR dev server |
| **UI Components** | [Ant Design 5](https://ant.design/) | Comprehensive enterprise design system and components |
| **State & Data Fetching**| [TanStack React Query v5](https://tanstack.com/query) | Asynchronous server state management and caching |
| **Reverse Proxy** | [NGINX Alpine](https://nginx.org/) | Gateway routing, static asset serving, load balancing |
| **CI/CD Automation** | [Jenkins](https://www.jenkins.io/) | Scripted pipeline automation across branches |
| **Testing** | xUnit, NetArchTest, FluentAssertions | Unit testing & architecture rule enforcement |
| **Integration Testing** | [Testcontainers for .NET](https://testcontainers.com/) | Ephemeral Docker containers for real DB integration tests |

---

## 📋 Architecture Decision Records (ADRs)

All core architectural decisions are documented in detail under `docs/adr/`:

| ADR | Title | Key Decision & Rationale |
|:---:|---|---|
| [**ADR-001**](docs/adr/ADR-001-modular-monolith.md) | Modular Monolith Architecture | Single deployable unit with high domain modularity; avoids microservice overhead |
| [**ADR-002**](docs/adr/ADR-002-clean-architecture.md) | Clean Architecture Pattern | Domain centricity, decoupled use-cases, and strict inward dependency flow |
| [**ADR-003**](docs/adr/ADR-003-react-typescript-vite.md) | React + TypeScript + Vite | Type safety, rapid dev iteration, rich enterprise UI ecosystem |
| [**ADR-004**](docs/adr/ADR-004-postgresql-pgvector.md) | PostgreSQL with pgvector Extension | Unified relational data + native vector similarity search for AI RAG |
| [**ADR-005**](docs/adr/ADR-005-identity-jwt.md) | ASP.NET Core Identity + JWT | Standardized enterprise user management and stateless bearer authentication |
| [**ADR-006**](docs/adr/ADR-006-minio-object-storage.md) | MinIO Object Storage | S3-compatible, self-hosted, scalable storage for ticket attachments |
| [**ADR-007**](docs/adr/ADR-007-ai-provider-abstraction.md) | AI Provider Abstraction | Vendor-agnostic ports allowing seamless switching between OpenAI, Ollama, or NoOp |
| [**ADR-008**](docs/adr/ADR-008-docker-compose-nginx.md) | Docker Compose + NGINX | Full multi-container local & staging orchestration with unified gateway routing |
| [**ADR-009**](docs/adr/ADR-009-jenkins-ci-cd.md) | Jenkins CI/CD Pipeline | Automated verification for builds, unit tests, and Docker composition |
| [**ADR-010**](docs/adr/ADR-010-testcontainers-postgresql.md) | Testcontainers for Integration Testing | Reliable, isolated, real-instance database integration tests without manual mocks |

---

## ⚡ Quick Start

### 📦 Prerequisites

Ensure the following tools are installed on your workstation:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22.x LTS](https://nodejs.org/) & `npm`
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine with Docker Compose v2

---

### Option 1: Full Stack via Docker Compose (Recommended)

Run the entire system (Frontend, 2x API nodes, Worker, NGINX, PostgreSQL + pgvector, MinIO) in one command:

```bash
# 1. Clone repository
git clone https://github.com/Duc-AnhTp/ai-it-service-desk.git
cd ai-it-service-desk

# 2. Setup environment configuration
cp .env.example .env

# 3. Start all services in detached mode
docker compose up --build -d

# 4. View real-time container logs
docker compose logs -f
```

---

### Option 2: Local Development Setup

For active day-to-day coding with hot-reload:

#### Step 1: Start Infrastructure Services (DB & Storage)
```bash
docker compose up postgres minio -d
```

#### Step 2: Run Backend API & Background Worker
```bash
# Terminal 1 — Backend Web API
cd src/backend
dotnet restore ServiceDesk.sln
dotnet run --project ServiceDesk.Api

# Terminal 2 — Background Worker (optional for background tasks)
cd src/backend
dotnet run --project ServiceDesk.Worker
```

#### Step 3: Run Frontend Web Application
```bash
# Terminal 3 — React Vite App
cd src/frontend/service-desk-web
npm install
npm run dev
```

---

## 🌐 Service Matrix & Default Endpoints

When running the full stack via Docker Compose:

| Component | Container | Internal / Host Port | Target URL | Credentials / Notes |
|---|---|---|---|---|
| **Web Portal (SPA)** | `frontend` | `nginx:80` $\rightarrow$ `:80` | [http://localhost](http://localhost) | React 19 Frontend Web Portal |
| **API Gateway** | `nginx` | `nginx:80/api` | [http://localhost/api](http://localhost/api) | Load balanced across `api-1` & `api-2` |
| **Direct API Node 1** | `api-1` | `8080` $\rightarrow$ `5000` | [http://localhost:5000](http://localhost:5000) | Direct API instance 1 |
| **Direct API Node 2** | `api-2` | `8080` $\rightarrow$ `5001` | [http://localhost:5001](http://localhost:5001) | Direct API instance 2 |
| **Swagger UI** | `api-1` | `5000/swagger` | [http://localhost:5000/swagger](http://localhost:5000/swagger) | Interactive OpenAPI documentation |
| **Health Check** | `nginx` | `80/health` | [http://localhost/health](http://localhost/health) | Subsystem health status |
| **MinIO Console** | `minio` | `9001` $\rightarrow$ `9001` | [http://localhost:9001](http://localhost:9001) | `minioadmin` / `minioadmin` |
| **MinIO S3 API** | `minio` | `9000` $\rightarrow$ `9000` | [http://localhost:9000](http://localhost:9000) | S3 API endpoint |
| **PostgreSQL DB** | `postgres` | `5432` $\rightarrow$ `5432` | `localhost:5432` | User: `servicedesk`, DB: `servicedesk` |

---

## 🧪 Testing Strategy

The solution enforces quality through a multi-tiered testing approach:

```bash
# Run all backend test projects
cd src/backend
dotnet test ServiceDesk.sln
```

### 1. Architecture Compliance Tests (`ServiceDesk.ArchitectureTests`)
Enforces Clean Architecture rules using **NetArchTest**:
- `Domain` layer has **zero** dependencies on outside layers or 3rd-party assemblies.
- `Application` depends only on `Domain`.
- `Infrastructure` and `Api` do not have circular references.

### 2. Unit Tests (`ServiceDesk.Domain.Tests` & `Application.Tests`)
Validates business entities, domain events, validation logic, and command/query handlers in isolation.

### 3. Integration Tests (`ServiceDesk.IntegrationTests`)
Uses **Testcontainers** to spin up an isolated, temporary PostgreSQL instance in Docker during test execution to verify real EF Core migrations, database queries, and repository operations.

### 4. Frontend Verification
```bash
cd src/frontend/service-desk-web

# TypeScript typecheck + production build verification
npm run build

# Code style and linting
npm run lint
```

---

## 📁 Project Structure

```
ai-it-service-desk/
├── src/
│   ├── backend/
│   │   ├── ServiceDesk.sln                 # Backend Solution file
│   │   ├── ServiceDesk.Domain/             # Enterprise Core: Entities, Value Objects, Domain Events
│   │   ├── ServiceDesk.Application/        # Application Layer: Commands, Queries, Interfaces, DTOs
│   │   ├── ServiceDesk.Infrastructure/     # Adapters: EF Core, Npgsql, MinIO Storage, AI Client
│   │   ├── ServiceDesk.Api/                # Host/Composition Root: Endpoints, Middleware, Swagger
│   │   └── ServiceDesk.Worker/             # Background Service: Event consumption, Scheduled tasks
│   └── frontend/
│       └── service-desk-web/               # React 19 Frontend (Vite, TypeScript, Ant Design)
│           ├── src/
│           │   ├── components/             # Reusable UI components
│           │   ├── features/               # Module-specific views & state
│           │   ├── services/               # API clients (Axios / React Query)
│           │   └── routes/                 # Application routing
│           ├── Dockerfile                  # Production NGINX frontend container
│           └── package.json
├── tests/
│   ├── ServiceDesk.ArchitectureTests/      # Dependency rule enforcement (NetArchTest)
│   ├── ServiceDesk.Domain.Tests/           # Domain entity unit tests
│   ├── ServiceDesk.Application.Tests/      # Use case handler unit tests
│   └── ServiceDesk.IntegrationTests/       # Integration tests with Docker Testcontainers
├── deploy/
│   └── nginx/                              # NGINX gateway & upstream reverse proxy configuration
├── docs/
│   ├── adr/                                # Architecture Decision Records (001 - 010)
│   ├── architecture/                       # Architectural diagrams & design specs
│   └── runbooks/                           # Operations & deployment runbooks
├── docker-compose.yml                      # 7-service orchestration stack
├── Jenkinsfile                             # Multi-stage CI/CD pipeline definition
└── .env.example                            # Template for environment variables
```

---

## 🌿 Git Flow & Branch Strategy

```
feature/*  ──►  dev  ──►  staging  ──►  main (Production)
```

- `feature/<name>`: New feature implementations or bugfixes.
- `dev`: Active integration branch tested by CI.
- `staging`: Pre-release testing environment mirroring production topology.
- `main`: Production-ready release branch with tag releases.

### Commit Message Convention
Commits follow [Conventional Commits](https://www.conventionalcommits.org/):
- `feat:` New business capability or feature
- `fix:` Bug fix
- `refactor:` Code refactoring without behavioral change
- `test:` Adding or updating test suites
- `docs:` Documentation updates or ADR additions
- `chore:` Infrastructure, docker, or tooling updates

---

## 🗺 Development Roadmap

| Phase | Focus | Status | Deliverables |
|:---:|---|:---:|---|
| **Phase 1** | **Foundation & Architecture Scaffold** | ✅ Completed | Clean Architecture setup, EF Core, Postgres + pgvector, MinIO, Docker Compose, Jenkins CI, Testcontainers, Architecture tests. |
| **Phase 2** | **Core Business & Ticket Lifecycle** | 🔄 Next Up | Ticket CRUD, State machine workflow, SLA tracking, Priority calculation, Assignment engine. |
| **Phase 3** | **Knowledge Base & AI Integration** | 📅 Planned | Markdown articles, Vector embeddings generation, RAG-powered search, Auto-triage AI suggestions. |
| **Phase 4** | **Analytics, Hardening & Production** | 📅 Planned | SLA violation alerts, Real-time dashboards, MinIO attachment management, Production hardening. |

---

## 📄 License & Attribution

This project is licensed under the **MIT License**.  
Developed and maintained as an AI-powered Enterprise IT Service Management solution.
