# ADR-002: Clean Architecture

> Bổ sung phạm vi ngày 2026-10-19: Clean Architecture vẫn áp dụng. Bốn project dưới đây mô tả bốn tầng ban đầu; Worker và Database là host/công cụ bổ sung. Application được dùng DI abstractions. Cấu trúc sáu project, feature folders và Published Contracts theo ADR-011/014. Xem [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md) và [bản đồ tài liệu](../README.md).

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
