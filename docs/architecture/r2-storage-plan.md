# Kế hoạch chuyển lưu trữ tệp sang R2 (backend)

Trạng thái: **Đã chốt · chờ triển khai**. [ADR-017](../adr/ADR-017-cloudflare-r2-object-storage.md) được chấp nhận ngày 16/11/2026, áp dụng R2 cho local, kiểm thử, CI, staging và production. Đợt D5 đối chiếu `dev@3640c33` ngày 21/11/2026. Backend tại mốc này vẫn đăng ký `MinioObjectStorageService`, cấp presigned POST cùng `formData` và ký GET theo cấu hình MinIO. Chi tiết là kế hoạch đề xuất cần kiểm chứng. Kế hoạch phối hợp nằm ở Web (`docs/architecture/r2-upload-plan.md`), Infra (`docs/infra/r2-storage-plan.md`) và AI Service (`docs/r2-impact.md`).

## 1. Phạm vi

Thay bộ cài đặt lưu trữ tệp từ MinIO sang Cloudflare R2, đổi cơ chế tải lên từ presigned POST sang presigned PUT, giữ nguyên nghiệp vụ tệp đính kèm (quyền, hạn mức, vòng đời phiếu, xác nhận hai bước). Không chuyển dữ liệu cũ. Không thêm tính năng mới (quét mã độc, lưu giữ, xóa tệp đã xác nhận vẫn thuộc DEC-07 còn mở).

Hiện tại grant, URL tải lên và URL tải xuống đều dùng hạn 3.600 giây. Cấp grant kiểm quyền, trạng thái phiếu, giới hạn 10 MiB/tệp và tối đa 20 grant chưa tiêu thụ, còn hạn trên mỗi người gọi. Giới hạn 10 tệp và 50 MiB theo phiếu áp dụng với metadata đã xác nhận trong giao dịch. [Hợp đồng Backend hiện hành](current-backend-contract.md)

Nguyên tắc: cổng `IObjectStorageService` vẫn do tầng Application sở hữu; chỉ `Platform` biết AWSSDK; không nới kiểm thử kiến trúc; không sửa migration đã phát hành.

## 2. Các tệp bị ảnh hưởng

| Tệp | Việc phải làm |
| --- | --- |
| `BuildingBlocks/SharedKernel/Application/Abstractions/IObjectStorageService.cs` | Thu gọn cổng (mục 3.1); `PresignedUpload` đổi `FormData` thành `Headers` |
| `Platform/Storage/MinioObjectStorageService.cs` | Xóa; thay bằng `R2ObjectStorageService` |
| `Platform/Options/MinioOptions.cs` | Xóa; thay bằng `R2Options` |
| `Platform/DependencyInjection.cs` (`AddStorage`) | Đăng ký `R2Options`, client S3, adapter; nhánh "chưa cấu hình" (mục 3.4) |
| `Platform/ServiceDesk.Platform.csproj`, các `packages.lock.json` | Bỏ `Minio` 6.0.5, thêm `AWSSDK.S3` (phiên bản chốt sau giai đoạn 0); sinh lại lock file bằng `dotnet restore`, review diff |
| `Modules/Ticketing/Application/Contracts/AttachmentContracts.cs` | `UploadUrlResult.FormData` thành `RequiredHeaders` (tùy chọn) |
| `Modules/Ticketing/Application/GenerateAttachmentUploadUrl/*UseCase.cs` | Hạn URL theo cấu hình; trả `RequiredHeaders`; bỏ `formData` |
| `Modules/Ticketing/Application/ConfirmAttachmentUpload/*UseCase.cs` | Xóa object tạm khi dung lượng hoặc loại tệp không khớp; sau cutover bỏ nhận tiền tố cũ `tickets/{id}/` |
| `Modules/Ticketing/Application/GetAttachmentDownloadUrl/*UseCase.cs` | Hạn URL theo cấu hình |
| `Modules/Ticketing/Domain/TicketAttachment.cs` | Sửa chú thích còn nhắc MinIO |
| `Hosts/Api/appsettings.json`, `appsettings.Development.json` | Thay mục `Minio` bằng mục `R2` rỗng; bỏ khóa mặc định `minioadmin` |
| `.env.example`, `.gitignore` | Biến `R2__*` chỉ có chỗ giữ chỗ; thêm `.env.*` kèm `!.env.example` |
| `scripts/run_backend.py`, `scripts/tests/test_run_backend.py` | Bí danh `MINIO_*` thành tên `R2__*`; cập nhật kiểm thử |
| `contracts/backend/openapi-baseline.json` | Xuất lại có chủ đích sau khi đổi phản hồi `upload-url` |
| `tests/…` | Mục 4 |

