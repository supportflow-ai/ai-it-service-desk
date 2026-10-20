# ADR-006: Cổng lưu trữ đối tượng và MinIO

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Bối cảnh

Hệ thống có định hướng lưu tệp đính kèm cho phiếu yêu cầu, cần giải pháp tương thích S3 và có thể tự vận hành.

## Quyết định

Chọn MinIO để lưu đối tượng, truy cập qua giao diện `IObjectStorageService` do Application sở hữu. Infrastructure triển khai `MinioObjectStorageService`.

## Phương án đã cân nhắc

- Lưu tệp trong PostgreSQL bằng `bytea`: tăng dữ liệu sao lưu và tải của cơ sở dữ liệu khi có tệp lớn.
- Lưu trực tiếp vào hệ thống tệp: khó dùng chung giữa các container, mở rộng và sao lưu.
- Gọi trực tiếp AWS S3: phụ thuộc nhà cung cấp và chi phí, không đáp ứng mục tiêu tự vận hành ban đầu.

## Hệ quả

Có API tương thích S3 và giao diện quản lý tệp; Application chỉ biết cổng lưu trữ. Đổi lại, phải vận hành thêm dịch vụ và quản lý vùng lưu dữ liệu. Việc thay nhà cung cấp cần đánh giá mức tương thích thực tế của bộ tích hợp.

Đã có cổng/bộ tích hợp không có nghĩa nghiệp vụ tải lên/tải xuống đã triển khai. Các quyết định còn mở nằm tại [DEC-06–07](../architecture/pending-business-decisions.md).

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-006-minio-object-storage.md).
