# ADR-007: AI Provider Abstraction

## Status
Accepted

## Context
Hệ thống tích hợp AI để hỗ trợ phân loại ticket và tìm kiếm knowledge base. Cần tránh vendor lock-in và đảm bảo core workflow hoạt động khi AI không khả dụng.

## Decision
Định nghĩa application-owned interfaces (`ITicketClassificationService`, `IEmbeddingService`). Infrastructure cung cấp NoOp implementations cho bootstrap. Provider thật (OpenAI, Azure AI, local model) sẽ được thêm sau qua adapter pattern.

## Alternatives Considered
- **Gọi trực tiếp SDK trong Application layer**: Vi phạm Clean Architecture, khó test, khó thay đổi provider.
- **Dùng một `IAiService` lớn duy nhất**: Vi phạm ISP, khó maintain khi thêm capability mới.
- **Không có abstraction, hardcode NoOp**: Không chuẩn bị cho tương lai, phải refactor lớn khi tích hợp AI thật.

## Consequences
- **Positive**: Dễ thay provider, test dùng NoOp/Fake, AI failure không break core workflow, ISP-compliant.
- **Negative**: Cần tạo adapter mới cho mỗi provider (đây là trade-off có chủ đích).
- **Neutral**: CI/CD không phụ thuộc live AI service.
