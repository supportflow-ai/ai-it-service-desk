# Rà soát tài liệu ngữ cảnh dự án backend

> Bản ghi trước chỉnh sửa, giữ phát hiện và số dòng tại thời điểm rà soát. Các mục đã được xử lý trong [báo cáo khắc phục](project-context-remediation.md); không dùng kết luận dưới đây để mô tả tài liệu sau chỉnh sửa.

Ngày rà soát: 2026-10-19. Nhánh: `codex/backend-clean-architecture`.
SHA nguồn: `8244cc1649eb2f8db28c8dcaf30b0d540eab1c58`.

**Kết luận: bộ tài liệu chưa nhất quán để dùng toàn bộ làm chỉ dẫn triển khai.** Kiến trúc hiện hành đã được mô tả tương đối rõ trong ADR-011–014 và tài liệu module/use case. Vấn đề lớn nhất là các chỉ dẫn cũ vẫn được trình bày như hợp đồng bắt buộc, có thể khiến người đọc đổi API, diễn giải sai dữ liệu hoặc triển khai nghiệp vụ ngoài phạm vi.

Đây là báo cáo đánh giá tài liệu, không phải quyết định thay đổi nghiệp vụ hay kết luận nghiệm thu ứng dụng. Không thay trạng thái phê duyệt, Jira, mã nguồn hoặc migration trong lần rà soát này.

## 1. Phạm vi và phương pháp

Tại thời điểm bắt đầu, nhánh đi trước nhánh remote một commit. Có hai thay đổi chưa commit được giữ nguyên: `AGENTS.md` chưa được Git theo dõi và `docs/runbooks/environment-runbook.md` đã sửa.

Đã kiểm kê và quét 98 tệp Markdown: 82 tệp được Git theo dõi, một AGENTS.md chưa được theo dõi, 15 tệp thuộc bộ ngữ cảnh local bị Git bỏ qua. Đọc sâu các tài liệu định hướng kiến trúc, hợp đồng nghiệp vụ, quy trình, vận hành và các đoạn có chỉ dẫn xung đột; đối chiếu với mã nguồn và cấu hình tại SHA trên. Báo cáo này được tạo sau kiểm kê nên không tính trong 98 tệp.

| Nhóm | Số tệp Markdown | Nội dung kiểm tra |
| --- | ---: | --- |
| README và AGENTS | 2 | Phạm vi repo, cách làm việc, ngôn ngữ, lệnh và nguồn tham chiếu |
| `docs/adr` | 13 | Quyết định hiện hành, quyết định bị thay thế, ranh giới tầng/module |
| `docs/architecture` | 16 | Hai bộ hợp đồng nghiệp vụ, chỉ mục, module và use case |
| `docs/process` | 2 | DoR/DoD, nhánh, commit, PR và Jira |
| `docs/runbooks` | 4 | Môi trường, database, kiểm chứng, CI và triển khai |
| `docs/reviews` | 10 | Mốc nguồn, giới hạn bằng chứng và các kết luận lịch sử |
| `docs/sprint-1` | 23 | Hồ sơ mười công việc, ngày đánh giá, SHA và trạng thái nghiệm thu |
| `artifacts` | 9 | Phân biệt bằng chứng cũ với hướng dẫn đang áp dụng |
| `contracts` | 1 | Mô tả thuật toán hash AI |
| `.github` | 3 | Mẫu PR và issue |
| Bộ ngữ cảnh local | 15 | Prompt, CLAUDE.md, bản tổng hợp và 11 tài liệu dựng khung |

Kiểm tra bổ sung: 21 tệp JSON bằng chứng sprint; manifest của bộ ngữ cảnh; OpenAPI backend/AI và fixtures; workflow, cấu hình ghim phiên bản, mẫu biến môi trường, cấu hình ứng dụng, các project và những đoạn mã liên quan. `planning` và `evaluation` không có tài liệu nội dung để đánh giá. Cấu hình Claude local chỉ có danh sách quyền công cụ; không được dùng làm đặc tả dự án.

Không đọc `.env` thật. Không kiểm tra trực tiếp Jira/Confluence, trạng thái PR/CI trên GitHub, frontend, infra hoặc repo AI. Các thông tin bên ngoài được ghi trong tài liệu chỉ được xem là bản ghi tại thời điểm của tài liệu đó, chưa xác nhận còn đúng hôm nay.

## 2. Hiện trạng đã đối chiếu với mã nguồn

