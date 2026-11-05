# Vận hành pipeline Knowledge Published

Cập nhật 05/11/2026, thay đổi local TC-346–TC-350 trên nền `febb4d7ec76dc1a846a554e349981b9b692c7091`. Tài liệu mô tả mã đang triển khai để review; chưa xác nhận rollout hoặc phê duyệt lựa chọn model. Phạm vi: publish, snapshot, job, worker và vector persistence. Semantic query/RAG ngoài phạm vi.

## Chuẩn bị và migration

PostgreSQL 16 có extension pgvector 0.8.1, image ghim trong repo Infra. AI phải chạy image có model E5 CPU offline và `SUPPORTFLOW_AI_EMBEDDING_PROVIDER=local-e5`; kiểm tra `/health/embedding` trả 200. Profile `kb-e5-small-384-v1`, model/revision/tokenizer/giới hạn được ghi trong [profile](../../contracts/ai/knowledge-index/v1/profile.json).

Migration mới `20261105142338_AddKnowledgeIndexPipeline` tạo extension vector, jobs, states, index sets, chunks, vector(384) và view active. Thêm unique `(article_id, version_number)` cho ArticleVersion. Không sửa migration cũ, không tự migrate/seed trong API/Worker. Trước khi chạy Database tool, xác minh host/cổng/database và bản backup theo [hướng dẫn môi trường](environment-runbook.md). Nếu bản cũ có duplicate article/version, cần xử lý dữ liệu được review trước; migration không tự xóa snapshot.

Runtime cần pgvector trước migration; database role thực thi migration cần quyền tạo extension. Kiểm tra chỉ đọc trên đúng database đã xác minh:

```sql
SELECT name, default_version, installed_version
FROM pg_available_extensions WHERE name = 'vector';
SELECT article_id, version_number, COUNT(*)
FROM public.article_versions GROUP BY article_id, version_number HAVING COUNT(*) > 1;
```

Sau migration, kiểm tra `pg_extension.extversion` và schema mới. Bài Published trước migration được giữ nguyên, chưa tự sinh job; tác giả/Admin gọi rebuild để backfill có kiểm soát. Không tự chạy AI hàng loạt khi nâng cấp.

## Cấu hình

| Biến môi trường | Giá trị/hành vi |
| --- | --- |
| `KnowledgeIndex__Enabled` | Mặc định false; true bật worker indexing |
| `KnowledgeIndex__Endpoint` | Origin AI, ví dụ `http://ai-service:8000`; không path/query/userinfo |
| `KnowledgeIndex__BearerToken` | S2S secret >= 32 ký tự, cùng token AI; không đưa vào Git/log |
| `KnowledgeIndex__TimeoutSeconds` | 60, tối đa 120 |
| `KnowledgeIndex__LeaseSeconds` | 180; phải lớn hơn timeout + 30 giây, tối đa 600 |
| `KnowledgeIndex__MaxAttempts` | 5, tối đa 20 |
| `KnowledgeIndex__PollSeconds` | 2, tối đa 60 |

Compose system/release cấu hình cả API và Worker; chỉ Worker chạy hosted service. Job vẫn được lưu khi worker disabled, để bật lại không mất sự kiện publish. `Ai__Enabled` của classification là cấu hình độc lập.

## API và demo

Giữ nguyên endpoint publish lần đầu. Các endpoint mới dùng JWT người dùng, policy Agent và kiểm tra ở Application: Agent chỉ thao tác bài do mình tạo; Admin được thao tác mọi bài. Requester không được xem trạng thái indexing hoặc gọi rebuild/re-publish. Không dùng S2S token để gọi API người dùng.

| Endpoint | Input và kết quả |
| --- | --- |
| `POST /api/knowledge/articles/{id}/republish` | `{title, content, tags, expectedRevision}` → 200 ArticleDto V2; bài phải Published |
| `POST /api/knowledge/articles/{id}/index/rebuild` | `{requestId, expectedRevision}` → 202 `{jobId}`, Location tới trạng thái; requestId UUID khác rỗng |
| `GET /api/knowledge/articles/{id}/index` | 200 `{articleId,jobId,activeIndexSetId,status,versionNumber,attempts,errorCode,chunkCount}` |

Thiếu/sai input: 400; thiếu JWT: 401; không có quyền: 403; không tồn tại: 404; xmin cũ hoặc requestId đã dùng cho snapshot khác: 409; trạng thái bài không cho phép: 422. Đây là phần bổ sung vào [OpenAPI hiện hành](../../contracts/backend/openapi-baseline.json); không thay endpoint/DTO cũ.

