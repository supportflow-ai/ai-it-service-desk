# Verification snapshot — TC-277

Ngày rà soát: 2026-10-15, Asia/Bangkok. Code được kiểm chứng: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554).

| Gate | Kết quả thực tế | Giới hạn |
|---|---|---|
| Domain tests | PASS, 15/15 | Current create Submitted invariants; không chứng minh proposed Draft flow |
| Application tests | PASS, 12/12 | Có SQLite-based ticket tests; không chứng minh PostgreSQL sequence/xmin |
| Architecture tests | PASS, 6/6 | Kiểm assembly dependencies; không bao phủ mọi module/namespace convention |
| Toàn solution test/build gate | FAIL, exit 1 | CS8602 tại TicketEndpointTests.cs:269:76; filter không bỏ qua compile integration project |
| PostgreSQL integration/E2E | NOT RUN | Compile blocker và Docker engine HTTP 500; không quy thành pass/skipped |
| Frontend npm ci | PASS | Lockfile giữ nguyên |
| Frontend build | PASS | Vite bundle-size warning |
| Frontend lint | PASS, 0 errors, 1 warning | AuthContext fast-refresh warning |
| Frontend regression | PASS, 9/9 | Auth DTO, HTTP error, production typecheck; không có status/list E2E coverage |

AI trên [main@e044f47](https://github.com/supportflow-ai/ai-it-service-desk/commit/e044f470d38cb9d00a4adca9e59de45abbde3ff5): 74 Domain + 13 Application AIAssistance tests PASS. Đây là run riêng; không tính vào dev acceptance.

Command, exit code, test names/outcomes, TRX SHA-256 và môi trường nằm trong [verification-results.json](verification-results.json). Raw TRX/log được giữ trong thư mục evidence của đợt audit; JSON này là bản xuất kết quả theo từng test, không phải báo cáo coverage. Ngày của đợt audit hiện tại không thay thế chứng cứ lịch sử của Sprint 1.

Source build blocker: [Integration compile diagnostic location:L269](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/tests/ServiceDesk.IntegrationTests/TicketEndpointTests.cs#L269); warning policy: [Build warning policy:L7](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/Directory.Build.props#L7).