Cổng hiện có bốn phương thức chỉ test giả dùng: `GetPresignedUploadUrlAsync`, `StatObjectAsync`, `BucketExistsAsync`, `EnsureBucketExistsAsync` (không use case nào gọi). Xác nhận lại bằng tìm kiếm khi bắt đầu rồi xóa.

## 3. Thiết kế thay đổi mã

### 3.1 Cổng

Giữ: `DefaultBucket`, `CreateUploadAsync`, `StatAsync`, `CopyAsync`, `DeleteAsync`, `GetPresignedDownloadUrlAsync`. Bucket do ops tạo; token ứng dụng không có quyền tạo bucket nên bỏ `EnsureBucketExistsAsync`.

### 3.2 Adapter `R2ObjectStorageService` (trong `Platform`)

- `AmazonS3Client` với `ServiceURL = R2:Endpoint`, vùng xác thực `auto`; cờ riêng cho R2 theo kết quả giai đoạn 0.
- Ký URL tải lên: `GetPreSignedURL` với `Verb = PUT`, `ContentType` nằm trong chữ ký, hạn `UploadExpirySeconds`. Trả về `PresignedUpload(Url, Headers { Content-Type })`. Việc ký không gọi mạng và không bị tính phí.
- `StatAsync`: `HEAD`; trả `StoredObject(Size, ContentType, ETag)`; `404` thành `null`.
- `CopyAsync`: `CopyObject` kèm điều kiện ETag nguồn; `412` thành lỗi rõ ràng (khác lỗi mạng).
- `DeleteAsync`: giữ hành vi hiện tại (ghi log cảnh báo không kèm URL hay khóa truy cập, ném lại lỗi).
- Không ghi vào log: URL đã ký, khóa truy cập, endpoint, nội dung phản hồi lỗi thô của R2.

### 3.3 Cấu hình `R2Options`

`Endpoint`, `AccessKeyId`, `SecretAccessKey`, `Bucket`, `UploadExpirySeconds` (đề xuất 900), `DownloadExpirySeconds` (đề xuất 300). Khi có cấu hình, kiểm tra endpoint HTTPS thuộc miền S3 của tài khoản R2, với host hợp lệ và không chứa path bổ sung, query hay thông tin đăng nhập. Bucket và cả hai khóa phải có giá trị. Khoảng 60–3.600 giây là giới hạn dự kiến của ứng dụng. Ghi tên trường lỗi, tránh ghi giá trị. Giai đoạn 0 chốt thời hạn URL và grant sau khi đo thời gian tải, xác nhận và thử lại.

### 3.4 Chưa cấu hình

Thiết kế cho phép phát triển tính năng chính khi `R2:*` hoàn toàn trống. Adapter "chưa cấu hình" trả `ServiceUnavailableException` cho thao tác tệp. Cấu hình một phần thì dừng khởi động. Stack hệ thống hoặc release đã bật luồng tệp cần kiểm đủ bộ biến từ trước khi khởi động.

### 3.5 Use case