1. Tác giả tạo draft rồi publish với revision mới nhất. Publish trả thành công ngay khi transaction bài + snapshot + job commit; chưa đảm bảo vector đã sẵn sàng.
2. GET trạng thái thấy Pending/Running; worker xử lý rồi Succeeded, chunkCount > 0, activeIndexSetId = jobId.
3. Re-publish với revision hiện tại và nội dung mới: V2, snapshot V1 giữ nguyên; index V1 hết hiệu lực ngay. Worker hoàn thành thì chỉ V2 active. Bài đọc theo nghiệp vụ vẫn Published ngay cả khi indexing lỗi.
4. Rebuild V2 với requestId mới. Replay cùng ID/snapshot trả cùng job, không tạo thêm. Trong lúc rebuild, index cũ của cùng V2 vẫn active; completion thay pointer nguyên tử. Snapshot/profile như nhau giữ chunk IDs.
5. Archive: active pointer bị xóa và job chưa xong bị supersede. Vector lịch sử giữ nguyên nhưng không nằm trong view active. Job V1 trả chậm hoặc lease token cũ không thể kích hoạt lại.

Kiểm chứng sau bước 2/3 bằng query chỉ đọc; lọc theo article ID demo khi dùng database chung:

```sql
SELECT s."ArticleId", s."ArticleVersionId", s."VersionNumber", s."ProfileId",
       s."SourceHash", s."ChunkCount", vector_dims(v."Embedding") AS dimension
FROM public.knowledge_index_sets s
JOIN public.knowledge_chunk_vectors v ON v."IndexSetId" = s."Id";

SELECT "ArticleId", "ArticleVersionId", "VersionNumber", "ChunkId", "Index",
       "StartChar", "EndChar", "StartLine", "EndLine"
FROM public.knowledge_active_chunks;
```

## Dữ liệu, retry và tính nhất quán

ArticleVersion giữ title/content bất biến. Index set giữ version/source hash, normalized content hash, toàn profile JSON và fingerprint; chunk giữ text/hash/heading/token count/offset; vector table liên kết bằng foreign key. Offset Unicode scalar trong content chuẩn hóa LF, end exclusive; line từ 1, end inclusive. Core kiểm tra text trùng source slice, toàn bộ source không trắng được phủ, chunk IDs, profile, dimension=384, finite và L2 trước khi persist.

Worker claim `FOR UPDATE SKIP LOCKED`, token riêng mỗi lease; retry 2^attempt giây tối đa 60 giây, tối đa số attempts cấu hình. Hết lease cho phép worker khác claim. Completion khóa article trước job và kiểm tra trạng thái/version/requestedJob/token; transaction chứa tất cả chunks, upsert vector và active pointer. Lỗi giữa chừng không để lại partial index. Publish/re-publish/archive/rebuild cùng khóa article nên xử lý được cạnh tranh.

HTTP 429/5xx, network và timeout được retry; 4xx khác và output contract sai không retry. AI giới hạn content 128 KiB UTF-8, 256 chunks; quá giới hạn làm job Failed, bài Published không bị mất. Chưa có chia nhỏ một bài vô hạn hoặc dashboard vận hành. Khi sửa nguyên nhân, tạo requestId mới để rebuild; không sửa trực tiếp snapshot/job/vector bằng SQL.

View active chỉ lọc Published/current version và index đã kích hoạt; không phải ranh giới authorization cho semantic search tương lai. Không thêm endpoint tìm kiếm vector.

## Kiểm thử và khôi phục

```powershell
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

Integration dùng PostgreSQL/pgvector container riêng dùng một lần. `KnowledgeIndexPipelineTests`: claim cạnh tranh, lease/retry, publish/re-publish, job cũ, archive, rebuild/replay, rollback job và lỗi vector giữa chừng, nâng cấp từ schema Knowledge cũ; `KnowledgeIndexAdapterTests`: fixture Python/.NET, validation và lỗi provider; `KnowledgeLifecycleEndpointTests`: HTTP, quyền, revision, version hiện hành. Repo AI có smoke model thật qua HTTP trong CI.

Để kiểm chứng riêng publish → worker processor → HTTP AI thật → pgvector, chạy AI trên localhost rồi đặt `KNOWLEDGE_SMOKE_AI_URL` và `KNOWLEDGE_SMOKE_AI_TOKEN` trong môi trường test, lọc `FullyQualifiedName~WorkerProcessorResolvesWithoutHttpContextAndPersistsOutcome`. Khi URL được đặt, nhánh thành công bắt buộc dùng HTTP adapter thật; nhánh provider lỗi vẫn dùng lỗi giả để kiểm retry. Database luôn là Testcontainers dùng một lần. Bỏ hai biến sau khi chạy; không dùng URL remote. Mặc định suite dùng provider giả cho test điều phối để không yêu cầu model ở mọi test job.

Để dừng xử lý nền, tắt `KnowledgeIndex__Enabled` ở Worker và giữ job/dữ liệu để bật lại. Rollback code nên giữ migration additive, không hạ schema trên dữ liệu thật. `Down` xóa toàn bộ dữ liệu indexing của tính năng; chỉ dùng khi đã xác nhận đích và chấp thuận mất dữ liệu. Không gỡ extension vector dùng chung. Snapshot bài viết vẫn là nguồn rebuild.
