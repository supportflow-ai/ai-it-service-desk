# Trạng thái thực thi kế hoạch củng cố production

Tệp trạng thái duy nhất của [kế hoạch củng cố production](production-hardening-plan.md) (thay cho `.audit/REFACTOR_LOOP_STATE.md` và `docs/reviews/production-readiness-register.md` để không có hai nơi theo dõi cùng nội dung). Đầu mỗi phiên: đọc tệp này, đối chiếu `git log` và working tree, rồi chọn phần việc có thể thực hiện theo phạm vi được giao, dependency và mức ảnh hưởng. Task `Implemented` chỉ chờ CI/duyệt được theo dõi gate còn thiếu, không viết lại mã chỉ vì chưa có nhãn `Verified`.

Trạng thái hợp lệ: Pending, In Progress, Implemented, Verified, Blocked, Not Applicable. `Implemented` không đồng nghĩa `Verified`.

## Phiên tiếp tục ngày 28/11/2026

- Mốc đầu phiên: `Refactor-2@0a96de157919c3a3b20fc49d9d599a938c259739`, cộng phần G2 chưa commit đã tồn tại trước phiên này. Baseline bên dưới là mốc ban đầu; dùng `git log` để xác định commit bàn giao mới nhất.
- Lúc tiếp nhận: G2-01/G2-02/G2-03/G2-05 **In Progress**, có adapter PostgreSQL, ánh xạ EF, `analysisId` và transaction HITL nhưng chưa có migration, kiểm thử PostgreSQL hay đồng bộ OpenAPI/web. Phần dở dang đã được giữ lại và hoàn thiện; trạng thái sau phiên nằm ở bảng task và bằng chứng cuối tệp.
- Kiểm tra lại trước phiên thực thi: locked restore và build Release đạt (0 warning/error). `Category!=Integration` exit 1: 227 đạt, 476 lỗi chứa `0x800711C7`, có lỗi discovery; không coi là bằng chứng đủ cho G1/G2. Log: `.cache/hardening-analysis-20261128-142917/tests.log` (artifact local, không đưa vào Git).
- Thứ tự đã được người dùng giao: mở môi trường kiểm thử → bù kiểm chứng G1 → hoàn tất G2-01/G2-02/G2-03 và audit liên quan → review → cập nhật trạng thái theo bằng chứng. Không push hay thao tác database dùng chung.
- Giữ nguyên chính sách Requester và trạng thái phiếu trong khi AI-01/DEC-09 còn mở. Đã sửa mục AI-01 cuối tệp để khớp bảng HTTP: GET theo phạm vi đọc, chỉ hai POST yêu cầu Agent/Admin; quyết định nghiệp vụ AI-01 vẫn chưa chốt.
- Mở được Docker Desktop local; bộ SDK Linux riêng `sd-hardening-20261128` dùng bản sao nguồn không chứa `.env`, không tác động PostgreSQL đang chạy của dự án khác. Baseline Linux: Domain 231/231, Application 362/362, Architecture 57/57; nhóm offline của IntegrationTests 70/73, ba lỗi đúng OpenAPI và model/snapshot chưa đồng bộ.
- Tìm thấy ba kho còn lại tại `D:/User/HUCE/HK1_2627/2. ThapCam/Code/`. Web đang có thay đổi riêng của người dùng ở LandingPage/style/test setup; không đưa các thay đổi đó vào nhiệm vụ này.
- Kế hoạch thực thi chi tiết: (1) `TicketAccessScopeTests` kiểm GET chỉ đọc và thu hồi quyền theo nhóm/Lead; (2) `AiTriagePostgresTests` kiểm lịch sử, replica/restart, quyết định, replay, đồng thời, rollback; (3) migration AIAssistance và snapshot, chỉ hai bảng AI; (4) kiểm hợp đồng, cập nhật baseline có chủ đích và caller web; (5) chạy suite/architecture/PostgreSQL, review độc lập, ghi mọi giới hạn còn thiếu. G2 dùng chung DbContext scoped với Ticketing, không mở HTTP/AI trong giao dịch và không tạo framework transaction mới.

## Baseline (28/11/2026)

| Kho | Nhánh | SHA | Ghi chú |
| --- | --- | --- | --- |
| backend `ai-it-service-desk` | `Refactor-2` | `1086d8d` | Trùng `origin/main`; `origin/dev` = `2e66eff` (HEAD đi trước dev 1 merge commit, cùng nội dung). Working tree sạch, chỉ có `.serena/` chưa theo dõi (của người dùng, không đụng). |
| web, ai-service, infra | — | — | **Blocked**: không có bản checkout trên máy này. |

### Quality gates ban đầu

| Lệnh | Kết quả |
| --- | --- |
| `dotnet restore src/backend/ServiceDesk.sln --locked-mode` | Đạt |
| `dotnet build src/backend/ServiceDesk.sln -c Release --no-restore` | Đạt, 0 warning, 0 error |
| `dotnet test ... --filter "Category!=Integration"` | Exit 1. ArchitectureTests 54/56, Application.Tests 44/330, Domain.Tests không nạp được. **Toàn bộ 288 lỗi là `FileLoadException 0x800711C7` ("An Application Control policy has blocked this file")**: Windows chặn nạp DLL vừa build. Không quan sát được lỗi logic nào; chạy lại cho kết quả giống hệt. |
| `dotnet test ... --filter "Category=Integration"` | Not Run: Docker Desktop không chạy (`dockerDesktopLinuxEngine` không tồn tại). |

Hệ quả: chưa task nào có thể chuyển `Verified` trên máy này cho tới khi có môi trường chạy được test (xem Blockers).

## Phân loại task theo mã nguồn hiện tại

