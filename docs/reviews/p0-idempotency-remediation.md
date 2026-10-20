# Hồ sơ lịch sử: Khắc phục chống tạo trùng phiếu

> **Tài liệu lịch sử.** Ghi lại phương án và chốt chặn kiểm chứng lỗi chống tạo trùng tại mốc gốc; không thay kết quả PostgreSQL của phiên bản mới.
> Chú thích tiếng Việt ngày 2026-10-20; phần bản gốc bên dưới giữ nguyên. Nguồn đang áp dụng: [hướng dẫn hiện hành](../runbooks/backend-hardening-verification.md).

<!-- original-record -->
# P0 — Ticket idempotency remediation

Ngày: 2026-10-09. **Trạng thái: IMPLEMENTED / BLOCKED merge và release do integration gate chưa đạt.**
Chưa xác nhận P0 được khắc phục trên PostgreSQL thật. Không commit, push, PR, merge hoặc deploy.

## 1. Phạm vi, nguồn và worktree

- Hotfix branch: `fix/p0-idempotency-isolation`, worktree `../ai-it-service-desk-p0-idempotency`.
- Baseline sau fetch và pull --ff-only: `origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de`; hotfix HEAD cùng SHA. Checkout ban đầu sạch.
- Evidence trước sửa ở SHA baseline trên `origin/dev`. Evidence sau sửa là **working-tree diff chưa commit** trên branch hotfix, không phải mã đã có trong commit baseline.
- Đã đọc cả ba báo cáo tại worktree audit: repository-audit.md, branch-integration-plan.md, technical-debt-register.md; đối chiếu source trực tiếp. Không cập nhật Jira, không thay authentication, roles hoặc lifecycle.
- Audit linkage: AUD-01 (P0), AUD-03 (P1); requirement linkage TC-265/266, TC-247/421 theo audit cũ. Đây không phải xác nhận Jira đã nghiệm thu hay issue đã đóng.

## 2. Root cause và trước/sau

Tất cả finding dưới đây có source baseline ở `origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de`.

| ID / severity | Evidence và source trước sửa | Ảnh hưởng | Sau sửa / hướng xử lý | Owner đề xuất |
|---|---|---|---|---|
| AUD-01 / P0, bug đã xác nhận trong audit | TicketEndpoints.cs:28–40 trong src/backend/ServiceDesk.Api/Endpoints: cache key chỉ là `idempotency:ticket:{key.Trim()}`; trả cached TicketDto trước kiểm ownership. Audit probe A/B có identity khác nhau trả cùng ticket, chỉ gọi create một lần. Probe là endpoint thật + fake service/current user, chưa phải JWT/DB E2E. | B biết hoặc tái sử dụng key của A có thể nhận ID/title/description của A trong 10 phút. | Scope requester/method/resource/key hash trong DB; identity gate tại API và service; strict ticket owner gate trước replay. T01/T02/T09 đang chờ PostgreSQL gate. | Anh Nguyen triển khai; Đức Anh security review; VÕ NGUYỄN ANH KIỆT chạy CI |
| AUD-03 / P1, race đã xác nhận trong audit | Cùng file:37/45/50: TryGet/Create/Set tách rời, không atomic. Audit probe 10 request cùng key tạo 10 IDs. | Duplicate tickets và response không ổn định. | Advisory transaction lock cho toàn bộ scope; ticket và receipt commit chung; unique constraint bảo vệ scope. T05/T06/T07 chờ PostgreSQL. | Anh Nguyen + Kiệt |
| REM-01 / P1, risk suy từ source | IMemoryCache ở TicketEndpoints.cs và AddMemoryCache tại src/backend/ServiceDesk.Api/Program.cs:21; docker-compose.yml có nhiều API instance. Cache thuộc từng process. | Cùng key ở hai node có thể tạo ticket khác nhau; restart mất replay. Không claim đã tái hiện multi-node trước sửa. | Loại bỏ cache; các node dùng cùng PostgreSQL primary và lock namespace. Hai API hosts độc lập trong regression fixture. | Anh Nguyen + Kiệt |
| REM-02 / P1, contract gap xác nhận qua source | TicketEndpoints.cs trả cache hit mà không so sánh Title/Description/CategoryId. | Thay payload vẫn nhận kết quả request cũ; dễ hiểu sai giao dịch đã thực hiện. | SHA-256 payload chuẩn hóa; 409 trước mọi insert nếu key còn hạn và body khác. T04 chờ DB; mapping HTTP409 đã pass. | Anh Nguyen; Đức Anh duyệt contract |
| REM-03 / P1, consistency risk | TicketService.cs CreateTicketAsync SaveChanges trước cache.Set; receipt không có trong DB/migrations. | Crash sau ticket commit hoặc retry node khác có thể duplicate; không có atomic replay state. | Ticket + exact accepted response snapshot cùng transaction; rollback khi receipt write fail; retry sau mất response đọc receipt nếu commit đã thành công. | Anh Nguyen + Kiệt |

