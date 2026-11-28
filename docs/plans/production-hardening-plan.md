# Kế hoạch củng cố production (Master Prompt)

> **Loại tài liệu:** đề xuất kế hoạch, chưa phải đặc tả đã duyệt. Bản làm việc được hiệu chỉnh ngày 28/11/2026 theo yêu cầu hoàn thiện master prompt; bản gốc còn trong lịch sử Git. Giữ nguyên 50 mã task G0–G7 để truy vết. Hành vi hiện hành theo [current-backend-contract.md](../architecture/current-backend-contract.md); tiến độ, SHA, kết quả và blocker chỉ ghi tại [production-hardening-state.md](production-hardening-state.md).

---

# MASTER PROMPT — SUPPORTFLOW AI BACKEND ARCHITECTURE REFACTORING, PRODUCTION HARDENING & CONTINUOUS VERIFICATION

# PHẦN 0. CÁCH ÁP DỤNG VÀ GIỚI HẠN ỦY QUYỀN

## 0.1. Nguồn yêu cầu và phạm vi phiên

Đọc yêu cầu mới nhất của người giao việc trước khi chọn task. Master prompt là danh mục công việc và điều kiện kiểm chứng; việc mở tệp này không tự giao thực hiện toàn bộ 50 task.

| Yêu cầu trong phiên | Phạm vi thực hiện |
| --- | --- |
| Phân tích, review hoặc chọn việc tiếp theo | Đọc nguồn, kiểm tra Git, xác minh vấn đề và đề xuất phạm vi; không tự sửa mã hoặc commit |
| Hoàn thiện kế hoạch/master prompt | Sửa tài liệu được giao và kiểm tra tài liệu; không bắt đầu sửa chức năng hay vận hành môi trường |
| Triển khai task/nhóm task đã giao | Thực hiện đến hết phạm vi đó, gồm kiểm thử, review và cập nhật trạng thái; tiếp tục phần độc lập khi gặp blocker |
| Giao thực hiện toàn bộ kế hoạch | Tiến theo dependency và mức ảnh hưởng; các chốt quyền thao tác bên ngoài dưới đây vẫn áp dụng |

Trong kho backend, áp dụng [AGENTS.md](../../AGENTS.md), [cẩm nang chung](../team-handbook.md), hợp đồng hiện hành và các quyết định đã duyệt đúng phạm vi. Khi làm ở kho khác, đọc hướng dẫn của kho đó. Đề xuất trong master prompt không tự thay đổi quyền, vòng đời phiếu, phạm vi MVP hoặc hợp đồng đã công bố.

Khi nguồn mâu thuẫn, ghi nguồn, ngày/SHA, trạng thái phê duyệt và tác động; hỏi đúng quyết định còn thiếu, rồi tiếp tục phần độc lập. Tự xử lý lựa chọn kỹ thuật thông thường trong phạm vi đã giao; không hỏi lại quyền đã được cấp cho cùng hành động và đích.

## 0.2. Chốt quyền và dữ liệu

- Kiểm tra nhánh, HEAD, index và working tree trước sửa. Giữ nhánh đã được chỉ định và thay đổi của người dùng; đã có worktree riêng thì không tạo thêm một worktree chỉ để lặp quy trình.
- Không tự push, tạo PR, hợp nhất, phát hành image hoặc triển khai nếu chưa được giao hành động tương ứng. Soạn thay đổi và bằng chứng có thể review trước khi cần quyết định cuối cùng về thao tác bên ngoài.
- Database dùng chung, staging, production, reset dữ liệu, migration Down và thao tác storage có thể mất dữ liệu cần ủy quyền rõ hành động, đích và tác động. Không suy ra quyền từ chữ “hardening”, `Development` hoặc địa chỉ `localhost`.
- Kiểm thử được tạo database/container dùng một lần thuộc phiên, sau khi xác minh Docker context local và quyền kiểm thử trong nhiệm vụ. Không tái sử dụng database, bucket/prefix hay container của dự án khác. Với database phát triển và EF có thể truy cập DB, làm phép kiểm host/cổng/tên database theo [hướng dẫn môi trường](../runbooks/environment-runbook.md), không in chuỗi kết nối hoặc lỗi EF thô.
- Không sửa/xóa migration đã phát hành hoặc đã áp dụng trong môi trường dùng chung. Xem cả Up, Down và snapshot; có dữ liệu mới thì phương án khôi phục có thể là migration sửa bổ sung, không mặc định chạy Down.
- Không yêu cầu gửi bí mật trong chat. Dùng môi trường/kho bí mật, không chép `.env` vào Git, artifact kiểm thử hoặc bản sao nguồn để build. Không tự kết nối AI thật ngoài task.
- API/Worker chỉ khởi chạy khi cần kiểm chứng; lưu PID hoặc tên/ID container, dùng timeout hữu hạn và dừng đúng tiến trình đã tạo. Không dừng dịch vụ của người dùng hay dọn Docker bằng lệnh toàn cục.
- Chỉ sửa `AGENTS.md` khi nhiệm vụ yêu cầu, hoặc đúng ngoại lệ cập nhật hướng dẫn cho cấu hình/lệnh được ghim trong hướng dẫn kho. Không thay instructions để né quality gate.

## 0.3. Bắt đầu hoặc tiếp tục phiên

1. Đọc tệp trạng thái duy nhất, xác định task được giao và các phần còn thiếu.
2. Đối chiếu Git/SHA/diff và kiểm tra môi trường liên quan; tìm checkout trước khi kết luận thiếu repository.
3. Phân biệt thiếu implementation, thiếu kiểm chứng và thiếu quyết định. `Implemented` chờ CI không có nghĩa là cần viết lại tính năng.
4. Chọn phần việc có thể thực hiện theo phần 18; mở rộng kiểm kê khi dependency hoặc hợp đồng thay đổi, không đọc lại toàn bộ codebase ở mỗi phiên.
5. Ghi phạm vi: tệp/module, caller, test/fixture, cấu hình, hợp đồng và kho bị ảnh hưởng; xác định kiểm tra phù hợp trước khi sửa.

Các nhãn P0/P1/P2 là mức ưu tiên, không phải bằng chứng lỗi còn tồn tại. Đối chiếu mã và trạng thái trước khi sửa; số test và baseline trong kế hoạch chỉ là mốc lịch sử.

# PHẦN 1. BỐI CẢNH VÀ MỤC TIÊU

## 1.1. Phạm vi dự án

Organization: https://github.com/supportflow-ai/

Các repository:

- Backend: `supportflow-ai/ai-it-service-desk`
- Frontend: `supportflow-ai/ai-it-service-desk-web`
- AI Service: `supportflow-ai/ai-service`
- Infrastructure: `supportflow-ai/supportflow-ai-infra`

Baseline tham chiếu từ đợt kiểm định ngày 28/11/2026:

- Backend: `main@1086d8d`.
- Kiến trúc: Modular Monolith kết hợp Module-first Clean Architecture.
- Backend sử dụng .NET 10, ASP.NET Core, EF Core và PostgreSQL.
- Các module chính: Identity, Ticketing, Knowledge, AIAssistance.
- Các host chính: API, Worker, Migrator.
- Object Storage: Cloudflare R2.
- Baseline từng ghi nhận 841 kiểm thử .NET thành công, 63 HTTP operations và 56 architecture tests.

Các số liệu trên chỉ là dữ liệu tham chiếu lịch sử, không mặc định là trạng thái mới nhất.

Trước khi thực thi, phải kiểm tra lại tên repository, nhánh, commit, mã nguồn, CI, tài liệu, API contract, database schema và các quyết định nghiệp vụ mới nhất. Nếu một vấn đề đã được sửa, chỉ cần kiểm chứng và ghi nhận bằng chứng, không sửa lại một cách dư thừa.

## 1.2. Mục tiêu tổng thể

Kế hoạch tổng thể hướng tới các kết quả sau; từng phiên thực hiện theo phạm vi đã giao ở phần 0:

1. Kiến trúc đúng đắn, rõ ràng, nhất quán và có khả năng mở rộng lâu dài.
2. Codebase sạch, đơn giản, dễ đọc, dễ kiểm thử và dễ bảo trì.
3. API đầy đủ trong phạm vi nghiệp vụ đã được phê duyệt.
4. Authentication và Authorization chính xác, an toàn.
5. Domain invariants được bảo vệ tại đúng tầng kiến trúc.
6. Dữ liệu được lưu bền, nhất quán và có khả năng phục hồi sau sự cố.
7. AI Triage và Human-in-the-Loop hoạt động thực tế, có thể kiểm chứng.
8. Không tồn tại hành vi thành công giả, silent failure hoặc fallback sai nghiệp vụ.
9. Các thành phần có khả năng xử lý concurrency, retry và process restart.
10. Bốn repository được đồng bộ về contracts, configuration và release.
11. Có bằng chứng kiểm thử độc lập đủ để nghiệm thu production.

Không rewrite toàn bộ hệ thống nếu kiến trúc hiện tại có thể được cải thiện từng bước.

Không coi việc tăng số lượng file, interface, design pattern, service hoặc project là cải thiện kiến trúc.

---

# PHẦN 2. VAI TRÒ VÀ PHƯƠNG PHÁP THỰC THI

## 2.1. Vai trò chuyên môn

Luân phiên thực hiện các vai trò sau:

### Vai trò A — Principal Software Architect

Chịu trách nhiệm:

- Phân tích kiến trúc hiện tại.
- Xác định ownership và dependency boundaries.
- Đánh giá sự phù hợp giữa kiến trúc và nghiệp vụ.
- Lựa chọn phương án đơn giản nhất nhưng vẫn bảo đảm correctness.
- Thiết kế refactor và phương án migration.
- Ngăn over-engineering.

### Vai trò B — Senior Backend Engineer

Chịu trách nhiệm:

- Viết regression tests.
- Chỉnh sửa mã nguồn.
- Hoàn thiện Application, Domain, Infrastructure và Presentation.
- Bảo vệ transaction, concurrency và data integrity.
- Tích hợp và kiểm thử thực tế.

### Vai trò C — Independent Architecture & Security Reviewer

Chịu trách nhiệm:

- Review implementation với tư duy độc lập.
- Không mặc định thiết kế của Builder là đúng.
- Tìm lỗi nghiệp vụ, race condition, authorization bypass và dependency violation.
- Kiểm tra test có thực sự chứng minh hành vi yêu cầu hay không.
- Yêu cầu sửa lại khi phát hiện thiếu sót.

Trong phạm vi được giao và khi công cụ cho phép, dùng reviewer độc lập cho nhóm thay đổi đáng kể. Reviewer nhận yêu cầu, mốc Git, toàn bộ diff liên quan và bằng chứng, làm việc chỉ đọc. Nếu không có reviewer độc lập, ghi rõ self-review và giới hạn; không tuyên bố đã có kiểm định bên ngoài. Review bằng sub-agent không thay phê duyệt PR của người khác theo quy trình nhóm.

### Vai trò D — QA, Reliability & Production Engineer

Chịu trách nhiệm:

- Unit, Integration, Contract, Architecture và E2E testing.
- Failure injection, concurrency và crash recovery.
- Security và performance verification.
- Migration, Docker, deployment, rollback và disaster recovery.
- Quản lý bằng chứng nghiệm thu.

## 2.2. Vòng lặp thực thi bắt buộc

Đối với từng task, thực hiện tuần tự:

1. DISCOVER: Đọc requirement, tài liệu, code và tests hiện tại.
2. DIAGNOSE: Xác định vấn đề bằng bằng chứng file, dòng code, class và method.
3. DESIGN: Đề xuất phương án ít phức tạp nhất.
4. TEST-FIRST: Với thay đổi chức năng, viết regression/characterization test phù hợp, quan sát lỗi đúng nguyên nhân rồi sửa; thiếu môi trường thì ghi phần chưa chứng minh, không dùng lỗi nạp DLL làm bằng chứng RED.
5. IMPLEMENT: Chỉnh sửa implementation với diff nhỏ, đúng ownership.
6. VERIFY: Chạy hợp các kiểm tra phù hợp với diff theo phần 12; kiểm exit code, discovery, Passed/Failed/Skipped và trạng thái lưu bền sau lỗi.
7. REVIEW: Chuyển sang vai trò reviewer, kiểm tra lại toàn bộ diff.
8. IMPROVE: Sửa các phát hiện mới và quay lại VERIFY.
9. CLEANUP: Dọn phần không còn dùng do task tạo ra và dừng tiến trình kiểm chứng của mình; không xóa fixture còn bảo vệ hành vi, artifact bằng chứng hay tệp của người dùng.
10. DOCUMENT: Cập nhật tài liệu canonical và trạng thái thực thi.
11. COMMIT: Trong task triển khai, commit tiếng Việt khi đạt gate tương ứng; chỉ stage tệp đã kiểm tra thuộc nhiệm vụ. Task chỉ phân tích/lập kế hoạch không tự commit.
12. RECHECK: Đối chiếu bằng chứng với commit và dirty diff. Kiểm tra CI khi có run trên đúng SHA; chưa có quyền push thì ghi thiếu CI, không tự push để tạo bằng chứng.

Lặp lại đến khi không còn vấn đề chưa được giải quyết trong phạm vi task, hoặc xác định được blocker thực sự cần quyết định nghiệp vụ, môi trường hay quyền truy cập.

Không được kết thúc vòng lặp chỉ vì implementation đã build thành công.

Không tự đặt số vòng lặp rồi coi việc chạy đủ số vòng là hoàn thành.

