# ADR-017: Chuyển lưu trữ tệp từ MinIO sang Cloudflare R2

## Trạng thái và nguồn quyết định

**Đề xuất, chờ chủ dự án duyệt phạm vi triển khai.** Hướng được chọn theo chỉ dẫn trực tiếp của chủ dự án ngày 2026-11-13: dùng hoàn toàn cơ chế tải lên, lưu trữ và xác nhận tệp của Cloudflare R2; dữ liệu cũ trên MinIO không chuyển; thông tin tài khoản và endpoint R2 không đưa vào Git; trong lúc phát triển cả nhóm dùng chung một bộ khóa qua `.env`. Chưa có mã nào được đổi. Khi triển khai xong, ADR này thay thế [ADR-006](ADR-006-minio-object-storage.md).

Kế hoạch triển khai theo từng kho: [kế hoạch backend](../architecture/r2-storage-plan.md). Phần hạ tầng, chi phí, truy cập công khai và quy trình khóa nằm ở kho `supportflow-ai-infra` (`docs/infra/r2-storage-plan.md`); phần giao diện ở kho `ai-it-service-desk-web` (`docs/architecture/r2-upload-plan.md`).

Các sự kiện về R2 dưới đây lấy từ tài liệu chính thức của Cloudflare, tra cứu ngày 2026-11-13 (danh sách ở cuối). Chỗ nào nguồn chưa chắc chắn hoặc chưa kiểm chứng bằng thực nghiệm thì ghi rõ và đưa vào bước kiểm chứng (mục "Điều kiện trước khi viết mã").

## Bối cảnh

Hiện trạng tại `dev@07ce07a`:

- `MinioObjectStorageService` cài đặt cổng `IObjectStorageService` bằng thư viện `Minio` 6.0.5. Cấu hình ở mục `Minio:*` (`Endpoint`, `PublicEndpoint`, `AccessKey`, `SecretKey`, `Bucket`, `UseSsl`).
- Tải lên dùng **presigned POST policy** có điều kiện `content-length-range` bằng đúng dung lượng khai báo, khóa object và `Content-Type` cố định (`CreateUploadAsync`). Web gửi `multipart/form-data` tới URL trả về; nếu `formData` rỗng thì web tự chuyển sang PUT thô.
- Xác nhận (`ConfirmAttachmentUploadUseCase`): kiểm grant, `Stat` object ở `uploads/pending/{ticketId}/…`, so dung lượng và `Content-Type` thực tế với giá trị đã khai báo, `Copy` kèm ETag kỳ vọng sang `tickets/{ticketId}/attachments/{guid}`, `Stat` lại, ghi metadata trong một giao dịch rồi xóa object tạm.
- Tải xuống: presigned GET 3600 giây, backend không gọi `Stat` trước khi ký.
- `appsettings.Development.json` đặt sẵn khóa mặc định của MinIO; hạ tầng có container `minio`, `minio-init`, cổng gateway lưu trữ riêng và `Minio__PublicEndpoint` để ký URL theo tên miền trình duyệt thấy.
- Việc dọn object mồ côi còn mở (xem [hợp đồng hiện hành](../architecture/current-backend-contract.md)).

## Quyết định

1. **Dùng R2 qua API tương thích S3** (`https://<ACCOUNT_ID>.r2.cloudflarestorage.com`, vùng `auto`) cho mọi môi trường, kể cả máy lập trình viên. Không giữ MinIO ở runtime.
2. **Tải lên bằng presigned PUT.** Backend ký URL PUT cho khóa `uploads/pending/{ticketId}/{guid}` với `Content-Type` nằm trong chữ ký. Trình duyệt tải thẳng lên R2. Bỏ hoàn toàn presigned POST và trường `formData`.
3. **Backend là nơi quyết định dung lượng.** R2 không hỗ trợ presigned POST policy, và tài liệu chính thức không mô tả cách ép giới hạn dung lượng trên presigned PUT; các nguồn cộng đồng cho thấy dung lượng ký sẵn không bị ép lúc PUT (chưa kiểm chứng, xem bước kiểm chứng). Do đó:
   - Backend vẫn chặn dung lượng khai báo khi cấp URL (≤ 10 MiB, hạn mức theo phiếu).
   - **Bước xác nhận là chốt chặn cuối**: `Stat` lấy dung lượng và `Content-Type` thật; sai khớp thì từ chối và **xóa ngay object tạm** (hành vi mới).
   - Object không được xác nhận bị quy tắc vòng đời xóa theo ngày ở tiền tố `uploads/pending/`, thay cho việc đối soát object mồ côi bằng tay.
