# Kế hoạch tích hợp AI vào backend (phân loại phiếu và chỉ mục Knowledge)

Trạng thái: kế hoạch đề xuất chờ chủ dự án duyệt, chưa triển khai. Hợp đồng và ranh giới hiện hành nằm ở [ADR-012](../adr/ADR-012-ai-contract-and-future-transport.md). Cập nhật 2026-11-13, đối chiếu mã `dev` tại `ef4390d`. Kế hoạch phía ai-service: `docs/ml-classifier-plan.md` trong kho `ai-service`.

## 1. Quyết định đã chốt (chủ dự án, 2026-11-13)

1. MVP tích hợp **mô hình phân loại thật**. ai-service dùng **mô hình học máy cục bộ cho cả phân loại và embedding và không gọi API hay dịch vụ model bên ngoài**. Backend vì vậy chỉ cần kết nối nội bộ tới ai-service bằng Bearer dịch vụ; không có khóa nhà cung cấp AI bên ngoài.
2. **Backend tiêu thụ** endpoint `POST /v1/knowledge-indexes:prepare` của ai-service.
3. Backend giữ nguyên quyền: phân quyền, trạng thái phiếu, bước người duyệt (HITL), lưu trữ, audit. AI chỉ gợi ý.

## 2. Hiện trạng (đã kiểm tra trong mã)

| Phần | Có | Chưa có |
| --- | --- | --- |
| Phân loại | Domain `AIAnalysis`, `AISuggestion`, `SuggestionDecision`, `ConfidencePolicy` (Low dưới 0,4, High từ 0,75), `TriagePolicy`, `InputHashComputer` (`ai-input-v2`); cổng `ITicketClassificationService`; bộ tích hợp HTTP (`HttpTicketClassificationService`, ngắt mạch `ClassificationCircuit`) và `NoOp`; `AiOptions` (`Enabled` mặc định tắt, `Endpoint`, `BearerToken` từ 32 ký tự, `TimeoutSeconds` 1 đến 30) | Không bảng, ánh xạ EF hay migration cho `AIAnalysis`/`AISuggestion`; không use case hay mã nào ngoài module AIAssistance gọi cổng phân loại; không endpoint xem kết quả hay duyệt gợi ý |
| Knowledge | `Article`, `ArticleVersion` (ảnh chụp tạo khi xuất bản), xuất bản, lưu trữ, tìm kiếm theo từ khóa | Xuất bản lại tạo phiên bản mới (`Version` vẫn là 1), hợp đồng chỉ mục, job nền, bảng chỉ mục và vector, phía gọi ai-service; `IEmbeddingService` chỉ có `NoOp` |
| Hạ tầng cho vector | Hạ tầng đã chạy pgvector (image `pgvector/pgvector`) và truyền `KnowledgeIndex__BearerToken` cho API và Worker | Backend chưa có cấu hình `KnowledgeIndex`; kiểm thử tích hợp dùng `postgres:17-alpine` **không có pgvector** |

## 3. Phần A: phân loại phiếu

### 3.1 Luồng đề xuất

1. Phiếu mới (và phiếu được sửa khi còn `Submitted`) tạo một **tác vụ phân loại nền** trong cùng giao dịch ghi phiếu. Tạo phiếu **không chờ AI** và không thất bại vì AI.
2. Worker lấy tác vụ, tính `inputHash`, gọi `POST /v1/ticket-classifications` tới ai-service (`ticketId`, `ticketRevision`, `inputHash`, tiêu đề, mô tả), kiểm tra phản hồi lặp đúng định danh và hash (mapper hiện có từ chối enum, điểm và trường lạ).
3. Lưu `AIAnalysis` và `AISuggestion` (Category, Impact, Urgency kèm điểm hoặc `null`, dải độ tin cậy). Gợi ý gắn với `ticketRevision`; revision phiếu đổi thì gợi ý hết hiệu lực (`Expired`).
4. Phiếu vẫn vào hàng đợi phân loại thủ công theo [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md) (`MANUAL_TRIAGE`). **AI không chọn nhóm hay Agent**; định tuyến tự động nếu có là chính sách của backend, ghi audit. Quy tắc SEC trong `TriagePolicy` vẫn bắt buộc phân loại thủ công.
5. AI không khả dụng (tắt, hết thời gian, ngắt mạch, 4xx/5xx): ghi trạng thái tác vụ, không ảnh hưởng phiếu; thử lại có giới hạn.

### 3.2 Bước người duyệt (HITL) và API

Đề xuất tối thiểu (tên và hình dạng chốt khi duyệt, cập nhật OpenAPI có chủ đích và báo web):

- `GET /api/tickets/{id}/triage`: nhân viên trong phạm vi đọc thấy gợi ý, dải tin cậy và trạng thái; người yêu cầu chỉ thấy dải tin cậy khi Thấp hoặc thiếu (theo quy tắc hiện có ở web), không thấy điểm.
- `POST /api/tickets/{id}/triage/decision`: chấp nhận, ghi đè (bắt buộc lý do) hoặc từ chối, kèm `expectedRevision`. Revision cũ hoặc gợi ý đã hết hiệu lực trả 409. Ghi audit người duyệt và thời điểm.

### 3.3 Cấu hình và vận hành

`Ai:Enabled` mặc định tắt; bật theo môi trường bằng cấu hình (`Ai__Endpoint`, `Ai__BearerToken`). Dữ liệu phiếu chỉ đi tới ai-service trong mạng nội bộ; không ghi tiêu đề hay mô tả vào log. Hợp đồng v1 vẫn do backend sở hữu; đổi hợp đồng phải cập nhật bản sao và `contract.lock.json` ở ai-service.

## 4. Phần B: chỉ mục Knowledge