Với thay đổi chỉ tài liệu hoặc thay đổi hiển thị đơn giản, chạy kiểm tra tương ứng thay vì tạo test hình thức. Khi sửa tiếp làm thay đổi hành vi, phải kiểm chứng lại phần chịu ảnh hưởng; không chạy lặp một suite đã đạt nếu không có thay đổi, lỗi hoặc nghi vấn mới.

---

# PHẦN 3. CÁC NGUYÊN TẮC KIẾN TRÚC BẤT BIẾN

Mọi thay đổi phải tuân thủ đồng thời:

| Nguyên tắc | Quy định thực thi |
|---|---|
| KISS | Ưu tiên thiết kế đơn giản, ít moving parts |
| YAGNI | Không triển khai trước yêu cầu chưa được duyệt |
| Rule of Three | Không trừu tượng hóa duplication chưa ổn định |
| SoC | Phân tách transport, orchestration, domain, persistence và integration |
| SRP | Mỗi component có nhóm lý do thay đổi nhất quán |
| High Cohesion | Các chức năng cùng nghiệp vụ nằm trong cùng module |
| Low Coupling | Hạn chế knowledge và dependency giữa các module |
| Composition | Ưu tiên constructor injection và composition |
| DIP | Application phụ thuộc abstraction phù hợp, không phụ thuộc concrete adapter |
| CQS | Query không thực hiện business mutation |
| Fail Fast | Chặn invalid state và invalid configuration đúng thời điểm |
| POLA | Hành vi nhất quán với tên gọi, API contract và nghiệp vụ |

Đồng thời áp dụng SOLID ở mức thực tế, không máy móc.

### Các điều cấm

- Không tự chuyển Modular Monolith sang Microservices.
- Không bổ sung Kafka, RabbitMQ, Redis, MediatR hoặc Event Sourcing nếu chưa có nhu cầu được chứng minh.
- Không tạo generic repository cho mọi entity.
- Không tạo generic Unit of Work nếu EF Core và transaction boundary hiện tại đã đủ.
- Không xây dựng framework nội bộ chỉ để giảm vài dòng code.
- Không tạo interface cho mọi class.
- Không chia class chỉ vì vượt một số dòng tùy ý.
- Không tạo base class phức tạp để tái sử dụng code cưỡng ép.
- Không làm domain trở thành anemic model.
- Không dùng SharedKernel làm nơi chứa mọi thứ dùng chung.
- Không để một module truy cập trực tiếp persistence nội bộ của module khác.
- Không vô hiệu hóa authorization, validation hoặc architecture tests để CI thành công.
- Không sửa test expectation chỉ để phù hợp với implementation sai.
- Không tự thay đổi public contract nếu chưa phân tích consumer.
- Không xóa migration đã phát hành.
- Không sửa production database hoặc triển khai production khi chưa được cấp quyền.

Rule of Three không ngăn việc tạo abstraction cần thiết để bảo vệ dependency boundary, security boundary hoặc khả năng kiểm thử.

---

# PHẦN 4. G0 — BASELINE VÀ QUẢN TRỊ YÊU CẦU

## G0-01. Kiểm kê repository và thiết lập trạng thái thực thi

**Ưu tiên: P0**

### Công việc

- Kiểm tra Git status của cả bốn repository.
- Fetch remote nếu có quyền đọc và kết nối; xác định nhánh liên quan thay vì mặc định nhánh mới nhất là baseline. Chưa fetch được thì ghi độ mới của refs local, không tự pull/merge.
- Đối chiếu `main`, `dev`, release branch và feature branches.
- Kiểm tra commit SHA, divergence, untracked files và local modifications.
- Đọc `AGENTS.md`, README, nguồn canonical và ADR/workflow/Dockerfile/contract liên quan. Kiểm kê tổng thể ở lần đầu; các phiên tiếp theo đọc phần thay đổi và dependency của task.
- Xác định canonical requirements và tài liệu đã lỗi thời.
- Chạy baseline build và test trước khi chỉnh sửa.
- Ghi nhận các test đang fail từ trước.

Trong repository này chỉ dùng hai tệp, không tạo thêm sổ tiến độ thay thế:

```text
docs/plans/production-hardening-plan.md
docs/plans/production-hardening-state.md
```

Tệp plan chứa yêu cầu và phương pháp; tệp state chứa tiến độ, SHA, review, bằng chứng và blocker. Artifact log/TRX/JSON có thể tách riêng và được dẫn từ state; không chép kết quả vào nhiều nguồn canonical.

### Kiểm thử và nghiệm thu

- Xác định được chính xác baseline SHA của mỗi repo.
- Có kết quả build/test ban đầu.
- Có danh sách vấn đề đã xác minh và vấn đề cần xác minh.
- Không ghi đè thay đổi local của người dùng.
- Không đánh dấu task Verified nếu thiếu bằng chứng.

## G0-02. Xây dựng ma trận truy vết toàn bộ API

**Ưu tiên: P0**

### Công việc

- Liệt kê endpoint từ runtime registration và OpenAPI thực tế.
- Đối chiếu toàn bộ HTTP operations với requirements.
- Kiểm tra endpoint nào thiếu, dư thừa, chưa hoàn thiện hoặc không còn consumer.
- Xác định Application use case, authorization, resource scope, transaction, response DTO và error contract của từng endpoint.

Lập ma trận:

```text
Endpoint
→ Requirement
→ Module ownership
→ Use case
→ Authorization
→ Resource scope
→ Domain invariants
→ Persistence
→ API response
→ Positive tests
→ Negative tests
→ Integration tests
→ Frontend consumer
→ Implementation status
```

### Kiểm thử và nghiệm thu

- Mọi endpoint trong MVP đều có requirement tương ứng.
- Không có API nghiệp vụ thiếu authorization hoặc test.
- Không có endpoint chỉ xuất hiện trong Swagger nhưng chưa thực hiện đúng nghiệp vụ.
- Các API bị thiếu được phân loại theo mức ưu tiên.
- Chỉ bổ sung API thực sự thuộc phạm vi đã duyệt.

## G0-03. Xác định và bảo vệ các quyết định nghiệp vụ

**Ưu tiên: P0**

### Công việc

Đọc lại các quyết định DEC và AI mới nhất, đặc biệt:

- DEC-01: Draft/Submit.
- DEC-02: Agent scope và assignment.
- DEC-05: Resolve, Reopen, Close và Auto-close.
- DEC-06: Presigned URL.
- DEC-07: Attachment limits, scanning và retention.
- DEC-08: Release acceptance.
- DEC-09: Triaged status.
- AI-01: Quyền xem AI suggestions.
- AI-02: Local ML và Human-in-the-Loop.
- AI-03: Thời điểm kích hoạt AI.
- AI-05: Impact/Urgency suggestions.
- AI-07: Model acceptance metrics.
- AI-09: Knowledge indexing.

Phân loại mỗi quyết định thành Approved, Pending, Superseded hoặc Unknown.

### Kiểm thử và nghiệm thu

- Mỗi quyết định đã duyệt có acceptance criteria.
- Không tự sáng tạo business rule mới.
- Công việc thiếu quyết định nghiệp vụ phải được đánh dấu Blocked và tiếp tục các công việc độc lập khác.
- Nếu tài liệu mâu thuẫn, ghi rõ nguồn và yêu cầu xác nhận thay vì chọn tùy ý.

---

# PHẦN 5. G1 — CRITICAL CORRECTNESS VÀ AUTHORIZATION

## G1-01. Refactor AI Triage Authorization

**Ưu tiên: P0 — Release blocker**

### Kiểm tra

Đọc toàn bộ:

- `AiTriageEndpoints`.
- `RequestAiTriageUseCase`.
- `GetTicketTriageUseCase`.
- `DecideAiTriageUseCase`.
- `ITicketTriageBridge`.
- Cài đặt `ITicketTriageBridge` đang được DI đăng ký (hiện là `TicketTriageBridge`); không tạo lại adapter cũ chỉ vì tên có trong kế hoạch.
- `TicketPermissions`.
- `TicketReadScope`.
- Các Ticketing commands có authorization hiện hành.

### Thay đổi

- Loại bỏ việc sử dụng `TicketReadScope.AllTickets()` vô điều kiện trong AI flow.
- Không chỉ kiểm tra Agent/Admin role tại HTTP endpoint.
- Chuyển quyền xác định resource scope về Ticketing.
- AIAssistance gọi published contract của Ticketing để kiểm tra và thực hiện thao tác.
- Sử dụng authenticated actor từ `ICurrentUser`.
- Không tin tưởng reviewer ID do request truyền lên.
- Kiểm tra quyền tại Application boundary, kể cả khi use case được gọi ngoài HTTP.
- Kiểm tra lại authorization tại thời điểm thực hiện mutation.
- Với quyết định HITL mới, giữ phạm vi phiếu/nhóm/thành viên ổn định tới commit, kể cả Reject chỉ ghi decision. Ghi rõ cách tuần tự hóa với thu hồi thành viên, vô hiệu hóa nhóm và chuyển nhóm.
- Bảo vệ public/private projection của AI data.

### Củng cố kiến trúc

Ticketing sở hữu Ticket access policy. AIAssistance không được sao chép hoặc tự diễn giải một policy khác.

Không truyền DbContext hoặc Ticket entity qua module boundary.

### Kiểm thử bắt buộc

Anonymous, Requester, Agent cùng nhóm, Agent ngoài nhóm, Agent được phân công trực tiếp, Team Lead và Admin; người dùng bị thu hồi quyền; nhóm bị vô hiệu hóa; Ticket chuyển nhóm; giả mạo actor ID; gọi trực tiếp Application use case.

Kiểm thử trên cả ba AI operations.

### Điều kiện hoàn thành

Không tồn tại đường đọc hoặc ghi Ticket thông qua AI mà vượt qua Ticketing authorization policy.

## G1-02. Refactor CQS của AI Triage

**Ưu tiên: P0**

### Thay đổi

- Loại bỏ việc GET triage tự kích hoạt AI classification.
- Loại dependency command khỏi `GetTicketTriageUseCase`.
- GET chỉ truy xuất analysis đã tồn tại.
- POST `ai-analyze` chịu trách nhiệm yêu cầu phân tích.
- POST `triage/decision` chịu trách nhiệm quyết định HITL.
- Giữ manual triage độc lập với AI.
- Chuẩn hóa trạng thái chưa có analysis hoặc AI unavailable.

### Củng cố kiến trúc

- Query không gọi mutation port.
- Không đưa command orchestration vào projection hoặc repository query.
- Cho phép technical telemetry thông thường, nhưng không phát sinh business mutation.

### Kiểm thử bắt buộc

- GET không gọi classifier.
- GET không ghi database.
- GET lặp lại không tạo analysis.
- GET khi AI Service không khả dụng vẫn đọc được dữ liệu đã lưu.
- POST là đường kích hoạt AI rõ ràng.
- Frontend không phụ thuộc side effect của GET.

## G1-03. Sửa Accept khi analysis không hợp lệ

**Ưu tiên: P0**

### Thay đổi

- Loại bỏ fallback `Impact.Medium` và `Urgency.Medium` nếu không có suggestion hợp lệ.
- Không Accept analysis không tồn tại, thất bại, hết hiệu lực hoặc thuộc Ticket khác.
- Không áp dụng suggestion có enum, score hoặc dữ liệu không hợp lệ.
- Phân biệt missing, unavailable, abstained và partial suggestions theo contract.
- Không báo thành công khi Ticket mutation thất bại.
- Không tự bổ sung giá trị nghiệp vụ mà AI hoặc reviewer chưa cung cấp.

### Kiểm thử bắt buộc

Accept không có analysis; failed analysis; empty suggestions; unknown enum; partial suggestion; stale suggestion; analysis thuộc Ticket khác; manual fallback; không phát sinh audit thành công hoặc mutation khi request bị từ chối.

## G1-04. Sửa Category không được áp dụng

**Ưu tiên: P0**

### Thay đổi

- Kiểm tra `ApplyTriageDecisionAsync` và việc sử dụng `categoryId`.
- Chuẩn hóa Category code/ID giữa Backend, AI Service và Frontend.
- Sử dụng domain method hoặc Ticketing command hợp lệ để cập nhật Category.
- Giữ Category, Impact, Urgency và Priority nhất quán.
- Không tự động thay đổi assignment hoặc routing nếu chưa có business rule.

### Kiểm thử bắt buộc

Accept Category; Override Category; Category không tồn tại; mapping sai; priority calculation; audit trước/sau; rollback khi Category update lỗi.

## G1-05. Loại bỏ Auto-close ngoài phạm vi MVP

**Ưu tiên: P0**

### Thay đổi

Nếu DEC-05 vẫn quy định MVP không auto-close:

- Loại đăng ký `AutoCloseResolvedTicketsWorker`.
- Kiểm tra các AutoClose use cases, ports, adapters và configuration.
- Loại mã không còn consumer.
- Cập nhật runtime docs.
- Giữ migration và audit history đã phát hành.

Nếu quyết định mới đã cho phép auto-close, không xóa tính năng mà phải kiểm định implementation theo policy mới.

### Kiểm thử bắt buộc

- Ticket Resolved quá 72 giờ không tự chuyển Closed nếu auto-close không được duyệt.
- Manual Close đúng quyền.
- Reopen đúng policy.
- Closed là terminal state.
- Production DI không đăng ký feature ngoài phạm vi.

---

# PHẦN 6. G2 — DURABLE AI TRIAGE VÀ HUMAN-IN-THE-LOOP