| Nội dung | Hiện trạng trên nhánh |
| --- | --- |
| Kiến trúc | Modular Monolith; Clean Architecture; Application tổ chức theo module và feature |
| Project | Domain, Application, Infrastructure, API, Worker, Database |
| Module đã có triển khai | Identity, Ticketing, AIAssistance |
| Điều phối nghiệp vụ | Sáu interface use case có `ExecuteAsync`; implementation nội bộ; không có mediator |
| Truy cập dữ liệu | Gateway chuyên biệt; một DbContext; mapping Ticketing nằm trong Infrastructure/Ticketing |
| Danh tính và quyền | Application kiểm tra actor; tạo/list dùng actor hiện tại; Requester đọc chi tiết của mình; Agent/Admin đọc chi tiết toàn hệ thống |
| API ticket | `/api/tickets`, `/api/tickets/me`, `/api/tickets/{id}`; tạo trực tiếp ở `Submitted` |
| Trạng thái | `Draft=0`, `Submitted=1`, `InProgress=2`, `PendingUser=3`, `Resolved=4`, `Closed=5`, `Cancelled=6`, `Triaged=7`, `Assigned=8`, `PendingExternal=9` |
| JSON trạng thái | `TicketDto.Status` là `int`; có enum mở rộng không đồng nghĩa đã có use case chuyển trạng thái |
| Đầu vào ticket | Tiêu đề tối đa 100 ký tự sau trim, cột DB 200; category là mã chuỗi; số ticket dạng `IT-YYYY-…` |
| Database | API/Worker không tự migration/seed; công cụ Database có các lệnh riêng |
| AI | NoOp; hợp đồng v1; hash `ai-input-v2`; chưa có adapter gọi mạng hoặc lưu entity AI vào DB |
| CI | GitHub Actions kiểm tra/build image; không phát hành image hoặc triển khai production |

Nguồn chính: [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md), [module/use case](../architecture/backend-modules-and-use-cases.md), [TicketStatus](../../src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs), [Ticket](../../src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs), [TicketDto](../../src/backend/ServiceDesk.Application/Ticketing/Contracts/TicketDto.cs), [GetTicketDetail](../../src/backend/ServiceDesk.Application/Ticketing/GetTicketDetail/GetTicketDetailUseCase.cs), [workflow](../../.github/workflows/ci.yml).

## 3. Các phát hiện cần xử lý

P1: có thể dẫn đến triển khai sai hợp đồng, sai dữ liệu hoặc thiết kế bảo mật sai nếu làm theo. P2: gây hướng dẫn không nhất quán, hiểu sai phạm vi hoặc bằng chứng. Đây là mức ưu tiên sửa tài liệu, không mặc nhiên là mức độ lỗi đang xảy ra trên ứng dụng.

### CTX-01 — P1: chỉ mục kiến trúc tự mâu thuẫn về nguồn có hiệu lực

**Bằng chứng:** [chỉ mục kiến trúc](../architecture/README.md), dòng 1–5 nói giữ tạo Submitted và hoãn lifecycle; dòng 11–21 gọi thư mục này là nguồn kỹ thuật duy nhất, đặt hợp đồng kiến trúc trên OpenAPI; dòng 43 lại yêu cầu sửa ngay tạo Draft và thêm Submit. Chỉ mục còn bỏ sót ADR-014, tài liệu module/use case và bộ đề xuất có đánh giá nguồn ở cùng thư mục.

[Biên bản governance](../architecture/08-sprint-acceptance-and-governance-record.md), dòng 24–26 và 71, kết luận đủ điều kiện đóng và `ACCEPTED WITH CARRY-OVER`; trong khi [review TC-285](../sprint-1/TC-285/review.md), dòng 51, chỉ ghi nhận `BLOCKED — PENDING QA EVIDENCE AND HUMAN REVIEW` tại một SHA/ngày khác. Chúng không phải bằng chứng nghiệm thu cho HEAD hiện tại.

**Tác động:** người mới hoặc trợ lý có thể coi yêu cầu lịch sử là nhiệm vụ bắt buộc hiện tại, hoặc kết luận đã nghiệm thu từ một nhãn trong tài liệu.

**Đề xuất:** sửa chỉ mục thành bản đồ nguồn với trạng thái, phạm vi, SHA/ngày và tài liệu thay thế. Phân biệt rõ mô tả đã triển khai, quyết định kiến trúc đang áp dụng, đề xuất nghiệp vụ và bằng chứng lịch sử. Không tự sửa kết luận nghiệm thu cũ; thêm thông tin phạm vi và liên kết tới hồ sơ liên quan.