Full baseline paths cho TicketService và migrations:
`src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs`,
`src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs`.
Audit reproduction source/outputs nằm ở technical-debt-register.md (AUD-01/AUD-03 và Appendix A); không chuyển probe giả lập thành kết luận PostgreSQL E2E.

Identity không lấy từ request body: ICurrentUser.UserId đọc ClaimTypes.NameIdentifier trong
`src/backend/ServiceDesk.Infrastructure/Identity/CurrentUser.cs`; JwtTokenGenerator tạo NameIdentifier và sub từ IdentityUser.Id.
Program.cs validate issuer/audience/signature/lifetime, map role theo ClaimTypes.Role.
Hotfix không đổi mapping; service yêu cầu IsAuthenticated + parse GUID + đúng requesterId.
API giữ RequireRequester (Requester/Agent/Admin), nên Agent/Admin tạo ticket vẫn dùng **own identity**, không dùng quyền xem detail rộng hơn để replay.

## 3. Quyết định thiết kế

| Phương án | Ưu điểm | Giới hạn | Quyết định |
|---|---|---|---|
| Thêm requesterId vào IMemoryCache | Chặn global-key leak trong một process | Không atomic, không multi-node, mất state khi restart; không giải quyết payload mismatch | Không đủ cho P0 |
| Distributed cache với SET NX / compare-and-set và TTL | Lock/replay dùng chung, nhanh | Ticket commit ở PostgreSQL và cache commit không cùng transaction; lease expiry, crash giữa hai commit cần fencing/outbox/reconciliation | Không thêm hạ tầng/dual-write cho hotfix |
| Persistent PostgreSQL receipt + unique scope + transaction lock | Ticket/receipt atomic; cùng nguồn dữ liệu; retry sau lost response; lock tự giải phóng khi transaction kết thúc | Thêm migration, phụ thuộc DB/lock; phải kiểm tải/timeout/retention | Đã chọn; cần real DB validation trước merge |

Scope: authenticated requester GUID + `POST` + canonical `/api/tickets` + SHA-256 của key đã trim.
Payload: versioned JSON array gồm title.Trim(), description.Trim(), CategoryId, đúng semantics create hiện có.
Không ghép bằng delimiter dễ mơ hồ. Method/resource được giữ cả trong lock hash và unique index.
Lock dùng signed 64-bit SHA-derived scope; hash collision chỉ làm các scope khác chờ nhau, lookup/unique scope vẫn đầy đủ, không replay chéo.
SHA-256 không phải encryption; key phải opaque, không chứa token/password.

Thứ tự transaction READ COMMITTED:
1. Kiểm identity, command, header key trước transaction.
2. SET LOCAL lock_timeout=5s; acquire pg_advisory_xact_lock(scope).
3. Đọc clock_timestamp() từ DB và receipt **sau lock**, tránh snapshot trước commit của request khác.
4. Còn TTL: so payload hash; kiểm TicketId + RequesterId trực tiếp; deserialize snapshot và kiểm ID; commit/replay201.
5. Không có hoặc hết TTL: insert ticket, reload DB TicketNumber, ghi hoặc cập nhật receipt với DTO gốc; commit chung.
6. Exception/cancellation trước commit: dispose transaction/rollback. Context scoped theo HTTP request, discard sau lỗi.
7. Lost/unknown response sau successful DB commit: retry cùng key/body đọc receipt; không tự retry bằng key mới.

