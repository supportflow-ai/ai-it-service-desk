# Hợp đồng backend đang triển khai

Trạng thái: mô tả hiện hành tại `dev@b3cc19a` sau PR #82 và các tính năng hoàn thiện quản trị đội nhóm, điều phối vé, báo cáo SLA, AI triage HITL, quản lý phòng ban. Bản đầu đối chiếu `dev@8036e2c` ngày 08/11/2026, sau PR #50–#55. Bổ sung 2026-11-16 (đối chiếu `dev@ef4390d`): phân công, hủy phân công, Impact/Urgency/Priority, nhóm hỗ trợ và các trạng thái chưa có lệnh chuyển. Bản chuẩn HTTP là [OpenAPI baseline](../../contracts/backend/openapi-baseline.json) (57 đường dẫn, 63 thao tác); tài liệu này mô tả thêm quyền, trạng thái và lỗi mà OpenAPI không nêu. Mốc nguồn có thể mới hơn nhánh local của người đọc; đối chiếu commit trước khi chạy. Cập nhật tài liệu không thay API, quyền hoặc dữ liệu và không xác nhận phiên bản VPS hay một bản phát hành. Quyết định kiến trúc: [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md). Chính sách cần chốt: [bảng quyết định](pending-business-decisions.md). Hồ sơ các đợt củng cố đầu tháng 11 là lịch sử: [giai đoạn 1](../runbooks/supportflow-hardening-phase-1.md), [giai đoạn 2–6](../runbooks/supportflow-hardening-phase-2-6.md).

## HTTP và danh tính

[Bảng mốc mã nguồn](source-baseline.md) ghi cách xác định mốc đối chiếu. Liên kết mã tương đối bên dưới mở checkout của người đọc; đối chiếu HEAD trước khi áp dụng. Cột "Chính sách" là chính sách HTTP của endpoint; Application kiểm tra thêm quyền theo phiếu (chủ sở hữu, người phụ trách, Admin).