| Task | Trạng thái | Bằng chứng |
| --- | --- | --- |
| G0-01 | In Progress | Tệp này; baseline ở trên |
| G0-02 | Pending | Chưa lập ma trận endpoint |
| G0-03 | In Progress | DEC-05 đã chốt "MVP không tự đóng"; DEC-09, AI-01, AI-03 còn mở ([pending-business-decisions](../architecture/pending-business-decisions.md), [ai-integration-plan](../architecture/ai-integration-plan.md)) |
| G1-01 | Implemented | Ticketing kiểm quyền; HTTP/PostgreSQL đã kiểm Agent/Lead, thu hồi thành viên, vô hiệu hóa nhóm, chuyển nhóm. Trong HITL khóa phiếu và nhóm/thành viên tới commit, kể cả Reject. Chờ CI và duyệt trên SHA bàn giao |
| G1-02 | Implemented | GET chỉ đọc; test HTTP kiểm không thêm analysis. Web hiện tắt panel AI, chưa có caller production dựa vào GET tự phân tích |
| G1-03 | Implemented | Bỏ mặc định `Medium`; Accept/Reject không hợp lệ trả 409 (`TriageDecisionConflictException`), không ghi phiếu; gợi ý chỉ đánh dấu sau khi ghi phiếu thành công. `analysisId` thuộc G2-02 |
| G1-04 | Implemented | Domain suite 231/231; Category và audit trước/sau đã kiểm PostgreSQL, gồm rollback khi ghi quyết định/audit lỗi |
| G1-05 | Implemented | Đã gỡ auto-close; ARCH-15 đạt. Image Worker khởi động được trên PostgreSQL smoke dùng một lần, giữ migration/audit cũ |
| G2-01 | Implemented | Scoped PostgreSQL adapter, hai bảng AI, hai forward migration và snapshot. Đã kiểm history, đọc qua host mới, outcome/lỗi xác thực, constraints và upgrade từ schema trước G2 |
| G2-02, G2-03, G2-05 | Implemented | `analysisId`, hash, khóa scope, kiểm trạng thái/revision, quyết định/replay lưu bền. Test hai reviewer, rollback ticket/decision/audit, lỗi INSERT audit ở DB, cancellation, mất session, timeout khóa đạt. Audit nội bộ không lộ lý do Override cho Requester |
| G2-04 | In Progress | Đã bảo toàn outcome và validation errors sau lưu/đọc; chưa đánh giá toàn bộ checklist lifecycle riêng |
| G2-06 | In Progress | Snapshot OpenAPI, nguồn/checksum và kiểu sinh của web đã đồng bộ; sửa bộ sinh để giữ nullable trên `$ref`. Luồng UI cũ chưa nối hợp đồng toàn analysis; panel tiếp tục tắt. AI v1 chưa có metadata model/version, không giả lập provenance |
| G3-01 | Implemented | Bắt buộc persistence, user/stamp hiện hành; RED đúng hai đường bypass, GREEN JWT/Identity/idempotency và PostgreSQL hai host. Chờ CI/duyệt đúng SHA; bằng chứng cuối tệp. |
| G3-02 | Implemented | Kiểm định rotation/replay, family, expiry và hai host; bổ sung 3 test, nhóm session/Identity/JWT 42/42. TTL đã được chủ dự án chốt access 60 phút, refresh 7 ngày; đồng bộ infra trong đợt này. Chờ CI/duyệt. |
| G3-03 | Implemented | PostgreSQL request/outbox nguyên tử, payload AES-GCM, claim/lease và retry cùng mã. Đã kiểm capacity/claim đồng thời, rollback, upgrade, lỗi SMTP và restart process thật; toàn suite nguồn cuối 994/994, ba image đạt. Chờ CI/duyệt đúng SHA; bằng chứng tiếp tục sau bàn giao tạm dừng ở cuối tệp. |
| G3-04 | Implemented | OTP HMAC v1 gắn email/mục đích với khóa riêng bắt buộc. HTTP negative, concurrent reset, rollback password/stamp/OTP/session và log redaction đã kiểm PostgreSQL. Chờ CI/duyệt. |
| G3-06 | Implemented | Actor bắt buộc ở Application, profile nguyên tử/kiểm IdentityResult, phòng ban kiểm input và conflict đồng thời, trả tên đã lưu và sửa status OpenAPI. RED/GREEN, ma trận role HTTP và hồi quy PostgreSQL đạt; chờ đồng bộ web, review toàn đợt, CI/duyệt đúng SHA. |
| G3-05 | Implemented | Admission/expiry/counter cùng khóa, tối đa 4096 partition email và 320 ký tự sau trim; đầy trả 429, giữ ngân sách cũ. RED/GREEN, proxy, toàn suite và image đạt local; chờ CI/duyệt đúng SHA. |
| G4-01 … G4-09 | Pending | Chưa rà |
| G5-01 … G5-08 | Pending | Chưa rà |
| G6-01 … G6-06 | Pending | Chưa rà |
| G7-01 … G7-07 | Blocked | Cần các kho web, ai-service, infra, môi trường và phê duyệt DEC-08 |