### CTX-02 — P1: hợp đồng cũ yêu cầu đổi nghĩa enum và phá hợp đồng HTTP hiện tại

**Bằng chứng:** [hợp đồng lifecycle cũ](../architecture/03-ticket-lifecycle-and-visibility-baseline.md), dòng 23–48, yêu cầu bảng trạng thái 0–8 và chuỗi JSON, loại Cancelled. Mã hiện tại trong [TicketStatus](../../src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs), dòng 5–15, giữ các nghĩa 0–6 và thêm 7–9; [TicketDto](../../src/backend/ServiceDesk.Application/Ticketing/Contracts/TicketDto.cs), dòng 9, dùng `int Status`.

[Hợp đồng field cũ](../architecture/02-ticket-domain-and-field-contract.md), dòng 22, 32–37, 69 và 82–113, còn yêu cầu `/api/v1`, tạo Draft, category GUID tùy chọn, tiêu đề tối đa 200, mã `TK-YYYY-00000` và Submit riêng. Những điều này khác [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md), dòng 10–14, và mã đang chạy.

**Tác động:** cùng số 2 có thể bị hiểu từ InProgress thành Triaged; số 6 từ Cancelled thành PendingExternal. Đổi DTO/route/category còn làm caller hiện tại mất tương thích. Không thể coi đây là chỉnh sửa tài liệu nhỏ hoặc refactor thuần túy.

**Đề xuất:** gắn cảnh báo lịch sử ngay tại các tệp này, dẫn tới bảng hợp đồng hiện hành có đầy đủ mười giá trị. Đưa Draft/Submit, đổi taxonomy hoặc phiên bản API vào đề xuất thay đổi riêng với phương án chuyển dữ liệu và kiểm thử tương thích. Không sửa enum, migration hay baseline HTTP để khớp tài liệu cũ.

### CTX-03 — P1: sơ đồ phân công kiểm tra revision trước quyền truy cập

**Bằng chứng:** [hợp đồng assignment cũ](../architecture/06-ticket-assignment-policy.md), dòng 53–59, chỉ tìm ticket theo ID rồi trả 409 nếu revision lệch, sau đó mới kiểm tra quyền caller. Không nêu bước tải tài nguyên trong phạm vi được phép. [Đề xuất assignment/comment](../architecture/05-assignment-and-comment-policy.md), dòng 48, quy định quyền hiển thị và quyền hành động trước revision.

**Tác động:** nếu triển khai đúng sơ đồ cũ mà không thêm phạm vi truy cập, caller ngoài quyền có thể phân biệt ticket tồn tại qua phản hồi 409/403 thay vì bị che bằng 404. Đây là lỗi trong chỉ dẫn thiết kế; chưa có endpoint assignment để kết luận ứng dụng đang rò rỉ theo đường này.

**Đề xuất:** chuẩn hóa thứ tự xác thực → kiểm tra cú pháp → phạm vi nhìn thấy → quyền hành động → revision → trạng thái/điều kiện → ghi nguyên tử. Thêm tình huống ticket ngoài phạm vi với revision sai để chứng minh không lộ sự tồn tại. Việc chọn ai được phân công vẫn là quyết định nghiệp vụ riêng.

### CTX-04 — P2: hướng dẫn RBAC vừa khác chính sách hiện hành, vừa đặt logic sai ranh giới module

**Bằng chứng:** [ma trận RBAC cũ](../architecture/01-rbac-and-api-permission-matrix.md), dòng 46, cho đặt predicate Ticket trong `Application/Common/Security` hoặc `Infrastructure/Identity`; đoạn mẫu dùng Ticket, team, assignee và Lead. Trong khi [ModuleRules](../../tests/ServiceDesk.ArchitectureTests/ModuleRules.cs) cấm shared code truy cập entity module; [ADR-014](../adr/ADR-014-feature-use-cases-and-published-contracts.md) giao phạm vi truy cập cho Application của module.

Hai bộ ma trận mô tả Agent theo team/assignment, còn [GetTicketDetail](../../src/backend/ServiceDesk.Application/Ticketing/GetTicketDetail/GetTicketDetailUseCase.cs), dòng 22–23, dùng AllTickets cho Agent/Admin. ADR-014 nói rõ chưa có cách ly theo team. `RoleNames.Lead` trong ví dụ chưa phải role được triển khai.