## G2-01. Thay InMemory AI persistence bằng PostgreSQL

**Ưu tiên: P0 — Release blocker**

### Kiểm tra

- `IAiAnalysisStore`.
- Adapter đang được `AIAssistanceModule` đăng ký; `InMemoryAiAnalysisStore` nếu còn trong mã. Đã lưu PostgreSQL thì kiểm định phần hiện có, không triển khai lại từ đầu.
- `AIAnalysis`, `AISuggestion` và các domain types liên quan.
- `AIAssistanceModule`.
- Shared DbContext, EF mappings và migrations.

### Thay đổi

- Thiết kế PostgreSQL persistence thuộc ownership AIAssistance.
- Tạo analysis ID độc lập với Ticket ID.
- Lưu ticket revision, input hash, execution outcome, validation errors, suggestions và timestamp. Lưu model/provider provenance nếu nguồn trả về; thiếu metadata phải ghi là chưa có, không tự gán version giả. Mở rộng hợp đồng nguồn thuộc G2-06 và cần đồng bộ consumer.
- Bảo toàn history khi phân tích lại.
- Lưu HITL decision, reviewer và reason.
- Bổ sung constraints và indexes cần thiết.
- Thêm EF Core configurations và forward migration.
- Thay production DI bằng PostgreSQL adapter.
- Chỉ giữ InMemory implementation trong test nếu thực sự cần.

Cân nhắc mô hình:

```text
ai_analyses
ai_suggestions
ai_triage_decisions
```

Không mặc định phải tạo đủ ba bảng nếu domain hiện tại có thể biểu diễn lịch sử và invariants bằng mô hình đơn giản hơn.

### Kiểm thử bắt buộc

Persistence trên PostgreSQL thật; history preservation; restart API; multi-replica reads; concurrent writes; constraints; migration từ schema cũ; EF model snapshot; production DI.

### Điều kiện hoàn thành

Không mất analysis hoặc decision khi process restart, không phụ thuộc replica đã nhận request.

## G2-02. Hoàn thiện revision, stale analysis và idempotency

**Ưu tiên: P0**

### Thay đổi

- Quyết định HITL phải tham chiếu chính xác `analysisId`.
- Xác minh analysis thuộc đúng Ticket.
- Giữ concurrency contract hiện hành: Accept/Override bắt buộc `expectedRevision` khác 0; Reject giữ revision tùy chọn nhưng phải kiểm nếu được gửi. Chỉ đổi chính sách này trong thay đổi hợp đồng được giao rõ.
- Kiểm tra input hash của nội dung đã phân tích.
- Phân biệt revision của snapshot phân tích với revision mong đợi của lệnh ghi. Không coi mọi thay đổi revision là đổi nội dung AI nếu hợp đồng hiện hành dùng hash cho validity; vẫn phải bảo vệ xung đột khi ghi phiếu.
- Chặn stale analysis khi dữ liệu liên quan đã thay đổi.
- Không âm thầm thay analysis người dùng đã xem bằng latest analysis.
- Xác định chính sách khi cùng một analysis nhận nhiều quyết định.
- Bảo vệ replay bằng durable uniqueness hoặc idempotency mechanism phù hợp.
- Quyết định thành công không được áp dụng hai lần.
- Replay phải so người duyệt từ actor, decision, revision gốc và các giá trị/lý do chuẩn hóa đã chốt. Replay đúng sau commit không ghi lần hai; đổi payload phải bị từ chối. Không dùng revision mới của phiếu thay revision gốc đã lưu trong receipt/decision.
- Phân biệt Override có `analysisId` với phân loại thủ công không có analysis. Không tự gắn fallback thủ công vào latest analysis hoặc tạo lịch sử AI giả.

### Kiểm thử bắt buộc

Ticket thay đổi sau phân tích; analysis cũ; analysis thuộc Ticket khác; hai Agent quyết định đồng thời; replay cùng payload; replay khác payload; revision conflict; Ticket đóng trước quyết định; authorization bị thu hồi.

## G2-03. Refactor transaction boundary của HITL

**Ưu tiên: P0 — Release blocker**

### Thay đổi

Thiết lập một transaction boundary rõ ràng cho HITL command:

```text
Validate actor and request
→ Begin transaction
→ Revalidate resource scope
→ Validate analysis and revision
→ Apply Ticket domain mutation
→ Persist AI decision
→ Persist business audit
→ Commit
```

- Ticketing sở hữu Ticket mutation.
- AIAssistance sở hữu analysis và decision.
- Sử dụng shared PostgreSQL transaction nếu kiến trúc hiện tại hỗ trợ.
- Bảo đảm các adapter tham gia cùng transaction.
- Kiểm chứng bằng DI thực rằng các adapter dùng đúng một DbContext scoped và cùng giao dịch; không chỉ suy ra từ tên interface hoặc lifetime khai báo.
- Loại bỏ transaction nesting sai.
- Không để `SaveChanges` trước đó làm commit riêng ngoài boundary.
- Không gọi AI Service hoặc external HTTP trong transaction.
- Xác định lock ordering và concurrency strategy.
- Đối chiếu thứ tự khóa đang có (analysis → phiếu → phạm vi nhóm/thành viên) với lệnh khác; không tăng isolation toàn hệ thống để che race. Khóa quá hạn phải theo error contract, gồm trường hợp provider bọc lỗi PostgreSQL trong ngoại lệ khác.

Chỉ tạo specialized transaction port nếu thực sự cần bảo vệ DIP hoặc coordination boundary.

### Kiểm thử bắt buộc

Failure injection:

- Lỗi trước Ticket mutation.
- Lỗi sau Ticket mutation.
- Lỗi lưu AI decision.
- Lỗi lưu audit.
- Database connection loss.
- Revision conflict.
- Cancellation trước commit.
- Commit thành công nhưng response bị mất.
- Replay sau restart.

### Điều kiện hoàn thành

Ticket, AI decision và audit cùng commit hoặc cùng rollback. Không tồn tại partial success.

## G2-04. Hoàn thiện AI analysis lifecycle

**Ưu tiên: P1**

### Thay đổi

Phân biệt:

- Analysis execution outcome.
- Suggestion validity.
- Human decision status.
- Stale state.
- Manual fallback.

Xử lý timeout, malformed response, empty suggestions, partial suggestions, cancellation và unavailable dependency.

- Outcome đã lưu/đọc lại phải giữ đúng ý nghĩa; không đổi Timeout/InvalidResponse thành Unavailable sau restart. Giữ validation errors có kiểm soát, không lưu raw response hoặc thông tin nhạy cảm ngoài phạm vi.
- Khi nội dung đổi trong lúc chờ AI, giữ snapshot/hash/revision đã phân tích và xác định stale state tại điểm đọc trả kết quả. Không trả `isStale = false` cố định; không đồng nhất “AI chạy thành công” với “gợi ý còn hiệu lực”.
- Kiểm tra quyền khi trả dữ liệu theo hợp đồng; không mở transaction DB trong suốt cuộc gọi AI. Timeout/concurrency có giới hạn và manual fallback phải kiểm được độc lập.

Nếu synchronous analysis với bounded timeout/concurrency đủ đáp ứng, giữ thiết kế đó. Không bổ sung distributed job orchestration chỉ vì AI có thể chạy nền.

### Kiểm thử bắt buộc

AI timeout; HTTP 5xx; malformed JSON; invalid enum; invalid confidence; empty suggestions; cancellation; Ticket đổi nội dung trong lúc phân tích; retry; manual fallback; không ghi dữ liệu nhạy cảm vào log.

## G2-05. Hoàn thiện AI audit trail

**Ưu tiên: P0**

### Thay đổi

Mỗi quyết định cần truy vết được:

- Ticket ID và analysis ID.
- Ticket revision và input hash.
- Model/provider version nếu có.
- Original suggestions.
- Accept, Override hoặc Reject.
- Reviewer.
- Reason.
- Previous và applied business values.
- Decision timestamp và outcome.

Reject phải lưu lịch sử nhưng không thay đổi Ticket business fields.

Không giả lập audit hệ thống thành bình luận của người dùng.

Áp dụng visibility theo hợp đồng hiện hành: audit triage/lý do Override là nội bộ, kể cả phân loại thủ công không có analysis. Kiểm tra cả timeline và projection, không chỉ DTO triage. Nếu cần thông báo công khai thì dùng nội dung trung tính theo chính sách đã duyệt, không làm mất audit chi tiết hoặc lộ lý do nội bộ.

### Kiểm thử bắt buộc

Accept audit; Override audit; Reject audit; failed decision; immutable history; requester projection; authorization đối với audit; không lộ token, secret hoặc dữ liệu nội bộ không được phép.

## G2-06. Đồng bộ Backend — AI Service — Frontend contracts

**Ưu tiên: P0**

### Thay đổi

- Đọc contract AI hiện tại và OpenAPI baseline.
- Xác định canonical owner của từng contract.
- Đồng bộ enum mapping, optional fields, analysis ID và revision.
- Khi cần model metadata, đối chiếu producer/consumer và thống nhất mở rộng v1 hay version mới; không tự thêm trường vào closed contract hoặc giả định AI Service đã trả dữ liệu đó.
- Không ngầm default unknown enum.
- Cập nhật generated Frontend types.
- Giữ backward compatibility khi khả thi; nếu breaking change, lập coordinated release plan.
- Rà nullable, kiểu số/chuỗi của enum và các schema tham chiếu, kể cả `$ref`/`allOf`. Khi hai hình chiếu có tập trường giao nhau, dùng biểu diễn hợp lệ như `anyOf`, không dùng `oneOf` làm phản hồi khớp cả hai bị coi là sai.
- Xuất baseline OpenAPI chỉ sau khi review thay đổi có chủ đích; tắt chế độ xuất và chạy lại kiểm tra. Web cập nhật snapshot, checksum, SHA/trạng thái nguồn và kiểu sinh trên đúng nguồn backend. Commit local chưa push phải được ghi rõ.
- Đồng bộ snapshot/kiểu chưa hoàn thành tích hợp UI. Kiểm tra caller thực tế dùng `/triage`, `/ai-analyze`, `/triage/decision`, quyết định theo `analysisId` toàn analysis và revision đúng; loại route dự kiến/quyết định từng suggestion trước khi bật capability. API 403/404 phải làm mất dữ liệu cũ khỏi giao diện; 409/503 không tự retry mutation.

### Kiểm thử bắt buộc

JSON fixtures hai chiều; contract validation; enum mapping; nullable reference; cả hai hình chiếu GET; partial/empty suggestions; incompatible version; Frontend conflict handling; OpenAPI diff; checksum của contract được đồng bộ. Bằng chứng UI dùng mock không thay E2E với backend thật của G7-02; không bật panel chỉ vì codegen đạt.

---

# PHẦN 7. G3 — IDENTITY VÀ SECURITY HARDENING

## G3-01. Refactor JWT validation thành fail-closed

**Ưu tiên: P1 — Security critical**

### Thay đổi

- Kiểm tra JWT validation và security stamp validation trong Identity.
- Loại bỏ đường bypass khi thiếu DbContext hoặc validator.
- Từ chối user không tồn tại, token sai hoặc security stamp không hợp lệ.
- Kiểm tra cả token đã qua xác minh chữ ký nhưng thiếu stamp và user không tồn tại. Giữ các kiểm tra signature, issuer, audience, lifetime của nền tảng; không coi đây là lý do thay toàn bộ JWT handler.
- Tách validator khỏi DI registration nếu có lợi cho SRP và kiểm thử.
- Đối chiếu đường cấp token của Login/Refresh/Register và fixture. Không thêm ngoại lệ thiếu stamp để giữ test hoạt động. Fixture cho ca nghiệp vụ thành công phải dựng danh tính hợp lệ; ca token/subject không hợp lệ phải kiểm từ chối. Giữ các assertion về nghiệp vụ và thêm hồi quy cho đường bypass.
- Thiếu dịch vụ bắt buộc là lỗi cấu hình/DI; database lỗi là lỗi dependency khi chạy. Cả hai đều không được xác thực thành công. Đối chiếu HTTP error contract của middleware và endpoint, không mặc định biến lỗi hạ tầng thành token sai hoặc lộ exception.
- Nếu có token cũ thực tế cần chuyển đổi, xác minh nguồn, TTL, ảnh hưởng phiên và quyết định rollout. Không mặc định có grace period bỏ kiểm stamp; không trì hoãn bản vá độc lập chỉ để thiết kế cơ chế tương thích chưa có nhu cầu.
- Không tự triển khai JWT framework riêng.

### Kiểm thử bắt buộc

Invalid signature, issuer, audience, expiry; missing stamp; stale stamp; deleted user; role change; password change; database failure; missing service; cross-replica validation.

Các ca kiểm dữ liệu Identity/thu hồi thực chạy PostgreSQL; test transport cô lập không thay bằng chứng đó. Khi đổi setup của test liên quan, giải thích vì sao setup cũ không còn đại diện cho caller hợp lệ, không giảm số ca hoặc nới kỳ vọng để đạt CI.

## G3-02. Kiểm định refresh token và session lifecycle

**Ưu tiên: P1**

### Thay đổi

- Kiểm tra rotation, revoke và token family hiện hành.
- Bảo đảm refresh token chỉ được sử dụng thành công một lần.
- Kiểm tra replay detection và atomic update.
- Đối chiếu logout, password reset, role change và session revocation.
- Chuẩn hóa access/refresh token TTL giữa appsettings và Infra.
- Chỉ refactor phần chưa đáp ứng invariants.

### Kiểm thử bắt buộc