## Kiểm chứng sau G1 (28/11/2026)

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build src/backend/ServiceDesk.sln -c Release --no-restore` | Đạt, 0 warning, 0 error |
| `dotnet test ... --filter "Category!=Integration"` | Application 355/355, IntegrationTests (nhóm không cần PostgreSQL, gồm `PublishedApiMatchesBaseline`) 73/73, ArchitectureTests 57/57. **`Domain.Tests` bị bỏ qua** (0x800711C7) dù lệnh trả exit 0 |
| Domain.Tests ở một lượt trước đó | 185 đạt, 37 lỗi: 34 do Application Control, 3 do giả định sai trong test mới (đã sửa, chưa chạy lại được) |
| `HttpClassificationAdapterTests` | Một lần đỏ `Timeout` (lỗi chập chờn đã ghi trong AGENTS.md); chạy riêng 3 lần đạt 11/11 và đạt trong lượt cuối |
| `python scripts/validate_project_docs.py` | Đạt |
| `Category=Integration`, build image, smoke | Not Run: Docker không chạy |

Mức chặn của Application Control thay đổi giữa các lần build. TDD: test hồi quy cho lỗi ghi sau commit ở Override đã thấy RED rồi GREEN; các test G1 còn lại **chưa quan sát được RED** trên mã cũ (baseline bị chặn, và chúng dùng chữ ký mới).

Review: tự rà (self-review), chưa có kiểm định độc lập.

## Thay đổi hành vi API cần báo kho web

- `GET /triage` không còn tự phân tích; web phải gọi `POST /ai-analyze`.
- `GET /triage`: phiếu ngoài phạm vi (kể cả Requester không phải chủ phiếu, trước đây 403) trả 404; Agent là chủ phiếu ngoài phạm vi xử lý chỉ nhận hình chiếu Requester.
- Accept/Reject khi không có kết quả hợp lệ trả 409 (trước đây 200).
- Accept/Override nay đổi cả Category; `categoryId` rỗng hoặc sai mã trả 400 (trước đây bị bỏ qua).

OpenAPI baseline không đổi.

## Xung đột tài liệu cần chủ dự án chốt

- **AI-01 (quyền xem gợi ý AI của Requester).** Hợp đồng hiện hành và mã nay cùng mô tả hai POST cần quyền nhân viên, GET theo phạm vi đọc phiếu và trả `RequesterTriageView` hạn chế cho chủ phiếu. Không còn xung đột mô tả HTTP cũ, nhưng quyết định nghiệp vụ AI-01 vẫn mở. Giữ hành vi hiện tại; cần chủ dự án chốt quyền hiển thị trước khi bật UI liên quan.
- **DEC-09 (`Triaged`).** Chưa chốt; luồng HITL giữ phương án hiện tại (ghi dữ liệu, không đổi trạng thái).

## Blockers và phụ thuộc phát hành

| Mô tả | Cần |
| --- | --- |
| Windows Application Control | Đã có đường kiểm chứng SDK Linux; không sửa chính sách Windows |
| Docker và checkout liên kho | Đã mở Docker local và tìm thấy đủ checkout. Chỉ sửa các tệp hợp đồng web thuộc nhiệm vụ; giữ thay đổi LandingPage/style/test setup của người dùng |
| CI, PR và nghiệm thu liên kho | Chưa push/PR, không có CI trên SHA mới và chưa có người duyệt PR. Giữ các task ở Implemented; bộ kiểm tra local không thay nghiệm thu production |
| AI-01/DEC-09 và UI đầu cuối | Chưa tự chốt chính sách hay bật panel AI. G2-06/G7-02 còn việc nối UI theo hợp đồng thật |

## Nhật ký vòng lặp

- 28/11/2026 — G0-01: lưu kế hoạch, lập baseline, xác minh lỗi P0/P1 bằng đọc mã.
- 28/11/2026 — G1-01…G1-05: đã triển khai (Implemented), chờ Domain.Tests, kiểm thử PostgreSQL và CI để Verified.

## Task tiếp theo

G3-01…G3-05 đã có commit cục bộ. Người dùng đã giao G3-06 và G4: kiểm định Identity đã GREEN, tiếp tục các task G4 có đủ requirement và môi trường kiểm chứng theo mục cuối tệp. G2-04, G2-06/G7-02 và nâng dependency web vẫn là các đợt riêng. Gate phát hành là CI/duyệt đúng SHA và smoke liên hệ thống khi có ủy quyền tương ứng; không tự push để lấy bằng chứng hoặc gắn Verified từ local pass.

## Bằng chứng sau G2 và review ngày 28/11/2026

- Mốc kiểm tra: `0a96de1` cộng toàn bộ diff G2 của phiên này; artifact local tại `.cache/hardening-linux/`, không đưa vào Git.
- Locked restore và build Release trong `mcr.microsoft.com/dotnet/sdk:10.0.300` đạt, 0 warning/error. Bộ không cần PostgreSQL: Domain 231, Application 365, Architecture 57, offline IntegrationTests 75 — tổng **728/728**. PostgreSQL thật: **190/190** (`final-integration`). Các lượt lỗi trước đó được giữ trong log, không dùng báo cáo cũ thay lượt cuối.
- EF `has-pending-model-changes`: exit 0, không còn lệch model/snapshot. Hai migration chỉ thêm dữ liệu AI; migration thứ hai backfill outcome cho bản ghi cũ, không sửa migration đã phát hành. `Down` xóa lịch sử AI nên rollback sau khi có dữ liệu phải dùng phương án tiến tới.
- Build image API, Worker, Migrator đạt. Smoke riêng trên Docker Desktop local: container `sd-hardening-smoke-pg`, PostgreSQL 17, database `hardening_smoke`, không publish cổng DB, không volume bền. `migrate` và `seed-roles` exit 0; API `/health/ready` trả `Healthy`; Worker chạy được. Không chạy seed-demo/bootstrap-admin/check-production hay chạm database của dự án khác. Đây là smoke backend local, không phải smoke toàn hệ thống của infra.
- Validator tài liệu và 6 test công cụ đạt; OpenAPI 3.1 của hợp đồng AI v1, fixture và checksum đạt qua Python venv.
- Review bằng sub-agent độc lập, chỉ đọc: phát hiện Reject không khóa/kiểm phiếu; replay bỏ revision; lộ lý do Override qua timeline; mất outcome; OpenAPI GET thiếu hình chiếu staff. Đã sửa cả năm và bổ sung hồi quy. Kiểm thử bổ sung lỗi audit tại DB, cancellation, mất kết nối, hai reviewer, upgrade và host mới. Không coi sub-agent là người duyệt PR.
- Quyết định phạm vi: giữ `expectedRevision` của Reject tùy chọn theo hợp đồng hiện hành, nhưng kiểm giá trị khi gửi và khóa snapshot tới commit. Web chỉ đồng bộ hợp đồng trong phiên này, chưa bật luồng AI; bật sớm sẽ gọi nhầm route/hình dạng request cũ.
- Khi đồng bộ web, typecheck tái hiện lỗi nullability: Swagger chưa mở rộng `$ref` cho `TicketDto.Sla` nullable, và bộ sinh kiểu bỏ qua nullable trên tham chiếu. Đã bổ sung cấu hình Swagger `allOf`, bộ sinh đọc `allOf`/nullable và test hồi quy API; không sửa fixture hay nới TypeScript để làm xanh. Số kiểm thử cuối cùng cập nhật sau lượt này ở dưới.
- Ngoài phạm vi G1/G2: `npm audit` trên lockfile web hiện tại báo 16 mục (8 moderate, 3 high, 5 critical), gồm chuỗi dev/test `vitest`, `tinypool`, `eslint-plugin-boundaries`, `@boundaries/elements`, `handlebars`. Có advisory đọc file/RCE của công cụ kiểm thử ([Vitest](https://github.com/advisories/GHSA-82fw-gwwq-j7x9), [Tinypool](https://github.com/advisories/GHSA-5gmw-xhrv-c9v3)); không tự suy ra đây là RCE trong bundle production. Cần xử lý dependency tại G6-06; không chạy `npm audit fix` hay đổi lockfile trong đợt này. Bằng chứng local: `web-audit-linux.json`.
- Lượt cuối trên mã bàn giao: **728/728** không cần PostgreSQL và **190/190** PostgreSQL (`final-unit`, `final-integration`), tổng **918/918**, không bỏ qua test. OpenAPI GET dùng `anyOf`, vì Requester là tập con trường của Agent nên `oneOf` sẽ loại phản hồi khớp cả hai; test hồi quy đã thấy RED rồi GREEN. Build API cuối dùng cùng mã metadata này; Worker/Migrator không phụ thuộc thay đổi Swagger.
- Web dùng Node 22 trong container riêng: `contract:check`, typecheck, lint, **481/481 test trong 65 tệp**, build đạt. Vite còn cảnh báo chunk lớn hơn 500 kB. Kết quả gắn với working tree web gồm các thay đổi UI/test setup có sẵn của người dùng; không xác nhận một checkout sạch chỉ có commit hợp đồng. Lượt Windows Node 26 treo đã dừng, không dùng làm bằng chứng.
- Chỉ tạo commit cục bộ cho phần backend và 5 tệp hợp đồng/công cụ/tài liệu web của nhiệm vụ; không đưa thay đổi LandingPage/style/test setup hay `.serena/` vào commit. Không push/PR, không có CI, không nghiệm thu production. Giữ task ở Implemented cho tới khi có CI và duyệt trên SHA thực tế.


## G3-01 — JWT fail-closed (28/11/2026)

- Giữ `Refactor-2`, mốc bắt đầu `afc0e965bae73111c72d9946336078e8923742b5`; web xác minh chỉ đọc ở `eef7b8cbe037fa127cb5d613654a3fd08d723fe8`. Hai tài liệu có sẵn đã được kiểm tra và lưu riêng tại `88f075d`; không đụng `.serena/` hay thay đổi UI/test setup của web.
- SDK theo `global.json` (Linux Docker), EF CLI đối chiếu `Microsoft.EntityFrameworkCore.Design` trong API, Python 3.12 theo CI (runtime Python 3.12 local). Không cài tool global, không đổi chính sách Windows. Container phiên `sd-hardening-g3-sdk` dùng Docker context local `desktop-linux`, PostgreSQL Testcontainers dùng một lần. Bản sao nguồn loại `.env*`, `.venv`, `.git`, `.serena`, output build và cache.
- Baseline mới: locked restore, Release build (0 warning/error), 728/728 ngoài Integration. Không dùng số 918 cũ làm bằng chứng. Artifact local: `.cache/hardening-linux/g3-restore.log`, `g3-baseline-build.log`, `g3-baseline.log` và thư mục TRX `g3-baseline`.
- Thay đổi: `IdentityModule` dùng persistence bắt buộc, từ chối user/stamp rỗng hoặc không khớp, giữ signature/issuer/audience/lifetime; không công bố chi tiết challenge. Lỗi DI hoặc database đi qua middleware production với 500 ProblemDetails chung, không biến thành 401 hay auth thành công. Không tách framework/validator mới và không thêm grace period. Register/Login/Refresh đã cấp stamp; không có bằng chứng token production cần cơ chế chuyển đổi.
- RED (`g3-jwt-red.log`): 17 đạt, 2 lỗi đúng nguyên nhân: thiếu persistence trả 200; user đã xóa + token thiếu stamp đi qua auth tới `/me` trả 404. Bản vá đóng cả hai. Fixture idempotency nay tạo user/stamp thật và dùng validation production, giữ assertion 201/409/503/replay/rollback; thêm từ chối thiếu/sai stamp và user đã xóa. Nhóm endpoint này chuyển từ Unit sang Integration nên số ngoài Integration giảm 8, không mất coverage.
- GREEN (`g3-jwt-green.log`): nhóm JWT/Identity/idempotency 82/82; ArchitectureTests 57/57 (`g3-jwt-architecture.log`). Có signature/issuer/audience/expiry sai, thiếu subject/stamp, stamp sai/rỗng, user đã xóa, thiếu dịch vụ, lỗi dependency inject, EF/Npgsql kết nối không khả dụng, Register/Login/Refresh và đổi/đặt lại mật khẩu/role/revoke trên hai host dùng chung PostgreSQL. Header 401 chỉ `Bearer`; lỗi DB không có challenge 401 hay chi tiết kết nối.
- Suite trước bổ sung hai characterization cuối: 722/722 ngoài Integration (Domain 231, Application 365, Architecture 57, offline HTTP 69), 218/218 Integration; Failed/Skipped = 0. `g3-jwt-unit.log`, `g3-jwt-integration.log` và các thư mục TRX cùng tên; các project không có Category=Integration có discovery 0 được ghi riêng, tổng bước vẫn thực chạy 218. Hai ca cuối được kiểm qua nhóm 82 test; toàn suite cuối phiên sẽ gắn với nguồn cuối G3-05.
- Build image API local đạt (`g3-jwt-image-api.log`), không publish. Worker/Migrator và image nguồn cuối kiểm ở bước bàn giao G3-05. Không thay migration, schema, dependency, DTO hay OpenAPI baseline. Web cần biết token không hợp lệ/stamp cũ nhận 401; không cần sinh lại contract. Đã sửa mô tả cũ về logout/role cho khớp mã thu hồi stamp có sẵn, không tự đổi chính sách.
- Review độc lập chỉ đọc (`review_g3`): không còn finding P1/P2; nhận xét ban đầu về logout được rút sau khi đọc `RevokeFamilyAsync` ở mã gốc. Hai gap nhỏ (header challenge và lỗi kết nối thật) đã bổ sung kiểm chứng. Review agent không thay phê duyệt PR của thành viên khác.
- Giữ **Implemented**, còn CI và duyệt PR đúng SHA; không push/PR, không gọi local pass là Verified hoặc production-ready.


## G3-05 và gate cuối phiên (28/11/2026)

- Commit G3-01: `3ba4ac5`; G3-05 là commit riêng chứa mục này (xác định SHA đầy đủ bằng `git log` và manifest local `.cache/hardening-linux/g3-evidence.json` sau commit). Nhánh giữ `Refactor-2`. Không viết lại G1/G2 đang chờ gate và không triển khai các task khác.
- Phạm vi: `AuthRateLimiting` và các caller Register/Login/ForgotPassword/ResetPassword/Refresh đã đối chiếu; không đổi route/DTO/cấu hình mặc định. Thay ConcurrentDictionary/counter riêng bằng Dictionary và một khóa bao phủ đọc clock, admission, expiry và increment. Counter hết hạn mới được loại; không có `Clear` do áp lực capacity và không eviction counter còn hiệu lực. `Clear` chỉ còn khi Dispose. Giữ tối đa 4096 partition email trên instance, key chứa tối đa 320 ký tự email sau trim + prefix thao tác. Dùng `IClock` sẵn có, không thêm dependency.
- Đầy capacity: email/thao tác mới nhận 429 với Retry-After tới expiry gần nhất, ngân sách cũ tiếp tục được giữ và dùng phần còn lại. Đây là đánh đổi khả dụng, có thể chặn người mới trong cửa sổ đầy. Khóa quá dài nhận cùng 429 trước khi lưu. Cả lớp IP/email trả cùng JSON `{error}` và Retry-After; không phụ thuộc tài khoản có tồn tại. Không coi limiter RAM là ngân sách chung toàn cụm, không thêm Redis/shared state; hai instance có ngân sách độc lập và restart mất counter.
- RED `g3-rate-red.log`: 10 Failed, 6 Passed, 0 Skipped. Đã tái hiện Clear khi đầy, 64 admissions khi chỉ còn một chỗ, bỏ qua clock expiry, khóa dài được lưu, full capacity/khóa dài qua HTTP và body 429 IP lệch email. Kiểm trusted/untrusted forwarding đã giữ đúng địa chỉ; hai ca proxy RED ở assertion nội dung lỗi, không phải thay policy proxy.
- GREEN `g3-rate-green.log`: 16/16, ArchitectureTests 57/57; build 0 warning/error. Bao phủ đủ full capacity, giữ mọi ngân sách cũ khi xuất hiện email mới, concurrent admission, concurrent expiry/renewal/eviction (5 vòng với gate đồng bộ), expiry/retry gần nhất, chuẩn hóa email, ngân sách theo từng thao tác, khóa 100.000 ký tự, ranh giới 320, disabled/empty input, hai limiter độc lập, HTTP 429/Retry-After và proxy spoofing. Fake clock thay sleep; gate kiểm thử được giải phóng cả khi readiness không đạt để tránh task treo.
- Review độc lập chỉ đọc (`review_g3`) cho hai nhóm thay đổi: không còn finding P1/P2. Đã bổ sung canonical contract về đầy capacity, giới hạn 320, ngân sách per-instance và policy proxy. Chưa benchmark tải thực; không suy ra throughput hay giới hạn toàn cụm từ các test này.

### Bằng chứng nguồn cuối G3-01/G3-05

Lệnh chạy trong SDK Linux riêng từ bản sao nguồn đã đồng bộ xong; không chép `.env*`, `.venv` hay bí mật. Các helper `g3-final-checks.sh` và `g3-image-context.py` chỉ là artifact local, không đưa vào Git.

| Lệnh/kiểm tra | Passed | Failed | Skipped | Artifact local dưới `.cache/hardening-linux/` |
| --- | --- | --- | --- | --- |
| `dotnet restore src/backend/ServiceDesk.sln --locked-mode` | Exit 0 | 0 | 0 | `g3-final-restore.log` |
| `dotnet build src/backend/ServiceDesk.sln -c Release --no-restore` | Exit 0, 0 warning/error | 0 | 0 | `g3-final-build.log` |
| `dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"` | 734 (Domain 231, Application 365, Architecture 57, offline HTTP 81) | 0 | 0 | `g3-final-unit.log`, TRX `g3-final-unit/` |
| `dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"` | 220 PostgreSQL/storage integration, gồm Identity/JWT/idempotency | 0 | 0 | `g3-final-integration.log`, TRX `g3-final-integration/` |
| Build image theo Dockerfile hiện hành: Api, Worker, Migrator | 3 image local | 0 | 0 | `g3-final-image-api.log`, `g3-final-image-worker.log`, `g3-final-image-migrator.log` |
| `python scripts/validate_project_docs.py` | 102 tài liệu, 639 liên kết | 0 | 0 | `g3-final-docs.log` (Python 3.12.14 theo dòng 3.12 của CI) |
| `python -m unittest discover -s scripts/tests -p "test_*.py"` | 28 | 0 | 0 | `g3-final-doc-tools.log` |

