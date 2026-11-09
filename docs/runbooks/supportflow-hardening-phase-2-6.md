# Hoàn thiện attachment, hợp đồng và kết nối AI

Hồ sơ đợt củng cố (lịch sử), đã hợp nhất vào `dev` qua PR #50–#53; nội dung giữ nguyên để truy vết. Hướng dẫn hiện hành: [kiểm chứng backend](backend-hardening-verification.md). Trạng thái "chưa commit/working tree" bên dưới là của thời điểm viết, không mô tả `dev` hôm nay.

Phạm vi: working tree trên `dev`, đã hợp nhất ba commit và fast-forward từ `44ba0b4` tới `659289c3a1cf8e4b9da86a15ea890ee59bcc3e6d`, cập nhật 03/11/2026. Đây là hướng dẫn kiểm chứng code đang sửa, chưa phải bằng chứng triển khai VPS hoặc phát hành. Fetch GitHub trong lần này bị lỗi mạng; mốc tích hợp là ref origin/dev đã có trên máy.

## Attachment

1. `upload-url` lưu yêu cầu có actor, ticket, metadata, hạn dùng một giờ. Mỗi actor có tối đa 20 yêu cầu chưa xác nhận còn hạn; PostgreSQL advisory lock bảo vệ giới hạn khi có nhiều API.
2. Kết quả thêm `formData` tùy chọn. Khi có trường này, client gửi multipart POST theo đúng các trường đã ký và đặt file cuối cùng; client cũ chỉ biết PUT cần được phát hành đồng bộ với backend. Không đổi hostname URL sau khi ký.
3. Confirm phải khớp actor/ticket/metadata, object tồn tại, đúng loại và kích thước thực tế. Adapter sao chép theo ETag sang key cuối, kiểm tra lại rồi ghi attachment và liên kết grant trong cùng transaction. URL upload cũ chỉ ghi được key nguồn, không sửa bản sao cuối được tạo qua luồng này.
4. Confirm lặp trả cùng attachment. API chỉ cấp download URL cho metadata đã xác nhận; tệp cũ đã xác nhận trước migration vẫn được đọc.
5. Dọn object chờ sau commit có timeout hai giây và không đổi kết quả commit thành lỗi. Không tự xóa key cuối sau lỗi commit vì kết quả có thể chưa xác định. Object cuối mồ côi còn cần quy trình đối soát metadata trước khi xóa; chưa có tác vụ tự động cho phần này.

Chỉ dùng Domain `AttachmentUploadGrant`, DbSet `AttachmentUploadGrants` và bảng `attachment_upload_grants`. Cơ chế `TicketAttachmentUpload` và hai tệp migration local `AddAttachmentUploadIntents` chưa áp dụng đã được lưu vào bản sao trước hợp nhất, đưa ra khỏi code đang hoạt động.

Giữ nguyên nội dung migration [AddAttachmentUploadGrant](../../src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20261103090000_AddAttachmentUploadGrant.cs) từ nhánh tích hợp. Migration bổ sung [LinkAttachmentUploadGrant](../../src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20261103090001_LinkAttachmentUploadGrant.cs) thêm `attachment_id` nullable, index và FK; thứ tự đứng sau migration cũ. Chưa áp dụng migration mới vào database nào.

Backfill chỉ nối grant đã consume khi có duy nhất một attachment khớp ticket, actor, key, tên, kích thước và MIME. Grant thiếu hoặc mơ hồ vẫn giữ nguyên và không được xác nhận lại thành một tệp khác. Metadata/key của tệp cũ không bị đổi; danh sách/download cũ vẫn dùng metadata đã có.

Grant PUT cũ còn hạn, chưa consume, được xác nhận bằng cách kiểm tra và promote như grant POST mới. Tệp đã xác nhận bằng phiên bản cũ trước nâng cấp vẫn chịu giới hạn URL PUT cũ cho tới khi URL hết hạn; migration SQL không thể sao chép object để thay đổi điều này. Chưa có tác vụ tự động promote tệp lịch sử.

