# 🚀 Hệ thống IT Service Desk Tích hợp AI

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

<br/>

[English](README.md) | **Tiếng Việt**

---

[Điểm nổi bật](#diem-noi-bat) •
[Kiến trúc](#kien-truc) •
[Công nghệ](#cong-nghe) •
[Bắt đầu nhanh](#bat-dau-nhanh) •
[Dịch vụ & API](#dich-vu-va-api) •
[Kiểm thử](#kiem-thu) •
[ADRs](#adrs) •
[Cấu trúc dự án](#cau-truc-du-an) •
[Lộ trình](#lo-trinh)

</div>

---

<a id="tong-quan"></a>
## 📌 Tổng quan

**AI IT Service Desk** là hệ thống hỗ trợ IT nội bộ cấp doanh nghiệp được thiết kế nhằm tối ưu hóa quy trình tiếp nhận và xử lý sự cố, tự động hóa phân loại yêu cầu (triage), đồng thời trợ giúp kỹ thuật viên thông qua tìm kiếm tri thức ngữ nghĩa và đề xuất thông minh bằng AI.

Nền tảng được phát triển theo các chuẩn mực kỹ thuật phần mềm hiện đại: kiến trúc **Modular Monolith** kết hợp phân tách ranh giới **Clean Architecture**, kiểm soát nghiêm ngặt chiều phụ thuộc mã nguồn, đóng gói container hóa toàn diện và kiểm thử tự động đa tầng.

---

<a id="diem-noi-bat"></a>
## ✨ Điểm nổi bật

- 🏢 **Thiết kế Modular Monolith**: 5 module logic gắn kết cao (high-cohesion), lỏng liên kết (loosely-coupled) triển khai trong một đơn vị đóng gói duy nhất.
- 📐 **Clean Architecture & Zero-Dependency Core**: Lớp Domain thuần túy, không phụ thuộc thư viện bên ngoài, được giám sát tự động bằng các bài test NetArchTest.
- 🤖 **Lõi AI-Ready (RAG & Triage)**: Tầng trừu tượng AI Provider linh hoạt kết hợp PostgreSQL `pgvector` phục vụ truy xuất tri thức ngữ nghĩa và tự động phân loại ticket.
- 🔐 **Quản lý danh tính & xác thực an toàn**: Tích hợp ASP.NET Core Identity, JWT Bearer Token và phân quyền theo vai trò (RBAC).
- 🗄️ **Lưu trữ chuẩn S3**: Adapter MinIO Object Storage hiệu năng cao cho tệp đính kèm và tài liệu tri thức.
- 🐳 **Container hóa toàn diện & Sẵn sàng chịu tải**: Nhiều node API phía sau NGINX Reverse Proxy cân bằng tải kèm kiểm tra sức khỏe tự động (Health Checks).
- 🧪 **Kiểm thử tự động đa tầng**: Unit tests, Architecture compliance tests và Integration tests với database PostgreSQL thật sử dụng Docker Testcontainers.
- 🔄 **Quy trình CI/CD tự động**: Pipeline Jenkins khép kín từ build, chạy test báo cáo kết quả đến đóng gói Docker artifact.

---

<a id="kien-truc"></a>
## 🏛 Kiến trúc hệ thống

### 1. Phân rã Module Logic

Hệ thống được cấu trúc xung quanh 5 module nghiệp vụ trọng tâm:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        AI IT SERVICE DESK                              │
├─────────────┬─────────────┬──────────────┬──────────────┬──────────────┤
│  Identity   │  Ticketing  │  Knowledge   │ AI Assistant │  Analytics   │
│   & Auth    │  Lifecycle  │  Base & RAG  │  & Triage    │   & SLA      │
└─────────────┴─────────────┴──────────────┴──────────────┴──────────────┘
```

### 2. Sơ đồ phụ thuộc các tầng Clean Architecture

Quy tắc phụ thuộc một chiều nghiêm ngặt hướng về lớp lõi Domain:

```
              ┌────────────────────────┐
              │   ServiceDesk.Domain   │  ◄── (Không phụ thuộc thư viện ngoài)
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
              │   ServiceDesk.Worker   │  ◄── (Xử lý tác vụ ngầm & Hàng đợi)
              └────────────────────────┘
```

### 3. Cấu trúc Runtime Container

```
                                 [ Trình duyệt Client ]
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

<a id="cong-nghe"></a>
## 🛠️ Công nghệ sử dụng (Tech Stack)

| Phân vùng | Công nghệ | Mục đích & Chi tiết |
|---|---|---|
| **Lõi Backend** | [.NET 10](https://dotnet.microsoft.com/) / C# 13 | Nền tảng runtime backend hiệu năng cao cấp doanh nghiệp |
| **Framework** | [ASP.NET Core](https://learn.microsoft.com/aspnet/core) | RESTful API, Health Checks, Swagger/OpenAPI |
| **ORM & Dữ liệu** | [EF Core 10](https://learn.microsoft.com/ef/core/) + [Npgsql](https://www.npgsql.org/) | Quản lý ánh xạ đối tượng quan hệ với PostgreSQL |
| **Cơ sở dữ liệu** | [PostgreSQL 17](https://www.postgresql.org/) + [pgvector](https://github.com/pgvector/pgvector) | Lưu trữ quan hệ kết hợp vector embeddings phục vụ RAG search |
| **Bảo mật & Xác thực** | ASP.NET Core Identity + JWT Bearer | Quản lý user, mã hóa mật khẩu, phân quyền RBAC |
| **Lưu trữ đối tượng** | [MinIO](https://min.io/) | Lưu trữ tệp đính kèm và tài liệu tri thức tương thích S3 |
| **Frontend Framework** | [React 19](https://react.dev/) + [TypeScript 5.7](https://www.typescriptlang.org/) | Ứng dụng Single Page Application (SPA) type-safe |
| **Build Tooling** | [Vite 6](https://vitejs.dev/) | Bundler frontend siêu tốc và dev server HMR |
| **UI Components** | [Ant Design 5](https://ant.design/) | Hệ thống thành phần giao diện doanh nghiệp toàn diện |
| **Quản lý State & Query**| [TanStack React Query v5](https://tanstack.com/query) | Quản lý state bất đồng bộ từ server và cơ chế caching |
| **Reverse Proxy** | [NGINX Alpine](https://nginx.org/) | Điều hướng gateway, phục vụ static assets, cân bằng tải |
| **Tự động hóa CI/CD** | [Jenkins](https://www.jenkins.io/) | Tự động hóa pipeline kiểm thử và đóng gói bản build |
| **Kiểm thử Unit & Arch** | xUnit, NetArchTest, FluentAssertions | Kiểm thử unit nghiệp vụ và cưỡng chế quy tắc kiến trúc |
| **Kiểm thử tích hợp** | [Testcontainers for .NET](https://testcontainers.com/) | Khởi tạo container Docker PostgreSQL thực tế khi chạy test |

---

<a id="adrs"></a>
## 📋 Hồ sơ Quyết định Kiến trúc (ADRs)

Toàn bộ quyết định kiến trúc cốt lõi được ghi lại chi tiết tại thư mục `docs/adr/`:

| ADR | Tiêu đề | Quyết định cốt lõi & Cơ sở lý luận |
|:---:|---|---|
| [**ADR-001**](docs/adr/ADR-001-modular-monolith.md) | Modular Monolith Architecture | Một đơn vị triển khai duy nhất với tính module hóa cao; tránh chi phí vận hành phức tạp của microservices |
| [**ADR-002**](docs/adr/ADR-002-clean-architecture.md) | Clean Architecture Pattern | Đặt Domain làm trung tâm, tách rời use cases và kiểm soát hướng phụ thuộc một chiều |
| [**ADR-003**](docs/adr/ADR-003-react-typescript-vite.md) | React + TypeScript + Vite | An toàn kiểu dữ liệu, tốc độ phát triển cao, hệ sinh thái UI doanh nghiệp phong phú |
| [**ADR-004**](docs/adr/ADR-004-postgresql-pgvector.md) | PostgreSQL with pgvector Extension | Hợp nhất lưu trữ dữ liệu quan hệ và tìm kiếm độ tương đồng vector cho AI RAG |
| [**ADR-005**](docs/adr/ADR-005-identity-jwt.md) | ASP.NET Core Identity + JWT | Chuẩn hóa quản lý người dùng doanh nghiệp và xác thực phi trạng thái (stateless) qua bearer token |
| [**ADR-006**](docs/adr/ADR-006-minio-object-storage.md) | MinIO Object Storage | Tương thích chuẩn S3, tự host, mở rộng linh hoạt cho tệp đính kèm ticket |
| [**ADR-007**](docs/adr/ADR-007-ai-provider-abstraction.md) | AI Provider Abstraction | Tầng adapter độc lập cho phép linh hoạt hoán đổi giữa OpenAI, Ollama hoặc NoOp |
| [**ADR-008**](docs/adr/ADR-008-docker-compose-nginx.md) | Docker Compose + NGINX | Điều phối đa container cho môi trường local/staging với gateway điều hướng tập trung |
| [**ADR-009**](docs/adr/ADR-009-jenkins-ci-cd.md) | Jenkins CI/CD Pipeline | Tự động hóa kiểm tra build, unit tests và kiểm thử cấu hình Docker đa tầng |
| [**ADR-010**](docs/adr/ADR-010-testcontainers-postgresql.md) | Testcontainers for Integration Testing | Kiểm thử tích hợp cơ sở dữ liệu thực tế, cô lập và đáng tin cậy mà không cần mock thủ công |

---

<a id="bat-dau-nhanh"></a>
## ⚡ Bắt đầu nhanh (Quick Start)

### 📦 Yêu cầu cài đặt môi trường

Đảm bảo máy trạm của bạn đã cài đặt các công cụ sau:
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22.x LTS](https://nodejs.org/) & `npm`
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) hoặc Docker Engine với Docker Compose v2

---

### Cách 1: Chạy toàn bộ hệ thống bằng Docker Compose (Khuyến nghị)

> [!NOTE]
> **Lưu ý về Image Registry:** Quá trình pull image `minio/minio:latest` trên Docker Hub có thể gặp lỗi nếu registry tạm khóa. Khi đó có thể sử dụng Cách 2 (chạy local development) kết hợp Docker Testcontainers để kiểm thử.

Khởi chạy toàn bộ hệ thống (Frontend, 2x API node, Worker, NGINX, PostgreSQL + pgvector, MinIO) bằng một lệnh duy nhất:

```bash
# 1. Clone repository
git clone https://github.com/Duc-AnhTp/ai-it-service-desk.git
cd ai-it-service-desk

# 2. Thiết lập file cấu hình môi trường
cp .env.example .env

# 3. Khởi chạy toàn bộ dịch vụ ở chế độ chạy ngầm
docker compose up --build -d

# 4. Xem log thời gian thực của các container
docker compose logs -f
```

---

### Cách 2: Chạy trực tiếp trên máy phục vụ Development

Dành cho quá trình phát triển mã nguồn hàng ngày với tính năng hot-reload:

#### Bước 1: Khởi động các dịch vụ hạ tầng (Database & Lưu trữ)
```bash
docker compose up postgres minio -d
```

#### Bước 2: Chạy Backend Web API & Background Worker
```bash
# Terminal 1 — Backend Web API
cd src/backend
dotnet restore ServiceDesk.sln
dotnet run --project ServiceDesk.Api

# Terminal 2 — Background Worker (xử lý tác vụ ngầm)
cd src/backend
dotnet run --project ServiceDesk.Worker
```

#### Bước 3: Chạy ứng dụng Frontend
```bash
# Terminal 3 — React Vite App
cd src/frontend/service-desk-web
npm install
npm run dev
```

---

<a id="dich-vu-va-api"></a>
## 🌐 Ma trận Dịch vụ & Endpoints Mặc định

Khi chạy đầy đủ stack qua Docker Compose:

| Thành phần | Container | Cổng Nội bộ / Máy chủ | URL truy cập | Thông tin tài khoản / Ghi chú |
|---|---|---|---|---|
| **Cổng Web (SPA)** | `frontend` | `nginx:80` $\rightarrow$ `:80` | [http://localhost](http://localhost) | Cổng giao diện người dùng React 19 |
| **API Gateway** | `nginx` | `nginx:80/api` | [http://localhost/api](http://localhost/api) | Cân bằng tải tới `api-1` & `api-2` |
| **Node API 1 trực tiếp** | `api-1` | `8080` $\rightarrow$ `5000` | [http://localhost:5000](http://localhost:5000) | Instance API trực tiếp 1 |
| **Node API 2 trực tiếp** | `api-2` | `8080` $\rightarrow$ `5001` | [http://localhost:5001](http://localhost:5001) | Instance API trực tiếp 2 |
| **Giao diện Swagger** | `api-1` | `5000/swagger` | [http://localhost:5000/swagger](http://localhost:5000/swagger) | Tài liệu OpenAPI tương tác trực tiếp |
| **Kiểm tra Sức khỏe** | `nginx` | `80/health` | [http://localhost/health](http://localhost/health) | Trạng thái sức khỏe các hệ thống con |
| **Bảng điều khiển MinIO** | `minio` | `9001` $\rightarrow$ `9001` | [http://localhost:9001](http://localhost:9001) | `minioadmin` / `minioadmin` |
| **MinIO S3 API** | `minio` | `9000` $\rightarrow$ `9000` | [http://localhost:9000](http://localhost:9000) | Endpoint giao tiếp S3 API |
| **PostgreSQL DB** | `postgres` | `5432` $\rightarrow$ `5432` | `localhost:5432` | User: `servicedesk`, DB: `servicedesk` |

### Danh sách API cốt lõi (Auth & Ticketing MVP)

#### Cấu hình Môi trường (Seed tài khoản Admin ban đầu)
```env
ADMIN_EMAIL=admin@example.test
ADMIN_PASSWORD=ValidAdminPass1
```

#### Endpoints Quản lý Xác thực & Tài khoản (Auth)
- `POST /auth/register` (Công khai) — Tự đăng ký tài khoản `Requester`; trả về JWT và thông tin user.
- `POST /auth/login` (Công khai) — Đăng nhập bằng email và mật khẩu; trả về JWT và thông tin user.
- `GET /auth/me` (Yêu cầu đăng nhập) — Lấy thông tin user hiện tại kèm danh sách role.
- `PUT /admin/users/{id}/role` (Chỉ Admin) — Cập nhật role cho user (`Requester`, `Agent`, `Admin`).

#### Endpoints Vòng đời Yêu cầu (Ticket Lifecycle)
- `POST /tickets` (Yêu cầu đăng nhập) — Tạo ticket mới ở trạng thái `Submitted` với priority tự động tính (`Impact × Urgency`).
- `GET /tickets/me` (Yêu cầu đăng nhập) — Xem danh sách ticket. Requester chỉ xem được ticket của mình. Agent/Admin xem workspace kèm các bộ lọc tùy chọn (`status`, `assigneeId`, `requesterId`, `page`, `pageSize`).
- `GET /tickets/{id}` (Chủ vé, Agent, Admin) — Xem thông tin chi tiết ticket.
- `PATCH /tickets/{id}` (Chủ vé Requester) — Sửa tiêu đề, mô tả, danh mục, impact, urgency của ticket khi còn ở trạng thái `Submitted`.
- `POST /tickets/{id}/claim` (Chỉ Agent) — Nhận xử lý ticket chưa có người gán (`Submitted` $\rightarrow$ `InProgress`).
- `POST /tickets/{id}/assign` (Chỉ Admin) — Gán ticket cho Agent hoặc Admin.
- `POST /tickets/{id}/resolve` (Agent được gán, Admin) — Đánh dấu ticket `InProgress` là `Resolved`.
- `POST /tickets/{id}/reopen` (Chủ vé Requester, Admin) — Mở lại ticket từ `Resolved` về `InProgress`.
- `POST /tickets/{id}/close` (Chủ vé Requester, Admin) — Đóng ticket `Resolved` hoàn tất (trạng thái kết thúc).
- `POST /tickets/{id}/cancel` (Chủ vé Requester, Admin) — Hủy ticket `Submitted` (trạng thái kết thúc).
- `POST /tickets/{id}/comments` (Chủ vé, Agent, Admin) — Thêm trao đổi công khai vào ticket chưa kết thúc.
- `GET /tickets/{id}/comments` (Chủ vé, Agent, Admin) — Xem danh sách trao đổi của ticket theo trình tự thời gian.

---

<a id="kiem-thu"></a>
## 🧪 Chiến lược Kiểm thử (Testing Strategy)

Hệ thống đảm bảo độ tin cậy và chất lượng thông qua chiến lược kiểm thử đa tầng:

```bash
# Chạy toàn bộ bộ test backend
cd src/backend
dotnet test ServiceDesk.sln
```

### 1. Kiểm thử Tuân thủ Kiến trúc (`ServiceDesk.ArchitectureTests`)
Cưỡng chế các nguyên tắc Clean Architecture bằng **NetArchTest**:
- Lớp `Domain` có **0** phụ thuộc vào các tầng ngoài hoặc thư viện thứ ba.
- Lớp `Application` chỉ phụ thuộc duy nhất vào `Domain`.
- `Infrastructure` và `Api` không có tham chiếu vòng (circular reference).

### 2. Kiểm thử Đơn vị (`ServiceDesk.Domain.Tests` & `Application.Tests`)
Xác thực các thực thể nghiệp vụ, domain events, logic kiểm tra tính hợp lệ và các handler xử lý use-case một cách độc lập.

### 3. Kiểm thử Tích hợp (`ServiceDesk.IntegrationTests`)
Sử dụng **Testcontainers** để tự động khởi tạo cơ sở dữ liệu PostgreSQL 17 thực sự trong container Docker trong suốt quá trình chạy test, đảm bảo migration EF Core, câu truy vấn database và thao tác dữ liệu hoạt động chính xác tuyệt đối.

### 4. Kiểm tra Frontend
```bash
cd src/frontend/service-desk-web

# Kiểm tra kiểu TypeScript + verify build production
npm run build

# Kiểm tra phong cách mã nguồn và linting
npm run lint
```

---

<a id="cau-truc-du-an"></a>
## 📁 Cấu trúc Thư mục Dự án

```
ai-it-service-desk/
├── src/
│   ├── backend/
│   │   ├── ServiceDesk.sln                 # Solution Backend
│   │   ├── ServiceDesk.Domain/             # Tầng lõi: Entities, Value Objects, Domain Events
│   │   ├── ServiceDesk.Application/        # Tầng ứng dụng: Commands, Queries, Interfaces, DTOs
│   │   ├── ServiceDesk.Infrastructure/     # Tầng hạ tầng: EF Core, Npgsql, MinIO Storage, AI Client
│   │   ├── ServiceDesk.Api/                # Host API: Endpoints, Middleware, Swagger
│   │   └── ServiceDesk.Worker/             # Background Service: Xử lý tác vụ ngầm
│   └── frontend/
│       └── service-desk-web/               # Ứng dụng Frontend React 19 (Vite, TypeScript, Ant Design)
│           ├── src/
│           │   ├── components/             # UI components dùng chung
│           │   ├── features/               # Views & state theo từng module
│           │   ├── services/               # API clients (Axios / React Query)
│           │   └── routes/                 # Định tuyến ứng dụng
│           ├── Dockerfile                  # Container NGINX phục vụ frontend production
│           └── package.json
├── tests/
│   ├── ServiceDesk.ArchitectureTests/      # Kiểm tra ranh giới kiến trúc (NetArchTest)
│   ├── ServiceDesk.Domain.Tests/           # Unit tests cho tầng Domain
│   ├── ServiceDesk.Application.Tests/      # Unit tests cho use cases tầng Application
│   └── ServiceDesk.IntegrationTests/       # Integration tests với Docker Testcontainers
├── deploy/
│   └── nginx/                              # Cấu hình gateway NGINX & reverse proxy upstream
├── docs/
│   ├── adr/                                # Hồ sơ Quyết định Kiến trúc (001 - 010)
│   ├── architecture/                       # Sơ đồ và đặc tả thiết kế kiến trúc
│   └── superpowers/                        # Specs và implementation plans chi tiết
├── docker-compose.yml                      # File điều phối toàn bộ 7 dịch vụ
├── Jenkinsfile                             # Định nghĩa pipeline CI/CD đa tầng
├── TIEN-TRINH.md                           # Nhật ký theo dõi tiến độ chi tiết của dự án
└── .env.example                            # File mẫu khai báo các biến môi trường
```

---

<a id="quy-trinh-git"></a>
## 🌿 Quy trình Git & Chiến lược Nhánh

```
feature/*  ──►  dev  ──►  staging  ──►  main (Production)
```

- `feature/<name>`: Triển khai tính năng mới hoặc sửa lỗi.
- `dev`: Nhánh tích hợp thường xuyên được kiểm tra tự động qua CI.
- `staging`: Môi trường kiểm thử tiền phát hành phản chiếu môi trường production.
- `main`: Nhánh production ổn định sẵn sàng gắn tag release.

### Quy ước Commit Message
Tuân thủ chuẩn [Conventional Commits](https://www.conventionalcommits.org/):
- `feat:` Bổ sung tính năng hoặc nghiệp vụ mới
- `fix:` Sửa lỗi
- `refactor:` Tái cấu trúc mã nguồn không thay đổi hành vi
- `test:` Bổ sung hoặc cập nhật bộ test
- `docs:` Cập nhật tài liệu kỹ thuật hoặc ADRs
- `chore:` Cập nhật hạ tầng, Docker hoặc công cụ phụ trợ

---

<a id="lo-trinh"></a>
## 🗺 Lộ trình Phát triển (Development Roadmap)

| Giai đoạn | Trọng tâm | Trạng thái | Hạng mục bàn giao |
|:---:|---|:---:|---|
| **Phase 1** | **Nền tảng & Khung kiến trúc** | ✅ Hoàn thành | Thiết lập Clean Architecture, EF Core, Postgres + pgvector, MinIO, Docker Compose, Jenkins CI, Testcontainers, Architecture tests. |
| **Phase 2** | **Nghiệp vụ Cốt lõi & Ticket Lifecycle** | ✅ Hoàn thành | CRUD Ticket, State machine workflow, Identity & RBAC 3 vai trò, tự động tính Priority, phân quyền an toàn, API endpoints hoàn chỉnh. |
| **Phase 3** | **Kho Tri thức & Tích hợp AI** | 📅 Kế hoạch tiếp theo | Bài viết Markdown, tạo vector embeddings, tìm kiếm RAG ngữ nghĩa, gợi ý phân loại tự động bằng AI. |
| **Phase 4** | **Phân tích, Quản trị SLA & Production** | 📅 Kế hoạch tiếp theo | Cảnh báo vi phạm SLA, dashboard thời gian thực, quản lý tệp đính kèm MinIO, tôi ưu hóa bảo mật production. |

---

<a id="giay-phep"></a>
## 📄 Bản quyền & Phân phối

Dự án được phân phối theo giấy phép mã nguồn mở **MIT License**.  
Được phát triển và duy trì như một giải pháp quản lý dịch vụ IT nội bộ tích hợp trí tuệ nhân tạo cấp doanh nghiệp.