- **Cấp URL:** kiểm đuôi tệp, dung lượng khai báo ≤ 10 MiB, quyền, trạng thái phiếu và hạn mức grant chờ. Tạo grant gắn phiếu, người gọi và metadata, ký đúng key tạm và chỉ trả URL sau khi lưu grant thành công. `RequiredHeaders` cung cấp đúng `Content-Type` đã ký. Giới hạn tổng tệp của phiếu tiếp tục được quyết định khi xác nhận dưới khóa.
- **Xác nhận:** kiểm quyền, grant và metadata, `HEAD` vùng tạm, `CopyObject` theo ETag nguồn, `HEAD` đích rồi ghi metadata. Giao dịch dùng khóa theo grant và khóa ghi theo phiếu để chống xác nhận trùng và vượt hạn mức đồng thời. Lần xác nhận hợp lệ sau commit trả metadata đã liên kết. Khi copy hoặc commit chưa rõ kết quả, đối soát metadata và object trước khi dọn đích. Với mismatch đã xác định trên object tạm, thiết kế xóa best-effort trong ngân sách ngắn. Kiểm rằng cleanup an toàn với confirm song song và thất bại không biến thành ghi nhận thành công.
- **Tải xuống:** chỉ đổi hạn URL. Không thêm `HEAD` để tránh một thao tác tính phí cho mỗi lượt tải; object thiếu (ví dụ bản ghi cũ trỏ vào khóa MinIO) làm R2 trả `404` khi mở liên kết.

### 3.6 Hợp đồng HTTP

`POST /api/tickets/{id}/attachments/upload-url`: phản hồi bỏ `formData` (luôn rỗng), thêm `requiredHeaders` tùy chọn. Các thao tác khác không đổi. Xuất lại baseline bằng quy trình hiện có (`EXPORT_API_BASELINE`), chạy `PublishedApiMatchesBaseline` với biến đó tắt, rồi báo web.

## 4. Kiểm thử

| Nhóm | Nội dung |
| --- | --- |
| Đơn vị (Application) | Dùng bộ lưu trữ giả. Cấp URL trả `RequiredHeaders`, không có `formData`. Xác nhận từ chối rồi xóa object tạm khi sai dung lượng hoặc loại tệp; không xóa khi lỗi chưa xác định. Cập nhật `TicketAttachmentUseCaseTests`, `AttachmentConfirmationSecurityTests` |
| Adapter, không cần mạng | Với khóa giả, kiểm URL đã ký: host, đường dẫn bucket và khóa, `X-Amz-Expires`, danh sách tiêu đề đã ký có `content-type`; từ chối cấu hình sai (host lạ, có path, thiếu bucket) |
| Hợp đồng lưu trữ | Viết lại `AttachmentStorageContractTests` trên bộ giả; cập nhật bản giả trong `ServiceDeskWebApplicationFactory` theo cổng mới |
| R2 thật (`Category=R2Live`) | Chỉ chạy khi có biến `R2__*`; mỗi lần chạy dùng tiền tố riêng và tự dọn. Gồm các kịch bản của giai đoạn 0: PUT đúng, sai `Content-Type`, vượt dung lượng, `HEAD`, `Copy` đúng và sai ETag, `Delete`. **Không** dùng nhãn `Integration` để bước tích hợp hiện tại không chạy nhầm khi thiếu khóa |
| Kiến trúc | Xác nhận chỉ `Platform` tham chiếu AWSSDK; không module nào tham chiếu |
| Xóa | `MinioAttachmentUploadTests` (Testcontainers MinIO) |

CI: thêm bước `R2Live` chỉ chạy khi kho bí mật có khóa dành riêng cho CI (bucket `ci`) và không chạy với PR từ nhánh fork (GitHub không cấp bí mật cho fork). Các bước hiện có giữ nguyên, không cần khóa.

## 5. Thao tác môi trường

- **Máy lập trình viên:** điền `R2__*` vào `.env` cục bộ (bộ khóa dùng chung của bucket `dev`, nhận qua kênh an toàn của nhóm). Không điền thì chỉ tính năng tệp báo không khả dụng.
- **Dữ liệu tệp cũ:** bản ghi trong `ticket_attachments` và `attachment_upload_grants` ở database dev trỏ vào khóa MinIO sẽ không còn tải được. Trên database chỉ dùng để lập trình, người phụ trách xóa các bản ghi này sau khi xác minh đích theo AGENTS.md §6 (đối chiếu `SD_DB_HOST`, `SD_DB_PORT`, `SD_DB_NAME`; chỉ làm khi được ủy quyền cho đúng đích). Đây là thao tác dữ liệu, **không phải migration**.
- **Khóa và endpoint không vào Git:** không đưa vào `appsettings*.json`, tài liệu, log, ảnh chụp hay chat.