**Tác động:** có thể đưa nghiệp vụ Ticketing vào Common/Identity, hoặc nhầm rằng team authorization đã tồn tại. Cũng có thể tự ý thu hẹp quyền khi chỉ được giao củng cố kiến trúc.

**Đề xuất:** ghi chính sách hiện tại và chính sách tương lai ở hai phần riêng. Ví dụ mới đặt trong `Application/Ticketing/Authorization`, dùng contract/value type được cho phép. Muốn thay quyền Agent cần một quyết định có phạm vi rõ; báo cáo này không chọn thay.

### CTX-05 — P2: các bộ đặc tả tương lai đưa ra kết quả nghiệp vụ khác nhau

| Chủ đề | Bộ hợp đồng cũ | Bộ đề xuất sau đó |
| --- | --- | --- |
| Requester phản hồi PendingUser | Tự chuyển InProgress: [privacy contract](../architecture/05-comment-and-internal-note-privacy-contract.md), dòng 91 | Giữ PendingUser, nhân viên chủ động resume: [assignment/comment](../architecture/05-assignment-and-comment-policy.md), dòng 103 |
| Resolve khi PendingExternal | Cho phép trực tiếp: [transition matrix](../architecture/04-ticket-state-transition-and-reopen-matrix.md), dòng 37 | Phải resume trước: [lifecycle policy](../architecture/04-ticket-lifecycle-policy.md), dòng 75 |
| Reopen/auto-close | Mở lại trong 168 giờ; tự đóng sau 72 giờ | Cảnh báo cửa sổ thực tế bị rút ngắn vì Closed là terminal; thông số và job vẫn chờ quyết định |
| Tải tệp | Presigned URL 900 giây: [attachment contract](../architecture/07-attachment-security-limits-and-policy.md), dòng 35 | API xác thực rồi truyền tệp, không trả URL MinIO: [attachment proposal](../architecture/06-attachment-security.md), dòng 32 |
| Giới hạn và xóa tệp | 10 MiB/file, 30 MiB/ticket, 10 file và quyền xóa trong một giờ | Giới hạn chưa duyệt; chưa mở API xóa trong MVP |

**Tác động:** hai nhóm triển khai có thể đều làm đúng tài liệu họ đọc nhưng không tích hợp được; QA cũng không có kết quả mong đợi thống nhất.

**Đề xuất:** lập bảng quyết định còn mở với người quyết định, phương án được chọn và tài liệu bị thay thế. Chưa kích hoạt tính năng chỉ vì một tệp ghi APPROVED. Khi chốt attachment cần rà riêng quota đồng thời, danh sách MIME/extension và khả năng thu hồi quyền tải; không coi kiểm tra chữ ký 512 byte là bảo đảm an toàn toàn bộ tệp.

### CTX-06 — P2: bộ ngữ cảnh local còn hướng dẫn dựng lại monorepo và Jenkins

**Bằng chứng:** bộ `ai-it-service-desk-claude-context-pack` có đủ 16 tệp theo manifest, gồm 15 Markdown và một JSON, bị `.gitignore` bỏ qua. README dòng 7 chỉ dựng foundation có frontend/Jenkins; dòng 13–14 hướng dẫn copy pack và đưa CLAUDE.md ra root. `docs/10-decisions-and-open-points.md`, dòng 7 và 22, vẫn khóa Monorepo/Jenkins. Bản tổng hợp 578 dòng lặp các chỉ dẫn này.

**Tác động:** dùng lại prompt khởi động sẽ đưa trợ lý về nhiệm vụ scaffold đã kết thúc, có thể tái tạo frontend/Compose/Jenkins trong backend hoặc từ chối phát triển nghiệp vụ. Đây là dữ liệu local cũ, không phải chỉ dẫn đang tự động áp dụng ở root và không phải nội dung đang có trên GitHub.

**Đề xuất:** lưu như hồ sơ dựng khung, ghi rõ đã hết hiệu lực cho repo hiện tại; không copy CLAUDE.md cũ lên root. Nếu cần gói ngữ cảnh mới, tạo bản chỉ dẫn ngắn trỏ tới tài liệu hiện hành thay vì sao chép cả bộ. Không xóa hồ sơ local trong nhiệm vụ chỉ review.

### CTX-07 — P2: README và runbook kiểm chứng chưa theo quy trình môi trường mới