Cấu hình `Minio:Bucket` là nguồn bucket duy nhất. `Minio:PublicEndpoint` là origin HTTP(S) trình duyệt truy cập được, không kèm path/query/credential; `Minio:Endpoint` dành cho mạng nội bộ. Infra cấp tài khoản ứng dụng giới hạn bucket, cấu hình lifecycle cho prefix `uploads/pending/` của bucket chuyên dụng. SDK MinIO 6.0.5 biểu diễn giới hạn policy bằng chuỗi số; MinIO có bộ chuyển đổi số cho dạng này. Kiểm thử vẫn kiểm tra đúng hai cận, không giả định cách biểu diễn JSON. [Nguồn bộ phân tích MinIO](https://github.com/minio/minio/blob/master/cmd/postpolicyform.go).

## Revision và contract

`ExpectedRevision` chỉ tùy chọn trên lược đồ OpenAPI (kiểu `uint?`); khi chạy, các lệnh cập nhật, phân công, hủy phân công, tiếp nhận, giải quyết, mở lại, đóng, hủy ticket và các lệnh sửa nháp, xuất bản, lưu trữ bài viết Knowledge đều trả 400 nếu thiếu hoặc bằng 0, và 409 nếu khác revision hiện tại (đối chiếu `dev@ef043b3`, `TicketCommandGuard.RequireCurrentRevision`). Frontend gửi revision hợp lệ, truyền body cho assign và query cho các lệnh còn lại. HTTP 409 không tự retry mutation.

[Baseline OpenAPI](../../contracts/backend/openapi-baseline.json) thêm `UploadUrlResult.formData`. Frontend lưu bản sao, manifest SHA nền + checksum working tree, sinh kiểu TypeScript và kiểm tra drift. Chỉ thay SHA nền thành SHA phát hành sau khi backend đã tích hợp/commit; không gọi bản working tree là contract của một commit mới.

## HTTP adapter AI — phần đầu đợt 5

`Ai:Enabled=false` giữ NoOp mặc định. Muốn bật adapter: cấu hình `Ai:Enabled=true`, `Endpoint` là origin nội bộ, `BearerToken` ít nhất 32 ký tự, `TimeoutSeconds` từ 1 đến 30 (mặc định 5). Không lưu token trong Git hoặc đưa vào log.

Adapter gửi snapshot tối thiểu theo contract v1; dùng một requestId cho tối đa hai lần gửi. Chỉ retry một lần cho HTTP 502/503/504 trong cùng ngân sách timeout. Không theo redirect; không chuyển token sang endpoint khác. Phản hồi tối đa 64 KiB; sai kiểu, trường lạ, requestId/ticketId/revision/hash lệch đều bị từ chối. Ba lần phân loại lỗi liên tiếp ngắt gọi 30 giây. Lỗi hạ tầng được đổi thành kết quả có kiểm soát; caller cancellation vẫn được tôn trọng.

Chưa có use case yêu cầu phân loại, persistence analysis/suggestion/review, Outbox, Worker xử lý tác vụ hoặc endpoint Agent duyệt. Vì vậy bật adapter chưa tạo ra luồng AI đầu cuối. Không bổ sung modelVersion vào payload đóng của v1.

## Kiểm chứng và phát hành

- Build solution và chạy các kiểm tra trong [AGENTS](../../AGENTS.md).
- `HttpClassificationAdapterTests`: HTTP giả ở transport boundary, xác minh hợp đồng, timeout, giới hạn phản hồi, retry và circuit. Đây không phải nghiệm thu AI Service thật.
- `AttachmentStorageContractTests`: mapping/migration snapshot và policy ký offline.
- `AttachmentGrantMigrationTests`: PostgreSQL riêng dùng một lần, nâng từ schema grant cũ lên schema mới, giữ nguyên metadata và chỉ backfill trường hợp duy nhất; kiểm tra chạy migrate lại an toàn. Đã biên dịch, chưa chạy do Docker không hoạt động.
- `MinioAttachmentUploadTests`: Docker MinIO thật, POST đúng kích thước, từ chối sai kích thước, replay URL cũ không đổi bản sao cuối. Chạy cùng nhóm `Category=Integration` trong CI.
- `TicketAttachmentPersistenceTests`, `TicketAuditPersistenceTests`, `TicketCommentAuthorizationEndpointTests`: PostgreSQL/HTTP thật; chưa thay bằng SQLite hoặc InMemory.

Workflow CI giữ các kiểm tra hiện có. Job `publish` chỉ chạy khi dispatch thủ công với `publish_images=true` ở `main`/`dev` của tổ chức dự án, sau job kiểm tra thành công. Nó phát hành tag SHA, ghi digest hợp lệ, provenance và SBOM. Không tự dispatch, push hoặc triển khai khi chạy kiểm tra local.

Ba commit `659289c`, `40c9e60`, `7a86731` đã được tích hợp vào nhánh local. Giữ test mới từ remote, bổ sung các bất biến local; mọi thao tác ticket–audit dùng cùng `UpdateTicketWithAuditAsync` với kiểm tra audit thuộc ticket. Requester bình luận ngoài quyền tiếp tục nhận 404 theo kế hoạch, đã đồng bộ assertion use case/HTTP; không đổi quyền Agent/Admin.

Kiểm chứng sau hợp nhất: build Release đạt bằng package cache, 330 kiểm thử xUnit chạy trực tiếp đạt (Domain 125, Application 123, Architecture 26, Integration assembly offline 56). Khôi phục phụ thuộc chuẩn/audit vẫn bị mạng chặn; lần restore bổ sung để biên dịch dùng `RestoreLockedMode=true` và `NuGetAudit=false` ở tham số lệnh, không sửa cấu hình repo/CI và không được coi là audit đạt. `dotnet test` chuẩn vẫn lỗi kết nối testhost. PostgreSQL/MinIO/HTTP tích hợp thật còn phải chạy trước nghiệm thu.