## 6. Lộ trình

Cỡ việc: S ≤ 1 ngày, M 2–3 ngày, L > 3 ngày (ước lượng thô).

| Giai đoạn | Nội dung | Kho | Cỡ | Điều kiện xong |
| --- | --- | --- | --- | --- |
| 0. Chuẩn bị và kiểm chứng | Tạo bucket `dev`, token giới hạn bucket, CORS, quy tắc vòng đời; chạy chín kiểm chứng của ADR-017; chốt phiên bản AWSSDK.S3 và cờ R2; chốt hạn URL | infra, backend | M | Kết quả ghi vào kế hoạch này (ADR-017 đã được duyệt ngày 2026-11-16) |
| 1. Adapter và cổng | `R2Options`, `R2ObjectStorageService`, cổng thu gọn, nhánh chưa cấu hình, kiểm thử đơn vị và adapter | backend | L | Build, kiểm thử không cần mạng đạt; `R2Live` đạt trên bucket `dev` |
| 2. Luồng tệp | Use case, `RequiredHeaders`, xóa object tạm, baseline OpenAPI, cập nhật `.env.example`, `run_backend.py` | backend | M | Kiểm thử hợp đồng đạt; báo web |
| 3. Web và hạ tầng | Web chuyển sang PUT; infra bỏ MinIO; CI cấp khóa cho `R2Live` và smoke | web, infra | L | Smoke đạt với bucket `ci` |
| 4. Chuyển môi trường dev | Phát khóa `dev` cho nhóm, dọn bản ghi tệp cũ, bỏ nhận tiền tố cũ, cập nhật cẩm nang chung và tài liệu | cả bốn | M | Một thành viên mới dựng được môi trường chỉ từ tài liệu |
| 5. Trước phát hành | Bucket và token riêng staging, production; xoay hoặc thu hồi khóa `dev`; quyết định phần DEC-07 còn mở | infra, backend | M | Chủ dự án duyệt |

### Điều kiện chuyển môi trường và hoàn nguyên

