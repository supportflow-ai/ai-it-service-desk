# ADR-006: MinIO Object Storage Abstraction

## Status
Accepted

## Context
Hệ thống cần lưu trữ file đính kèm (attachments) cho ticket. Cần giải pháp S3-compatible, self-hosted, nhẹ, không phụ thuộc cloud provider.

## Decision
Chọn **MinIO** làm object storage, truy cập qua application-owned abstraction `IObjectStorageService`. Infrastructure cung cấp `MinioObjectStorageService`.

## Alternatives Considered
- **Lưu file trong PostgreSQL (bytea)**: Không phù hợp cho file lớn, tăng backup size, giảm DB performance.
- **Lưu trực tiếp trên filesystem**: Không portable giữa containers, không scale, khó backup.
- **AWS S3 trực tiếp**: Phụ thuộc cloud provider, tốn chi phí, không self-hosted.

## Consequences
- **Positive**: Self-hosted, S3-compatible API, dễ thay thế bằng AWS S3/R2/GCS nếu cần, UI console quản lý file.
- **Negative**: Thêm một service trong Docker Compose, cần quản lý storage volume.
- **Neutral**: Application layer không biết MinIO — chỉ biết `IObjectStorageService`.