Concurrent refresh; refresh/revoke race; replay; expired token; revoked session; password reset đồng thời refresh; restart; multi-replica.

## G3-03. Xây dựng durable OTP delivery

**Ưu tiên: P1**

### Thay đổi

Nếu password reset thuộc production scope:

Đối chiếu G3-02/G3-04 và chính sách phát mã trước khi chọn mô hình job; durable delivery không được làm suy yếu single-use, expiry hoặc session revocation.

- Thay hàng đợi OTP chỉ tồn tại trong RAM bằng durable job phù hợp.
- Ưu tiên PostgreSQL-backed job hoặc outbox chuyên biệt.
- Ghi công việc bền vững trước khi xác nhận đã tiếp nhận.
- Có claim/lease nếu nhiều worker xử lý.
- Có bounded retry, next-attempt time và expiry.
- Không gửi OTP đã hết hạn.
- Không lưu OTP plaintext.
- Thiết kế cách bảo vệ payload hoặc phát OTP nhất quán khi retry.
- Xác định rõ transaction giữa phát mã, bản ghi xác minh và công việc gửi. Bản băm một chiều không dùng để khôi phục OTP gửi lại; payload có thể phục hồi phải được bảo vệ, giới hạn lưu giữ và quản lý khóa phù hợp. Không tạo mã mới ở mỗi lần retry nếu làm mất hiệu lực mã người dùng vừa nhận trái policy.
- Giữ phản hồi chung để chống account enumeration.
- Phân biệt lỗi SMTP tạm thời và vĩnh viễn.
- Không tuyên bố exactly-once SMTP delivery.
- Nêu hành vi khi SMTP thiếu hoặc không khả dụng theo scope/config đã duyệt. Phản hồi chống enumeration không phải bằng chứng thư đã gửi; không dùng NoOp để nghiệm thu delivery bắt buộc.

### Kiểm thử bắt buộc

API restart; Worker crash trước/sau SMTP send; concurrent claim; SMTP timeout; retry; permanent failure; expired OTP; queue overload; duplicate delivery; unknown email; SMTP chưa cấu hình.

## G3-04. Hardening OTP và reset password

**Ưu tiên: P1**

### Thay đổi

- Sử dụng cryptographically secure random.
- Bảo vệ OTP entropy thấp bằng HMAC hoặc cơ chế keyed verification thích hợp.
- Dùng secret chuyên biệt và constant-time comparison.
- Kiểm tra TTL, attempt limits và issuance limits.
- OTP chỉ được sử dụng một lần.
- OTP mới vô hiệu hóa OTP cũ theo policy.
- Đổi password và tiêu thụ OTP trong transaction phù hợp.
- Thu hồi session sau password reset.

### Kiểm thử bắt buộc

Correct/incorrect OTP; expired/consumed OTP; wrong account; wrong purpose; old OTP; brute-force limits; concurrent reset; persistence failure; missing secret; log redaction.

## G3-05. Refactor rate limiting

**Ưu tiên: P1**

### Thay đổi

- Loại bỏ hành vi reset toàn bộ active counters khi đạt partition limit.
- Giữ giới hạn bộ nhớ có kiểm soát.
- Làm admission, kiểm giới hạn và tạo partition nhất quán khi có request đồng thời; `Count` rồi `GetOrAdd` riêng lẻ không chứng minh được giới hạn cứng. Không xóa counter vừa được gia hạn giữa lúc kiểm expiry và eviction.
- Khi đầy, giữ counter còn hiệu lực; chọn cơ chế từ chối/quá tải có giới hạn theo contract và ghi đánh đổi khả dụng. Không thay `Clear()` bằng eviction active counter hoặc cho qua không giới hạn.
- Kiểm độ dài khóa và dữ liệu đầu vào trước khi giữ trong bộ nhớ; chỉ giới hạn số khóa chưa chứng minh được giới hạn RAM. Tận dụng clock hiện có để kiểm expiry không cần sleep dài.
- Chuẩn hóa email partition key.
- Kiểm định forwarded headers và trusted proxy configuration.
- Phân biệt gateway rate limiting với authoritative security controls.
- Dùng PostgreSQL/shared state nếu cần giới hạn chính xác giữa replica.
- Phân biệt ngân sách per-instance hiện hành với yêu cầu giới hạn toàn cụm. Hai instance phải được kiểm theo phạm vi đã chốt; không tự công bố limiter trong RAM là authoritative global limit. Nếu cần shared state, ưu tiên nền tảng PostgreSQL đã có và phân tích transaction/chi phí trước khi thêm hạ tầng.
- Chỉ bổ sung Redis nếu có bằng chứng cần thiết.

### Kiểm thử bắt buộc

429; Retry-After; partition expiry; full partition capacity; giữ ngân sách email đã bị chặn sau khi nhiều email mới xuất hiện; concurrent admission/expiry; case-insensitive email; independent operation budgets; khóa quá dài; two replicas; proxy spoofing; memory growth.

## G3-06. Kiểm định toàn bộ Identity APIs

**Ưu tiên: P1**

### Công việc

Kiểm tra registration policy, email confirmation, profile update, user directory, department assignment, role management và last-admin protection.

Không tự bổ sung invitation, email verification hoặc account administration ngoài phạm vi đã duyệt.

### Kiểm thử bắt buộc

Mọi Identity endpoint theo role; mass assignment; role escalation; invalid department; disabled account nếu được hỗ trợ; concurrent admin updates; không trả password hash, security stamp, OTP hash hoặc refresh token hash trong API response.

---

# PHẦN 8. G4 — TICKETING, SLA, WORKER, ATTACHMENT VÀ KNOWLEDGE

## G4-01. Chuẩn hóa Ticket lifecycle

**Ưu tiên: P1**

### Thay đổi

- Lập state transition matrix canonical.
- Đối chiếu toàn bộ Ticket commands với requirement.
- Kiểm tra Create, Update, Assign, Unassign, Transfer, Claim, Pending, Resume, Resolve, Reopen, Close và Cancel.
- Bảo vệ invariant tại Ticket aggregate.
- Không để endpoint hoặc repository tự thay đổi trạng thái ngoài domain.
- Xử lý Triaged theo DEC-09, không tự diễn giải enum.
- Chuẩn hóa revision và audit cho state transitions.

### Kiểm thử bắt buộc

Parameterized tests cho source state, target state, role, resource scope, invalid transition, revision conflict và terminal state.

## G4-02. Hoàn thiện concurrency và idempotency của Ticket commands

**Ưu tiên: P1**

### Thay đổi

- Kiểm định existing idempotency keys.
- Kiểm tra optimistic concurrency và PostgreSQL row locks.
- Xác định transaction boundary cho từng mutation.
- Bảo vệ consistency giữa Ticket, assignment, timeline và audit.
- Kiểm tra database commit ambiguity.
- Chuẩn hóa lock ordering.
- Không tăng isolation level toàn hệ thống nếu không cần thiết.

### Kiểm thử bắt buộc

Duplicate Create; same key/different payload; competing Claim; Assign/Transfer race; Update/Resolve race; Close/Reopen race; audit failure; cancellation; retry sau mất response.

## G4-03. Củng cố Support Team, routing và RBAC

**Ưu tiên: P1**

### Thay đổi

Ticketing phải sở hữu policy thống nhất cho:

- Active team.
- Active membership.
- Team Lead.
- Direct assignment.
- Team transfer.
- Ticket detail.
- Internal notes.
- Attachments.
- Manual triage.
- AI triage.

Không sao chép policy sang từng adapter hoặc module.

Team Lead được xác định theo membership/policy của ADR-016 và hợp đồng hiện hành; không tự thêm role JWT hay đổi các vai trò đã lưu để khớp tên trong checklist.

### Kiểm thử bắt buộc

Hai Support Team, nhiều Agent, Requester, Team Lead và Admin; chuyển nhóm; thu hồi membership; vô hiệu hóa team; assignment khác nhóm; Agent đồng thời là owner; internal note visibility; attachment visibility.

## G4-04. Refactor SLA domain có kiểm soát

**Ưu tiên: P1**

### Thay đổi

- Viết characterization tests trước khi refactor.
- Kiểm định First Response SLA, Resolution SLA, pause, resume, breach, priority change và reopen.
- Tách phép tính thời gian thuần túy nếu có cohesion riêng.
- Giữ Ticket aggregate kiểm soát mutation.
- Sử dụng clock abstraction hiện hành.
- Không thay đổi business semantics khi chỉ refactor.

### Kiểm thử bắt buộc

Ngay trước, đúng và sau deadline; multiple pause/resume; priority change; reopen; resolved/closed; UTC; deterministic clock; domain/report consistency.

## G4-05. Hardening Background Worker

**Ưu tiên: P1**

### Thay đổi

- Không chuyển exception thành kết quả zero-success gây hiểu nhầm.
- Phân biệt no work, success, partial failure và failure.
- Truyền cancellation đúng cách.
- Bounded retry/backoff.
- Batch size hợp lý.
- Không overlapping sweep ngoài chủ đích.
- Bảo vệ concurrent workers.
- Ghi last successful execution, processed, failed và conflicts.
- Không xây dựng scheduler framework nếu chưa cần.

### Kiểm thử bắt buộc

Worker restart; database outage; partial batch failure; concurrent workers; graceful shutdown; cancellation; retry; stale execution status; không nhân đôi business effects.

## G4-06. Chuẩn hóa Attachment policy và R2 configuration

**Ưu tiên: P1**

### Thay đổi

- Loại hardcoded upload/download TTL trong use cases.
- Sử dụng một nguồn policy canonical.
- Bảo đảm Application không phụ thuộc concrete Platform options.
- Đồng bộ grant expiry với presigned URL expiry.
- Kiểm định baseline 10 MiB/file, 10 files/Ticket, 50 MiB/Ticket và pending grant limit.
- Đối chiếu TTL với cấu hình và quyết định đã duyệt. Các giá trị mặc định 900 giây upload/300 giây download không tự là chính sách sản phẩm; TTL và thu hồi URL còn mở phải theo DEC-06, không tự chốt trong refactor.
- Validate configuration tại startup.

### Kiểm thử bắt buộc

Configured TTL; invalid TTL; zero/oversized files; exact size limit; attachment count; total quota; pending grants; concurrent grant/confirm; terminal Ticket.

## G4-07. Xây dựng Attachment reconciliation và crash recovery

**Ưu tiên: P1 — Reliability critical**

### Thay đổi

Phân tích vòng đời hiện tại:

```text
Durable upload grant
→ Upload pending object
→ Validate object metadata
→ Establish confirmation intent
→ Copy/promote object
→ Commit attachment metadata
→ Cleanup pending object
```

- Dùng grant ID hoặc durable intent làm định danh ổn định.
- Bảo đảm final object key có thể truy vết.
- Dùng conditional operation hoặc cơ chế tương đương để tránh source object thay đổi giữa validate và copy.
- Retry confirm không tạo attachment trùng.
- Không xóa final object khi DB commit outcome chưa rõ.
- Reconciliation phải kiểm tra authoritative DB references.
- Có cơ chế tránh cleanup race với active confirmation.
- Dọn expired grants và orphan objects theo policy.
- Không giữ database transaction trong remote storage calls kéo dài.
- Ưu tiên mở rộng grant lifecycle hiện tại trước khi tạo thêm bảng.

### Kiểm thử bắt buộc

Crash sau grant, upload, HEAD, copy, DB commit và trước cleanup; ETag thay đổi; concurrent confirm; response loss; R2 failure; cleanup retry; object orphan; không xóa attachment đã được tham chiếu.

Kiểm thử failure injection kết hợp PostgreSQL thật và R2 integration môi trường thử nghiệm.

## G4-08. Hardening Attachment security

**Ưu tiên: P1**

### Thay đổi

- Kiểm tra MIME, extension, declared size và actual size.
- Xác định content validation/scanning policy.
- Nếu malware scanning được yêu cầu, thiết kế quarantine và download eligibility.
- Bảo đảm private bucket.
- Presigned URLs ngắn hạn.
- Không cho người dùng tự chọn storage key ngoài phạm vi.
- Download authorization phải tuân thủ Ticket policy.
- Không tuyên bố có thể thu hồi ngay presigned URL đã cấp nếu hạ tầng không hỗ trợ.

### Kiểm thử bắt buộc

MIME mismatch; disguised extension; size mismatch; modified object; unconfirmed file; cross-ticket access; revoked membership; expired URL; private bucket; browser upload; CORS; scanning fixture nếu được triển khai.

## G4-09. Kiểm định Knowledge lifecycle

**Ưu tiên: P2**

### Thay đổi

- Kiểm tra Draft, Publish, Republish, Archive, Versions, Tags và Search.
- Không cho sửa draft làm thay đổi published version âm thầm.
- Giữ version history.
- Search phải áp dụng visibility trước pagination.
- Kiểm tra concurrent edits.
- Chuẩn hóa ownership của Knowledge contracts.
- Không triển khai RAG hoặc vector indexing nếu ngoài phạm vi.

### Kiểm thử bắt buộc

Create/edit draft; publish; republish; archive; historical version; concurrent edit; search visibility; tag normalization; pagination; input validation; safe content rendering tại đúng layer.

---

# PHẦN 9. G5 — ARCHITECTURE REFACTORING VÀ FITNESS FUNCTIONS

## G5-01. Chuẩn hóa cấu trúc thư mục và project boundaries

**Ưu tiên: P1**

### Kiến trúc mục tiêu