Thiết kế của phía ai-service nằm ở `docs/knowledge-index-pipeline.md` (kho ai-service). Backend ("Core" trong tài liệu đó) cần làm:

1. **Hợp đồng chuẩn**: đặt `contracts/ai/knowledge-index/v1/` (OpenAPI, profile, fixture, manifest) ở backend làm bản chuẩn; ai-service giữ bản sao cùng byte (hiện bản này mới nằm ở ai-service và chưa có SHA nguồn để ghim).
2. **Xuất bản lại và ảnh chụp bất biến**: xuất bản lần đầu ghi `ArticleVersion` V1; xuất bản lại tạo V2 trong một giao dịch, giữ V1, tăng phiên bản, kiểm tra `xmin`. Đây là thay đổi hành vi Knowledge hiện nay (phiên bản luôn là 1).
3. **Migration và dữ liệu**: ảnh chụp, tập chỉ mục (version, nguồn, profile, vân tay), khối (chunk), bảng vector kích thước 384, tác vụ chỉ mục, con trỏ hoạt động, view `knowledge_active_chunks`. Cần tiện ích mở rộng `vector` của PostgreSQL và cách ánh xạ EF cho kiểu vector (chọn thư viện, kiểm chứng với Npgsql EF Core hiện dùng).
4. **Tác vụ nền**: `Pending → Running → Succeeded`, hoặc `Failed` hay `Superseded`; lấy bằng `FOR UPDATE SKIP LOCKED`, hạn thuê 180 giây, tối đa 5 lần thử, lùi `2^lần` giây tối đa 60 giây. Hoàn tất khóa bài và tác vụ, kiểm tra còn Published, đúng phiên bản hiện hành rồi ghi toàn bộ trong một giao dịch.
5. **Gọi ai-service**: Bearer `KnowledgeIndex__BearerToken` (hạ tầng đã truyền); yêu cầu tối đa 1 MiB, phản hồi đọc tối đa 8 MiB, thời gian chờ 60 giây, thử lại 429, 5xx, mạng; 4xx khác là lỗi cuối. Kiểm lại tương quan, profile, hash, offset, vector trước khi lưu.
6. **Vận hành**: tác vụ dựng lại (rebuild) có `RequestId` do người gọi tạo (cùng ID và ảnh chụp là phát lại); không tự gọi AI khi migrate hoặc khởi động; bài đã Published có trước migration cần gọi dựng lại.
7. **Kiểm thử**: dùng image PostgreSQL có pgvector cho kiểm thử tích hợp (đổi `postgres:17-alpine` ở các test liên quan sang `pgvector/pgvector:0.8.1-pg17-bookworm`); kiểm thử thuê hạn, tác vụ trùng và cũ, xuất bản lại, lưu trữ, rollback.

Ngoài phạm vi: truy vấn ngữ nghĩa và sinh câu trả lời (RAG).

## 5. Thứ tự thực hiện

| Bước | Nội dung | Phụ thuộc |
| --- | --- | --- |
| 1 | Chốt hình dạng API triage, chính sách hiển thị cho người yêu cầu, định tuyến tự động, lưu giữ dữ liệu AI | Chủ dự án |
| 2 | Kiểm thử tích hợp dùng image pgvector; chuẩn của PostgreSQL 17 trong infra | infra |
| 3 | Xuất bản lại Knowledge và ảnh chụp bất biến (không cần AI) | – |
| 4 | Migration và tác vụ chỉ mục, gọi ai-service ở chế độ NoOp rồi `local-e5` | Bước 2, 3 và ai-service |
| 5 | Lưu `AIAnalysis`/`AISuggestion`, tác vụ phân loại nền (dùng `rule` trước khi có mô hình thật) | Bước 1 |
| 6 | Endpoint xem và duyệt triage; cập nhật OpenAPI; web | Bước 5 |
| 7 | Bật `Ai:Enabled` theo môi trường khi mô hình thật đạt tiêu chí đánh giá | ai-service |

Bước 7 không thuộc riêng backend: tiêu chí đạt (chỉ số, tập đánh giá, ngưỡng) do kế hoạch của ai-service và chủ dự án chốt.

## 6. Rủi ro và quyết định còn mở

| Chủ đề | Nội dung |
| --- | --- |
| Hiển thị cho người yêu cầu | Quy tắc "chỉ thấy dải Thấp hoặc thiếu" đang ở web; cần chốt ở backend để API không lộ điểm |
| Định tuyến tự động theo AI | ADR-016 để dành cho chính sách backend; chưa có quyết định |
| Lưu giữ dữ liệu phân tích AI | Thời hạn giữ `AIAnalysis`/`AISuggestion` và nội dung đã gửi |
| Tải lên hàng đợi | Số tác vụ chờ, thứ tự ưu tiên, giới hạn thử lại khi ai-service quá tải |
| Mô hình chưa sẵn sàng | Dùng `rule` làm provider mặc định đến khi mô hình học máy đạt tiêu chí; không bật `Ai:Enabled` ở production trước đó |
| Thay đổi hành vi Knowledge | Xuất bản lại tạo phiên bản mới ảnh hưởng web và hợp đồng Knowledge |

## 7. Tài liệu phải cập nhật khi triển khai

[Hợp đồng hiện hành](current-backend-contract.md) (mục "Chưa có … AI thật"), [mô-đun và luồng xử lý](backend-modules-and-use-cases.md) (mục Ranh giới AI), README (mục AI contract), [ADR-012](../adr/ADR-012-ai-contract-and-future-transport.md) (mục "tương lai"), [baseline OpenAPI](../../contracts/backend/openapi-baseline.json), `docs/knowledge-index-pipeline.md` ở ai-service (mốc backend), [cẩm nang chung](../team-handbook.md) (bốn kho cùng lúc).