- Tổng .NET nguồn cuối: **954 Passed, 0 Failed, 0 Skipped**. Bước Integration có ba project không chứa test thuộc category này (discovery 0); project IntegrationTests thực chạy 220 test, không lấy exit code của project rỗng làm bằng chứng.
- OpenAPI baseline, hợp đồng AI, lockfile, schema và migration không đổi. Không cần AI contract validator riêng vì không chạm hợp đồng/fixture/mapper AI; `ApiContractTests` nằm trong suite ngoài Integration đã đạt. Không chạy lệnh EF/Migrator vào database ngoài Testcontainers; migrate và seed trong fixture dùng một lần giữ nguyên. Không khởi động backend ngoài các test host, không gửi email thật và không kết nối AI thật. Storage test cũ được chạy nguyên trạng trong suite, không thêm mã/cấu hình/test MinIO mới.
- Ba image chỉ build, không publish/chạy Migrator. Context image chỉ lấy 351 tệp được theo dõi, gồm `src/backend` và hai tệp ghim build, không lấy cache/.env/venv. Mã đã kiểm thử và image cùng nội dung nguồn cuối; manifest local gắn SHA/file hash sau commit.
- G3-01/G3-05 giữ **Implemented**. Chưa push, chưa PR, chưa CI trên SHA mới, chưa duyệt PR của thành viên khác; không gọi đây là Verified hoặc production-ready. Web chỉ cần biết 401 với token thiếu/stamp cũ và body 429 thống nhất; không có snapshot OpenAPI cần đồng bộ. G2-04, G2-06/G7-02 và dependency web vẫn là các đợt riêng.