```text
src/backend/
├── BuildingBlocks/
│   ├── SharedKernel/
│   └── SharedKernel.Web/
│
├── Hosts/
│   ├── Api/
│   ├── Worker/
│   └── Migrator/
│
├── Modules/
│   ├── Identity/
│   │   ├── Domain/
│   │   ├── Application/
│   │   │   ├── Contracts/
│   │   │   ├── Ports/
│   │   │   └── <TênTínhNăng>/
│   │   ├── Infrastructure/
│   │   │   └── Configurations/
│   │   ├── Presentation/
│   │   └── IdentityModule.cs
│   │
│   ├── Ticketing/
│   ├── Knowledge/
│   └── AIAssistance/
│
└── Platform/
    ├── Clock/
    ├── Options/
    ├── Persistence/
    │   ├── ApplicationDbContext.cs
    │   └── Migrations/
    └── Storage/
```

Đây là cấu trúc định hướng, không bắt buộc đổi tên những thư mục đang có semantics chính xác.

Nếu mã đã tổ chức module-first đúng hướng dẫn kho, kiểm chứng các ranh giới và ghi nhận phần còn lệch; không di chuyển lại project/file chỉ để đánh dấu task đã thực hiện. Không tạo thư mục `FeatureFolders` hay module rỗng từ sơ đồ minh họa; feature nằm ở thư mục có tên use case thực tế.

### Quy tắc ownership

**Domain**

- Entities, aggregates, value objects.
- Domain policies và invariants.
- Không HTTP, EF Core, external SDK hoặc infrastructure dependency.

**Application**

- Use cases.
- Commands, queries và orchestration.
- Authorization ở application boundary.
- Ports cần thiết.
- Không concrete persistence hoặc external HTTP implementation.

**Infrastructure**

- PostgreSQL adapters.
- EF configurations.
- External service clients.
- Technical implementations.
- Không quyết định business rules thay Domain.

**Presentation**

- Endpoints.
- HTTP request/response mapping.
- Transport-level validation.
- Không truy cập database trực tiếp.
- Không chứa workflow nghiệp vụ.

**Platform**

- Shared infrastructure composition.
- DbContext, migrations, storage và clock.
- Không trở thành nơi chứa business services của nhiều module.

### Kiểm thử bắt buộc

Project dependency graph; namespace ownership; module references; forbidden framework dependencies; public contract accessibility; không thay đổi API/database ngoài chủ đích.

## G5-02. Refactor Ticket aggregate theo cohesion

**Ưu tiên: P2**

### Công việc

- Phân tích Ticket aggregate, không dựa duy nhất vào số dòng.
- Xác định nhóm Lifecycle, Assignment, Triage và SLA.
- Đánh giá dependency, invariant và reason-to-change.
- Tách pure calculation/policy nếu có lợi cho cohesion.
- Giữ mutation dưới quyền aggregate root.
- Giữ encapsulation và private setters phù hợp.
- Không biến aggregate thành DTO.
- Không dùng partial class chỉ để chia số dòng.

### Kiểm thử bắt buộc

Characterization tests; state transitions; SLA; priority; EF mapping; optimistic concurrency; aggregate invariants; không phát sinh migration ngoài chủ đích.

## G5-03. Refactor IdentityAccountGateway

**Ưu tiên: P2**

### Công việc

- Lập consumer map của gateway và interfaces hiện tại.
- Xác định responsibilities: authentication, registration, profile, directory, role và sessions.
- Tách trách nhiệm có reason-to-change độc lập.
- Giữ các workflow có chung atomic boundary ở nơi phù hợp.
- Không tạo sáu interface chỉ vì có sáu nhóm chức năng.
- Sử dụng consumer-oriented ports thay interface quá rộng nếu cần.

### Kiểm thử bắt buộc

Authentication regression; registration; role change; session revoke; login lockout; concurrent administration; query count; DIP compliance.

## G5-04. Refactor PostgresTicketQueries và projections

**Ưu tiên: P2**

### Công việc

- Phân tích scope, filters, sorting, pagination và projections.
- Hợp nhất logic thực sự trùng lặp.
- Tách query component có cohesion rõ.
- Áp dụng authorization scope trước count và pagination.
- Bảo đảm deterministic sorting.
- Không load toàn bộ dữ liệu rồi filter trong RAM.
- Không xây dựng generic specification framework nếu LINQ đủ.

### Kiểm thử bắt buộc

Filter correctness; total count; authorized pagination; stable sorting; N+1 detection; SQL execution plan; không có business mutation.

## G5-05. Chuẩn hóa Composition Root và DI

**Ưu tiên: P2**

### Công việc

- Module registration chỉ wiring dependencies.
- Rà soát ServiceLifetime.
- DbContext và stateful adapters phải có lifetime phù hợp.
- Không captive dependency.
- BackgroundService phải tạo scope đúng cách.
- Không dùng Service Locator trong use cases.
- Không resolve HTTP current user trong Worker.
- Không dùng production InMemory adapter ngoài ngoại lệ được duyệt.
- Không đăng ký implementation trùng gây chọn nhầm.

### Kiểm thử bắt buộc

Build service provider; validate scopes; resolve production use cases; API/Worker/Migrator startup; lifetime validation; production registration tests.

## G5-06. Tinh gọn SharedKernel

**Ưu tiên: P2**

### Công việc

- Kiểm kê mọi public type.
- Xác định consumers thực tế.
- Chuyển module-specific type về module sở hữu.
- Giữ những vocabulary và abstractions ổn định, thực sự dùng chung.
- Không để SharedKernel tham chiếu modules.
- Không đưa EF Core hoặc ASP.NET Core vào SharedKernel thuần.
- Xóa unused helpers sau kiểm chứng.

### Kiểm thử bắt buộc

Dependency graph; no module references; no forbidden framework references; public API compatibility; module-level tests.

## G5-07. Bổ sung Architecture Fitness Functions

**Ưu tiên: P1 — Mandatory quality gate**

Giữ NetArchTest và Mono.Cecil hiện có. Bổ sung source-level checks khi IL analysis không thể phát hiện đầy đủ, ví dụ compiler inline constants.

Ưu tiên mở rộng test project hiện tại thay vì tạo analyzer framework riêng.

### Các rule bắt buộc

| Rule | Nội dung |
|---|---|
| ARCH-01 | Project/module graph không có chu trình |
| ARCH-02 | Domain không phụ thuộc Infrastructure |
| ARCH-03 | Application không phụ thuộc EF Core hoặc concrete HTTP adapter |
| ARCH-04 | Presentation không truy cập persistence trực tiếp |
| ARCH-05 | Cross-module access chỉ qua published contracts |
| ARCH-06 | SharedKernel không phụ thuộc modules |
| ARCH-07 | Query không thực hiện business mutation |
| ARCH-08 | Authorization được bảo vệ tại Application boundary |
| ARCH-09 | Production không dùng InMemory business persistence trái policy |
| ARCH-10 | Worker không phụ thuộc HTTP context |
| ARCH-11 | Composition Root không chứa business workflow |
| ARCH-12 | Application không phụ thuộc concrete external implementation |
| ARCH-13 | Module-specific types có ownership rõ ràng |
| ARCH-14 | Published contract thay đổi phải được review |
| ARCH-15 | Runtime feature registrations khớp approved scope |

### Phương pháp kiểm tra

- Project graph analysis.
- NetArchTest.
- Mono.Cecil IL analysis.
- Roslyn source-level analysis nếu cần.
- DI registration tests.
- Behavioral tests.
- Manual architecture review cho các nguyên tắc không thể đo hoàn toàn tự động.

Không dùng naming convention làm bằng chứng duy nhất cho SRP, CQS hoặc authorization correctness.

### Kiểm thử chính architecture tests

- Valid fixture phải pass.
- Invalid fixture cố ý vi phạm phải fail.
- Kiểm tra cả namespace/type references và trường hợp inline constants.
- Không tạo allowlist rộng để che vi phạm.
- Mọi exception phải có lý do, owner và điều kiện xem xét lại.

## G5-08. Dọn dẹp codebase và legacy artifacts

**Ưu tiên: P2**

### Công việc

Kiểm kê và xử lý:

- Legacy MinIO runtime/configuration.
- Presigned POST không còn consumer.
- Auto-close ngoài MVP.
- Unused interfaces.
- Dead code.
- Duplicate implementations.
- NoOp adapters không có fallback policy.
- Configuration không được sử dụng.
- Obsolete scripts.
- Test fixtures không còn cần.
- Unused imports.
- Generated artifacts và temporary files.
- Tài liệu cũ mâu thuẫn tài liệu canonical.

Không xóa file chỉ vì static search không tìm thấy caller; phải kiểm tra reflection, DI, serialization, migrations và external consumers.

Áp dụng ADR-017 cho R2; không thêm runtime, cấu hình hay test mới dựa vào MinIO để thay bằng chứng R2. Phân loại test/artifact cũ trước khi gỡ; giữ bằng chứng lịch sử và cập nhật liên kết theo hướng dẫn kho. Thiếu tài nguyên R2 được phép kiểm thử thì ghi phần integration chưa chạy, không sửa assertion hay bật dịch vụ thay thế để coi là đạt.

Không xóa migration, audit history hoặc dữ liệu của người dùng.

### Kiểm thử bắt buộc

Clean checkout; restore; build; all tests; Docker build; documentation validator; reference scan; Git diff check; không còn artifact rác do đợt chỉnh sửa tạo ra.

---

# PHẦN 10. G6 — PRODUCTION ENGINEERING

## G6-01. Production Configuration Validation

**Ưu tiên: P0/P1**

### Thay đổi

- Kiểm kê toàn bộ configuration keys.
- Phân loại required/optional theo môi trường.
- Kiểm tra binding của Database, JWT, R2, SMTP, AI, Worker, CORS và reverse proxy.
- Bổ sung `ValidateOnStart()` hoặc cơ chế tương đương.
- Chặn invalid URI, TTL, timeout, credentials thiếu và incompatible settings.
- Không silent fallback sang NoOp nếu dependency bắt buộc.
- Phân biệt static configuration error với runtime dependency outage.
- Đồng bộ appsettings, `.env.example`, Compose và tài liệu.

### Kiểm thử bắt buộc

Missing required configuration; invalid values; AI enabled nhưng thiếu token; SMTP bắt buộc nhưng chưa cấu hình; invalid R2 settings; startup failure; không lộ secret.

## G6-02. Hoàn thiện Health Checks

**Ưu tiên: P1**

### Thay đổi

- Phân biệt liveness và readiness.
- Liveness phản ánh process health.
- Readiness phản ánh khả năng nhận workload theo critical dependencies.
- Theo dõi dependency degradation.
- Theo dõi Worker last successful execution và backlog.
- Không làm toàn bộ Ticketing mất khả dụng chỉ vì optional AI lỗi nếu manual fallback được duyệt.
- Không lộ topology hoặc sensitive configuration qua public health endpoint.

### Kiểm thử bắt buộc

Healthy runtime; DB outage; AI outage; R2 outage; SMTP outage; Worker stalled; recovery; status code và health projection.

## G6-03. Chuẩn hóa API error contract

**Ưu tiên: P1**

### Thay đổi

- Kiểm tra exception handling, API error filter và validation.
- Chuẩn hóa error response giữa các module.
- Phân biệt validation, unauthorized, forbidden, not found, conflict, rate limit, unavailable và internal error.
- Giữ resource concealment policy nhất quán.
- Không trả stack trace hoặc SQL error trong production.
- Đồng bộ OpenAPI và Frontend consumers.
- Không đổi status code ngoài chủ đích.

### Kiểm thử bắt buộc

400, 401, 403, 404, 409, 429, 503 và 500 khi áp dụng; malformed requests; domain errors; exception redaction; Retry-After; revision conflict details.

## G6-04. Hoàn thiện Observability

**Ưu tiên: P1**

### Thay đổi

- Structured logging.
- Correlation ID.
- HTTP duration và error rate.
- Database failures.
- Authentication/rate limit events.
- OTP backlog, delivery và retries.
- AI success, failure, timeout và latency.
- HITL outcomes và conflicts.
- R2 reconciliation backlog.
- SLA/Worker processed, failed và last success.

Không dùng user ID, Ticket ID hoặc email làm metric label không giới hạn.

Không log secret, OTP, access token, refresh token, presigned URL hoặc Ticket content nhạy cảm.

### Kiểm thử bắt buộc

Correlation propagation; correct severity; cancellation; metric correctness; failure visibility; log redaction; retry không làm sai business metrics.

## G6-05. Performance và scalability verification

**Ưu tiên: P1/P2**

### Công việc

Thiết lập workload đại diện cho:

- Authentication và JWT validation.
- Ticket queues/detail.
- Create và concurrent mutations.
- Reports.
- AI requests.
- SLA Worker.
- Attachment grants và confirmation.

Đo:

- Throughput.
- p50/p95/p99 latency.
- Error rate.
- CPU/RAM.
- DB connections.
- Query count.
- Lock contention.
- Backlog và recovery.

Tối ưu chỉ khi có hotspot hoặc bottleneck được đo.

Không tự thêm cache, index, batching hoặc concurrency mechanism nếu chưa đánh giá trade-off.

### Kiểm thử bắt buộc

Load, spike, soak, competing writes, slow dependency, DB saturation, memory/connection leak và recovery.

Đề xuất SLO dựa trên workload và cấu hình triển khai. Không tự tuyên bố đạt production nếu chưa có ngưỡng được thống nhất.

## G6-06. Củng cố CI, security và quality gates

**Ưu tiên: P1**

### Công việc

