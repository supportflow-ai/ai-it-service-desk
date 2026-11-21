# Kế hoạch tích hợp AI vào backend (phân loại phiếu và chỉ mục Knowledge)

Trạng thái: kế hoạch đề xuất cho luồng tích hợp. Mục 1 ghi phần đã chốt, mục 2 ghi phần có trong mã. Đối chiếu ngày 21/11/2026 tại Backend `dev@3640c33`, Web `dev@89eabc4`, AI Service `b35f08b` và Infra `df08561`. [Hợp đồng và adapter Backend](../adr/ADR-012-ai-contract-and-future-transport.md)

## 1. Quyết định đã chốt

1. Ngày 13/11/2026, chủ dự án chốt chạy mô hình phân loại và embedding cục bộ, với Bearer dịch vụ trên kết nối nội bộ giữa Backend và AI Service.
2. Ngày 21/11/2026, `AI-02` chốt nghiệm thu Identity, Ticketing và phân loại bằng mô hình học máy cục bộ có người duyệt trên cùng ứng viên. `rule` dùng kiểm thử tích hợp. Chọn mô hình và tiêu chí đánh giá theo `AI-07`. Xem tiêu chí trong `supportflow-ai-infra/docs/project/mvp.md` của bộ checkout liên kho.
3. Backend sở hữu phân quyền, dữ liệu phiếu, quyết định của người duyệt, Priority và audit. AI Service trả gợi ý theo hợp đồng phân loại v1. Backend sẽ tiêu thụ `POST /v1/knowledge-indexes:prepare` theo hướng đã chốt ngày 13/11. Mốc tích hợp prepare thuộc `AI-09`. Phạm vi phát hành Kho tri thức, truy hồi ngữ nghĩa và RAG được xét riêng.

## 2. Hiện trạng (đã kiểm tra trong mã)

| Phần | Có | Chưa có |
| --- | --- | --- |
| Phân loại Backend | `AIAnalysis`, `AISuggestion`, `SuggestionDecision`, `ConfidencePolicy`, `TriagePolicy`, `InputHashComputer` và cổng `ITicketClassificationService`. Chọn HTTP hoặc NoOp theo `Ai:Enabled`, mặc định NoOp | Luồng Ticketing gọi cổng phân loại, lưu phân tích/gợi ý và API xem/duyệt. Domain đã có kiểu dữ liệu, schema chưa lưu các kiểu AI này |
| Web và AI Service | Web có `WorkspaceTriagePanel` truyền `result={null}` và `ticketRevision={null}`. AI Service có endpoint v1 và provider `rule`/`noop` | Kết nối Web với API triage của Backend và provider phân loại học máy cục bộ cho nghiệm thu |
| Knowledge | Backend có bài viết, ảnh chụp `ArticleVersion` khi xuất bản và tìm kiếm từ khóa. AI Service có prepare với embedding `local-e5` | Backend chưa có consumer prepare, hợp đồng nguồn, job, migration chỉ mục, lưu vector hay truy hồi theo vector. `IEmbeddingService` ở Backend vẫn là NoOp |
| Hạ tầng cho vector | Compose có image pgvector và truyền nhóm biến `KnowledgeIndex__*` cho API/Worker | Backend chưa dùng nhóm biến này. Việc có image pgvector chỉ chứng minh cấu hình runtime, còn schema chỉ mục phụ thuộc migration của Backend |

## 3. Phần A: phân loại phiếu

### 3.1 Luồng đề xuất

`AI-03` còn mở. Hai phương án cùng bảo toàn việc lưu phiếu độc lập với lần gọi AI:

| Phương án | Điều kiện thực hiện |
| --- | --- |
| Nhân viên yêu cầu phân tích sau khi phiếu đã lưu | Kiểm quyền trên phiếu trước khi lấy snapshot và giới hạn yêu cầu lặp. Là phương án đề xuất để thử pilot |
| Chạy nền khi tạo phiếu hoặc nội dung phiếu `Submitted` đổi | Ghi yêu cầu phân tích cùng giao dịch lưu phiếu, dùng Worker gọi AI sau khi commit. Thiết kế khóa chống tác vụ trùng, giới hạn hàng đợi, quyền đọc của Worker và thời hạn thử lại |