- Cleanup: đã đối chiếu SHA-256 hai tệp production giữa workspace và bản sao Linux đã kiểm thử, khớp hoàn toàn. Không còn container Testcontainers đang chạy. Đã kiểm nhãn sở hữu và dừng riêng `sd-hardening-g3-sdk`; giữ nguyên container/volume cũ, dịch vụ và dữ liệu của người dùng. Không sửa `.serena/`.


## Đợt session và OTP — kế hoạch thực thi (28/11/2026)

- Người dùng giao toàn bộ phạm vi G3-02 → G3-04 → G3-03, commit riêng. Mốc bắt đầu `Refactor-2@e40c0a14d68e1490867afcaa2a7796b8a9df5a21`, tracked/index sạch, giữ `.serena/`. Không mở rộng AI/UI, nâng dependency hay triển khai.
- G3-02: giữ khóa dòng người dùng và rotation có điều kiện; kiểm refresh/revoke/reset, replay, TTL, host mới và hai host cùng PostgreSQL. Bổ sung `IdentitySessionPostgresTests`: host tái tạo + replay/family độc lập, concurrent refresh/logout qua hai host, expired refresh không ảnh hưởng thiết bị khác. Nhóm kiểm định 42/42, Failed/Skipped 0 (`.cache/hardening-linux/otp-session.log`, TRX `otp-session/`); build Release đạt. Host tái tạo là bằng chứng host/context mới, chưa gọi là restart process thật.
- Quyết định TTL của chủ dự án trong phiên: access **60 phút**, refresh **7 ngày**. Infra đang override 1440 phút tại hai Compose triển khai; sửa default và mẫu env theo quyết định, không thao tác môi trường thật. Infra baseline `docs/dong-bo-tai-lieu-2026-11-21@fae1d98b7732b71b70db38b477158d713f57e046`, working tree sạch. Giữ nguyên release lock/digest.
- G3-04: thêm cổng bảo vệ OTP và cấu hình khóa OTP riêng, HMAC gắn email/mục đích, giữ CSPRNG, constant-time, TTL/attempts/issuance và transaction hiện có. Không thay SHA-256 của refresh token entropy cao. OTP digest cũ bị từ chối, người dùng yêu cầu mã mới; không thêm fallback digest không khóa. Tests/fixture dựng khóa thử hợp lệ, không giảm assertion nghiệp vụ. Kiểm thiếu/sai khóa, khác email/mục đích/khóa, rollback và thu hồi phiên trên PostgreSQL.
- G3-03: lưu yêu cầu trước khi xác nhận tiếp nhận; PostgreSQL claim/lease, outbox payload mã hóa cùng transaction phát OTP, gửi SMTP ngoài transaction, bounded retry và expiry. Giữ cùng mã khi retry, không lưu OTP plaintext. Worker gửi chạy trong API như thiết kế hiện hành và phối hợp qua PostgreSQL; không thêm Redis hay scheduler framework. Kiểm không cấu hình SMTP, timeout/permanent failure, quá tải, retry sau restart/lease và không gửi mã cũ/hết hạn.
- Gate mỗi task: RED phù hợp → sửa nhỏ → target/Architecture/PostgreSQL → review độc lập, tài liệu canonical/state, commit cục bộ; cuối đợt locked restore/build/toàn suite/ba image và migration/model check nếu có schema mới. Không push/PR/publish, không gửi email thật, không dùng DB ngoài fixture dùng một lần. G3-04/G3-03 đang triển khai; CI/người duyệt PR còn là gate riêng.


### G3-04 — bảo vệ OTP (28/11/2026)

- G3-02 đã commit `86f2e06`. G3-04 dùng `IOtpCodeProtector`/`OtpCodeProtector`, HMAC-SHA-256 trên tuple có namespace/version, mục đích, email và mã được serialize không nhập nhằng. Khóa `Otp:Key` riêng 32 byte Base64 bắt buộc ở startup, không dùng lại JWT; cột hiện tại đủ chứa digest `v1:` + 64 hex nên không đổi schema/migration. Không đổi hashing refresh.
- Giữ CSPRNG, constant-time, TTL/attempts/issuance và transaction hiện hành; token/digest OTP cũ không được fallback. Người dùng yêu cầu lại mã khi rollout hoặc xoay khóa. Đã bổ sung `.env.example`, README, contract và fixtures với khóa thử riêng; không có khóa thật trong Git/chat/log.
- RED `otp-hash-red.log`: 1 test thất bại vì issuer cũ không lưu digest có phiên bản/bảo vệ. GREEN: Identity Application 58/58; nhóm HTTP/Identity/JWT/contract/startup/OTP 126/126; Architecture 57/57, Failed/Skipped 0. Release build 0 warning/error. Artifact `.cache/hardening-linux/otp-hash-{build,application,integration,architecture}.log` và thư mục TRX cùng tên. Có lỗi dựng test ban đầu (thiếu import, EF1002 khi DDL inline), đã sửa bằng Npgsql DDL chỉ cho fixture dùng một lần, không hạ cảnh báo.
- Reviewer độc lập không thấy bug HMAC/config; chỉ ra ba gap bắt buộc, đã bổ sung: hai HTTP reset qua hai host cùng OTP chỉ một thắng; trigger PostgreSQL từ chối đánh dấu OTP sau khi ghi password chứng minh rollback password/stamp/OTP/session rồi retry thành công; capture logger của HTTP chứng minh không có OTP/email/khóa/password. Ba test bổ sung GREEN trong nhóm 126.
- Infra đã đồng bộ default access 60 phút ở hai Compose và cả hai mẫu env triển khai, giữ refresh 7 ngày trong backend; truyền `OTP_KEY` vào hai API ở Compose triển khai và CI/local. Không đổi env thật hoặc release lock. Ba lệnh Compose `config --quiet` theo AGENTS infra và 22 unit test infra đạt (`otp-infra-unit.log`), không chạy stack/smoke phát hành. Commit infra riêng ở bước cấu hình của đợt này.
- G3-04 giữ Implemented, chờ CI và duyệt đúng SHA; G3-03 tiếp theo. Chưa gọi local pass là Verified. Gate toàn suite/image nguồn cuối sẽ chạy sau thay đổi durable delivery.