4. **Giữ cơ chế hai bước** (tạm → xác nhận → khóa cuối): `Copy` kèm điều kiện ETag nguồn, kiểm lại object đích, rồi mới ghi metadata. Điều kiện ETag của `CopyObject` phải được kiểm chứng trên R2.
5. **Bucket luôn riêng tư.** Không bật `r2.dev`, không gắn tên miền tùy chỉnh cho bucket tệp phiếu. Tải xuống chỉ qua presigned GET ngắn hạn do backend cấp sau khi kiểm quyền. Nếu sau này cần tệp công khai (ảnh tĩnh, tài liệu công bố), dùng bucket khác.
6. **Tên cấu hình trung lập nhà cung cấp ở cổng, cụ thể ở adapter:** mục cấu hình `R2:*` (`Endpoint`, `AccessKeyId`, `SecretAccessKey`, `Bucket`, `UploadExpirySeconds`, `DownloadExpirySeconds`) và `R2ObjectStorageService`. `Endpoint` chứa account ID nên được coi là cấu hình nhạy cảm.
7. **Không chuyển dữ liệu từ MinIO.** Bản ghi `ticket_attachments` và grant cũ trỏ vào khóa MinIO sẽ không còn tải được. Môi trường chỉ dùng để lập trình thì xóa các bản ghi đó theo quy trình ở kế hoạch; không sửa migration đã phát hành.
8. **Khóa và endpoint không vào Git.** Chỉ nằm trong `.env` cục bộ (đã được `.gitignore`), kho bí mật của GitHub Actions và tệp môi trường trên máy chủ. Mọi tài liệu dùng chỗ giữ chỗ `<ACCOUNT_ID>`, `<ACCESS_KEY_ID>`.
9. **Trong giai đoạn phát triển**, cả nhóm dùng chung một bucket `dev` và một token giới hạn đúng bucket đó, phát qua kênh an toàn của nhóm. Staging, production và CI mỗi nơi một bucket và một token riêng; không đưa khóa production vào `.env` của lập trình viên.

## Điều R2 khác MinIO

| Chủ đề | MinIO (hiện tại) | R2 (theo tài liệu) | Hệ quả thiết kế |
| --- | --- | --- | --- |
| Presigned POST | Có, kèm policy | **Không hỗ trợ**; có presigned GET, HEAD, PUT, DELETE | Chuyển sang PUT |
| Hạn URL | Tùy cấu hình | 1 giây đến 7 ngày | Cấp ngắn (đề xuất 900 giây tải lên, 300 giây tải xuống, cấu hình được) |
| Ép dung lượng lúc tải | `content-length-range` | Không có chính sách tương đương được tài liệu hóa | Xác nhận phía backend là chốt chặn; xem bước kiểm chứng |
| Ép `Content-Type` | Trong policy | Có, nếu ký `Content-Type` thì client phải gửi đúng | Web phải gửi đúng giá trị đã ký |
| CORS | Biến môi trường container | Cấu hình theo bucket; cần `PUT`, `Content-Type`, và `ExposeHeaders: ETag` | Ops cấu hình; đổi origin phải sửa CORS |
| URL hết hạn | Trả lỗi | 403 `ExpiredRequest` **không kèm tiêu đề CORS** | Trình duyệt chỉ thấy lỗi mạng; web phải coi là "liên kết hết hạn" |
| `CopyObject` có điều kiện | `WithMatchETag` | Điều kiện nguồn được kiểm khi chọn nguồn, điều kiện đích kiểm khi commit, không nguyên tử; sai điều kiện trả 412 | Kiểm chứng cách dùng điều kiện ETag nguồn |
| Dọn object tạm | Việc bảo trì riêng | Quy tắc vòng đời theo tiền tố và số ngày; xóa thường xong trong khoảng 24 giờ | Quy tắc cho `uploads/pending/` |
| Quyền | Tài khoản dịch vụ | Token S3 có thể giới hạn theo bucket (Object Read & Write) | Token chỉ chạm bucket cần thiết; không tạo bucket từ ứng dụng |
| Chi phí băng thông ra | Tự vận hành | Không tính phí egress | Xem phân tích chi phí ở kho infra |

## Luồng sau khi chuyển

1. `POST /api/tickets/{id}/attachments/upload-url`: kiểm quyền, đuôi tệp, dung lượng, hạn mức; tạo grant; ký PUT. Phản hồi `{ uploadUrl, storageKey, expirySeconds }` kèm thông tin tiêu đề bắt buộc (đề xuất trường tùy chọn `requiredHeaders`); `formData` luôn rỗng.
2. Trình duyệt `PUT` tệp lên `uploadUrl` với đúng `Content-Type` đã ký.
3. `POST …/attachments/confirm`: như hiện nay, cộng với xóa object tạm khi dung lượng hoặc loại tệp thật không khớp.
4. `GET …/attachments/download-url`: ký GET ngắn hạn cho khóa cuối `tickets/{ticketId}/attachments/{guid}`.
5. Quy tắc vòng đời xóa `uploads/pending/` sau 1 ngày.

