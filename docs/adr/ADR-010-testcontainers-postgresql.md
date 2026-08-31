# ADR-010: Testcontainers for Integration Tests

## Status
Accepted

## Context
Integration tests cần PostgreSQL thật để kiểm tra EF Core migrations, Identity schema, và database behavior. Dùng EF Core InMemory provider sẽ bỏ qua các PostgreSQL-specific behavior (constraints, transactions, pgvector).

## Decision
Dùng **Testcontainers for .NET** với PostgreSQL container thật. Mỗi test run khởi động một PostgreSQL container tạm, chạy tests, rồi dọn dẹp. WebApplicationFactory replace DbContext connection string sang Testcontainer.

## Alternatives Considered
- **EF Core InMemory provider**: Không hỗ trợ constraints, transactions, PostgreSQL-specific features (pgvector). False positives khi test pass nhưng production fail.
- **SQLite InMemory**: Gần SQL hơn nhưng vẫn thiếu PostgreSQL dialect, không hỗ trợ pgvector.
- **Shared dev PostgreSQL instance**: Flaky tests do shared state, không reproducible, cần cleanup logic phức tạp.

## Consequences
- **Positive**: Tests chạy trên PostgreSQL thật, reproducible, isolated, phát hiện migration errors sớm.
- **Negative**: Cần Docker runtime trên dev machine và CI agent, test startup chậm hơn InMemory (~2-5s cho container).
- **Neutral**: MinIO và external AI provider được mock/stub trong test environment.