PostgreSQL xác nhận transaction-level advisory locks tự giải phóng ở transaction end và lock_timeout giới hạn thời gian chờ lock:
[explicit locking](https://www.postgresql.org/docs/17/explicit-locking.html),
[client settings](https://www.postgresql.org/docs/17/runtime-config-client.html).
Đây là cơ sở thiết kế; không thay cho test runtime.

## 4. API và expiry contract

| Trường hợp | Kết quả |
|---|---|
| Không gửi Idempotency-Key | Create/detail như cũ; không có cam kết dedup cho no-key |
| Valid identity + key + payload lần đầu | 201 + Location `/api/tickets/{id}` + TicketDto hiện có |
| Cùng requester/key/normalized payload còn hạn | Cùng 201/Location/DTO gốc, kể cả ticket đã được cập nhật sau create; TTL không được kéo dài |
| Requester khác dùng cùng key | Scope khác, tạo ticket của chính requester đó; không nhận ticket gốc |
| Cùng scope, key còn hạn, payload khác | 409, thông báo cố định không chứa payload/ticket của request cũ |
| Chờ lock quá 5 giây | 503 + Retry-After:1; retry cùng key/body; không state mới |
| Missing/invalid/expired JWT hoặc identity không hợp lệ | 401 trước replay; valid JWT không có role được phép:403 |
| Receipt trỏ đến ticket requester khác hoặc ID snapshot sai | Fail closed500; không trả snapshot; investigate corruption |
| Lỗi validation | 400, không bind key; sửa payload rồi retry được |
| Lỗi DB trước commit | 500, ticket/receipt rollback; retry được |
| Key hết hạn | Thế hệ mới được phép tạo ticket mới, kể cả body khác; concurrent retry thế hệ này vẫn serialize |

TTL cố định **10 phút từ lúc ghi receipt thành công**, dùng DB clock trên mọi node; so ExpiresAt > now.
Không tồn tại pending receipt/ticket commit riêng. Sequence có thể bỏ số khi transaction rollback; không coi đó là ticket duplicate.
Expiry là giới hạn dedup: client không được kỳ vọng replay vô hạn sau10 phút.

Thay đổi contract được giải thích:
- Key vẫn optional, outer whitespace được trim như cũ.
- Key có mặt nhưng rỗng/nhiều header values bị400; key phải1–200 visible ASCII characters, không internal whitespace/control/nonASCII.
  Đây là ràng buộc bounded input mới; frontend hiện gửi UUID, không bắt buộc key là UUID.
- Bổ sung409/503 cho conflict/timeout; 201 DTO/Location và GET200/404, Submitted=1, category/title/description validation giữ nguyên.


## 5. Security regression tests và trạng thái

Tests PostgreSQL đều dùng production TicketEndpoints/TicketService/CurrentUser/JWT validation, real migrations và một PostgreSQL17 container.
Fixture tạo hai WebApplicationFactory/DI hosts độc lập, DbContext scoped; không fake ticket/cache service.
Đây là hai API hosts trong cùng test process, chưa phải hai container deploy; thực thi cross-host và deployment smoke còn chờ CI.
Seeder Identity chỉ bị loại trong **fixture mới** do AUD-04; không thay production seeder, không giả lập ticket persistence; Auth integration suite cũ giữ nguyên.

| Test | Assertions thực chất | File | Trạng thái |
|---|---|---|---|
| T01 | A nhận201, có ID và một ticket/receipt persisted | TicketIdempotencyPostgresTests.cs | BLOCKED DB fixture |
| T02 | B cùng key nhận ID/title riêng; GET ticket A404; mỗi owner chỉ một ticket/receipt | Cùng file, T01_Create_T02... | BLOCKED DB fixture |
| T03 | Retry từ host2 có body/Location đúng lần đầu, kể cả ticket đã sửa trong DB; count1 | Cùng file, T03_SamePayload... | BLOCKED DB fixture |
| T04 | Thay CategoryId trả409, không thêm ticket, không lộ description cũ | Cùng file, T04... | BLOCKED DB fixture; HTTP mapping PASS |
| T05 | 8 request overlapping; external advisory gate và pg_locks chứng minh >=2 waiter; tất cả201/cùng ID; DB count1 | Cùng file, theory(twoHosts=false) | BLOCKED DB fixture |
| T06 | 8 request giữa hai hosts vẫn count1; concurrent body khác có một201 và một409 | Cùng file, theory(twoHosts=true) và T06_ConcurrentChangedPayload... | BLOCKED DB fixture |
| T07 | Trigger fail tại receipt INSERT sau ticket INSERT:500 và count0/0, retry thành công; invalid body không bind key | Cùng file, hai T07 tests | BLOCKED DB fixture |
| T08 | Expired receipt: concurrent hosts cùng tạo một thế hệ mới; old ticket giữ lại;2 tickets/1 receipt; thế hệ mới replay đúng | Cùng file, T08... | BLOCKED DB fixture |
| T09 | Sau successful create, missing token không replay; invalid/expired/missing JWT không gọi service; role lạ403; spoof identity bị từ chối tại service | PostgreSQL + Endpoint + Identity tests | Real DB BLOCKED;8 endpoint và5 identity cases PASS |
| T10 | No-key create hai ticket như cũ; status1; own detail200 và DTO đúng | Cùng file, T10...; existing TicketServiceTests.cs | Real DB BLOCKED; baseline unit suite PASS |
| Extra: timeout | Giữ scope lock >5s:503/RetryAfter1/count0; nhả lock rồi retry201/count1 | TicketIdempotencyPostgresTests.cs | BLOCKED DB fixture |
| Extra: ownership guard | Corrupt receipt của B trỏ ticket A:500, không private description, không tạo ticket B | Cùng file | BLOCKED DB fixture |
| Fingerprints/input | Trim semantics, field boundaries, category/description diff, distinct requester/key scope, hashes không raw key/payload, length/ASCII limits | CreateTicketFingerprintTests.cs,11 cases | PASS |

Full test source paths:
- `tests/ServiceDesk.Application.Tests/Ticketing/CreateTicketFingerprintTests.cs`
- `tests/ServiceDesk.Application.Tests/Ticketing/TicketIdempotencyIdentityTests.cs`
- `tests/ServiceDesk.IntegrationTests/TicketIdempotencyEndpointTests.cs` (Category=Unit; TestServer thật, service mocked)
- `tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresFixture.cs`
- `tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresTests.cs` (Category=Integration;13 cases)

Có16 fingerprint/identity và8 endpoint cases mới pass, cộng21 baseline unit/architecture = **45 PASS**.
Không có measured line/branch coverage%; không dùng mock/SQLite để xác nhận locking, rollback hoặc multi-node PostgreSQL.

## 6. Verification đã thực hiện

| Command | Kết quả cuối | Chi tiết |
|---|---|---|
| `dotnet build src/backend/ServiceDesk.sln -c Release` | PASS / exit0 |0 warnings,0 errors |
| `dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category!=Integration"` | PASS / exit0 |45 pass: Domain4, Architecture6, Application27, API endpoint8;0 skipped |
| `dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category=Integration"` | FAIL / exit1 — environment BLOCKED |41 fixture failures,0 pass,0 skipped:28 existing +13 new. No PostgreSQL assertion ran |
| EF `migrations has-pending-model-changes` | PASS / exit0 |No model change since last migration |
| EF upgrade/rollback script generation | PASS / exit0 |Add receipt table/index/check/FK; Down only drops receipt table; no live DB execution |
| `git diff --check` | PASS / exit0 |Whitespace reviewed |
| Migration apply/Down on real PostgreSQL | NOT RUN |Docker unavailable; script generation is not an apply/rollback test |
| Live two-process/deploy/load/commit-ack failure smoke | NOT RUN |Requires real environment; cancellation/commit uncertainty remains a runtime test gate |

Testcontainers exact blocker:
`DockerUnavailableException: Failed to connect to Docker endpoint at 'npipe://./pipe/docker_engine'`.
Fixture constructors fail before starting container.41 failed tests are **not41 confirmed product bugs**.
Initial intermediate build failed because the new endpoint tests needed Moq; test-only package reference added; final build above passes.
An initial integration run had39 fixture failures; final41 reflects the two extra security tests, not a change in failure cause.

Artifacts local, ignored, only inside hotfix worktree:
`.cache/build-release.log`, `.cache/non-integration.log`, `.cache/integration.log`,
`.cache/test-results/final-*.trx`, `.cache/idempotency-up.sql`, `.cache/idempotency-down.sql`.
No CI success claim; no deployment/PR check was triggered.

Commands to repeat in hotfix worktree or CI with Docker/PostgreSQL available:

```powershell
dotnet build src/backend/ServiceDesk.sln -c Release
dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category=Integration"

dotnet ef migrations has-pending-model-changes --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --configuration Release
dotnet ef migrations script 20260918143000_AddTicketTable 20261009120000_AddTicketIdempotency --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --configuration Release
dotnet ef migrations script 20261009120000_AddTicketIdempotency 20260918143000_AddTicketTable --project src/backend/ServiceDesk.Infrastructure --startup-project src/backend/ServiceDesk.Api --configuration Release

git diff --check
git diff --stat
git ls-files --others --exclude-standard
```

Local EF tool installed under `.cache/tools/dotnet-ef` version10.0.12; local commands used this executable and --no-build.
TRX/results-directory logger options added for evidence; the required build/configuration/filter are unchanged.

## 7. Migration, rollout và rollback plan — chưa thực hiện

Migration ID: `20261009120000_AddTicketIdempotency`.
Up additive: public.ticket_idempotency; unique(requester_id,method,resource,key_hash), expiry index/check, ticket FK RESTRICT.
No business enum/default, Identity DDL, Ticket columns hoặc applied historical migration changed.
Designer và ModelSnapshot generated từ current EF/Npgsql model để pending-model gate hoạt động. Baseline chưa có snapshot.
Snapshot có metadata Identity hiện có trong model, **không chứng minh Identity tables tồn tại trên DB** và không sửa AUD-04.
[EF migration management](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) là reference cho snapshot/pending-model check.

Owner rollout: Kiệt (CI/DB/deployment), Anh Nguyen (Backend), Đức Anh (security go/no-go).

1. Chạy full required suite trên Docker; giải quyết baseline fixture/schema failures nếu xuất hiện bằng reviewed dependency change riêng; không bỏ Auth tests để đạt gate.
2. Thử migration upgrade từ DB có current migration history và dữ liệu; verify identity/schema prerequisites độc lập; thử Down trên disposable database, xác nhận ticket IDs/count giữ nguyên.
3. Backup DB, ghi image/commit/migration digests; tạm chặn create-ticket writes tại ingress, drain in-flight requests và dừng **tất cả old vulnerable API nodes**.
4. Apply reviewed additive migration **một lần**; khởi động toàn bộ nodes cùng patched version/scope algorithm, cùng writable PostgreSQL primary; clear old process caches bằng việc retire old processes.
5. Smoke A/B shared key, changed-body409, concurrent eight retries trên hai nodes, snapshot replay,503 and rollback; bật lại writes khi evidence đạt.
6. Keys của old cache chưa có persistent receipts: không thể backfill an toàn từ cache không có owner/payload hash. Invalidate old cache/deploy window; client retries cũ có thể tạo thêm ticket trong chuyển giao, phải reconcile bằng incident workflow.
7. Không rolling mixed old/new writers: old code bỏ qua lock/receipt và vẫn có thể leak/duplicate. Hotfix không bảo vệ request đang được old process phục vụ.

Rollback:
- Ưu tiên fix-forward hoặc redeploy một **đã được kiểm thử và an toàn** compatible patched build; giữ receipt table/state để retries vẫn dedup.
- Không đưa vulnerable origin/dev build trở lại khi create endpoint đang mở. Nếu chỉ có image cũ, chặn POST /api/tickets đến khi bản an toàn sẵn sàng.
- Down chỉ dùng sau drain/stop mọi receipt writer trên disposable DB hoặc khi có approval/data plan; generated SQL chỉ DROP receipt table, không tickets.
- Dropping receipts mất replay history; tuy tickets còn, retry có thể duplicate. Source rollback không đồng nghĩa data rollback; giữ dữ liệu mặc định.
- Không reset/force-push lịch sử Git, không volume deletion. Receipt FK RESTRICT cần được cân nhắc nếu tương lai thêm ticket deletion (không có trong scope này).


## 8. Changed files và diff summary

Tổng18 files:6 modified +12 added; chỉ trong hotfix worktree. Các file sau chưa commit.

| File | Thay đổi |
|---|---|
| src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs | Bỏ global cache; dispatch persistent use case; identity/header gates;409/503 mapping;201/GET contract giữ nguyên |
| src/backend/ServiceDesk.Api/Program.cs | Bỏ AddMemoryCache không còn consumer |
| src/backend/ServiceDesk.Application/Ticketing/Interfaces/ITicketService.cs | Thêm explicit idempotent create method; giữ create/detail signatures cũ |
| src/backend/ServiceDesk.Application/Ticketing/Idempotency/CreateTicketFingerprint.cs | Added: scope, versioned semantic payload fingerprints, bounded key |
| src/backend/ServiceDesk.Application/Ticketing/Idempotency/IdempotencyConflictException.cs | Added: application conflict/in-progress exceptions; không HTTP/EF dependency |
| src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs | Atomic lock/transaction/receipt/replay; share original validation/new-ticket/DTO logic |
| src/backend/ServiceDesk.Infrastructure/Ticketing/TicketIdempotencyRecord.cs | Added: infrastructure persistence receipt |
| src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs | Receipt mapping, unique scope, expiry constraint/index, restricted FK |
| src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20261009120000_AddTicketIdempotency.cs | Added: receipt-only additive Up/Down |
| src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20261009120000_AddTicketIdempotency.Designer.cs | Added: generated target-model metadata |
| src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs | Added: generated current snapshot; không sửa thiếu Identity DDL |
| tests/ServiceDesk.Application.Tests/Ticketing/CreateTicketFingerprintTests.cs | Added:11 normalization/hash/key cases |
| tests/ServiceDesk.Application.Tests/Ticketing/TicketIdempotencyIdentityTests.cs | Added:5 spoofing/provider fail-closed cases |
| tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj | Moq test-only dependency, cùng range4.* đã dùng tại Application.Tests |
| tests/ServiceDesk.IntegrationTests/TicketIdempotencyEndpointTests.cs | Added:8 real endpoint/JWT boundary cases, mocked use case; Category=Unit |
| tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresFixture.cs | Added: two real API hosts/shared PostgreSQL, production auth middleware |
| tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresTests.cs | Added:13 real PostgreSQL cases covering T01–T10/timeout/corruption |
| docs/reviews/p0-idempotency-remediation.md | Added: root cause, contract/design, evidence/tests, rollout/rollback/blockers |

Generated EF metadata chiếm phần lớn migration diff, chứa current Identity/Ticket model chứ không có Identity schema operation.
`git diff --stat` chỉ liệt kê tracked modified files trước staging; cần thêm `git ls-files --others --exclude-standard` để không bỏ12 files mới.
Không có frontend/domain lifecycle/old migration/Jenkins/business feature ngoài phạm vi bị thay đổi.

## 9. Self-review

- Source review: API không còn global-cache hit trước owner gate; authenticated identity kiểm cả API và use case.
- Replay đọc full scope; owner kiểm trực tiếp Ticket.RequesterId; không dùng GetTicketById quyền Agent/Admin.
- Payload conflict xảy ra trước insert; malformed/unauthorized inputs không bind receipt; response lỗi không echo payload.
- Lock acquire -> fresh READ COMMITTED lookup -> insert/replay -> common commit. Unique DB index; shared primary required.
- Ticket/receipt failure trước commit rollback; context per request; no process-local fallback.
- Contract/lifecycle: no-key flow, original fields/Location, title/description/category validation, Submitted=1 và detail role/ownership behavior giữ nguyên.
- Clean Architecture: application fingerprints/exceptions/interfaces không phụ thuộc API/EF/Npgsql; DB record/lock implementation ở Infrastructure.
  TicketService giữ namespace lịch sử ServiceDesk.Application.Ticketing nhưng **assembly vật lý là Infrastructure**; architecture suite6 cases PASS, không refactor namespace ngoài scope.
- Migration Up/Down SQL review và current-model check PASS; old migration không bị sửa. Real apply/rollback NOT RUN.
- Assertions kiểm identity/no service calls,409/503, response equality, DB counts, actual pg_locks overlap, failing receipt trigger, TTL generation và corrupt owner.
- **Chưa pass** thực thi PostgreSQL lock/atomicity/multi-host/expiry; không kết luận “không còn duplicate” từ unit tests.
- Đã kiểm whitespace và toàn bộ changed source. Self-review hoàn tất ở mức static/non-integration; chưa hoàn tất merge gate.

## 10. Blockers, residual risks và thứ tự xử lý

Source baseline cho AUD findings vẫn là `origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de`.
New tests/receipt references là pending diff ở `fix/p0-idempotency-isolation` trên cùng HEAD.

| Severity / item | Evidence / ảnh hưởng | Owner và bước tiếp theo |
|---|---|---|
| P0 AUD-01, chưa close | Source leak được thay nhưng TicketIdempotencyPostgresTests.cs chưa chạy do DockerUnavailableException tại TicketIdempotencyPostgresFixture.cs. Production remediation chưa verified. | Kiệt: chạy Docker-enabled full gates + two-node smoke; Anh Nguyen sửa nếu fail; Đức Anh review acceptance trước merge |
| P0 AUD-02, ngoài hotfix | Baseline src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs; src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs; docker-compose.yml: unconditional seed/default secret vẫn còn. Không đổi production Auth trong scope này. | Anh Nguyen + Kiệt xử lý độc lập seed gate/secret provisioning; Đức Anh chặn production release nếu chưa xử lý |
| P1 integration environment | Final integration TRX:41 failures, npipe docker_engine unavailable, zero DB assertions. | Kiệt cung cấp Docker runner và publish TRX/check đúng; không waive integration gate |
| P1 AUD-04 residual migration baseline | Baseline20260918_AddTicketTable.cs không tạo Identity tables; IdentityDataSeeder gọi migrate/seed. Snapshot mới không bổ sung Identity DDL. Clean-DB Auth/startup có thể fail khi Docker hoạt động; đây là source-confirmed gap, runtime outcome chưa chạy. | Anh Nguyen + Kiệt review separate Identity baseline/upgrade fix; repeat **all** Auth/DB/Ticket integration tests, không chỉ new fixture |
| P1 deployment transition / unknown commit ack | Existing-memory receipts không có trustworthy owner để migrate; mixed old/new nodes không đồng nhất; disconnect trong commit cần real chaos check. Source: TicketService.cs transaction; TicketEndpoints.cs baseline cache; docker-compose.yml topology. | Kiệt drain/block writes; retire all old nodes; test lost response/crash after commit; reconcile old retries, không blind retry bằng key mới |
| P2 receipt retention/capacity | ResponseJson chứa accepted ticket DTO (bao gồm title/description) để exact replay. TTL hết hạn không tự xóa row; expiry index có sẵn. Không raw request/Authorization/token/raw key và không thêm log payload. Không có cleanup worker mới. | Kiệt + Anh Nguyen thiết lập bounded, monitored delete of expired receipts theo DB runbook/data retention; kiểm capacity, transaction contention, delete semantics trước scale |
| P2 future compatibility | Hash/version/normalization phải thống nhất giữa writers; all nodes cùng primary; replica lag/independent DB không được hỗ trợ cho replay. Header validation mới cần client awareness. | Backend/FE/QA giữ key/body khi retry503, handle409, không đổi version hash trong mixed rollout; kiểm contract phía client |

Ưu tiên: Docker/real test evidence -> schema prerequisites/full baseline gates -> reviewed migration rehearsal -> all-node safe rollout -> lost-response/load/retention operations.
Không mở rộng feature hoặc sửa auth để che test failure trong hotfix này.

## 11. Bảo toàn hai worktree cũ

So sánh đầu/cuối bằng branch, HEAD, git status --short, git ls-files --stage, git diff --binary,
untracked paths và SHA-256 **mọi tracked/nonignored-untracked file**:

| Worktree | Trạng thái đầu và cuối | Kết quả |
|---|---|---|
| ai-it-service-desk | frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7; clean;154 files | Branch/HEAD/status/index/diff/untracked/all file hashes identical |
| ai-it-service-desk-audit | audit/repository-audit-20261009@6b124e73e9cb95350a505a24a548cb663ab1b7de; ?? docs/reviews/;148 files gồm3 reports | Branch/HEAD/status/index/diff/untracked/all file hashes identical |

Yêu cầu nhắc “9 file đang stage” là trạng thái trước đó. Khi bắt đầu hotfix, user đã có commit
`063489b97c1f706cebeaeea1d3038f0e63a39fd7`, message “docs: add architecture contracts and governance baseline”,
gồm9 docs/architecture files,1006 insertions. Original checkout đã clean trước mọi thao tác hotfix.
Hotfix không commit những file này; cả9 file và toàn bộ index/hash giữ nguyên đúng snapshot đầu hotfix.
Ba audit reports vẫn untracked/chưa commit và nội dung giữ nguyên.

Không switch/stash/reset/discard/index edit hoặc business file write trong hai worktree cũ.
Fetch/worktree registration chỉ thay metadata Git dùng chung theo user authorization; original/audit branch tips không đổi.
Other registered dev/main worktrees cũng không được thao tác.

## 12. Kết luận bàn giao

Code/migration/tests/report sẵn sàng review trên branch hotfix riêng; **BLOCKED**, chưa đủ điều kiện merge/release.
Không tạo commit vì required integration verification chưa đạt; không push, PR, merge hoặc deploy.
Chỉ close AUD-01/AUD-03 và commit sau khi PostgreSQL/full required gates thành công và reviewer xác nhận rollout/rollback.

## Publication verification — 2026-10-11

User đã yêu cầu commit/push các nhánh và merge lần lượt vào dev. Commit hotfix trong lượt này là checkpoint để chạy CI/review; không ghi nhận production verification PASS trước khi PostgreSQL gate thành công.

- Release build: PASS,0errors/0warnings.
- Category!=Integration: PASS45tests (Domain4, Application27, Architecture6, endpoint-unit8).
- Category=Integration: FAIL41/41 tại Docker fixture initialization;0passed,0DB assertions. Artifact ignored: tests/ServiceDesk.IntegrationTests/TestResults/premerge-integration.trx.
- Đã thử khởi động Docker Desktop; engine trả HTTP500. wsl --status báo WSL2/Virtual Machine Platform/virtualization chưa khả dụng; không thay đổi Windows features/BIOS hoặc reboot.
- Thêm .github/workflows/backend-security-regression.yml để chạy đúng build/unit/integration trên GitHub-hosted Ubuntu24.04 Docker runner. Actions pinned commit SHA, contents:read, không lấy production credentials. Workflow/model tests phải PASS trước merge hotfix.
- Trạng thái CI tại lúc commit: PENDING; merge/release vẫn BLOCKED. Những dòng “không commit” trong báo cáo phía trên mô tả audit ban đầu, trước authorization mới.

### Linux CI first run: confirmed blockers and fixture correction

- CI [38070464842](https://github.com/supportflow-ai/ai-it-service-desk/actions/runs/38070464842): Docker/build/unit PASS; full integration FAIL41/41.
- Existing Auth/Health/Database/Ticket tests fail PostgreSQL42P01: relation public.AspNetRoles does not exist. This confirms missing Identity migration/bootstrap at baseline; owner Anh Nguyen/Backend Core, QA Kiệt. Requires separate migration/data-compatibility review; not hidden with EnsureCreated or ignored tests.
- Hotfix PostgreSQL requests fail401 before exercising persistence because Program captures JwtOptions before WebApplicationFactory's configuration override. Corrected fixture uses PostConfigure on the existing JwtBearer scheme only for test issuer/audience/key; production authentication and validation flags remain unchanged.
- Added four negative real-host JWT replay cases: wrong signature/issuer/audience and expired token must401 without new writes or private replay. PostgreSQL hotfix cases now17; nonintegration remains45.
- Fixture correction Release build/nonintegration45PASS locally. Full Linux CI rerun pending at this commit. No Identity schema/production Auth logic changed; hotfix merge remains blocked until all required gates PASS.