Thay đổi này đổi hợp đồng HTTP ở phần phản hồi `upload-url` (bỏ `formData`, có thể thêm `requiredHeaders`), nên phải cập nhật [baseline OpenAPI](../../contracts/backend/openapi-baseline.json) có chủ đích và báo kho web theo [cẩm nang chung](../team-handbook.md#4-thay-đổi-ảnh-hưởng-kho-khác).

## Phương án đã cân nhắc

- **Giữ MinIO:** không đáp ứng chỉ dẫn; phải tự vận hành thêm dịch vụ, cổng lưu trữ riêng và băng thông.
- **Đi qua Cloudflare Worker (binding R2) để ép dung lượng cứng:** cho phép kiểm `Content-Length` trước khi ghi, nhưng thêm thành phần mới (mã, triển khai, chi phí Worker) và đi ngược yêu cầu dùng cơ chế tải của R2. Chỉ cân nhắc lại nếu thực nghiệm cho thấy việc tải vượt dung lượng gây thiệt hại đo được.
- **Tải qua backend (proxy):** ép được mọi điều kiện nhưng backend gánh băng thông và bộ nhớ cho tệp 10 MiB; mất lợi thế tải trực tiếp.
- **AWS S3:** phụ thuộc nhà cung cấp có phí egress; R2 cùng giao diện S3 nhưng không tính egress.
- **Cổng trung lập (giữ `IObjectStorageService`):** vẫn giữ để có thể đổi nhà cung cấp hoặc dùng bản giả khi kiểm thử; không thêm lớp trừu tượng mới.

## Điều kiện trước khi viết mã (bước kiểm chứng, "giai đoạn 0")

Chạy trên bucket `dev` thật, ghi kết quả vào [kế hoạch](../architecture/r2-storage-plan.md):

1. PUT vượt dung lượng đã khai báo, có và không ký `Content-Length`: R2 chấp nhận hay từ chối?
2. PUT sai `Content-Type` so với chữ ký: kỳ vọng 403.
3. `CopyObject` với điều kiện ETag nguồn đúng và sai: kỳ vọng thành công và 412.
4. `HEAD` trả `Content-Length`, `Content-Type`, `ETag` đúng; ETag của PUT đơn giản có phải MD5 hex.
5. Phiên bản `AWSSDK.S3` được chọn có chạy được với R2 (tài liệu R2 cho .NET dùng AWSSDK.S3 v3 kèm cờ riêng cho từng yêu cầu; các SDK JavaScript và Java mới đổi cách tính checksum mặc định làm R2 từ chối, chưa xác nhận cho .NET).
6. Presigned URL ký với host nào được R2 chấp nhận (host S3 của tài khoản; tên miền tùy chỉnh chưa xác nhận).
7. Hành vi khi tải lên URL hết hạn từ trình duyệt thật, và quy tắc vòng đời đã áp dụng trên `uploads/pending/`.
8. Presigned GET có chấp nhận tham số `response-content-disposition` để tải về với tên tệp gốc không (khóa cuối `tickets/{id}/attachments/{guid}` không có tên và đuôi tệp).

Nếu kết quả mục 1 cho thấy R2 chấp nhận tệp lớn hơn nhiều so với khai báo, ghi nhận mức thiệt hại tối đa (một PUT đơn tối đa gần 5 GiB theo tài liệu giới hạn) và quyết định lại giữa "chấp nhận kèm xóa khi xác nhận" và "Worker".

## Hệ quả

- Bỏ một dịch vụ phải vận hành (container MinIO, `minio-init`, cổng lưu trữ riêng, `PublicEndpoint`) và bỏ phụ thuộc thư viện `Minio`.
- Thêm phụ thuộc vào Cloudflare R2 và vào một bộ khóa phải được bảo vệ; không có bản chạy cục bộ của R2 qua S3 API, nên mọi người cần mạng và khóa để thử luồng tệp (kiểm thử tự động dùng bản giả).
- Tải xuống không tính phí băng thông, nhưng mỗi thao tác ghi, sao chép, đọc được tính theo [bảng giá R2](https://developers.cloudflare.com/r2/pricing/).
- Dung lượng tối đa chỉ được bảo đảm ở bước xác nhận, không ở bước tải lên.
- Đóng được phần "đối soát object mồ côi" của DEC-07 cho vùng tạm; phần quét mã độc, lưu giữ và xóa tệp đã xác nhận vẫn **còn mở**.
- Cẩm nang chung, README, runbook và tài liệu hạ tầng phải bỏ nhắc MinIO sau khi triển khai.

## Nguồn tham khảo (Cloudflare, tra cứu 2026-11-13)

- [Giá R2](https://developers.cloudflare.com/r2/pricing/)
- [Presigned URL](https://developers.cloudflare.com/r2/api/s3/presigned-urls/)
- [Bucket công khai và r2.dev](https://developers.cloudflare.com/r2/buckets/public-buckets/)
- [Giới hạn](https://developers.cloudflare.com/r2/platform/limits/)
- [Tương thích S3 API](https://developers.cloudflare.com/r2/api/s3/api/) và [thao tác có điều kiện](https://developers.cloudflare.com/r2/api/s3/conditional-operations/)
- [CORS](https://developers.cloudflare.com/r2/buckets/cors/)
- [Vòng đời đối tượng](https://developers.cloudflare.com/r2/buckets/object-lifecycles/)
- [Token và khóa S3](https://developers.cloudflare.com/r2/api/tokens/)
- [Ví dụ AWSSDK.S3 cho .NET](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/)
