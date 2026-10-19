# ADR-001: Modular Monolith Architecture

> Bổ sung phạm vi ngày 2026-10-19: Quyết định Modular Monolith vẫn áp dụng. Năm module là định hướng sản phẩm ban đầu, không phải yêu cầu tạo module rỗng. Phần đã triển khai và ranh giới hiện hành theo ADR-014: Identity, Ticketing, AIAssistance; Knowledge/Analytics chưa triển khai. Xem [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md) và [bản đồ tài liệu](../README.md).

## Status
Accepted

## Context
Project nhóm 5 người cần tạo hệ thống IT Service Desk với nhiều domain area (Identity, Ticketing, Knowledge, AI Assistance, Analytics). Cần kiến trúc rõ ràng nhưng không quá phức tạp.

## Decision
Chọn **Modular Monolith** — 5 logical modules trong một deployment unit.

## Alternatives Considered
- **Microservices**: Quá phức tạp cho nhóm 5 người, distributed-system overhead không cần thiết.
- **Simple Monolith (no modules)**: Thiếu boundary rõ ràng, khó bảo trì khi project lớn lên.

## Consequences
- **Positive**: Đơn giản deploy, dễ debug, vertical PR review, tránh distributed overhead.
- **Negative**: Cần discipline để giữ module boundary (enforced bằng architecture tests).
- **Neutral**: Có thể tách module thành service sau nếu cần (unlikely).
