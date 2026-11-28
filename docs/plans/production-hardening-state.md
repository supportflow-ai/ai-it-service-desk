# Trạng thái thực thi kế hoạch củng cố production

Tệp trạng thái duy nhất của [kế hoạch củng cố production](production-hardening-plan.md) (thay cho `.audit/REFACTOR_LOOP_STATE.md` và `docs/reviews/production-readiness-register.md` để không có hai nơi theo dõi cùng nội dung). Đầu mỗi phiên: đọc tệp này, đối chiếu `git log` và working tree, rồi tiếp tục task đầu tiên chưa `Verified`.

Trạng thái hợp lệ: Pending, In Progress, Implemented, Verified, Blocked, Not Applicable. `Implemented` không đồng nghĩa `Verified`.

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
| G1-01 | Pending — lỗi đã xác minh | `PostgresTicketTriageBridge.cs:55` dùng `TicketReadScope.AllTickets()` vô điều kiện; `RequestAiTriageUseCase` và `DecideAiTriageUseCase` không kiểm actor hay phạm vi, chỉ dựa `RequireAgent` ở endpoint; `GetTicketTriageUseCase.cs:32` coi mọi Agent là staff mà không xét nhóm/phân công (trái ADR-016) |
| G1-02 | Pending — lỗi đã xác minh | `GetTicketTriageUseCase.cs:43` gọi `IRequestAiTriage` khi chưa có analysis (GET gây mutation và gọi AI) |
| G1-03 | Pending — lỗi đã xác minh | `DecideAiTriageUseCase.cs:36-37` mặc định `Impact.Medium`/`Urgency.Medium` khi không có analysis hợp lệ rồi vẫn ghi phiếu và báo thành công |
| G1-04 | Pending — lỗi đã xác minh | `PostgresTicketTriageBridge.cs:62` chỉ gọi `SetTriage(impact, urgency)`; tham số `categoryId` bị bỏ qua. Domain chưa có lệnh đổi Category cho staff |
| G1-05 | Pending — lỗi đã xác minh | DEC-05 "MVP không tự đóng" nhưng `WorkerServices.cs:30` đăng ký `AutoCloseResolvedTicketsWorker`; use case/gateway ở `TicketingModule.cs:78,96` |
| G2-01 | Pending — lỗi đã xác minh | `AIAssistanceModule.cs:44` đăng ký `InMemoryAiAnalysisStore` (singleton, ghi đè theo `TicketId`, mất khi restart, không chia sẻ giữa replica) |
| G2-02, G2-03, G2-05 | Pending | Phụ thuộc G2-01; hiện quyết định không tham chiếu `analysisId`, lưu analysis trước rồi mới ghi phiếu (không cùng giao dịch) |
| G2-04, G2-06 | Pending | Chưa rà |
| G3-01 | Pending — lỗi đã xác minh | `IdentityModule.cs:74-76` bỏ qua kiểm security stamp khi không resolve được `DbContext`; `:93-100` chấp nhận token không có stamp của user không tồn tại (fail-open) |
| G3-02 | Pending | Chưa rà |
| G3-03 | Pending — lỗi đã xác minh | `PasswordResetQueue.cs` dùng `Channel<string>` trong RAM, mất khi tiến trình dừng |
| G3-04, G3-06 | Pending | Chưa rà |
| G3-05 | Pending — lỗi đã xác minh | `AuthRateLimiting.cs:176` xóa toàn bộ bộ đếm khi đạt `MaxPartitions` (kẻ tấn công lấp đầy partition để reset giới hạn) |
| G4-01 … G4-09 | Pending | Chưa rà |
| G5-01 … G5-08 | Pending | Chưa rà |
| G6-01 … G6-06 | Pending | Chưa rà |
| G7-01 … G7-07 | Blocked | Cần các kho web, ai-service, infra, môi trường và phê duyệt DEC-08 |

## Xung đột tài liệu cần chủ dự án chốt

- **AI-01 (quyền xem gợi ý AI của Requester).** [Hợp đồng hiện hành](../architecture/current-backend-contract.md) dòng 19 ghi cả ba endpoint triage là `RequireAgent`; mã `AiTriageEndpoints` cho `GET /triage` dùng `RequireAuthorization()` và trả `RequesterTriageView` (gợi ý Category và dải tin cậy) cho chủ phiếu. AI-01 còn mở. Tạm giữ hành vi mã hiện tại để không đổi hợp đồng HTTP với web; chỉ siết phạm vi phiếu. Cần chốt: Requester có được xem gợi ý không.
- **DEC-09 (`Triaged`).** Chưa chốt; luồng HITL giữ phương án hiện tại (ghi dữ liệu, không đổi trạng thái).

## Blockers

| Mô tả | Cần |
| --- | --- |
| Windows Application Control chặn nạp DLL test vừa build | Người dùng chọn cách chạy test: bật Docker Desktop để chạy `dotnet test` trong container SDK Linux như CI, hoặc chạy trên CI (cần quyền push nhánh), hoặc tự điều chỉnh chính sách máy |
| Docker không chạy | Bật Docker Desktop cho kiểm thử tích hợp PostgreSQL (Testcontainers) |
| Không có kho web, ai-service, infra | Clone hoặc chỉ đường dẫn; quyền push/PR nếu cần sửa |

## Nhật ký vòng lặp

- 28/11/2026 — G0-01: lưu kế hoạch, lập baseline, xác minh lỗi P0/P1 bằng đọc mã. Chưa sửa mã nghiệp vụ.

## Task tiếp theo

G1-01 + G1-02 (một PR): chuyển kiểm quyền về Ticketing qua `ITicketTriageBridge`, bỏ phụ thuộc lệnh khỏi `GetTicketTriageUseCase`. Tệp: `Modules/Ticketing/Application/Contracts/TicketTriageBridge.cs`, `Modules/Ticketing/Infrastructure/PostgresTicketTriageBridge.cs`, `Modules/AIAssistance/Application/Triage/*.cs`, `tests/ServiceDesk.Application.Tests/AIAssistance/AiTriageUseCaseTests.cs`.
