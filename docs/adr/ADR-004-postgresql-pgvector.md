# ADR-004: PostgreSQL + pgvector

## Status
Accepted

## Context
Cần database cho business data và vector embeddings cho AI similarity search.

## Decision
Chọn **PostgreSQL** làm database chính với **pgvector** extension cho vector storage.

## Alternatives Considered
- **SQL Server**: License cost, ít phù hợp Linux-first stack.
- **PostgreSQL + separate vector DB (Pinecone/Weaviate)**: Thêm complexity không cần thiết cho scale hiện tại.

## Consequences
- **Positive**: Một database cho cả relational và vector, open source, mature, pgvector performance đủ cho scale dự án.
- **Negative**: pgvector không scale bằng dedicated vector DB ở millions of vectors (không phải concern hiện tại).
