# Trạng thái thực thi kế hoạch củng cố production

Tệp trạng thái duy nhất của [kế hoạch củng cố production](production-hardening-plan.md) (thay cho `.audit/REFACTOR_LOOP_STATE.md` và `docs/reviews/production-readiness-register.md` để không có hai nơi theo dõi cùng nội dung). Đầu mỗi phiên: đọc tệp này, đối chiếu `git log` và working tree, rồi tiếp tục task đầu tiên chưa `Verified`.

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
| G3-01 | Pending — lỗi đã xác minh | `IdentityModule.cs:74-76` bỏ qua kiểm security stamp khi không resolve được `DbContext`; `:93-100` chấp nhận token không có stamp của user không tồn tại (fail-open) |
| G3-02 | Pending | Chưa rà |
| G3-03 | Pending — lỗi đã xác minh | `PasswordResetQueue.cs` dùng `Channel<string>` trong RAM, mất khi tiến trình dừng |
| G3-04, G3-06 | Pending | Chưa rà |
| G3-05 | Pending — lỗi đã xác minh | `AuthRateLimiting.cs:176` xóa toàn bộ bộ đếm khi đạt `MaxPartitions` (kẻ tấn công lấp đầy partition để reset giới hạn) |
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

G3-01 (JWT fail-closed), sau đó G3-05 (rate limit), với test trong SDK Linux đã hoạt động. G2-06/G7-02 là nhánh công việc UI riêng: adapter hiện vẫn dùng route dự kiến và quyết định từng suggestion, cần chuyển sang quyết định toàn analysis trước khi bật capability. Trước khi gắn Verified cho phần backend: CI và duyệt đúng SHA bàn giao; không tự push để lấy bằng chứng này.

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