## Bàn giao tạm dừng theo yêu cầu người dùng — 28/11/2026

Đây là mốc lịch sử lúc tạm dừng. Trạng thái sau khi người dùng yêu cầu tiếp tục được ghi ở mục kế tiếp, không dùng các dòng CHƯA bên dưới làm trạng thái hiện tại.

- Người dùng yêu cầu tạm dừng để gập máy di chuyển. Đã dừng riêng container `sd-hardening-otp-sdk` (nhãn `supportflow.hardening=otp-20261128`), không còn Testcontainers chạy; không xóa volume hoặc dịch vụ của người dùng. Không push/PR/publish/deploy.
- Backend: nhánh `Refactor-2`, HEAD `9f6f3bb` (G3-04); G3-02 `86f2e06`. Hai task Implemented, chưa Verified/CI/duyệt PR. Các thay đổi G3-03 đang ở working tree và tệp mới chưa tracked, CHƯA commit; không restore/reset hoặc bỏ các tệp này. Giữ `.serena/` nguyên trạng.
- Quyết định của người dùng: access token 60 phút, refresh 7 ngày. Infra checkout `D:/User/HUCE/HK1_2627/2. ThapCam/Code/supportflow-ai-infra`, nhánh `docs/dong-bo-tai-lieu-2026-11-21`, HEAD ban đầu `fae1d98b7732b71b70db38b477158d713f57e046`. Có 6 tệp cấu hình chưa commit của nhiệm vụ: `.env.local.example`, `compose.yaml`, `deploy/.env.example`, `deploy/example.env`, `deploy/compose.release.yml`, `deploy/compose.system.yml`. Đã đổi TTL 1440→60 và truyền OTP_KEY; 3 Compose config + 22 unit infra đạt. Còn phải tạo OTP_KEY hợp lệ cho smoke CI dùng một lần (mẫu local hiện là placeholder), ghi docs/commit riêng; không đổi sources.lock/digest hoặc env thật.
- G3-04 trước khi sang durable: Application Identity 58/58, HTTP/Identity/JWT/OTP 126/126, Architecture 57/57, build 0 warning/error. Reviewer chỉ ra concurrent reset/rollback/redaction; đã bổ sung cả 3 và đạt. Log/TRX `otp-hash-*` dưới `.cache/hardening-linux`.
- G3-03 đã có `PasswordResetJob`, `IPasswordResetJobStore`, `PostgresPasswordResetJobStore`, mapping; queue API chuyển EnqueueAsync, issuer ProcessAsync chuẩn bị OTP+payload cùng transaction rồi gửi ngoài transaction, worker API claim/lease và retry giới hạn; AES-GCM dùng khóa con HKDF và AAD email/otpId. SMTP lỗi được ném lại, NoOp không giả thành công.
- RED gốc: 3 ca thất bại đúng nguyên nhân (DB lỗi vẫn 200, SMTP bị nuốt, NoOp giả thành công), TRX `otp-durable-red/_58549de4a3ee_2026-11-28_11_19_10_net10.0.trx`. GREEN khởi đầu: 3/3 ở TRX cùng thư mục ngày 11:24:52. File log `otp-durable-red.log` đã bị lượt GREEN ghi đè; dùng đúng TRX timestamp để phân biệt, không gọi log hiện tại là bằng chứng RED.
- Migration mới `20261128112618_AddDurablePasswordResetJobs` và Designer đã sinh thành công bằng EF CLI cục bộ, copy về cùng `ApplicationDbContextModelSnapshot`. Đã xem Up chỉ CREATE bảng/index mới; Down chỉ DROP bảng job mới, không chạy Down. Snapshot chỉ thêm 65 dòng của PasswordResetJob. CHƯA build lại sau migration, CHƯA chạy model drift/upgrade/transaction/job tests, CHƯA áp dụng DB ngoài Testcontainers.
- Việc tiếp theo: (1) đọc diff và các class job hiện tại; (2) thêm PostgreSQL tests claim/capacity đồng thời, crash trước/sau chuẩn bị/sau SMTP, retry giữ cùng mã, expiry/invalidated, permanent/transient failure, payload tamper và rollback OTP+outbox; chuyển coverage full RAM queue cũ sang PG (test Channel cũ đã gỡ do class không còn); (3) kiểm process restart thật, tách rõ với host mới của G3-02; (4) rà lease/ack và tránh gửi mã hết hạn/cũ; (5) xét HTTP503 khi full queue, cập nhật metadata/OpenAPI/caller có chủ đích nếu đổi contract; (6) docs rollout/key/retention/at-least-once, review độc lập; (7) toàn suite/locked restore/Release/build3image/model drift/migration và commit riêng G3-03 + infra.
- Test offline ApiContract/AuthRateLimiting đã tắt riêng worker OTP để không tự truy cập DB appsettings ngoài fixture; test PG giữ worker thật. Helper source sync loại `.env*`, `.venv`, `.git`, `.serena`, cache/bin/obj. Không chép `.env` thật vào nguồn build.
- Resume môi trường: kiểm Docker context local và nhãn rồi start `sd-hardening-otp-sdk` nếu còn; mount `/input` readonly trỏ workspace, `/evidence` trỏ `.cache/hardening-linux`, `/work` là volume `sd-hardening-20261128-work`. Script `.cache/hardening-linux/sync.sh` đã kiểm tra; chờ sync/build trước test. EF CLI `/work/.cache/dotnet-tools/dotnet-ef` còn tồn tại. Không chạy lại script `otp-migration.sh` (đã sinh migration), không tái sử dụng tên log/TRX cũ cho vòng mới. Các helper `.sh` cần LF.
- Bản sao dự phòng nguồn chưa commit: `.cache/hardening-linux/otp-handoff-20261128.zip`, manifest/diff cùng prefix. Không chứa `.env` thật, `.venv`, `.serena` hoặc bí mật. Không dựa vào cache session để phục hồi; đọc state, Git và diff rồi tiếp tục.

## Tiếp tục và hoàn tất G3-03 sau tạm dừng — 28/11/2026

