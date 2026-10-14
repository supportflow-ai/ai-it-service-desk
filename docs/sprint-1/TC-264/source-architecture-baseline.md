# 02 — Architecture Baseline

## System architecture

Chọn **Modular Monolith** vì có nhiều domain area rõ nhưng vẫn là một sản phẩm/deployment unit; phù hợp nhóm 5 người và tránh distributed-system overhead.

Không chọn Microservices, Kubernetes, Kafka-first/event-driven architecture, database-per-service.

## Logical modules

```text
Identity
Ticketing
Knowledge
AI Assistance
Analytics
```

Dependency intent:
```text
Identity contracts ← Ticketing
Identity contracts ← Knowledge
Ticketing + Knowledge application contracts ← AI Assistance
Ticketing + Knowledge + AI projections → Analytics
```

Không cyclic dependency. Analytics không điều khiển workflow. AI Assistance không sở hữu core Ticket state transition.

## Clean Architecture

```text
Presentation/API → Application → Domain
Infrastructure   → Application-owned ports
```

Domain chỉ phụ thuộc BCL/domain-owned abstractions.

Application phụ thuộc Domain + application-owned interfaces; không phụ thuộc Infrastructure hoặc UI.

Infrastructure implement adapters, không chứa core business decisions.

API/Presentation là HTTP/composition boundary, không bypass Application để ghi DB/gọi provider.

## EF Core policy

Không bắt buộc Generic Repository. DbContext/DbSet đã cung cấp nhiều repository-like behavior. Chỉ tạo `IApplicationDbContext` hoặc gateway/repository chuyên biệt khi giúp bảo vệ use case, testability hoặc module boundary.

## SOLID intent

- SRP: tách theo reason-to-change.
- OCP: provider mới qua adapter.
- LSP: MinIO/S3/R2 hoặc fake AI provider thay thế cùng contract.
- ISP: tránh `IAiService` khổng lồ.
- DIP: Application phụ thuộc abstraction, không external SDK.

## Ports phù hợp foundation

Có thể tạo nếu wiring thực sự dùng:
`IApplicationDbContext`, `ICurrentUser`, `IClock`, `IObjectStorageService`, `ITicketClassificationService`, `IEmbeddingService`.

Không tạo speculative interfaces.