**Bằng chứng:** [README](../../README.md), dòng 53–64, có đường chạy nhanh migrate/seed mà không dẫn tới bước xác minh database trong runbook đang sửa. Dòng 89–90 và [runbook kiểm chứng](../runbooks/backend-hardening-verification.md), dòng 17–18, dùng `python -m pip install` trực tiếp, không yêu cầu venv. [Runbook môi trường](../runbooks/environment-runbook.md) đã hướng dẫn metadata đích, kiểm tra tài nguyên local, venv và EF CLI cục bộ.

**Tác động:** người theo README có thể bỏ qua quy trình xác minh đích hoặc cài package vào Python đang hoạt động. Người chỉ có bản Git chưa commit cũng chưa nhận được hướng dẫn mới.

**Đề xuất:** giữ một nguồn lệnh local tại runbook môi trường, README dẫn trực tiếp tới đó. Đồng bộ lệnh validator trong runbook kiểm chứng với interpreter venv. Giữ lệnh pip hiện tại trong workflow chạy trên runner tạm nếu vẫn phù hợp; không đồng nhất runner CI với máy phát triển.

### CTX-08 — P2: quy ước nhánh, ngôn ngữ và điều kiện kiểm thử chưa thống nhất

**Bằng chứng:** [quy ước Git/Jira](../process/pr-branch-jira-convention.md), dòng 11–31, bắt buộc các loại nhánh liệt kê nhưng chưa nêu `codex/` và ngoại lệ đã được chỉ định. Workflow chỉ có push vào main/dev/staging/codex; PR vẫn được chạy cho mọi nhánh. Vì vậy nhánh feature theo ví dụ chưa có PR sẽ không chạy kiểm tra khi push.

[DoR/DoD](../process/definition-of-ready-done.md), dòng 25–32, áp toàn bộ kiểm thử cho mọi task/PR; [AGENTS](../../AGENTS.md), dòng 109, cho phép thay đổi chỉ tài liệu không chạy toàn bộ suite. Ví dụ commit/PR tiếng Anh trong tài liệu quy trình chưa theo quy tắc tiếng Việt; liên kết Jira mẫu tại dòng 66 vẫn là `jira.company.internal`.

**Tác động:** trợ lý có thể hỏi lại về nhánh đã được giao, yêu cầu kiểm thử không cần thiết cho tài liệu hoặc viết commit không đúng ngôn ngữ. Không có CI trên push feature không đồng nghĩa không có CI trên PR.

**Đề xuất:** quy định rõ ngoại lệ cho nhánh do người dùng chỉ định, ngôn ngữ và kiểm thử theo loại thay đổi; ghi trung thực điều kiện kích hoạt workflow. Thay ví dụ Jira bằng hướng dẫn lấy URL thực tế. Nếu muốn mở rộng push trigger, làm thay đổi CI riêng, không coi đó là hệ quả tự động của review tài liệu.

### CTX-09 — P2: ADR nền tảng thiếu liên kết tới phạm vi đã được điều chỉnh

**Bằng chứng:** [ADR-001](../adr/ADR-001-modular-monolith.md), dòng 11, ghi năm module trong một đơn vị triển khai; [ADR-002](../adr/ADR-002-clean-architecture.md), dòng 11, ghi bốn project. ADR-014 mô tả sáu project và ba module hiện triển khai. [ADR-008](../adr/ADR-008-docker-compose-nginx.md) mô tả Compose nhưng chưa chỉ vị trí sở hữu ở repo infra. Ngược lại, ADR-009 đã ghi rõ bị ADR-013 thay thế, nên phần Jenkins được lưu ở đó là hợp lệ.

**Tác động:** đọc riêng ADR cũ có thể hiểu thiếu Worker/Database hoặc cho rằng phải tạo Knowledge/Analytics ngay. Mục tiêu sản phẩm dài hạn không phải số module đã được triển khai.

**Đề xuất:** bổ sung quan hệ được làm rõ/bổ sung bởi ADR mới, phạm vi giai đoạn và repo sở hữu; giữ nguyên lý do quyết định lịch sử. Không đánh dấu toàn bộ Clean Architecture hay Modular Monolith là hết hiệu lực chỉ vì số project thay đổi.

## 4. Những điểm đúng và giới hạn còn lại