- Đối chiếu trước khi sửa: giữ `Refactor-2@9f6f3bb7063a794193faf83dd2e750030904c1b2`, index rỗng; 26 tệp trong bàn giao khớp SHA-256 bản ZIP, không ghi đè thay đổi của người dùng. Kiểm nhãn/container và Docker context local trước khi dùng lại SDK Linux. Bản sao kiểm thử loại `.env*`, `.venv`, `.git`, `.serena`, cache và output build; không sửa chính sách Windows hay cài tool global.
- Thay RAM queue bằng yêu cầu/outbox PostgreSQL: tiếp nhận chỉ thành công sau commit; claim có lease và fencing, admission tối đa 1000 job còn hiệu lực dùng khóa giao dịch chung giữa các API. Phát OTP và lưu ciphertext trong cùng transaction; SMTP ở ngoài transaction. Retry giữ cùng mã, tối đa 5 claim; lỗi vĩnh viễn/thiếu SMTP không giả thành công. Chi tiết expiry, khóa con AES-GCM, retention và giới hạn at-least-once ở [hợp đồng hiện hành](../architecture/current-backend-contract.md).
- Migration mới `20261128112618_AddDurablePasswordResetJobs` chỉ thêm bảng/index job; snapshot chỉ thêm thực thể này. PostgreSQL upgrade test từ schema trước migration bảo toàn Identity/OTP/refresh token. Không sửa migration đã phát hành, không chạy Down hay dùng database ngoài Testcontainers dùng một lần. Model drift kiểm bằng EF CLI cục bộ: không có thay đổi.
- Giữ API 200 là thông báo tiếp nhận chung cho mọi email. Hồi quy HTTP quan sát RED: queue đầy từng trả 500, email rỗng từng trả 200; nay lần lượt là 503 và 400. DB lỗi vẫn là 500 chung và không ACK yêu cầu. OpenAPI chỉ bổ sung 400/503 cho forgot-password, không đổi DTO/schema. Snapshot web đồng bộ có chủ đích; không đổi UI/test setup có sẵn.
- RED gốc 3 ca được giữ ở TRX nêu trong bàn giao. RED HTTP: `otp-http-red.log` (2 lỗi). Reviewer độc lập chỉ đọc `review_otp_final` phát hiện hai P2: retry dùng expiry cũ của claim khi OTP đã được chuẩn bị muộn; resend OTP đã hết 5 lượt nhập sai. Đã sửa bằng đọc expiry persisted khi ghi failure và kiểm `FailedAttempts` trước send; thêm guard expiry ngay trước SMTP, timeout không vượt TTL còn lại. `otp-review-red/` lượt 12:02:04 có 3 lỗi đúng nguyên nhân (backlog, expired trước send, exhausted), 2 ca khác đạt; sau sửa `otp-review-green.log` **28/28**, không còn finding đã xác nhận chưa xử lý trong phạm vi. Agent review không thay người duyệt PR.
- Kiểm PostgreSQL gồm claim/capacity đồng thời, stale owner không ACK/fail job mới, crash sau SMTP nhận thư rồi process mới gửi lại cùng mã, consumed/expired/exhausted OTP không gửi, transient/permanent/timeout/missing SMTP, rollback OTP/outbox bằng lỗi trigger thật. `PasswordResetProcessTests` khởi chạy và kill đúng process API con, fake SMTP loopback, khởi động API mới và kiểm reset/refresh; đây là restart process thật, bổ sung cho host mới của G3-02. Không gửi email hay gọi AI thật.

| Lệnh/gate trên nguồn cuối | Passed | Failed | Skipped | Artifact local `.cache/hardening-linux/` |
| --- | --- | --- | --- | --- |
| `dotnet restore src/backend/ServiceDesk.sln --locked-mode` | Exit 0 | 0 | 0 | `otp-accepted-restore.log` |
| `dotnet build src/backend/ServiceDesk.sln -c Release --no-restore` | Exit 0, 0 warning/error | 0 | 0 | `otp-accepted-build.log` |
| `dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"` | 746 (Domain 231, Application 370, Architecture 57, offline HTTP 88) | 0 | 0 | `otp-accepted-unit.log`, TRX `otp-accepted-unit/` |
| `dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"` | 248 PostgreSQL/storage integration | 0 | 0 | `otp-accepted-integration.log`, TRX `otp-accepted-integration/` |
| EF `migrations has-pending-model-changes` đúng Platform/API/context, Release, `--no-build` | Exit 0, không lệch model | 0 | 0 | `otp-accepted-model.log` |
| Build image theo Dockerfile Api, Worker, Migrator | 3 image local | 0 | 0 | `otp-image-{api,worker,migrator}.log` |
| `python scripts/validate_project_docs.py` | 102 tài liệu, liên kết hợp lệ | 0 | 0 | `otp-docs.log` |
| `python -m unittest discover -s scripts/tests -p "test_*.py"` | 28 | 0 | 0 | `otp-doc-tools.log` |

- Tổng .NET nguồn cuối **994 Passed, 0 Failed, 0 Skipped**; ba project không có Category=Integration discovery 0 được phân biệt với project IntegrationTests thực chạy 248. Không cộng lượt cũ vào tổng. Các lượt 991 trước review không dùng xác nhận mã cuối. Đã đối chiếu SHA-256 **489 tệp nguồn/test** với bản Linux đã kiểm thử, **360 tệp nguồn** của ba image khớp workspace (`otp-tested-source.sha256`, `otp-source-compare.log`). Lượt build image đầu bị helper PowerShell dừng nhầm vì progress stderr của Docker; sửa helper, ba lệnh build sau exit 0, không sửa Dockerfile để né lỗi.
- Infra giữ nhánh `docs/dong-bo-tai-lieu-2026-11-21`, commit cục bộ `fd4847bd21eefe1a8b65ab1196aa84005fde396c`: access 60 phút, refresh 7 ngày; OTP_KEY riêng dùng chung hai API, CI tạo khóa test ngẫu nhiên không in giá trị; docs rollout/at-least-once. Ba Compose `config --quiet` và **22 unit test** đạt (`otp-resume-infra-unit.log`). Không đổi env thật, sources lock/digest hay triển khai.
- Web: test adapter mới kiểm 503 bị từ chối và chỉ gửi một request, **12/12** test lớp auth đạt (`otp-web-auth-green.log`). Lượt toàn suite đầu 481 đạt/1 lỗi do test mới kỳ vọng raw server message, trái chính sách thông báo lỗi chung hiện có; đã sửa assertion, không sửa lớp HTTP/UI để hiển thị lỗi thô. Node 22: `npm ci`, `contract:generate`, `contract:check -- --backend-file ...`, typecheck, lint, **482/482 test trong 65 tệp**, build đạt (`otp-web-final-{contract,typecheck,lint,tests,build}.log`). Kiểu TypeScript sinh có SHA-256 giống hệt file hiện hành nên không sửa file sinh. Lượt cuối chạy trên working tree có cả thay đổi UI/test setup của người dùng được bảo toàn, không xác nhận checkout sạch chỉ chứa commit hợp đồng. Build còn cảnh báo chunk trên 500 kB; `npm ci` giữ lockfile, báo 16 mục dependency đã biết (8 moderate, 3 high, 5 critical), xử lý riêng tại G6-06; không chạy audit fix.
- G3-02/G3-04/G3-03 giữ **Implemented**, chưa Verified. Còn CI, duyệt PR đúng SHA và smoke toàn hệ thống với bộ image/source đã duyệt; không có ủy quyền push/PR/publish/deploy. Không chạy AI contract validator riêng vì không đổi hợp đồng/fixture/mapper AI. Không chạy browser E2E; không thay luồng UI.

### Commit và cleanup cuối đợt