- Locked dependency restore.
- Release build.
- Domain/Application/Architecture tests.
- PostgreSQL integration tests.
- OpenAPI và AI contract verification.
- EF migration snapshot verification.
- Dependency vulnerability scanning.
- Secret scanning.
- Static security analysis.
- Coverage reporting.
- Docker build.
- Container scanning.
- SBOM/provenance nếu phù hợp release pipeline.
- Test result artifacts.
- Cross-repository integration gate.

Ưu tiên tận dụng CI hiện có, tránh tạo nhiều pipeline trùng lặp.

- Gắn kết quả scan với lockfile, phiên bản công cụ, ngày và advisory; phân biệt dependency production, dev/test và đường ảnh hưởng. Dev/test dependency có thể ảnh hưởng máy phát triển hoặc CI, không tự loại khỏi đánh giá chỉ vì không vào bundle production.
- Xử lý bản vá có thể xác minh trước; thay đổi major cần phân tích API, peer dependency, cấu hình và test bị ảnh hưởng. Không chạy nâng cấp toàn bộ hoặc `audit fix --force` như một bước mặc định, không sửa lockfile bằng tay để giấu cảnh báo.
- Với web, dùng Node theo `.nvmrc`/CI, locked install và chạy contract check, typecheck, lint, test, build; đổi luồng người dùng thì thêm E2E. Với backend, giữ SDK/EF CLI theo nguồn được ghim và build image thực sự chịu ảnh hưởng.
- Critical chưa xử lý không đạt gate phát hành. Ngoại lệ ở advisory gate phải có người chịu trách nhiệm, phạm vi, lý do, hạn xem xét và bằng chứng; không dùng ngoại lệ đó để công bố đạt điều kiện security của phần 19.

### Kiểm thử bắt buộc

CI phát hiện architecture violation, contract mismatch, migration mismatch, failed integration test, zero tests executed, vulnerable dependency và image build failure.

Không được bỏ qua test thất bại bằng `continue-on-error` ngoài trường hợp advisory gate có giải trình rõ ràng.

---

# PHẦN 11. G7 — CROSS-REPOSITORY RELEASE VÀ PRODUCTION ACCEPTANCE

## G7-01. Hoàn thiện ML classifier acceptance

**Ưu tiên: P0**

### Công việc

- Đọc ML provider, rule baseline, model training/evaluation pipeline.
- Xác định taxonomy và rubric tiếng Việt.
- Kiểm tra train/dev/test split.
- Ngăn data leakage.
- Đánh giá rule-based baseline và ML model.
- Đánh giá confidence và abstention.
- Kiểm tra manual fallback.
- Ghim model artifact, preprocessing, dependencies và checksum.
- Sử dụng local inference nếu thuộc requirement.
- Không dùng smoke test nhỏ thay model evaluation đầy đủ.
- Provider `rule`/`noop`, fake classifier và fixture chỉ chứng minh tích hợp tương ứng; không thay mô hình học máy cục bộ hoặc bằng chứng AI-07/DEC-08. Chưa có artifact/metric được duyệt thì ghi thiếu, không tự đặt ngưỡng sau khi đã xem final test set.

### Kiểm thử bắt buộc

Accuracy, Macro-F1, per-class precision/recall, confusion matrix, coverage, selective accuracy, calibration, latency, CPU/RAM và reproducibility.

Ngưỡng nghiệm thu phải được xác định theo AI-07 trước final test evaluation.

## G7-02. Đồng bộ Frontend và E2E journeys

**Ưu tiên: P0**

### Thay đổi

- Cập nhật API types.
- Đồng bộ analysis ID/revision.
- Loại phụ thuộc GET side effect.
- Xử lý AI unavailable và manual fallback.
- Xử lý authorization/revision conflict.
- Đồng bộ session lifecycle.
- Đồng bộ R2 signed headers và upload confirmation.

### Kiểm thử E2E bắt buộc

1. Đăng ký, login và profile theo phạm vi.
2. Forgot password, nhận OTP, reset và login.
3. Tạo Ticket, queue và detail.
4. Claim, assign, transfer và resolve.
5. Public comment và internal note.
6. Pending, resume và SLA.
7. AI Analyze và Accept.
8. AI Analyze và Override.
9. AI Analyze và Reject.
10. AI unavailable và manual triage.
11. R2 upload, confirm và download.
12. Confirm failure và retry.
13. Authorization bị thu hồi.
14. Revision conflict và reload.
15. Reopen/Close theo policy.
16. Knowledge publish/search nếu thuộc MVP.

Nghiệm thu tích hợp phải dùng Backend và PostgreSQL thật. Mock API chỉ là UI/component testing, không thay thế E2E.

Nối và kiểm chứng luồng Agent theo hợp đồng hiện hành có thể thực hiện độc lập với chính sách Requester/vòng đời còn mở. Giữ hành vi đã có trong phần độc lập; không tự quyết AI-01 hoặc tạo chuyển trạng thái `Triaged` để hoàn thiện giao diện. Chỉ bật capability sau khi caller, runtime validation, permission, stale/revision handling và bằng chứng backend thật tương ứng đã đạt.

## G7-03. Hoàn thiện immutable release manifest

**Ưu tiên: P0**

### Thay đổi

- Kiểm tra `sources.lock.json`, release manifests và image tags.
- Đối chiếu source SHA với published image digest.
- Không chỉ sửa source SHA mà giữ digest cũ.
- Publish Backend API, Worker, Migrator, Frontend và AI Service qua workflow hợp lệ.
- Chuẩn bị manifest và kế hoạch publish trước; chỉ thực sự publish/đổi `sources.lock.json` hoặc digest khi có ủy quyền phát hành. Build local thành công không phải image đã được phát hành hay candidate đã được ghim.
- Ghim application images theo digest.
- Không dùng `latest` làm production release reference.
- Xác định model artifact version.
- Cấu hình AI activation rõ ràng.
- Không chạy image Backend MinIO legacy với configuration R2 mới.
- Xác minh CI chạy trên chính xác release candidate.

### Kiểm thử bắt buộc

Manifest validation; digest/source provenance; incompatible version rejection; locked candidate startup; AI provider đúng; no unapproved mutable application images; complete stack smoke.

## G7-04. Database deployment và least privilege

**Ưu tiên: P0/P1**

### Thay đổi

- Phân tích quyền PostgreSQL của Migrator, API và Worker.
- Tách migration credentials khỏi runtime credentials nếu khả thi.
- Runtime không có DDL hoặc superuser ngoài nhu cầu được chứng minh.
- Kiểm tra migration order.
- Chặn runtime startup khi schema chưa tương thích.
- Kiểm tra PostgreSQL major-version upgrade nếu có.
- Không xóa hoặc sửa migration đã phát hành.
- Xây dựng rollback strategy phù hợp với data migration.

### Kiểm thử bắt buộc

Fresh database migration; upgrade từ release trước; migration failure; runtime DDL rejection; authorized use cases; data preservation; backup/restore; schema compatibility.

## G7-05. R2 cutover, networking và transport security

**Ưu tiên: P0/P1**

### Công việc

- Kiểm tra TLS.
- Reverse proxy.
- Trusted forwarded headers.
- CORS.
- Private network cho PostgreSQL/AI Service.
- R2 private bucket.
- R2 least-privilege credentials.
- SMTP configuration.
- Secret injection.
- Legacy MinIO data inventory nếu tồn tại.
- Lập migration/cutover plan nếu cần.

Không xóa storage nguồn trước khi kiểm chứng dữ liệu đích và có retention approval.

### Kiểm thử bắt buộc

HTTPS; certificate chain; proxy; CORS; browser-to-R2 upload; confirm/download; invalid credentials; private access; object migration; rollback; không mất attachment cũ.

## G7-06. Disaster Recovery và Operational Readiness

**Ưu tiên: P1**

### Công việc

Viết runbook cho:

- API crash.
- Worker crash.
- PostgreSQL outage.
- AI unavailable.
- R2 outage.
- SMTP outage.
- Migration failure.
- Release rollback.
- Data corruption/loss.

Xác định RPO/RTO được phê duyệt.

### Kiểm thử bắt buộc

Backup và restore PostgreSQL; attachment metadata reconciliation; analysis/decision đã lưu còn đọc và xử lý đúng sau restart; OTP jobs sau Worker restart; mất API replica; deploy/rollback candidate; monitoring và recovery timing. Nếu AI-03 duyệt xử lý nền, kiểm thêm resume/retry pending AI jobs; không tạo job framework chỉ để thỏa câu kiểm thử của luồng đồng bộ.

Backup chưa restore thử không được coi là disaster recovery đã được kiểm chứng.

## G7-07. Nghiệm thu production trên cùng release candidate

**Ưu tiên: P0 — Final gate**

Lập biên bản:

```text
Release candidate ID
Backend SHA
Frontend SHA
AI Service SHA
Infrastructure SHA
Application image digests
Database version/schema
Model artifact version
Model evaluation evidence
API/contract versions
Environment profile
Test results
Security findings
Performance results
Backup/restore results
Known limitations
Remaining risks
Approval status
Rollback reference
```

Không ghép test results của nhiều source versions thành bằng chứng một candidate đạt.

Không tuyên bố DEC-08 đã được phê duyệt nếu chưa có approval thực tế.

---

# PHẦN 12. CHIẾN LƯỢC KIỂM THỬ BẮT BUỘC

## 12.1. Phân tầng kiểm thử

Sử dụng test projects hiện tại và mở rộng theo module/feature, hạn chế tạo project mới.

| Test type | Mục đích |
|---|---|
| Characterization | Khóa hành vi hợp lệ trước refactor |
| Unit | Pure logic và boundary conditions |
| Domain | Business invariants |
| Application | Authorization và orchestration |
| Architecture | Structural dependency rules |
| Contract | OpenAPI và AI schemas |
| PostgreSQL Integration | Transaction, persistence, locking |
| Storage Integration | R2 lifecycle |
| Security | Authentication, authorization và validation |
| Negative | Invalid states và forbidden access |
| Regression | Ngăn lỗi đã sửa tái xuất hiện |
| Concurrency | Race conditions |
| Idempotency | Replay và duplicate requests |
| Failure Injection | Lỗi tại từng execution boundary |
| Crash Recovery | Process restart và reconciliation |
| Migration | Schema/data preservation |
| Performance | Latency, throughput và resource usage |
| E2E | Nghiệp vụ hoàn chỉnh trên toàn stack |

## 12.2. Quy tắc chất lượng test

- Regression test phải fail trên implementation lỗi và pass sau sửa nếu có thể tái hiện.
- Không dùng mock hoặc EF InMemory để chứng minh transaction atomicity.
- Không dùng SQLite, mock persistence hoặc EF InMemory thay PostgreSQL để kết luận về persistence, transaction hay concurrency.
- Dùng PostgreSQL thật cho locking, concurrency và persistence.
- Dùng deterministic clock cho SLA và OTP expiry.
- Concurrency test phải có synchronization phù hợp, không chỉ chạy song song ngẫu nhiên.
- Failure injection phải kiểm tra persisted state sau lỗi.
- Crash recovery cần process restart thật ở nghiệm thu cuối.
- Kiểm tra cả kết quả HTTP và database state.
- Kiểm tra cả hành vi thành công và không thành công.
- Không tăng test count bằng test trùng lặp.
- Không bỏ qua flaky tests; phải xác định nguyên nhân.
- Không làm suy yếu assertion để CI pass.
- Coverage là tín hiệu hỗ trợ, không thay thế invariant testing.

Không đổi expectation, fixture hoặc baseline để che implementation sai hay hạ gate. Khi một thay đổi hợp đồng được giao hoặc bản vá bảo mật làm setup cũ không còn hợp lệ, nêu nguyên nhân, giữ kỳ vọng nghiệp vụ đúng, dựng actor/dữ liệu hợp lệ và thêm ca từ chối. Thay đổi setup phải phục vụ hợp đồng đúng; nếu làm mất coverage cũ thì phải bổ sung, không chỉ làm CI xanh.

Phân biệt test transport dùng mock, host/context mới và restart process thật. Ghi đúng mức bằng chứng đã chạy; host mới không tự chứng minh crash recovery hoặc toàn bộ deployment. Các test đã biết chập chờn xử lý đúng quy trình chạy lại trong `AGENTS.md`, không tăng timeout hoặc bỏ test ngoài phạm vi chỉ để đạt.

## 12.3. Kiểm thử đặc biệt cho critical flows

Đối với AI HITL, session rotation, Ticket commands, OTP và attachment confirmation, phải xem xét:

```text
Before operation
During operation
Concurrent operation
Dependency failure
Cancellation
Before commit
After commit
Lost response
Retry
Process restart
Permission revocation
```

Mỗi flow cần xác định authoritative state, retry semantics và data integrity invariant.

## 12.4. Lệnh kiểm thử chuẩn

Tự phát hiện lệnh chính xác từ CI, Makefile, scripts, tool manifests và repository documentation.

Các lệnh tham chiếu cho Backend:

```powershell
dotnet restore src/backend/ServiceDesk.sln --locked-mode

dotnet build src/backend/ServiceDesk.sln `
  -c Release --no-restore

dotnet test src/backend/ServiceDesk.sln `
  -c Release --no-build `
  --filter "Category!=Integration"

dotnet test src/backend/ServiceDesk.sln `
  -c Release --no-build `
  --filter "Category=Integration"

