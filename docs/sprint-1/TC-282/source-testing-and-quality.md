# 06 — Testing & Quality Foundation

## Test projects
`ServiceDesk.Domain.Tests`, `ServiceDesk.Application.Tests`, `ServiceDesk.IntegrationTests`, `ServiceDesk.ArchitectureTests`.

## Architecture tests — Must
- Domain không reference Infrastructure.
- Domain không reference EF Core/ASP.NET Core.
- Application không reference Infrastructure.
- Application không phụ thuộc Presentation.

Dùng library phù hợp hoặc assembly-reference assertions đơn giản; không build custom framework lớn.

## Integration foundation
Dùng Testcontainers for .NET với PostgreSQL thật. Không dùng EF Core InMemory để giả PostgreSQL behavior.

Tests phù hợp:
- API host khởi động.
- `/health` hoạt động.
- PostgreSQL Testcontainer connect được.
- DbContext open/migrate baseline Identity schema nếu Identity đã setup.
- critical configuration validation rõ ràng.

## Frontend
TypeScript compile, Vite production build, lint nếu cấu hình ESLint. Không cần fake component tests.

## CI
Live AI không là dependency của CI. AI abstraction dùng Fake/NoOp.

Foundation tốt = dependency đúng + build/test/startup reproducible + config rõ + infrastructure tách core + team có thể thêm feature mà không phá solution structure.