- Không phát hiện đích tệp/thư mục bị thiếu trong 145 liên kết Markdown cục bộ ngoài khối mã. Phép kiểm tra này không xác minh anchor, URL bên ngoài hoặc đường dẫn chỉ viết trong backtick. Tham chiếu tới context pack vẫn chỉ có trên máy này, không có trong bản clone sạch.
- 21 JSON bằng chứng sprint đọc được; các bản `verification-results.json` ghi SHA `249608e…` và ngày đánh giá. Không dùng kết quả cũ đó để kết luận mã hiện tại thất bại hoặc đã đạt.
- Manifest bộ ngữ cảnh không thiếu tệp. Bộ này đầy đủ về tệp nhưng lỗi thời về nhiệm vụ.
- JSON OpenAPI backend, OpenAPI AI dạng JSON-compatible YAML và fixtures phân tích được. Tính lại vector hash trong README AI bằng SHA-256 và độ dài UTF-8 big-endian cho đúng kết quả `e737e044…9d5dc02c`. Đây không thay thế kiểm thử mapper hoặc bộ validator schema đầy đủ.
- `.venv/`, `.cache/` và context pack được Git bỏ qua đúng quy tắc. Không phát hiện thêm tài liệu PDF/Word/HTML được theo dõi trong repo.
- ADR-011–014, tài liệu module/use case và workflow tương đối thống nhất về sáu use case, gateway chuyên biệt, NoOp, ranh giới module và CI không deploy.
- Runbook môi trường đang sửa đã có các chốt chặn thiếu trước đây: không lộ chuỗi kết nối, đối chiếu metadata, xác minh tài nguyên local, không coi dev DB là dùng một lần, gỡ migration có điều kiện, không tự chạy công cụ tài khoản. Không loại bỏ các chốt chặn này khi rút gọn tài liệu.
- `check-production` chỉ kiểm tra nhóm tài khoản mẫu; runbook đã nêu rõ không phải chứng nhận an toàn production và không mặc định chỉ đọc.
- Thư mục reviews/artifacts đã có chỉ mục giải thích tính lịch sử. Không cần xóa mọi nhắc tới frontend/Jenkins trong bằng chứng cũ; cần tránh đưa chúng trở lại thành hướng dẫn hiện hành.

**Chưa kiểm chứng trong lần này:** chạy thực tế các khối Bash/PowerShell, build/test .NET, PostgreSQL, image, smoke toàn hệ thống, checksum repo AI, tính khả dụng URL bên ngoài, phê duyệt của người có thẩm quyền và CI/branch protection hiện tại. Các kết quả chạy từ lượt trước không được tính là kết quả mới của báo cáo này. Không thực hiện lệnh database hoặc khởi chạy dịch vụ.

## 5. Thứ tự chỉnh sửa đề xuất

1. **Chỉnh chỉ mục và trạng thái nguồn:** giải quyết CTX-01 trước; dẫn tới ADR-014/module/use case; lập bảng hiện hành/đề xuất/lịch sử. Với tệp cũ, thêm chú thích phạm vi và nguồn mới mà không viết lại bằng chứng.
2. **Khóa bảng tương thích hiện hành:** ghi route, JSON, mười mã trạng thái, tạo Submitted, category chuỗi, giới hạn tiêu đề và quyền hiện tại. Dẫn tới mã/test bảo vệ; không phát sinh migration hay thay đổi API.
3. **Sửa chỉ dẫn kiến trúc/bảo mật:** chỉnh sơ đồ kiểm tra quyền và vị trí policy Ticketing; phân biệt role đã có với Lead/team/assignment chưa triển khai.
4. **Đồng bộ hướng dẫn làm việc:** thống nhất README, runbook, quy trình Git và ma trận kiểm thử; xử lý AGENTS.md cùng runbook đang sửa trong một thay đổi được review. Không tự commit trong nhiệm vụ đánh giá này.
5. **Đưa các bất đồng nghiệp vụ vào bảng quyết định:** Draft/Submit, quyền Agent, reply/resume, reopen/auto-close, attachment và retention. Chỉ cập nhật đặc tả sau quyết định; chưa triển khai các tính năng này.
6. **Bổ sung kiểm tra tài liệu nhẹ:** liên kết nội bộ, tệp được tham chiếu có trong Git, metadata trạng thái/phạm vi và liên kết tới tài liệu thay thế. Không chạy migration để kiểm tra hướng dẫn; không tự sửa baseline để vượt kiểm tra.

Điều kiện hoàn tất đợt chỉnh tài liệu: người đọc từ README hoặc AGENTS xác định được nguồn có hiệu lực; không còn yêu cầu đổi mã trạng thái/API trái baseline; chỉ dẫn bảo mật đúng ranh giới; lệnh local có một nguồn thống nhất; hồ sơ lịch sử giữ nguyên bằng chứng và được gắn phạm vi rõ. Chưa cần thay đổi kiến trúc mã nguồn để giải quyết các phát hiện tài liệu này.