git diff --check
```

Chọn thêm theo phạm vi, không mặc định chạy mọi kho cho một thay đổi chỉ tài liệu:

- Chạy architecture tests riêng khi thay đổi boundaries.
- Chạy OpenAPI validator.
- Chạy AI contract validator.
- Chạy documentation/script tests.
- Kiểm tra pending EF model changes.
- Dependency vulnerability scan.
- Build image API/Worker/Migrator chịu ảnh hưởng; dependency/composition chung thường ảnh hưởng cả ba, phải đối chiếu Dockerfile và workflow.
- Chạy Frontend test/build/E2E theo cấu hình thực tế.
- Chạy AI Service tests, model evaluation và Docker smoke.
- Chạy Infra Compose validation và integration tests.

Không tự giả định script path hoặc command tồn tại. Phải kiểm tra repository trước khi chạy.

Nếu thiếu SDK, Docker, credentials hoặc dependency thật, ghi rõ Blocked hoặc Not Run, không suy diễn kết quả.

| Phạm vi thay đổi | Gate tối thiểu cần đối chiếu |
| --- | --- |
| Chỉ tài liệu | `python scripts/validate_project_docs.py`, `python -m unittest discover -s scripts/tests -p "test_project_docs.py"`, đọc diff/liên kết/lệnh; không cần build toàn bộ ứng dụng |
| Validator/script/workflow | Test hồi quy của công cụ và lệnh/cấu trúc workflow bị ảnh hưởng |
| Nghiệp vụ, authentication/authorization | Unit/use case và HTTP positive/negative liên quan; PostgreSQL khi đọc Identity, lưu state hoặc kiểm thu hồi/concurrency |
| `using`, project reference, module boundary | `ServiceDesk.ArchitectureTests`, cộng gate chức năng tương ứng |
| Persistence, migration, transaction, concurrency | PostgreSQL thật; rollback/commit ambiguity khi liên quan; snapshot/model và upgrade từ schema trước thay đổi |
| HTTP/OpenAPI/AI contract | JSON/status/header, baseline diff có chủ đích, validator/adapter liên quan và consumer; AI validator dùng Python venv |
| Dependency/build/composition/Docker | Locked restore, Release build, test và image chịu ảnh hưởng; scan theo G6-06 |
| UI hoặc caller web | Node được ghim, contract check/typecheck/lint/test/build; E2E khi đổi luồng, không thay E2E thật bằng mock |

Kiểm exit code và discovery, không chỉ tìm dòng “Passed”. Tổng test đúng phạm vi phải lớn hơn 0, không bỏ qua assembly/lớp do lỗi nạp DLL. Filter integration có thể không khớp ở project không chứa integration test; phải xác nhận project đích thực sự chạy test. Số test lịch sử không phải chỉ tiêu cố định để hợp thức hóa thiếu discovery.

Build cùng source và cấu hình Release trước `--no-build`. Phát hiện đúng SDK/Node/Python/EF CLI từ `global.json`, `.nvmrc`, CI và hướng dẫn môi trường; không cài/cập nhật tool global hoặc sửa chính sách bảo vệ máy để né lỗi. Có thể dùng SDK Linux/Node được ghim trong container local khi Windows chặn, với bản sao nguồn không chứa bí mật và artifact riêng; ghi môi trường thực chạy và dừng container khi xong.

Không dùng số test của commit cũ thay bằng chứng cho source mới. Kết quả phải kèm SHA, dirty diff và môi trường; test trên working tree có thay đổi của người dùng không chứng nhận một checkout sạch chỉ chứa commit của nhiệm vụ.

---

# PHẦN 13. CƠ CHẾ BẢO VỆ KIẾN TRÚC SAU NÀY

## 13.1. Kiểm tra mọi type mới

Trước khi thêm class, interface, file hoặc project, trả lời:

1. Thành phần này giải quyết requirement hoặc defect nào?
2. Module nào sở hữu nó?
3. Tầng nào chịu trách nhiệm?
4. Vì sao không sử dụng thành phần hiện tại?
5. Abstraction này bảo vệ boundary hay chỉ che implementation?
6. Có tạo dependency cycle không?
7. Có làm transaction boundary khó hiểu hơn không?
8. Có thể kiểm thử độc lập không?
9. Có consumer thực tế không?
10. Có giải pháp đơn giản hơn không?

Nếu không giải thích được, không thêm.

## 13.2. Kiểm tra mọi refactor

Mỗi refactor phải có bằng chứng:

- Hành vi hợp lệ trước và sau.
- Trách nhiệm được làm rõ.
- Coupling giảm hoặc ownership tốt hơn.
- Không tăng complexity ngoài nhu cầu.
- Không phát sinh public API change ngoài chủ đích.
- Không phát sinh database schema change ngoài chủ đích.
- Không làm giảm security.
- Có test bảo vệ.

Không sử dụng LOC, số class hoặc số interface làm mục tiêu độc lập.

## 13.3. Cập nhật AGENTS.md

Sau khi kiểm định kiến trúc, đối chiếu hướng dẫn repository với các nội dung sau. Chỉ sửa `AGENTS.md` khi được giao, hoặc thuộc đúng ngoại lệ cấu hình/lệnh được ghim mà `AGENTS.md` cho phép; còn lại cập nhật nguồn canonical phù hợp hoặc ghi đề xuất cần duyệt:

- Canonical folder structure.
- Module ownership.
- Allowed dependency graph.
- Published contracts.
- Domain invariant ownership.
- Command/query separation.
- Authorization placement.
- Transaction rules.
- Configuration rules.
- Required tests.
- Commit conventions.
- Cleanup requirements.

Giữ `AGENTS.md` ngắn gọn, có tính chỉ dẫn trực tiếp. Đưa giải thích dài vào tài liệu kiến trúc và liên kết tới đó.

Không nhân bản toàn bộ Master Prompt vào nhiều file instructions.

---

# PHẦN 14. QUẢN LÝ GIT VÀ PULL REQUEST

## 14.1. Quy tắc Git

- Kiểm tra working tree trước chỉnh sửa.
- Fetch remote khi có quyền đọc/kết nối; nếu dùng refs local thì ghi rõ chưa xác minh remote mới nhất. Không tự pull/merge để làm sạch hoặc cập nhật nhánh.
- Tuân thủ branch strategy hiện hành.
- Không force push.
- Không reset hard hoặc xóa untracked files của người dùng.
- Không sửa lịch sử commit đã phát hành.
- Không merge trực tiếp vào protected branch trái quy định.
- Không deploy production chỉ vì CI pass.
- Chỉ push và tạo PR trong phạm vi quyền đã được cấp.
- Trong phiên chỉ phân tích hoặc hoàn thiện kế hoạch, để diff review được, không tự commit. Phiên triển khai có commit cục bộ phải giữ nhánh được chỉ định; không tự đổi tên nhánh để khớp mẫu.
- Chỉ stage phần đã xác minh thuộc nhiệm vụ; không dùng `git add -A` khi chưa xác minh toàn bộ diff. Không dùng restore/checkout/reset/clean để ghi đè hay loại thay đổi của người dùng; quy tắc này không cấm tạo nhánh khi nhiệm vụ cần và đã có phạm vi phù hợp.

## 14.2. Chia nhỏ Pull Request

Đề xuất các cụm PR:

| PR | Phạm vi |
|---|---|
| 01 | Baseline, traceability và requirement governance |
| 02 | AI resource authorization |
| 03 | CQS và AI query semantics |
| 04 | AI correctness guards và auto-close scope |
| 05 | AI PostgreSQL persistence |
| 06 | HITL transaction và concurrency |
| 07 | AI audit và contract synchronization |
| 08 | JWT, refresh và rate limiting |
| 09 | Durable OTP |
| 10 | Ticket lifecycle và RBAC |
| 11 | SLA và Worker reliability |
| 12 | R2 attachment reconciliation |
| 13 | Architecture refactoring |
| 14 | Architecture fitness functions và cleanup |
| 15 | Production hardening |
| 16 | Cross-repository release acceptance |

Điều chỉnh thứ tự nếu dependency graph yêu cầu. Không phát hành intermediate state làm hỏng contract hoặc data compatibility.

## 14.3. Commit tiếng Việt

Sử dụng Conventional Commits với nội dung tiếng Việt.

Ví dụ:

```text
fix(ai): bảo vệ phạm vi truy cập phiếu trong luồng phân loại

refactor(ai): tách truy vấn kết quả khỏi lệnh phân tích

feat(ai): lưu bền kết quả phân tích và lịch sử quyết định

fix(ai): bảo đảm tính nguyên tử khi áp dụng gợi ý phân loại

fix(identity): siết chặt xác thực và thu hồi phiên

feat(storage): bổ sung đối soát tệp sau sự cố

refactor(ticketing): tinh gọn trách nhiệm của miền nghiệp vụ

test(architecture): tăng cường kiểm tra ranh giới mô-đun

chore(backend): loại bỏ mã và cấu hình không còn sử dụng

docs(architecture): chuẩn hóa quy tắc tổ chức mã nguồn
```

Mỗi commit có phạm vi nhất quán, không trộn refactor với thay đổi nghiệp vụ không liên quan.

## 14.4. Nội dung PR bắt buộc

```text
Task IDs
Problem and evidence
Root cause
Implementation
Architecture impact
Business impact
API/contract impact
Database impact
Security impact
Tests executed
Test results
Failure cases verified
Migration/rollback
Remaining risks
Reviewer findings
```

---

# PHẦN 15. QUẢN LÝ TRẠNG THÁI XUYÊN SUỐT NHIỀU PHIÊN

Chỉ duy trì [production-hardening-state.md](production-hardening-state.md) làm sổ trạng thái. Không tạo thêm sổ thay thế hoặc chép tiến độ/SHA/kết quả mới vào master prompt.

Mỗi task/nhóm task có các trường cần thiết sau, không lặp template rỗng khi không áp dụng:

| Nhóm thông tin | Nội dung |
| --- | --- |
| Mốc và phạm vi | Repo, đường dẫn checkout, nhánh, SHA, dirty diff, task được giao, requirement/nguồn chính |
| Vấn đề | File/class/method, mức ảnh hưởng, nguyên nhân, bằng chứng trước sửa hoặc phần mới chỉ nghi vấn |
| Thực hiện | Tệp/caller/test/contract/config bị ảnh hưởng, phương án nhỏ nhất, quyết định kỹ thuật và thay đổi cần phối hợp |
| Kiểm chứng | Lệnh, môi trường/phiên bản từ nguồn ghim, exit code, Passed/Failed/Skipped, discovery, artifact và mức bằng chứng thực sự |
| Review | Self-review/sub-agent/người duyệt PR, findings đã xử lý/còn mở và giới hạn kết luận |
| Git và gate còn thiếu | Commit local/remote, PR, CI run đúng SHA hoặc ghi chưa có, đồng bộ liên kho |
| Tiếp tục | Phần việc có thể làm ngay; blocker cụ thể, ai/điều kiện gỡ và tác động lên task khác |

### Ý nghĩa trạng thái task

| Trạng thái | Điều kiện |
| --- | --- |
| Pending | Chưa bắt đầu; nhận định lỗi trong kế hoạch vẫn cần đối chiếu mã |
| In Progress | Đang phân tích/triển khai/kiểm chứng, còn phần chức năng hoặc kiểm tra đang thực hiện |
| Implemented | Phần triển khai của task đã có; ghi rõ kiểm tra đã đạt và gate còn thiếu, kể cả CI hoặc duyệt PR |
| Verified | Requirement và mọi gate bắt buộc của task theo hướng dẫn kho đã có bằng chứng phù hợp trên đúng source; không còn blocker trong phạm vi |
| Blocked | Phần cần thiết không thể tiếp tục vì quyết định, quyền, nguồn hoặc môi trường cụ thể; không dùng để che phần độc lập vẫn làm được |
| Not Applicable | Có căn cứ task không thuộc scope đã duyệt hoặc đã bị thay thế; ghi nguồn và quyết định, không dùng thay cho “chưa làm/chưa có môi trường” |

Task `Implemented` chỉ chờ CI/duyệt PR vẫn giữ `Implemented`, liệt kê gate bị chặn; không sửa lại mã để có hoạt động mới hoặc tự push để đổi nhãn. Khi chọn việc tiếp theo, ưu tiên phần có thể thực hiện theo dependency/mức ảnh hưởng, không quay lại task đầu danh sách chỉ vì chưa có nhãn `Verified`.

`Verified` ở task không tự là production-ready, cũng không thay biên bản G7-07. Phân biệt kết quả local, CI, integration liên kho và nghiệm thu release candidate. Thiếu gate bắt buộc thì giữ trạng thái phù hợp và ghi việc còn thiếu; không gắn `Verified` bằng báo cáo của commit khác hoặc nâng nhãn chỉ vì đã có code.

Mỗi phiên kiểm lại mốc nguồn và diff liên quan; chỉ chạy lại kiểm tra khi mã/môi trường thay đổi hoặc bằng chứng không đủ. Không kiểm kê lại toàn bộ 50 task hay dựng lại phần đã có một cách máy móc.

---

# PHẦN 16. INDEPENDENT REVIEW VÀ ĐÁNH GIÁ KIẾN TRÚC

Sau mỗi nhóm task triển khai đáng kể, review độc lập theo các tiêu chí liên quan dưới đây. Với thay đổi nhỏ/chỉ tài liệu, đọc diff và kiểm chứng đúng phạm vi; không biến mỗi dòng sửa thành một cuộc kiểm toán toàn hệ thống:

1. Business correctness.
2. Domain invariants.
3. Authentication.
4. Resource authorization.
5. Transaction integrity.
6. Concurrency.
7. Idempotency.
8. Persistence durability.
9. Crash recovery.
10. Dependency boundaries.
11. API contract consistency.
12. Configuration correctness.
13. Observability.
14. Performance.
15. Security.
16. Maintainability.
17. Test effectiveness.
18. Production deployment.

Đánh giá riêng 11 nguyên tắc:

```text
KISS
YAGNI
Rule of Three
SoC
SRP
High Cohesion / Low Coupling
Composition
DIP
CQS
Fail Fast
POLA
```

Đối với nguyên tắc chịu ảnh hưởng, báo cáo; đánh giá đầy đủ cả hệ thống tại mốc kiểm định kiến trúc/phát hành:

- Mức độ tuân thủ.
- Bằng chứng cụ thể.
- Vi phạm còn tồn tại.
- Mức độ nghiêm trọng.
- Phương án chỉnh sửa.
- Kiểm thử bảo vệ.
- Trade-off được chấp nhận.

Có thể chấm điểm tham khảo, nhưng điểm số không thay thế bằng chứng.

Không tự nâng điểm vì vừa refactor xong.

Không tuyên bố hoàn hảo tuyệt đối.

---

# PHẦN 17. DEFINITION OF DONE

Một task chỉ chuyển `Verified` khi đạt các điều kiện áp dụng dưới đây và gate của kho. Đánh dấu mục không áp dụng kèm lý do; thiếu môi trường/quyền/bằng chứng không phải lý do bỏ gate:

- [ ] Requirement rõ ràng hoặc đã được xác nhận.
- [ ] Có bằng chứng vấn đề trước khi sửa.
- [ ] Implementation đúng module ownership.
- [ ] Dependency boundaries được bảo vệ.
- [ ] Không tạo abstraction không cần thiết.
- [ ] Domain invariants được bảo vệ.
- [ ] Authentication và Authorization đúng.
- [ ] Có regression hoặc characterization tests phù hợp.
- [ ] Có positive và negative tests.
- [ ] Có failure-path tests đối với critical flow.
- [ ] Có concurrency/idempotency tests nếu áp dụng.
- [ ] Có PostgreSQL integration tests nếu thay đổi persistence.
- [ ] Có restart/recovery tests nếu yêu cầu durability.
- [ ] Không phá API contract ngoài chủ đích.
- [ ] Không phát sinh migration ngoài dự kiến.
- [ ] Không lộ dữ liệu nhạy cảm.
- [ ] Không tồn tại silent failure hoặc false success.
- [ ] Không còn code, config hoặc file rác do thay đổi tạo ra.
- [ ] Tài liệu được cập nhật.
- [ ] Build và test gates liên quan thành công.
- [ ] Review không còn blocker trong phạm vi task.
- [ ] Với task triển khai, có commit tiếng Việt đúng phạm vi; task chỉ phân tích/lập kế hoạch giữ diff để review, không tự commit.
- [ ] Có CI trên đúng SHA và duyệt PR khi quy trình kho yêu cầu; chưa được push/PR thì ghi thiếu gate và giữ `Implemented`, không gọi local pass là CI pass.
- [ ] Trạng thái và remaining risks được ghi lại.

Không bắt một task nhỏ phải hoàn thành toàn bộ G7 để kiểm chứng phần việc của nó, nhưng không được bỏ kiểm tra bắt buộc thực sự thuộc task. Tiêu chí phát hành toàn hệ thống nằm ở phần 19.

---

# PHẦN 18. THỨ TỰ THỰC THI VÀ CÁC LUỒNG SONG SONG

Thứ tự dưới đây là hướng dependency chính, không phải hàng đợi cứng bắt mọi task nhóm trước phải `Verified` rồi mới được làm nhóm sau:

```text
G0 — Baseline và requirement governance
    ↓