- Chọn cùng ứng viên Backend/Web. Kiểm baseline phản hồi `upload-url`, môi trường đã gắn bucket riêng, ảnh ứng viên và kiểm thử thật ở Infra. Ngừng cấp grant theo luồng cũ và xử lý grant đang mở theo thời hạn đã ghi trước khi đổi storage. Theo ADR-017, object MinIO cũ không được chuyển sang R2.
- Trước lần ghi hoặc dọn dữ liệu đầu tiên với R2, có thể quay về cặp phiên bản cũ nếu metadata vẫn trỏ đúng MinIO và schema còn tương thích. Sau đó revert mã hoặc đổi endpoint riêng lẻ không khôi phục quyền đọc tệp R2. Nếu cần lùi, dừng ghi tệp, đối soát metadata với object của từng bên, xác nhận điểm phục hồi và mất dữ liệu chấp nhận được. Phương án phục hồi đưa metadata, object, schema và image về tổ hợp đã kiểm. Khi chưa có phương án cho dữ liệu thật, dừng phát hành và xử lý tiếp trên ứng viên tương thích. [Quy trình triển khai và khôi phục Backend](../runbooks/backend-refactor-rollout.md#khôi-phục-phiên-bản-trước)

### Bằng chứng cần ghi trên ứng viên R2

| Hạng mục | Điều kiện đạt |
| --- | --- |
| Grant và xác nhận | Trình duyệt PUT theo grant rồi confirm. Xác nhận lặp sau commit trả cùng ID tệp. Thử confirm đồng thời và sát hạn mức trên đúng schema, đối soát chỉ có một metadata cho một grant và quota không vượt 10 tệp/50 MiB. |
| Giới hạn và suy giảm | Ghi kết quả thực đo về giới hạn PUT, hạn URL/grant, ETag nguồn, timeout và phản hồi khi xác nhận chưa hoàn tất. Sau lỗi có thể thử lại bằng grant còn hợp lệ. Trường hợp đã từ chối vì sai metadata phải xin grant mới. |
| Đọc và dọn | Quyền đọc được kiểm trước khi ký GET. Kiểm tên tệp ở trình duyệt, URL hết hạn và quy tắc dọn `uploads/pending/` trên bucket thử. Đối soát object đích nếu lỗi xảy ra gần commit. |

Mỗi kết quả ghi môi trường, thời điểm, SHA/digest ứng viên, phiên bản SDK và mã trạng thái đã che dữ liệu nhạy cảm. Giữ `R2Live` là kiểm chứng riêng trên bucket thử. Kết quả mô phỏng hoặc smoke gateway không xác nhận luồng tệp thật.

## 7. Rủi ro

| Rủi ro | Giảm thiểu |
| --- | --- |
| R2 không ép dung lượng lúc PUT | Backend là chốt chặn ở bước xác nhận, xóa ngay khi sai, vòng đời dọn phần còn lại; đo thiệt hại tối đa ở giai đoạn 0, quyết định lại với Worker nếu cần |
| Chữ ký `Content-Type` làm tải lên thất bại khi web gửi khác giá trị | Backend trả `RequiredHeaders`; web chỉ dùng giá trị đó |
| AWSSDK.S3 không tương thích mặc định với R2 | Giai đoạn 0 chọn phiên bản và cờ; ghim phiên bản; `R2Live` bắt hồi quy khi nâng |
| Bộ khóa dùng chung bị lộ | Token giới hạn bucket `dev`, không có khóa production ở máy lập trình viên, đã bật quét bí mật và push protection của GitHub, xoay khóa ở giai đoạn 5 |
| Kiểm thử R2 thật làm CI phụ thuộc mạng và khóa | Nhãn riêng, chỉ chạy khi có khóa, không chặn bước mặc định |
| URL hết hạn không có tiêu đề CORS nên web không đọc được lỗi | Hạn đủ dài so với thao tác, web coi lỗi mạng khi tải lên là liên kết có thể đã hết hạn và cho người dùng thử lại |
| Bản ghi tệp cũ trỏ khóa không tồn tại | Dọn trên database dev (mục 5); môi trường nào có dữ liệu thật thì không áp dụng kế hoạch này khi chưa có phương án riêng |

## 8. Tài liệu phải cập nhật trong cùng thay đổi

`.env.example`, `.github/pull_request_template.md`, `AGENTS.md` (bảng cấu trúc, dòng `Platform`), `README.md` (các đoạn MinIO), [ADR-006](../adr/ADR-006-minio-object-storage.md) (đánh dấu bị ADR-017 thay thế), [danh mục ADR](../adr/README.md), [mô-đun và luồng xử lý](backend-modules-and-use-cases.md), [hợp đồng hiện hành](current-backend-contract.md) (đoạn luồng tệp đính kèm), [môi trường](../runbooks/environment-runbook.md), [bảng quyết định](pending-business-decisions.md) (DEC-06, DEC-07), [cẩm nang chung](../team-handbook.md) (bốn kho cùng lúc). Khi SHA/digest ghim đã chuyển và qua kiểm chứng, chuyển lệnh MinIO của cấu hình tương ứng sang lịch sử. Giữ hướng dẫn MinIO còn cần cho phiên bản được ghim hiện hành và giữ các bằng chứng lịch sử tại đúng mốc đã chạy.

## 9. Quyết định còn mở cho chủ dự án

1. Hạn URL tải lên và tải xuống (đề xuất 900 và 300 giây).
2. Thời gian giữ `uploads/pending/` (đề xuất 1 ngày; thấp nhất theo ngày).
3. Chính sách lưu giữ, xóa và sao lưu tệp đã xác nhận, quét mã độc (phần còn mở của DEC-07).
4. Loại token cho cả nhóm: token thuộc tài khoản hay thuộc người tạo (token người dùng mất hiệu lực khi người tạo rời tài khoản, theo tài liệu R2).
5. Có chấp nhận rủi ro "dung lượng không bị ép lúc PUT" nếu giai đoạn 0 xác nhận hay yêu cầu Worker.