| Nhóm | Thao tác | Đường dẫn | Chính sách | Ghi chú |
| --- | --- | --- | --- | --- |
| Xác thực | Đăng ký, đăng nhập, làm mới, quên và đặt lại mật khẩu | `POST /api/auth/register`, `/login`, `/refresh`, `/forgot-password`, `/reset-password` | Ẩn danh, có giới hạn tần suất | Chi tiết ở mục Identity bên dưới |
| Xác thực | Đăng xuất | `POST /api/auth/revoke` | Ẩn danh | Luôn 204 |
| Xác thực | Đổi mật khẩu, hồ sơ bản thân | `POST /api/auth/change-password`, `GET /api/auth/me` | Đã đăng nhập | JWT phải tham chiếu tài khoản còn tồn tại và security stamp hiện hành; token không hợp lệ trả 401 |
| Người dùng | Danh sách theo vai trò; gán vai trò | `GET /api/users`, `PUT /api/users/{id}/roles` | Agent hoặc Admin; Admin | |
| Phòng ban | Danh sách; tạo; cập nhật | `GET /api/departments`, `POST /api/departments`, `PUT /api/departments/{id}` | Đã đăng nhập; tạo/cập nhật chỉ Admin | Quản lý phòng ban độc lập với đội nhóm hỗ trợ theo ADR-016 |
| Đội nhóm | Tạo đội; cập nhật đội | `POST /api/support-teams`, `PUT /api/support-teams/{id}` | Admin hoặc Lead (`RequireLead`) | Quản lý đội ngũ kỹ thuật theo ADR-016 |
| Danh mục | Danh sách danh mục dịch vụ chuẩn ITIL | `GET /api/categories` | Đã đăng nhập | Danh mục chuẩn ITIL cho phiếu |
| Báo cáo | Báo cáo SLA; phân bố phiếu | `GET /api/reports/sla-performance`, `GET /api/reports/ticket-distribution` | Admin hoặc Lead (`RequireLead`) | Báo cáo thống kê hiệu suất tuân thủ SLA và phân bố phiếu |
| Trợ lý AI | Phân tích vé; lấy kết quả triage; ra quyết định | `POST /api/tickets/{id}/ai-analyze`, `GET /api/tickets/{id}/triage`, `POST /api/tickets/{id}/triage/decision` | Hai POST: Agent hoặc Admin (`RequireAgent`) có quyền xử lý phiếu. GET: đã đăng nhập, theo phạm vi đọc phiếu | Triage AI HITL cho phép Accept, Override kèm lý do, Reject. [Quy tắc triage AI](#triage-ai) |
| Phiếu | Tạo; danh sách cá nhân; hàng đợi; chi tiết; cập nhật | `POST /api/tickets`, `GET /api/tickets/me`, `GET /api/tickets/queue`, `GET /api/tickets/{id}`, `POST /api/tickets/{id}/update` | Requester (gồm Agent/Admin); hàng đợi chỉ Agent/Admin | Tạo: Submitted, 201 với `Location`, hỗ trợ `Idempotency-Key`; danh sách phân trang; hàng đợi hỗ trợ lọc theo trạng thái, danh mục, phạm vi, mức ưu tiên và `slaStatus`; cập nhật chỉ ở Submitted, chủ sở hữu hoặc Admin |
| Phiếu | Nhận, phân công, hủy phân công, chuyển đội | `POST /api/tickets/{id}/claim`, `/assign`, `/unassign`, `/transfer` | Agent hoặc Admin | Lưu người phụ trách và ghi audit; phân công/hủy phân công yêu cầu Lead của nhóm đang phụ trách phiếu hoặc Admin; chuyển đội yêu cầu lý do chuyển và lưu timeline audit; phiếu đã phân công cho người khác chỉ Admin nhận thay được |
| Phiếu | Giải quyết, mở lại, đóng, hủy, tạm dừng, tiếp tục | `POST /api/tickets/{id}/resolve`, `/reopen`, `/close`, `/cancel`, `/pending`, `/resume` | Đã đăng nhập; Application kiểm vai trò và chủ sở hữu (tạm dừng/tiếp tục yêu cầu Agent hoặc Admin) | Bảng chuyển trạng thái bên dưới |
| Trao đổi | Bình luận; ghi chú nội bộ; timeline | `POST /api/tickets/{id}/comments`, `POST /api/tickets/{id}/internal-notes`, `GET /api/tickets/{id}/timeline` | Bình luận và timeline theo phạm vi đọc. Ghi chú nội bộ yêu cầu quyền nhân viên trên phiếu | Quyền chủ sở hữu chỉ cấp nội dung công khai |
| Tệp | Xin quyền tải lên; xác nhận; danh sách; xin URL tải xuống | `POST /api/tickets/{id}/attachments/upload-url`, `/attachments/confirm`, `GET /api/tickets/{id}/attachments`, `/attachments/download-url` | Requester (theo phạm vi phiếu) | Luồng ở cuối tài liệu |
| Tri thức | Tạo bản nháp, sửa, xuất bản, lưu trữ | `POST /api/knowledge/articles`, `PUT .../{id}/draft`, `POST .../{id}/publish`, `POST .../{id}/archive` | Agent hoặc Admin | Sửa, xuất bản và lưu trữ bắt buộc `expectedRevision` |
| Tri thức | Chi tiết, tìm kiếm, danh sách | `GET /api/knowledge/articles/{id}`, `/search`, `GET /api/knowledge/articles` | Đã đăng nhập | Quyền đọc theo trạng thái bài viết |

Admin đọc toàn hệ thống. Phạm vi xử lý của Agent là hợp của phiếu thuộc nhóm đang hoạt động mà Agent là thành viên và phiếu đang được gán trực tiếp cho Agent. Hàng đợi, tổng số phiếu và các lệnh của nhân viên dùng phạm vi này. Phiếu ngoài phạm vi của thao tác trả `404`.

Requester và Agent là chủ phiếu truy cập chi tiết, bình luận công khai, timeline công khai và các API tệp theo quyền chủ sở hữu. Đọc hoặc viết ghi chú nội bộ cần quyền nhân viên trên phiếu. Tạo phiếu lấy người gọi làm chủ sở hữu. Danh sách cá nhân dùng danh tính của người gọi. [Ma trận quyền hiện hành](rbac-permission-matrix.md) · [Phương án SupportTeam](../adr/ADR-016-support-team-routing-and-agent-scope.md#phương-án-supportteam-đã-chốt)

Application từ chối actor chưa xác thực, ID không phải GUID hợp lệ hoặc GUID rỗng trước persistence. Endpoint còn có policy role; role không được cấp bị từ chối tại HTTP. Worker dùng actor anonymous, không đăng ký HttpContext. Không chỉ dựa vào middleware để bảo vệ use case.

Nguồn: [AuthEndpoints](../../src/backend/Modules/Identity/Presentation/AuthEndpoints.cs), [TicketEndpoints](../../src/backend/Modules/Ticketing/Presentation/TicketEndpoints.cs) (tách thành các tệp `TicketEndpoints.*.cs` cùng thư mục), [KnowledgeEndpoints](../../src/backend/Modules/Knowledge/Presentation/KnowledgeEndpoints.cs), [GetTicketDetail](../../src/backend/Modules/Ticketing/Application/GetTicketDetail/GetTicketDetailUseCase.cs).

## Triage AI

- Quyền do Ticketing quyết định qua `ITicketTriageBridge`, ở cả use case chứ không chỉ ở endpoint. Hai POST yêu cầu quyền xử lý phiếu; quyền của chủ phiếu không đủ (403). Phiếu ngoài phạm vi đọc trả `404`.
- `GET /triage` chỉ đọc kết quả đã lưu, không gọi dịch vụ AI và không ghi dữ liệu. Chưa có kết quả thì trả `outcome` là Unavailable. Chỉ `POST /ai-analyze` yêu cầu phân tích. Người có quyền xử lý phiếu nhận đủ trường; người chỉ có quyền chủ phiếu nhận gợi ý Category và dải tin cậy (chính sách hiển thị AI-01 còn mở).
- Accept cần kết quả phân tích thành công, chưa có quyết định, đúng nội dung phiếu hiện tại và có đủ Impact, Urgency; thiếu thì trả `409`, không dùng giá trị mặc định. Reject cũng cần kết quả như vậy (không bắt buộc đủ Impact, Urgency) và không đổi phiếu. Override cần lý do, Impact, Urgency; dùng được cả khi AI không khả dụng.
- Accept và Override ghi Category (nếu có), Impact, Urgency, Priority cùng audit hệ thống, bắt buộc `expectedRevision`. Không đổi trạng thái, phân công hay nhóm hỗ trợ. Mã Category không hợp lệ trả `400`.
- Củng cố G2 trên nhánh `Refactor-2` ngày 28/11/2026: analysis, gợi ý và quyết định lưu ở PostgreSQL (`ai_analyses`, `ai_suggestions`), giữ lịch sử, outcome và lỗi xác thực qua lần khởi động lại. Agent nhận thêm `analysisId`, `analyzedTicketRevision`, `isStale`, `decisionStatus`; Requester không nhận metadata nội bộ. OpenAPI của GET mô tả hai hình chiếu bằng `anyOf` vì tập trường có thể giao nhau.
- Accept/Reject bắt buộc gửi đúng `analysisId` thuộc phiếu đang xem. Override không kèm analysis là phân loại thủ công. Accept/Override bắt buộc `expectedRevision` khác 0; Reject giữ revision tùy chọn, nhưng nếu gửi thì phải hợp lệ. Quyết định mới kiểm trạng thái phiếu và hash nội dung trên snapshot đã khóa; không quyết định phiếu Resolved/Closed/Cancelled.
- Quyết định tham chiếu analysis khóa analysis → phiếu → nhóm/thành viên trong cùng giao dịch. Ticket, audit và quyết định cùng commit hoặc cùng rollback. Replay cùng người duyệt, quyết định, revision và nội dung chuẩn hóa không ghi lần hai; payload khác trả `409`. Chờ khóa quá hạn trả `503` với `Retry-After`. Không gọi AI trong giao dịch.
- Audit triage là sự kiện hệ thống **nội bộ**, gồm cả lý do phân loại thủ công; người yêu cầu không thấy lý do Override qua timeline. Triển khai cần hai migration `AddDurableAiTriage`, `PreserveAiOutcomeAndDecisionRevision` trước API/Worker; không dùng `Down` để rollback sau khi có lịch sử AI, vì sẽ mất dữ liệu mới. Chi tiết kiểm chứng và giới hạn phát hành ở [trạng thái kế hoạch củng cố](../plans/production-hardening-state.md).

## Identity sau PR #47, #48 và nhánh refactor

Cập nhật 2026-11-07, đối chiếu PR #50 đến #53 vào `dev`. Mã ở [AuthEndpoints](../../src/backend/Modules/Identity/Presentation/AuthEndpoints.cs) và [DepartmentEndpoints](../../src/backend/Modules/Identity/Presentation/DepartmentEndpoints.cs).

| Thao tác | Đường dẫn | Quyền | Hành vi |
| --- | --- | --- | --- |
| Làm mới phiên | `POST /api/auth/refresh` | Ẩn danh, có rate limit | Thu hồi token cũ bằng một lệnh cập nhật có điều kiện và lưu token mới trong cùng giao dịch. Token đã thu hồi bị gửi lại, hoặc hai request cùng token chạy song song, thì cả chuỗi phiên của token đó (mọi token sinh từ cùng một lần đăng nhập, cột `family_id`) bị thu hồi, kể cả token vừa cấp cho request thắng; phiên trên thiết bị khác giữ nguyên. |
| Đăng xuất | `POST /api/auth/revoke` | Ẩn danh | Thu hồi cả chuỗi phiên của refresh token được gửi, kể cả khi token đó đã được xoay vòng hoặc một lần làm mới đang chạy song song; token không tồn tại thì bỏ qua. Luôn trả 204. Thu hồi chuỗi phiên cũng đổi security stamp của người dùng: access token cũ trên mọi thiết bị bị từ chối; refresh token của chuỗi phiên khác vẫn có thể cấp access token mới. |
| Đổi mật khẩu | `POST /api/auth/change-password` | Đã đăng nhập | Đổi mật khẩu và thu hồi mọi refresh token của người đó trong cùng giao dịch. |
| Quên mật khẩu | `POST /api/auth/forgot-password` | Ẩn danh, có rate limit | Lưu yêu cầu vào PostgreSQL trước khi trả 200 với cùng thông điệp tiếp nhận; request không tra tài khoản. Email rỗng trả 400, queue đầy trả 503 chung, lỗi database trả 500 chung; chưa ghi bền vững thì không xác nhận thành công. Worker nhúng trong các API dùng claim/lease để phát và gửi mã. 200 xác nhận đã tiếp nhận yêu cầu, không xác nhận email đã gửi. Chi tiết retry, expiry và giới hạn bên dưới. |
| Đặt lại mật khẩu | `POST /api/auth/reset-password` | Ẩn danh, có rate limit | Một giao dịch khóa dòng mã (`FOR UPDATE`). Sai mã tăng `failed_attempts`; sau 5 lần sai mã bị từ chối kể cả khi đúng. Đúng mã thì đổi mật khẩu, đánh dấu đã dùng và thu hồi mọi refresh token; mật khẩu mới không hợp lệ thì rollback, mã vẫn dùng được. |
| Danh sách người dùng theo vai trò | `GET /api/users?role=` | Agent hoặc Admin (`RequireAgent`) | Mặc định vai trò Agent; trả `UserId`, `FullName`. |
| Gán vai trò | `PUT /api/users/{id}/roles` | Admin | Đổi vai trò và thu hồi refresh token của người được gán trong cùng giao dịch. Không cho gỡ Admin của quản trị viên cuối cùng; các lần gán vai trò khóa dòng vai trò Admin nên hai yêu cầu đồng thời không thể gỡ hai Admin cuối. Đổi security stamp trong cùng giao dịch làm access token mang vai trò cũ bị từ chối ở request xác thực tiếp theo. |
| Phòng ban | `GET /api/departments` | Đã đăng nhập | Danh sách phòng ban. |

Kiểm định G3-06: các use case đọc phòng ban/danh sách nhân viên và ghi phòng ban/vai trò đều kiểm actor đã xác thực với ID GUID khác rỗng trước persistence, ngoài policy role ở HTTP. Đăng ký chỉ tạo Requester, không nhận role, stamp, hash hay department do client chèn vào; profile chỉ cho sửa tên và điện thoại của actor. `UserInfo.SecurityStamp` không serialize ra API; danh sách nhân viên chỉ trả ID/tên. Chưa có API vô hiệu hóa tài khoản hoặc gán phòng ban cho tài khoản; phòng ban không cấp quyền xử lý phiếu. Login vẫn kiểm lockout. Đăng ký hiện đặt `EmailConfirmed=true` và chưa có bước chứng minh sở hữu email; G3-06 không tự thêm email verification/invitation hay chính sách quản trị tài khoản mới.

Tên trả về khi đăng ký và tên dùng cấp JWT lấy từ kết quả đã lưu/chuẩn hóa. Cập nhật profile khóa dòng người dùng trong transaction, kiểm mọi `IdentityResult`, ghi tên và điện thoại cùng commit hoặc rollback; không báo thành công khi thao tác claim thất bại. Phòng ban kiểm giới hạn hiện có của schema (mã 50, tên 100, mô tả 500 ký tự sau trim) trước ghi; đầu vào sai trả 400, trùng unique khi chạy đồng thời trả 409 chung. Không đổi schema hoặc migration.

OpenAPI Identity mô tả status thực tế: register 201/400/429; login và refresh 200/401/429; revoke 204/429; reset-password 200/400/429; change-password 200/400/401; GET me 200/401/404. Đây là sửa metadata, không đổi route/DTO hay status thành công runtime. Nguồn: [hồi quy biên Identity](../../tests/ServiceDesk.IntegrationTests/IdentityApiBoundaryTests.cs), [actor tại Application](../../tests/ServiceDesk.Application.Tests/Identity/IdentityActorBoundaryTests.cs).

Giới hạn tần suất hai lớp, **trên từng instance API**. Theo IP: đăng ký, đăng nhập, quên và đặt lại mật khẩu tối đa 60 request mỗi 60 giây (`auth-ip`); `/refresh` có ngân sách riêng 120 request mỗi 60 giây (`auth-refresh`) nên không tiêu hết lượt đăng nhập. Theo email: email đã chuẩn hóa (cắt khoảng trắng, chữ thường) cộng loại thao tác (đăng ký, đăng nhập, quên mật khẩu, đặt lại mật khẩu) tối đa 10 request mỗi 900 giây, độc lập với IP và giữa các thao tác. Vượt ngưỡng ở cả hai lớp trả 429 cùng JSON `{error}` và `Retry-After` (làm tròn lên giây, tối thiểu 1), không phụ thuộc email có tồn tại. Cấu hình ở mục `RateLimiting:Auth` (`Enabled`, `PermitLimit`, `WindowSeconds`, `RefreshPermitLimit`, `EmailPermitLimit`, `EmailWindowSeconds`). Các ngưỡng 60/60 giây, 10/900 giây, 5 lần sai và 5 mã mỗi giờ là giá trị khởi điểm chưa được nhóm chốt; cần đo sau NGINX trên staging.

Bộ đếm email giữ tối đa 4096 partition đang được lưu, gồm mọi thao tác. Admission, kiểm expiry và tăng bộ đếm cùng một khóa đồng bộ; chỉ loại partition hết hạn, không reset hoặc loại counter còn hiệu lực khi đầy. Khi chưa có chỗ, email/thao tác mới nhận 429 tới thời điểm hết hạn gần nhất; email/thao tác cũ tiếp tục dùng ngân sách còn lại. Đây là đánh đổi khả dụng để giữ ngân sách đã dùng, có thể từ chối người mới khi nhiều email khác chiếm đầy cửa sổ. Email sau trim dài hơn 320 ký tự nhận cùng 429/Retry-After trước khi tạo khóa, không được giữ trong bộ nhớ limiter; giới hạn này không thay kiểm tra cú pháp email của use case.

Request không có địa chỉ IP không bị giới hạn theo IP, lớp email vẫn áp dụng. Forwarded Headers chỉ đổi địa chỉ từ proxy/network được tin cậy (`ReverseProxy:KnownProxies`, `ReverseProxy:KnownNetworks`, cùng mặc định loopback của nền tảng); `X-Forwarded-For` từ peer không tin cậy bị bỏ qua. Mỗi replica có ngân sách riêng và restart làm mất counter trong RAM. Đây là lớp giảm tải per-instance, không phải giới hạn bảo mật toàn cụm; không thêm Redis/shared state trong G3-05. Nguồn: [AuthRateLimiting](../../src/backend/Modules/Identity/Presentation/AuthRateLimiting.cs), [hồi quy limiter](../../tests/ServiceDesk.IntegrationTests/AuthEmailRateLimiterTests.cs), [HTTP và proxy](../../tests/ServiceDesk.IntegrationTests/AuthRateLimitingTests.cs).

Theo quyết định chủ dự án ngày 28/11/2026: access token 60 phút (`Jwt:ExpiryMinutes`), refresh token 7 ngày; rotation cấp lại cửa sổ 7 ngày. Các template infra đồng bộ mặc định 60 phút; môi trường triển khai phải dùng đúng giá trị này, việc sửa template không tự đổi biến môi trường hoặc token đã phát.

Mọi thao tác tạo hoặc thu hồi refresh token (đăng nhập, làm mới phiên, đăng xuất, đổi/đặt lại mật khẩu, đổi vai trò, thu hồi chuỗi phiên khi phát hiện token bị dùng lại) lấy khóa dòng người dùng (`AspNetUsers ... FOR UPDATE`) trước trong giao dịch của mình. Lệnh thu hồi vì vậy luôn chạy sau khi token mới đã commit; đăng nhập kiểm mật khẩu và lưu refresh token trong cùng một giao dịch. Đổi vai trò cũng đổi security stamp.

JWT giữ kiểm tra chữ ký, issuer, audience và lifetime của nền tảng. Mỗi request có token đã qua các kiểm tra này còn phải đọc tài khoản/stamp hiện hành từ PostgreSQL; thiếu subject, thiếu/khác stamp hoặc tài khoản đã xóa trả 401 trên mọi API replica. Đổi/đặt lại mật khẩu và thu hồi chuỗi phiên làm stamp cũ hết hiệu lực. Thiếu persistence bắt buộc là lỗi DI; database lỗi là lỗi dependency, không chuyển thành xác thực thành công hay lỗi token 401: middleware production trả 500 ProblemDetails chung, không chứa chi tiết nội bộ. Challenge không công bố chi tiết lỗi JWT.

OTP mới lưu digest HMAC-SHA-256 có tiền tố `v1:`, gắn email đã chuẩn hóa và mục đích. `Otp:Key` là khóa riêng 32 byte dạng Base64, bắt buộc lúc API khởi động và giống nhau trên các replica. Giữ CSPRNG, so sánh constant-time, TTL 15 phút, 5 lần sai và 5 lần phát/giờ. Digest SHA-256 cũ không được fallback; sau rollout/đổi khóa, người dùng yêu cầu mã mới và vẫn chịu hạn mức phát hiện hành. Hash của refresh token giữ nguyên.

Hàng đợi OTP G3-03 dùng `password_reset_jobs`: tối đa 1000 yêu cầu chưa kết thúc và còn hạn trên PostgreSQL dùng chung, admission có khóa tư vấn để không vượt giới hạn khi đồng thời. Yêu cầu chờ phát mã hết hạn sau 15 phút; khi phát, expiry của job bằng expiry của OTP mới. Kiểm tài khoản và hạn mức phát chạy nền. OTP, vô hiệu mã cũ và payload gửi được ghi cùng giao dịch. Payload là AES-GCM với khóa con HKDF tách mục đích từ `Otp:Key`, gắn email/OTP ID; không lưu mã gốc trong DB hoặc log.

Các API claim bằng `FOR UPDATE SKIP LOCKED`, lease 60 giây và ID riêng, gia hạn trước gửi. Worker cũ không được ACK/sửa job sau khi lease mất. Tối đa 5 lần claim (gồm lần phục hồi sau crash); lỗi tạm thời lùi 5/10/20/40 giây trước lần kế tiếp. Lỗi SMTP 5xx, thiếu cấu hình hoặc payload sai kết thúc job; lỗi tạm thời/timeout có retry khi còn hạn và còn lượt. Trước attempt gửi phải còn OTP chưa dùng, chưa hết hạn và chưa hết lượt nhập sai. Kiểm expiry lần nữa sát lúc gửi; timeout SMTP không vượt 20 giây hoặc TTL còn lại. Gửi ngoài giao dịch DB và retry dùng đúng mã/payload đã lưu, không phát OTP mới.

SMTP có thể đã nhận thư trước khi API chết hoặc mất ACK, nên có thể nhận thư trùng cùng mã. Kiểm tra freshness là tại đầu attempt; không thể thu hồi thư đã gửi, thư có thể tới sau khi mã hết hạn hoặc bị thay thế. Không công bố exactly-once hoặc bảo đảm thời điểm tới hộp thư. Job kết thúc xóa ciphertext; bản ghi kết quả giữ một ngày, dọn từng batch tối đa 100 khi worker chạy. SMTP thiếu cấu hình được ghi là thất bại, không coi `NoOpOtpSender` là delivery thành công. Log chỉ ghi loại lỗi, trạng thái SMTP hoặc job ID, không ghi email/OTP/khóa.

Triển khai migration `AddDurablePasswordResetJobs` trước API/Worker; không chạy Down sau khi có job đang chờ. Thứ tự deploy toàn hệ thống và nghiệm thu cùng SHA thuộc infra. Nguồn và kiểm chứng: [job store](../../src/backend/Modules/Identity/Infrastructure/PostgresPasswordResetJobStore.cs), [hồi quy PostgreSQL](../../tests/ServiceDesk.IntegrationTests/PasswordResetJobPostgresTests.cs), [restart process thật](../../tests/ServiceDesk.IntegrationTests/PasswordResetProcessTests.cs).

Bản vá G3-01 không có grace period cho token thiếu stamp. Register/Login/Refresh hiện cấp stamp; token tự cấp ngoài đường chuẩn phải được thay bằng đăng nhập lại. Không có bằng chứng phiên production cần cơ chế chuyển đổi, không nới xác thực để giữ token không hợp lệ.

Bộ lọc lỗi `ApiErrorFilter` giữ mã 400/403/404/409/503. Thông điệp của `ArgumentException` và `UnauthorizedAccessException` chỉ được trả khi mã ServiceDesk ném ra; lỗi cùng loại từ .NET hoặc thư viện trả thông điệp chung ("Yêu cầu không hợp lệ.", "Bạn không có quyền thực hiện thao tác này.") và chỉ ghi chi tiết vào log. Knowledge chỉ trả 422 kèm thông điệp cho `InvalidOperationException` ném từ `ServiceDesk.Modules.Knowledge.Domain`; ngoại lệ cùng loại từ adapter hay thư viện thành lỗi 500 chung.

## Thao tác vòng đời phiếu

Các thao tác đều dùng POST và nhận `id` trên đường dẫn. `update` nhận JSON với `title`, `description`, `categoryId` và `expectedRevision`. `assign` nhận JSON với `agentId` và `expectedRevision`. `pending` nhận JSON với `status` và `expectedRevision`. Các lệnh `claim`, `unassign`, `resolve`, `reopen`, `close`, `cancel` và `resume` nhận `expectedRevision` qua query. `Ticket` kiểm chuyển trạng thái. Các lệnh `update/reopen/close/cancel` yêu cầu quyền chủ sở hữu hoặc Admin.

| Thao tác | Trạng thái trước | Trạng thái sau | Giới hạn hiện tại |
| --- | --- | --- | --- |
| Cập nhật | Submitted | Submitted | Quy tắc tiêu đề, mô tả và danh mục như khi tạo |
| Nhận xử lý | Submitted hoặc Assigned | InProgress | Agent trong phạm vi xử lý hoặc Admin. Phiếu đã phân công cho người khác chỉ Admin nhận thay được. Lệnh lưu người phụ trách và ghi audit |
| Phân công | Mọi trạng thái trừ Closed, Cancelled và Resolved | Assigned nếu đang Submitted. Trạng thái khác giữ nguyên | Lead của nhóm phụ trách phiếu hoặc Admin. Người nhận có vai trò Agent hoặc Admin theo [`AgentDirectory`](../../src/backend/Modules/Ticketing/Infrastructure/AgentDirectory.cs). Lead chỉ gán cho thành viên cùng nhóm. Admin được gán ngoài nhóm |
| Hủy phân công | Assigned | Submitted | Lead của nhóm phụ trách phiếu hoặc Admin. Chỉ khi đang Assigned và đã có người phụ trách |
| Tạm dừng | InProgress | PendingUser hoặc PendingExternal | Agent hoặc Admin (`RequireAgent`); body `PendingTicketRequest` (Status, ExpectedRevision) |
| Tiếp tục | PendingUser hoặc PendingExternal | InProgress | Agent hoặc Admin (`RequireAgent`); query `expectedRevision` |
| Giải quyết | InProgress hoặc PendingExternal | Resolved | Hỗ trợ ghi chú giải quyết (`ResolutionNote`), lưu vào timeline audit. Đồng hồ SLA ghi `ResolvedAt` |
| Mở lại | Resolved | InProgress | Chưa có cửa sổ thời gian, kiểm tra người phụ trách hoặc tác vụ tự đóng |
| Đóng | Resolved | Closed | Chưa có tác vụ đóng tự động |
| Hủy | Submitted | Cancelled | Chưa nhận lý do hủy hoặc lưu lịch sử nghiệp vụ |

Mọi lệnh ghi phiếu (cập nhật, nhận xử lý, phân công, hủy phân công, chuyển đội, giải quyết, mở lại, đóng, hủy, tạm dừng, tiếp tục) bắt buộc `expectedRevision`: thiếu hoặc bằng 0 trả 400, khác revision hiện tại (`xmin`) trả 409. Trạng thái khác bị Domain từ chối; `TicketCommandGuard` chuyển lỗi trạng thái thành `ArgumentException`, bộ lọc HTTP trả 400 với `{ error }`. Phiếu thiếu hoặc ngoài quyền chủ sở hữu của các lệnh `update/reopen/close/cancel` trả 404. Các lệnh mới chưa có cơ chế khóa chống tạo trùng hoặc phát lại phản hồi như thao tác tạo phiếu. Yêu cầu đã thực hiện thành công rồi gửi lại có thể nhận lỗi trạng thái.

## Các điểm đã xử lý sau refactor

Đối chiếu `dev@314f5b9`. Các vấn đề từng ghi ở mục này tại mốc PR #29 đã được sửa trong mã:

| Vấn đề cũ | Hành vi hiện tại |
| --- | --- |
| Quyền nhận xử lý chưa thống nhất | `ClaimTicketUseCase` nhận Agent hoặc Admin, khớp chính sách `RequireAgent`; phiếu đã phân công cho người khác chỉ Admin nhận thay được (403 với người khác). |
| Giải quyết tải phiếu trước khi kiểm vai trò | `ResolveTicketUseCase` kiểm Agent/Admin trước khi tải phiếu; Requester luôn nhận 403, không phân biệt được phiếu có tồn tại hay không. |
| Ghi đồng thời chưa có phản hồi ổn định | Lệnh ghi kiểm `expectedRevision` với `xmin` trước khi lưu; `DbUpdateConcurrencyException` khi lưu được chuyển thành `ConcurrencyConflictException` và trả 409. |
| Phân trang Knowledge tràn offset | `(page - 1) * pageSize` vượt `int` trả 400, cùng hợp đồng với danh sách phiếu (trước đây phép nhân tràn số và trả nhầm trang đầu). |
| Bình luận/tệp ghi vào phiếu vừa đóng | Bình luận công khai và xác nhận tệp đọc lại trạng thái phiếu với `FOR SHARE` trong giao dịch ghi (`TicketRowLock`); người ngoài phạm vi nhận 404 trước khi xét trạng thái. |

`GetForUpdateAsync` vẫn đọc phiếu bằng `SingleOrDefaultAsync` (không khóa dòng); xung đột được phát hiện bằng revision. Xem [quy tắc bảo mật](security-design-rules.md) và [hướng dẫn kiểm chứng](../runbooks/backend-hardening-verification.md).

## Phân công tại mốc trước PR #82

Phần này ghi tình trạng tại `dev@3640c33`. Tại `dev@b3cc19a`, PR #82 bổ sung quyền Lead/Admin và sửa sai lệch DEC-05. [Ma trận quyền hiện hành](rbac-permission-matrix.md)

| Chính sách đã chốt | Mã tại mốc cũ | Việc còn thiếu tại mốc cũ |
| --- | --- | --- |
| Không phân công (`assign`) khi phiếu ở trạng thái Resolved (DEC-05, xác nhận 2026-11-13) | `Ticket.Assign` chỉ chặn Closed và Cancelled; Resolved vẫn được gán | Sửa `Ticket.Assign`, trả 400 cho phiếu Resolved, thêm kiểm thử từ chối ở Domain, Application và HTTP, cập nhật OpenAPI nếu mô tả đổi, báo web |

## Dữ liệu phiếu và mã trạng thái

| Trường | Ràng buộc hiện tại |
| --- | --- |
| `title` | Trim, không rỗng, tối đa 100 ký tự; cột DB vẫn tối đa 200 để giữ dữ liệu cũ |
| `description` | Trim, tối thiểu 10 ký tự; không tự thêm giới hạn 4000 từ đề xuất cũ |
| `categoryId` | Mã chuỗi thuộc danh sách được cho phép, không phải GUID |
| `requesterId` | Lấy từ actor trong Application, không nhận từ command |
| `ticketNumber` | Sinh bằng sequence DB, `IT-YYYY-…`, độ rộng tối thiểu bốn chữ số; không reset theo năm |
| `requesterDepartmentId` | Nullable; chưa có luồng snapshot hồ sơ/phòng ban hoàn chỉnh |
| `status` | Số nguyên trong JSON; khi tạo là Submitted (1) |
| `impact`, `urgency`, `priority` | Khi tạo là Medium, Medium, P3. Chưa có endpoint hay use case ghi các trường này (`Ticket.SetTriage` có trong Domain nhưng chưa được gọi), nên mọi phiếu hiện mang P3; bộ lọc và sắp xếp theo `priority` của hàng đợi chạy trên giá trị này |
| `supportTeamId` | Phiếu mới vào `MANUAL_TRIAGE`. Agent trực thấy phiếu chưa gán khi đã được cấp thành viên của hàng đợi đang hoạt động. `RoutingRule` và API chuyển nhóm thuộc [phương án SupportTeam](../adr/ADR-016-support-team-routing-and-agent-scope.md#phương-án-supportteam-đã-chốt) |
| `sla` | Nullable, DTO `SlaSummaryDto` gồm `isApplicable`, `evaluatedAt`, `overallStatus`, `firstResponse`, `resolution` |
| Phân trang | Mặc định page=1, pageSize=10; page ≥ 1, pageSize từ 1 đến 100; kiểm tra tràn offset ở Application |

| Giá trị đã định nghĩa | Tên |
| ---: | --- |
| 0 | Draft |
| 1 | Submitted |
| 2 | InProgress |
| 3 | PendingUser |
| 4 | Resolved |
| 5 | Closed |
| 6 | Cancelled |
| 7 | Triaged |
| 8 | Assigned |
| 9 | PendingExternal |

Nguồn: [TicketStatus](../../src/backend/Modules/Ticketing/Domain/TicketStatus.cs), [Ticket](../../src/backend/Modules/Ticketing/Domain/Ticket.cs), [TicketDto](../../src/backend/Modules/Ticketing/Application/Contracts/TicketDto.cs), [mapping](../../src/backend/Modules/Ticketing/Infrastructure/Configurations/TicketConfiguration.cs). Có enum không có nghĩa đã triển khai lệnh chuyển trạng thái: chưa lệnh nào tạo hoặc chuyển tới Draft (0) và Triaged (7). Lệnh tạm dừng đã dùng PendingUser (3) và PendingExternal (9). Không đổi nghĩa 0–6, loại Cancelled hoặc đổi JSON sang chuỗi để khớp bảng 0–8 trong tài liệu lịch sử.

## Lỗi và idempotency

- Giữ hình dạng phản hồi lỗi hiện hành, không chuyển đồng loạt sang ProblemDetails. [ApiErrorFilter](../../src/backend/BuildingBlocks/SharedKernel.Web/ApiErrorFilter.cs) giữ `{ error }` cho lỗi đầu vào/chống tạo trùng; 404 chi tiết không dựng một DTO lỗi mới.
- Tạo ticket hỗ trợ `Idempotency-Key` tùy chọn. Receipt nằm trong phạm vi requester + method + resource + key hash; khác payload cùng key trả 409.
- Advisory lock và transaction PostgreSQL giữ ticket/receipt nguyên tử. Receipt dùng giờ DB, hiệu lực 10 phút; lock timeout 5 giây; timeout trả 503 và `Retry-After: 1`.
- Replay trả response đã lưu sau kiểm tra quyền sở hữu, không dựng lại từ ticket có thể thay đổi. Không dùng cache trong tiến trình thay cơ chế này.
- Dấu vân tay tạo phiếu giữ thuật toán riêng; không thay bằng mã băm AI `ai-input-v2`.

## Kiểm chứng và giới hạn

[OpenAPI baseline](../../contracts/backend/openapi-baseline.json) cùng [ApiContractTests](../../tests/ServiceDesk.IntegrationTests/ApiContractTests.cs), [TicketAccessScopeTests](../../tests/ServiceDesk.IntegrationTests/TicketAccessScopeTests.cs), [TicketIdempotencyPostgresTests](../../tests/ServiceDesk.IntegrationTests/TicketIdempotencyPostgresTests.cs) bảo vệ các phần tương ứng. Baseline OpenAPI không thay kiểm thử hành vi, dữ liệu và quyền.

Kiểm thử `PublishedApiMatchesBaseline` so sánh toàn bộ tài liệu OpenAPI với bản chuẩn, phát hiện cả mục mới chưa công bố và thay đổi mô tả/bảo mật. Cập nhật bản chuẩn có chủ đích theo [quy chuẩn tài liệu](../process/documentation-standard.md).

Đã bổ sung API quản trị nhóm hỗ trợ (`/api/support-teams`), điều phối chuyển đội (`/api/tickets/{id}/transfer`), danh mục chuẩn ITIL (`/api/categories`), báo cáo thống kê SLA/phân bố phiếu (`/api/reports/*`), AI triage HITL (`/api/tickets/{id}/triage/*`), quản lý phòng ban (`/api/departments`). Luồng Draft/Submit riêng vẫn đang mở; thu hồi access token qua security stamp đã được kiểm chứng trên hai host dùng chung PostgreSQL trong G3-01. Dữ liệu SLA tóm tắt được gắn kèm trong `TicketDto` và hỗ trợ lọc qua tham số query `slaStatus` trên hàng đợi. Luồng tệp đính kèm: xin quyền tải lên nhận form multipart POST đã ký (giới hạn 10 MiB, gắn actor/ticket/tên/dung lượng/MIME/key/hết hạn) → xác nhận đối chiếu metadata object, sao chép sang key cuối bất biến rồi mới ghi metadata vào DB → tải xuống bằng URL ký ngắn hạn. Grant PUT cũ còn hạn vẫn được xác nhận theo cùng cách (xem [runbook](../runbooks/supportflow-hardening-phase-2-6.md)). Còn mở: đối soát object mồ côi khi lưu trữ thành công nhưng kết quả giao dịch DB không xác định. FullName chưa có dữ liệu hồ sơ riêng được lưu. Các phần còn thiếu thuộc đợt phát triển được quyết định riêng.

Ứng viên B3 áp dụng [DEC-07](pending-business-decisions.md) cho tệp đã xác nhận. Mỗi phiếu có tối đa 10 tệp với tổng dung lượng tối đa 50 MiB. Khi xác nhận tệp mới, backend kiểm tra hạn mức và lưu metadata trong cùng giao dịch khóa theo phiếu. Nếu vượt hạn mức, API trả 400. Nếu hết thời gian chờ khóa, API trả 503 với `Retry-After` và giữ grant để gửi lại. Gửi lại yêu cầu xác nhận thành công trả về tệp đã ghi mà không tính thêm dung lượng. Grant đang chờ xác nhận chưa chiếm hạn mức tệp đã xác nhận.