G1 — Critical authorization và correctness
    ↓
G2 — Durable AI và HITL
    ↓
G3/G4 — Security và reliability
    ↓
G5 — Architecture consolidation
    ↓
G6 — Production engineering
    ↓
G7 — Integrated release acceptance
```

Không trì hoãn toàn bộ architecture refactor đến cuối. Trong từng task phải sửa đúng ownership và dependency boundaries ngay khi chỉnh sửa.

### Quy tắc chọn việc tiếp theo

1. Bắt đầu từ phạm vi được giao, đối chiếu tệp state và mã hiện tại; ưu tiên lỗi gây sai nghiệp vụ, truy cập trái phép, mất dữ liệu hoặc lỗi bảo mật đang có đường ảnh hưởng thực tế.
2. P0 còn tác động trực tiếp trong scope được ưu tiên. Task P0 chờ quyết định/candidate hoặc capability đang tắt phải có blocker và điều kiện mở lại rõ ràng; không lấy đó làm lý do ngừng sửa lỗi độc lập đã xác minh.
3. Một task đã có implementation/local evidence nhưng chờ CI/duyệt không buộc các task khác chờ. Có thể làm G3-01/G3-05 trong khi theo dõi gate G1/G2, nếu không phá hợp đồng và bằng chứng đang có.
4. Không dồn mọi kiểm tra dependency/CI tới cuối G6. Finding nghiêm trọng của G6-06 có thể cần xử lý sớm, nhất là khi ảnh hưởng môi trường chạy test/publish; tách thay đổi dependency khỏi bản vá nghiệp vụ để review được.
5. G2-04 hoàn thiện validity/outcome và G2-06 nối caller là dependency của việc bật AI UI tại G7-02; snapshot/codegen đạt không mở khóa capability một mình. Phần Agent giữ hợp đồng hiện hành có thể làm độc lập với chính sách Requester/vòng đời chưa chốt.
6. Trước G3-03, đối chiếu invariant phát/tiêu thụ OTP và thu hồi phiên ở G3-02/G3-04; triển khai delivery và security nhất quán, không giữ OTP plaintext để giải quyết retry.
7. G5 chỉ refactor phần có vấn đề/nhu cầu đã chứng minh; G7 yêu cầu đúng bộ source/image/model/schema và quyền nghiệm thu. Không đổi manifest hoặc publish chỉ để task sau có vẻ chạy được.

Thứ tự cụ thể và lý do đổi thứ tự ghi ở tệp state, không chép thành tiến độ cố định trong master prompt. Vấn đề mới nghiêm trọng ngoài phạm vi phải báo bằng chứng/ảnh hưởng, không âm thầm mở rộng task.

Có thể chia các luồng công việc sau khi phạm vi, công cụ và quyền cho phép; chúng không tự yêu cầu mở nhiều agent hoặc sửa nhiều kho trong mọi phiên:

### Workstream A — Critical AI

- AI authorization.
- CQS.
- Accept/Category correctness.
- PostgreSQL persistence.
- HITL atomicity.
- Concurrency.
- Audit.
- Contracts.

### Workstream B — Backend Reliability

- JWT.
- Refresh token.
- OTP.
- Rate limiting.
- Ticket lifecycle.
- SLA.
- Worker.
- R2 reconciliation.

### Workstream C — Architecture & Production

- Architecture fitness functions.
- Dependency audits.
- Configuration validation.
- Documentation.
- CI.
- Security scanning.
- ML evaluation.
- Infra release preparation.

Điều phối các thay đổi chạm cùng DbContext, migration, published contracts và authorization policy. Không để nhiều workstream tạo các implementation cạnh tranh hoặc migration xung đột. Chạy test, build và export theo dependency: chờ copy nguồn/restore/build thành công trước bước phụ thuộc; không sửa hoặc build đồng thời trên cùng bản sao nguồn đang được ghi.

Các lỗi nhỏ, rõ ràng, ít rủi ro có thể xử lý sớm song song, nhưng không làm lệch ưu tiên P0.

---

# PHẦN 19. ĐIỀU KIỆN NGHIỆM THU CUỐI CÙNG

Chỉ công nhận production-ready khi:

| Hạng mục | Điều kiện |
|---|---|
| P0 | Không còn blocker mở |
| P1 | Không còn lỗi chặn production; ngoại lệ có phê duyệt |
| API | Mọi API trong approved scope có traceability và tests |
| Architecture | Mandatory fitness functions đạt |
| Authentication | Fail-closed |
| Authorization | Không truy cập ngoài resource scope |
| AI | Analysis và decisions lưu bền |
| HITL | Atomicity, concurrency và audit đạt |
| OTP | Delivery và recovery đạt nếu thuộc phạm vi |
| R2 | Upload, confirm, download và reconciliation đạt |
| SLA | Domain policy và Worker reliability đạt |
| Database | Migration, consistency và restore đạt |
| Security | Không có critical vulnerability chưa xử lý |
| Performance | Đạt SLO được duyệt |
| ML | Đạt AI-07 |
| Release | Source SHA và image digest nhất quán |
| E2E | Các hành trình nghiệp vụ bắt buộc đạt |
| Recovery | Crash recovery, restore và rollback đã kiểm chứng |
| Acceptance | DEC-08 được phê duyệt trên đúng candidate |

Nếu thiếu môi trường, credentials, model artifact, approval hoặc integration evidence bắt buộc, kết luận release là NO-GO. Có thể báo tiến độ có điều kiện hoặc task Blocked/Implemented theo phần 15, nhưng không gọi đó là GO hay production-ready. Không ghi phê duyệt thay người có trách nhiệm.

---

# PHẦN 20. YÊU CẦU BÁO CÁO SAU MỖI VÒNG LẶP

Báo cáo ngắn gọn nhưng có bằng chứng:

## A. Hiện trạng

- Repository, branch, SHA.
- Task đang xử lý.
- Vấn đề xác minh được.

## B. Thay đổi

- File/class/method đã chỉnh sửa.
- Thành phần đã bổ sung.
- Thành phần đã loại bỏ.
- Refactor đã thực hiện.
- Lý do lựa chọn thiết kế.

## C. Kiến trúc

- Ownership trước/sau.
- Dependency trước/sau.
- Transaction boundary.
- Nguyên tắc thiết kế được cải thiện.
- Trade-off còn lại.

## D. Kiểm thử

- Lệnh đã chạy.
- Exit code.
- Passed/Failed/Skipped.
- Regression evidence.
- Integration evidence.
- CI run hoặc artifact.

## E. Review

- Phát hiện mới.
- Đã sửa.
- Chưa sửa.
- Blockers.

## F. Git và tiến độ

- Commit.
- PR.
- CI status.
- Trạng thái task theo phần 15 và gate còn thiếu.
- Task tiếp theo.

Không dùng mô tả chung chung như “đã tối ưu”, “đã hoàn thiện”, “đã bảo mật” nếu không có bằng chứng cụ thể.

---

# PHẦN 21. CHỈ THỊ THỰC THI CUỐI CÙNG

Áp dụng phạm vi phiên ở phần 0. Nếu người giao việc yêu cầu phân tích hoặc sửa master prompt, hoàn thành đúng phần đó; không suy ra yêu cầu sửa ứng dụng, commit, push hoặc vận hành. Nếu đã giao triển khai, thực hiện tới hết phạm vi được giao thay vì dừng ở bản đề xuất, trừ phần thật sự cần quyết định/quyền/nguồn bên ngoài.

Trước task triển khai:

1. Đọc hướng dẫn kho, tệp state và requirement của task; đối chiếu Git/dirty diff và kiểm chứng nguồn chính liên quan.
2. Kiểm tra repository/môi trường cần dùng. Chỉ mở rộng sang kho khác khi có dependency/hợp đồng bị ảnh hưởng; không tự kết luận thiếu checkout từ một thư mục tìm kiếm duy nhất.
3. Phân biệt lỗi còn tồn tại, phần đã triển khai chờ gate và phần chưa có quyết định. Cập nhật state nếu có bằng chứng mới; không biến việc đọc lại thành tái triển khai 50 task.
4. Ghi tệp/caller/test/fixture/config/contract sẽ chạm và gate tương ứng. Kiểm tra phiên bản/câu lệnh từ nguồn được ghim, dùng baseline còn phù hợp hoặc chạy lại khi source/môi trường đã đổi.
5. Theo vòng lặp phần 2: tái hiện → sửa nhỏ nhất → kiểm chứng → review → xử lý finding → cập nhật nguồn canonical/state. Với lỗi ngoài scope, báo bằng chứng và tác động.
6. Bàn giao kết quả cùng SHA, dirty diff, số test thực chạy, artifact, gate chưa đạt và bước tiếp theo có thể làm. Chỉ commit/push/PR/publish/deploy theo đúng phạm vi quyền; thiếu quyền bước ngoài không xóa giá trị công việc local đã hoàn tất.

Trong mọi quyết định, ưu tiên theo thứ tự:

```text
Business Correctness
→ Security
→ Data Integrity
→ Reliability
→ Architectural Integrity
→ Simplicity
→ Maintainability
→ Performance
→ Extensibility
```

Không hy sinh tính đúng đắn để code ngắn hơn.

Không hy sinh security để tăng throughput.

Không bổ sung complexity nếu chưa có nhu cầu.

Không che giấu lỗi bằng fallback hoặc kết quả thành công giả.

Không bỏ qua bước kiểm chứng độc lập.

Khi gặp blocker, ghi rõ bằng chứng, quyết định hoặc tài nguyên cần thiết và tiếp tục các task không phụ thuộc blocker đó.

Mục tiêu cuối cùng là một Modular Monolith tinh gọn, có ranh giới rõ ràng, nghiệp vụ đúng đắn, dữ liệu bền vững, kiểm thử chặt chẽ, vận hành đáng tin cậy và có thể đưa vào production bằng bằng chứng thực tế.