| Kho/nhánh giữ nguyên | Commit cục bộ | Nội dung |
| --- | --- | --- |
| Backend `Refactor-2` | `86f2e06`, `9f6f3bb`, `7bafb643652c9e8455b3b35a6e1a5ea113b09893` | G3-02, G3-04 và G3-03 riêng; commit docs bàn giao sau G3-03 chỉ sửa tệp state này |
| Infra `docs/dong-bo-tai-lieu-2026-11-21` | `fd4847bd21eefe1a8b65ab1196aa84005fde396c` | 8 tệp cấu hình/CI/docs, access 60 phút và khóa OTP |
| Web `feature/phan-tich-nghiep-vu-va-man-hinh` | `e2ae873dc705df545621621b43c6eee0752c42ac` | Chỉ 3 tệp snapshot/nguồn/test auth; metadata ghim backend `7bafb64` và checksum tương ứng |

- Đã kiểm nhãn `supportflow.hardening=otp-20261128`, dừng riêng `sd-hardening-otp-sdk` và `sd-hardening-otp-web`; không còn Testcontainers hoặc container mang nhãn phiên đang chạy. Giữ volume/cache/image local, không dừng dịch vụ hay đụng dữ liệu người dùng. Các process API/SMTP con trong test đã được fixture dọn.
- Backend tracked/index sạch sau commit, chỉ còn `.serena/` có sẵn không theo dõi; infra sạch. Web còn nguyên các thay đổi riêng `.gitignore`, LandingPage, styles, test setup, Vitest và tài liệu MUJI/audit, không stage vào commit nhiệm vụ. Không push, tạo PR, publish image hay triển khai.
- Artifact tổng hợp sau commit docs: `.cache/hardening-linux/otp-evidence.json`, cùng log/TRX/hash đã nêu; không đưa cache vào Git. SHA docs cuối không đổi mã đã kiểm thử hoặc baseline. Phiên sau đọc mục này và Git trước khi sửa; ưu tiên gate CI/duyệt, hoặc nhận phạm vi G3-06 riêng.

## Đợt G3-06 và G4 — đang thực hiện

- Người dùng giao tiếp tục G3-06 và G4 nếu có thể. Mốc đầu đợt `Refactor-2@841dc03f8407bcd090eff55079f2e94a7fda540b`, index/tracked sạch, giữ `.serena/`. Không đổi nhánh, không push/PR/publish/deploy hay truy cập database dùng chung.
- Thứ tự: G3-06 → G4-01/G4-02/G4-03 → G4-04/G4-05 → G4-06/G4-07/G4-08 → G4-09. Đối chiếu mã và kiểm thử hiện có trước khi sửa; mỗi bug có RED đúng nguyên nhân rồi GREEN. Mỗi phần hoàn tất có commit riêng; review độc lập toàn đợt và kiểm chứng phần sửa finding trước bàn giao.
- G3-06 đọc endpoint/use case/gateway: registration chỉ Requester, profile chỉ trường được phép, user directory và department theo role, role management và last-admin; bổ sung HTTP/PostgreSQL cho mass assignment, invalid department, concurrent admin và response không lộ trường nhạy cảm. Giữ chính sách email confirmation và khả năng account administration hiện hành, không tự thêm tính năng.
- Pre-flight: G4-01/G4-02/G4-03 cùng dùng aggregate, command guard, revision/audit và policy team; kiểm ordering/transaction ở adapter thay vì sao chép policy. G4-04/G4-05 chung SLA clock/Worker; G4-06/G4-07/G4-08 chung upload grant/policy/storage. Giữ DEC-09, DEC-06 và DEC-07 hiện hành; khi cần quyết định mới, ghi blocker và tiếp tục phần độc lập. R2 thật chỉ kiểm nếu có môi trường thử nghiệm và đích được xác nhận; Testcontainers không thay bằng chứng R2.
- Môi trường: SDK `global.json`, EF CLI khớp Design của API, Python theo CI; reuse SDK Linux chỉ sau kiểm nhãn/mount/local npipe và không có container khác dùng volume. Sync loại `.env*`, `.venv`, `.git`, `.serena`, cache/bin/obj; chờ copy/build xong trước `--no-build`. Baseline/gate mới dùng prefix `g34-*` tại `.cache/hardening-linux/`, không ghi đè artifact cũ.

### G3-06 — kiểm định API Identity

- Baseline mới: locked restore và Release build 0 warning/error; 746/746 ngoài Integration, 248/248 Integration (`g34-baseline-*`). Không dùng kết quả 994 của phiên trước thay baseline này.
- RED trước sửa: 25 ca actor thiếu/chưa xác thực/ID sai/rỗng đều đi qua use case; 8/12 HTTP/PostgreSQL lỗi đúng nguyên nhân (4 invalid department trả 500, ghi claim lỗi để lại điện thoại đã đổi, IdentityResult lỗi vẫn trả thành công, trùng unique không phân loại, register trả tên thô khác tên đã lưu). 4 ca role matrix đã đạt trước sửa. Artifact `g306-actor-red/`, `g306-http-red/`.
- Bản vá dùng `AuthenticatedActor` hiện có cho 5 use case, không tạo framework quyền mới; fixture dương dựng actor hợp lệ, giữ assertion nghiệp vụ. Profile dùng transaction/khóa dòng và kiểm kết quả Identity; rollback đã chứng minh bằng lỗi trigger PostgreSQL thật. Domain Department giữ giới hạn schema, validate trước lookup/ghi, adapter ánh xạ SQLSTATE unique thành 409 bằng API chuẩn .NET, không thêm dependency.
- HTTP kiểm Requester/Agent/Admin/anonymous trên 8 endpoint được bảo vệ; mass assignment trong đăng ký/profile không đổi ID/email/roles/stamp, response không lộ hash/stamp. Hồi quy last-admin/concurrent role/session/lockout hiện có chạy nguyên trạng. Không hỗ trợ disabled-account/department assignment ở Identity nên không tự thêm API; email confirmation vẫn là chính sách auto-confirm hiện hành, không đồng nghĩa đã chứng minh sở hữu email. G3-06 chỉ kiểm định, không tự triển khai verification/invitation.
- GREEN: Application Identity **83/83** (`g306-application-green`), Architecture **57/57** (`g306-architecture`), HTTP/Identity/JWT/idempotency **104/104** (`g306-http-green-v2`), Failed/Skipped 0. Lượt HTTP đầu bị shell làm mất quote của filter có `|` (exit 127), không dùng làm bằng chứng; chuyển sang script LF, không đổi filter để bỏ test.
- Metadata OpenAPI cũ sai/thiếu status trên 7 thao tác: quan sát RED **7/7**, sửa mô tả theo status runtime, GREEN **7/7**; chỉ sau đó xuất baseline có chủ đích, tắt export và chạy lại ApiContractTests **10/10** (`g306-contract-{red,green,check}`), diff chỉ status không đổi schema/DTO. Web phải đồng bộ snapshot/checksum/nguồn, không đổi UI của người dùng; kết quả/commit liên kho sẽ ghi ở gate cuối đợt.
- G3-06 giữ Implemented, còn review độc lập toàn đợt, suite/image/model check cuối, CI và người duyệt PR. Không gọi local pass là Verified. Không sửa G1/G2 hoặc tự làm G6-03 toàn bộ error contract.
