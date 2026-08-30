# ADR-002: Clean Architecture

## Status
Accepted

## Context
Cần tách biệt business logic khỏi framework/infrastructure, đảm bảo testability và maintainability.

## Decision
Áp dụng **Clean Architecture** với 4 projects:
- **Domain** — entities, value objects, business rules (zero dependencies)
- **Application** — use cases, port interfaces (depends on Domain only)
- **Infrastructure** — adapters: EF Core, MinIO, AI providers (implements Application ports)
- **Api** — composition root, HTTP endpoints (references Application + Infrastructure)

## Alternatives Considered
- **N-tier (Controller → Service → Repository)**: Không enforce dependency inversion, Service layer thường thành god class.
- **Vertical Slices only**: Khó enforce module boundary rõ ràng cho nhóm.

## Consequences
- **Positive**: Domain không phụ thuộc framework, testable, dễ swap infrastructure.
- **Negative**: Nhiều project hơn, cần hiểu dependency rule.
- **Enforcement**: Architecture tests (NetArchTest) tự động kiểm tra dependency rule.