Sau khi lấy ảnh chụp hợp lệ, Backend gửi `requestId`, `ticketId`, `ticketRevision`, `inputHash`, tiêu đề và mô tả đến `POST /v1/ticket-classifications`. Adapter hiện đối chiếu bốn trường tương quan của phản hồi. Việc lưu phân tích/gợi ý và mở bước duyệt là thiết kế cần triển khai.

Phiếu mới tiếp tục vào `MANUAL_TRIAGE`. Backend đề xuất đội theo quy trình SupportTeam đã chốt và người có quyền xác nhận trước khi gán Agent. Phiếu `SEC` phân loại thủ công. Khi AI lỗi, hết thời gian hoặc trả về `suggestions: []`, nhân viên tiếp tục xử lý thủ công. [Phương án SupportTeam](../adr/ADR-016-support-team-routing-and-agent-scope.md#phương-án-supportteam-đã-chốt)

### 3.2 Bước người duyệt (HITL) và API

Đề xuất tối thiểu (tên và hình dạng chốt khi duyệt, cập nhật OpenAPI có chủ đích và báo web):

- `GET /api/tickets/{id}/triage`: trả gợi ý, dải tin cậy và trạng thái theo chính sách hiển thị được duyệt ở `AI-01`. Quy tắc hiển thị cục bộ của Web chưa phải quyền đọc ở API.
- `POST /api/tickets/{id}/triage/decision`: chấp nhận, ghi đè (bắt buộc lý do) hoặc từ chối, kèm `expectedRevision`. Revision cũ hoặc gợi ý đã hết hiệu lực trả 409. Ghi audit người duyệt và thời điểm.
- `POST /api/tickets/{id}/triage`: Agent hoặc Admin tự đặt Category, Impact, Urgency, không cần gợi ý AI. Đây cũng là đường ghi duy nhất mà luồng duyệt gợi ý dùng lại.

Trong thiết kế duyệt, `inputHash` kiểm gợi ý còn khớp tiêu đề và mô tả hiện hành theo `ai-input-v2`. Phản hồi trả `ticketRevision` của snapshot để đối chiếu. Thiết kế sẽ lưu revision đó để truy vết. `expectedRevision` kiểm ghi đồng thời theo revision Ticketing (`xmin`) lúc lưu quyết định. Một thay đổi phân công có thể làm đổi revision nhưng vẫn giữ nguyên nội dung. Trong trường hợp đó gợi ý có thể còn mới và yêu cầu ghi vẫn phải gửi revision hiện tại. Bước xem và ghi tiếp tục kiểm quyền và trạng thái.

### 3.2.1 Ràng buộc cho thiết kế và kiểm thử

Các điểm dưới đây do kiểm thử kiến trúc hoặc mã hiện có buộc phải theo; vi phạm sẽ làm đỏ CI hoặc mất dữ liệu.

- **Ghi Category sau `Submitted`.** `Ticket.Update` chỉ chạy khi `Submitted`, còn `Ticket.SetTriage` chỉ nhận Impact, Urgency và chưa có guard trạng thái. Cần mở rộng `SetTriage` để nhận Category tùy chọn, chặn Resolved, Closed, Cancelled, và không đổi `Status`.
- **Giữ nhãn của người gửi.** Phương án thêm `RequesterCategoryId` khi tạo và chỉ đổi khi chủ phiếu cập nhật. Trước backfill, kiểm lại nguồn ghi lịch sử có bảo toàn nghĩa của `category_id`. Nhãn phiếu chỉ dùng làm dữ liệu sau khi đáp ứng `AI-06` và quy trình đánh giá theo `AI-07`.
- **Ranh giới module.** AIAssistance chỉ được dùng kiểu Ticketing nằm trong `ModuleRules.Published` (hiện là `TicketCategory`, `Impact`, `Urgency`). Ticketing công bố một contract (lấy ảnh chụp để phân tích, áp dụng kết quả triage) và thêm tên kiểu vào `Published`. Ticketing không tham chiếu ngược AIAssistance được, vì sẽ thành phụ thuộc vòng.
- **Lưu nguyên tử mà không mở transaction ở Application.** `CleanArchitectureTests` cấm Application dùng EF Core. Theo mẫu `UpdateTicketWithAuditAsync`: contract của Ticketing kiểm quyền, kiểm `expectedRevision`, sửa phiếu và thêm audit vào `DbContext` dùng chung nhưng **không lưu**; luồng duyệt thêm `AIReview` rồi lưu **một lần** qua adapter Infrastructure, adapter này chuyển `DbUpdateConcurrencyException` thành 409. Không ghi audit triage qua `ITicketTimelineStore`, vì `AddToOpenTicketAsync` tự mở transaction riêng. Cần kiểm thử tích hợp: phiếu và bản ghi duyệt cùng được lưu, hoặc cùng không.
- **Worker không có người dùng.** Nếu chọn chạy nền ở `AI-03`, thiết kế quyền đọc snapshot cho tác vụ hệ thống. Worker hiện đăng ký `AnonymousCurrentUser`.
- **Kiểu revision.** `uint` ở Ticketing, `long` từ 0 trở lên ở AIAssistance, `int` với `ge=0` ở ai-service; giữ nguyên hợp đồng v1.
- **Phụ thuộc quyết định.** Có dùng trạng thái `Triaged` hay không thuộc [DEC-09](pending-business-decisions.md). Các đề xuất ghi triage ở trên giả định giữ `Status` hiện hành.

### 3.3 Cấu hình và vận hành

`Ai:Enabled` mặc định tắt. HTTP yêu cầu bật `Ai__Enabled`, cấu hình origin `Ai__Endpoint` và `Ai__BearerToken` riêng. Có endpoint và token trong Compose chưa làm luồng phân loại hoạt động. Adapter dùng ngân sách chung mặc định 5 giây, cấu hình 1–30 giây. Chỉ thử lại một lần với 502/503/504 sau 100 ms, giữ cùng request và ngân sách. Circuit của từng tiến trình ngắt 30 giây sau ba kết quả lỗi liên tiếp. [Chi tiết và khác biệt với thiết kế gốc](../adr/ADR-012-ai-contract-and-future-transport.md#adapter-http-tại-mốc-đối-chiếu)

Dữ liệu phiếu đi qua API nội bộ. Log vận hành chỉ cần định danh, thời lượng và loại kết quả. Backend sở hữu hợp đồng v1. Thay đổi hợp đồng cần phối hợp với bản ghim và fixture phía AI Service.

## 4. Phần B: chỉ mục Knowledge

Các bước sau là thiết kế Backend khi xác định mốc `AI-09`. AI Service đã cung cấp route prepare và bản hợp đồng v1 riêng. Mục này mô tả việc Backend tiêu thụ và lưu chỉ mục. Tìm kiếm từ khóa của Knowledge hiện vẫn hoạt động riêng. Tham chiếu `docs/knowledge-index-pipeline.md` của kho `ai-service`.

1. **Hợp đồng chuẩn**: đặt `contracts/ai/knowledge-index/v1/` (OpenAPI, profile, fixture, manifest) ở backend làm bản chuẩn; ai-service giữ bản sao cùng byte (hiện bản này mới nằm ở ai-service và chưa có SHA nguồn để ghim).
2. **Xuất bản lại và ảnh chụp bất biến**: xuất bản lần đầu ghi `ArticleVersion` V1; xuất bản lại tạo V2 trong một giao dịch, giữ V1, tăng phiên bản, kiểm tra `xmin`. Đây là thay đổi hành vi Knowledge hiện nay (phiên bản luôn là 1).
3. **Migration và dữ liệu**: ảnh chụp, tập chỉ mục (version, nguồn, profile, vân tay), khối (chunk), bảng vector kích thước 384, tác vụ chỉ mục, con trỏ hoạt động, view `knowledge_active_chunks`. Cần tiện ích mở rộng `vector` của PostgreSQL và cách ánh xạ EF cho kiểu vector (chọn thư viện, kiểm chứng với Npgsql EF Core hiện dùng).
4. **Tác vụ nền**: `Pending → Running → Succeeded`, hoặc `Failed` hay `Superseded`; lấy bằng `FOR UPDATE SKIP LOCKED`, hạn thuê 180 giây, tối đa 5 lần thử, lùi `2^lần` giây tối đa 60 giây. Hoàn tất khóa bài và tác vụ, kiểm tra còn Published, đúng phiên bản hiện hành rồi ghi toàn bộ trong một giao dịch.
5. **Gọi ai-service**: Bearer `KnowledgeIndex__BearerToken` (hạ tầng đã truyền); yêu cầu tối đa 1 MiB, phản hồi đọc tối đa 8 MiB, thời gian chờ 60 giây, thử lại 429, 5xx, mạng; 4xx khác là lỗi cuối. Kiểm lại tương quan, profile, hash, offset, vector trước khi lưu.
6. **Vận hành**: tác vụ dựng lại (rebuild) có `RequestId` do người gọi tạo (cùng ID và ảnh chụp là phát lại); không tự gọi AI khi migrate hoặc khởi động; bài đã Published có trước migration cần gọi dựng lại.
7. **Kiểm thử**: dùng image PostgreSQL có pgvector cho kiểm thử tích hợp (đổi `postgres:17-alpine` ở các test liên quan sang `pgvector/pgvector:0.8.1-pg17-bookworm`); kiểm thử thuê hạn, tác vụ trùng và cũ, xuất bản lại, lưu trữ, rollback.

Ngoài phạm vi: truy vấn ngữ nghĩa và sinh câu trả lời (RAG).

## 5. Thứ tự thực hiện

| Nhánh | Nội dung | Điều kiện |
| --- | --- | --- |
| Phân loại 1 | Chốt thời điểm gọi, quyền hiển thị, API triage, vòng đời và dữ liệu lưu | `AI-01`, `AI-03`, `AI-06`, `DEC-09` |
| Phân loại 2 | Nối snapshot → AI → lưu gợi ý → người duyệt → cập nhật và audit. Cập nhật API và Web khi triển khai | Chính sách phân loại đã chốt. Dùng `rule` thử tích hợp và kiểm tra lỗi |
| Phân loại 3 | Đánh giá mô hình học máy cục bộ và chạy nghiệm thu chung | `AI-07` và bằng chứng cùng ứng viên theo `AI-02`/`DEC-08` |
| Knowledge | Ghim hợp đồng riêng, làm ảnh chụp phiên bản, migration, job và consumer. Kiểm đường lỗi khi embedding chưa sẵn sàng, rồi thử prepare với `local-e5` và PostgreSQL có pgvector | Mốc `AI-09` và các điều kiện ở mục 4 |

Hai nhánh dùng tiêu chí riêng. Việc Backend tiêu thụ prepare có mốc riêng với nghiệm thu phân loại.

## 6. Rủi ro và quyết định còn mở

| Chủ đề | Nội dung |
| --- | --- |
| Hiển thị cho người yêu cầu | Chốt trường và đối tượng được xem ở AI-01 trước khi xuất bản API |
| Định tuyến tự động theo AI | ADR-016 để dành cho chính sách backend; chưa có quyết định |
| Lưu giữ dữ liệu phân tích AI | Thời hạn giữ `AIAnalysis`/`AISuggestion` và nội dung đã gửi |
| Tải lên hàng đợi | Nếu chọn chạy nền ở AI-03, chốt số tác vụ chờ, thứ tự ưu tiên và giới hạn thử lại |
| Mô hình chưa sẵn sàng | Dùng `rule` để thử tích hợp ở môi trường kiểm thử. Chốt tiêu chí và chọn mô hình cho ứng viên nghiệm thu theo AI-02/07 |
| Thay đổi hành vi Knowledge | Xuất bản lại tạo phiên bản mới ảnh hưởng web và hợp đồng Knowledge |

## 7. Tài liệu phải cập nhật khi triển khai

[Hợp đồng Backend hiện hành](current-backend-contract.md) · [Mô-đun và luồng xử lý](backend-modules-and-use-cases.md) · [ADR-012](../adr/ADR-012-ai-contract-and-future-transport.md) · [OpenAPI Backend](../../contracts/backend/openapi-baseline.json). Khi triển khai consumer Knowledge, cập nhật hợp đồng riêng và mốc Backend ở kho AI Service, rồi kiểm lại cấu hình Infra.
