# Kiểm kê yêu cầu từ tài liệu kiến trúc cũ

Trạng thái: tài liệu hiện hành, đối chiếu mã `dev@db756d2` (2026-11-08). Mục đích: biết yêu cầu nào trong 14 tệp `docs/architecture/01–08` đã có trong mã, đã được duyệt nhưng chưa làm, còn mở hay đã bị thay thế. Đây là kiểm kê theo nhóm quy tắc, không phải truy vết từng dòng, và không thay đổi quyết định nghiệp vụ. Tám hồ sơ lịch sử (mục 2) đã được đưa ra khỏi HEAD; sáu đề xuất (mục 1) vẫn còn trong repo.

Git history và SHA commit là nguồn lưu trữ: mỗi tệp dưới đây có liên kết tới bản tại `db756d2`. Chủ dự án không yêu cầu manifest SHA-256 cho cả 14 tệp. Quyết định nguồn: [ADR-016](../adr/ADR-016-support-team-routing-and-agent-scope.md), [ma trận quyền](rbac-permission-matrix.md), [quyết định còn mở](pending-business-decisions.md).

Nhãn: **Đã triển khai** (có trong mã, không phải nghiệm thu) · **Đã duyệt – chưa triển khai** · **Mở** (chưa có quyết định) · **Lịch sử** (bị thay thế, không dùng làm yêu cầu). Chỉ kết luận "an toàn đưa khỏi HEAD" cho một tệp khi mọi dòng của nó thuộc **Đã triển khai**, **Lịch sử**, hoặc đã được chuyển vào ADR, bảng quyết định hay backlog.

## 1. Sáu đề xuất còn yêu cầu sống

### 1.1 [01-actor-use-cases.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/01-actor-use-cases.md) (ACT-01–13)

| Nhóm quy tắc | Trạng thái | Nơi chuyển hoặc đối chiếu |
| --- | --- | --- |
| ACT-01, 02, 10: danh tính từ JWT hợp lệ, quyền tường minh, API là nơi thi hành | **Đã triển khai** | [Ma trận quyền](rbac-permission-matrix.md), [quy tắc bảo mật](security-design-rules.md) |
| ACT-03: chủ sở hữu bất biến; cùng phòng ban không có quyền chéo | **Đã triển khai** | Ma trận quyền |
| ACT-04, 05: Agent đọc theo nhóm hoặc phân công; ghi theo phân công hoặc quyền điều phối của nhóm | Đọc: **Đã duyệt – chưa triển khai**. Ghi của Agent thường: **Mở** | ADR-016, DEC-02 |
| ACT-06: cổng trạng thái tách khỏi quyền; Closed là terminal | **Đã triển khai** (Closed). Quy tắc Draft: **Mở** | DEC-01 |
| ACT-07: Requester không thấy ghi chú nội bộ, nhật ký nhân viên, phân tích AI | Ghi chú nội bộ: **Đã triển khai**. Tệp nội bộ và AI: **Mở** (chưa có khái niệm) | Ma trận quyền |
| ACT-08: Requester/tìm kiếm chỉ thấy bài đã xuất bản | **Đã triển khai**. Quyền "biên tập viên theo phạm vi": **Mở** | `KnowledgePermissions` |
| ACT-09: phân tích theo phạm vi | **Mở** (chưa có module) | Backlog |
| ACT-11: đổi vai trò chỉ Admin, có thu hồi | **Đã triển khai** một phần. SLA hiệu lực quyền: **Mở** | DEC-02, hợp đồng hiện hành |
| ACT-12: chống tạo trùng gắn người gọi, method, resource, key | **Đã triển khai** (tạo phiếu) | Hợp đồng hiện hành |
| ACT-13: AI/nền không tăng quyền | **Đã triển khai** (`TriagePolicy`), được ghi lại trong ADR-016 | ADR-016 |
| DEC-01–06 trong tệp: giữ ba vai trò + Lead nghiệp vụ; `/api`; không remap enum; 404 cho vật thể ẩn | Lead: **Đã duyệt – chưa triển khai** (khả năng, không phải vai trò JWT). Còn lại **Đã triển khai** | ADR-016, hợp đồng hiện hành |

### 1.2 [02-rbac-permission-matrix.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/02-rbac-permission-matrix.md) (AUTH, TKT-01–27, KB, ANA, JWT, SEC-01–08)

| Nhóm quy tắc | Trạng thái | Nơi chuyển hoặc đối chiếu |
| --- | --- | --- |
| AUTH-01–04 | **Đã triển khai** | Ma trận quyền |
| TKT-01, 02, 09 (tự nhận), 13 (bắt đầu xử lý), 16 (đóng), 17 (mở lại), 18–22 (bình luận, ghi chú, timeline), 24–25 (tệp) | **Đã triển khai** (mở lại chưa có thời hạn/lý do; tệp qua URL ký) | Ma trận quyền; DEC-05, DEC-06 |
| TKT-03, 04: hàng đợi và chi tiết theo nhóm | **Đã duyệt – chưa triển khai** | ADR-016 |
| TKT-10–12: phân công, đổi nhóm, hủy phân công bởi Lead | **Đã duyệt – chưa triển khai** | ADR-016 |
| TKT-05–07: sửa, xóa, gửi bản nháp | **Mở** | DEC-01 |
| TKT-08: phân loại thủ công (triage) | **Mở**; AI chỉ đề xuất, định tuyến theo ADR-016 | ADR-016 |
| TKT-14: yêu cầu thông tin, PendingUser | **Mở** | DEC-03 |
| TKT-15: giải quyết với nội dung giải quyết | Chuyển trạng thái: **Đã triển khai**. Nội dung giải quyết: **Mở** | |
| TKT-23: toàn bộ lịch sử audit | **Đã triển khai** một phần (timeline). Phạm vi đầy đủ: **Mở** | |
| TKT-26, 27: xem và chấp nhận gợi ý AI | **Mở** (chưa có endpoint) | ADR-016 |
| KB-01–07 | **Đã triển khai** phần lớn (xem 1.1, ACT-08); quyền biên tập theo phạm vi: **Mở** | `KnowledgePermissions` |
| ANA-01–04 | **Mở** (chưa có module) | Backlog |
| JWT-01–08: kiểm thử token, quyền, IDOR | Phần lớn là danh sách kiểm thử cần có; JWT-06 (độ mới của quyền) **Mở** | DEC-02 |
| SEC-01–08: phát hiện bảo mật | **Lịch sử**: tài liệu tự ghi là bằng chứng tại mốc cũ; không dùng làm finding hiện hành | Báo cáo bảo mật trong `docs/reviews` |

### 1.3 [03-ticket-domain-contract.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/03-ticket-domain-contract.md) (SRC, DOM-01–11, DEC-01–10)

| Nhóm quy tắc | Trạng thái | Nơi chuyển hoặc đối chiếu |
| --- | --- | --- |
| DOM-01–04, 06–11: chủ sở hữu bất biến, mã phiếu từ database, chuẩn hóa dữ liệu, mã danh mục dạng chuỗi, tách vai trò/phạm vi/chủ sở hữu/trạng thái, bền vững enum, ghi nguyên tử + version + audit, chống lộ dữ liệu riêng, chống tạo trùng | **Đã triển khai** | Hợp đồng hiện hành |
| Giữ tạo phiếu ở Submitted; danh mục là chuỗi, không GUID; enum 0–6 không đổi nghĩa | **Đã triển khai** | Hợp đồng hiện hành |
| Giới hạn trường (tiêu đề, mô tả), tiền tố/độ rộng mã phiếu mâu thuẫn giữa các nguồn | **Lịch sử**: quyết định theo mã hiện hành (tiêu đề ≤ 100, mô tả ≥ 10, mã `IT-YYYY-…`) | Hợp đồng hiện hành |
| Draft/Submit, danh mục tùy chọn, hợp đồng đầu vào mới | **Mở** | DEC-01 |
| DOM-05: độ ưu tiên tính từ Impact/Urgency | **Mở** | |
| Department khác SupportTeam; snapshot `RequesterDepartmentId` | **Đã duyệt – chưa triển khai** (luồng snapshot hồ sơ chưa hoàn chỉnh) | ADR-016 |
| Reopen 168 giờ, auto-close 72 giờ trong DEC-07 của tệp | **Lịch sử**: không phải quyết định production | DEC-05 |
| Lead so với ba vai trò; mô hình hồ sơ/membership | Lead: **Đã duyệt – chưa triển khai** | ADR-016 |

### 1.4 [04-ticket-lifecycle-policy.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/04-ticket-lifecycle-policy.md) (LC-01–14)

| Nhóm quy tắc | Trạng thái | Nơi chuyển hoặc đối chiếu |
| --- | --- | --- |
| LC-01–04, 06, 11: lệnh có ý định, quyền trước trạng thái/revision, đồng thời, bình luận của Requester không phải quyền chuyển trạng thái, ghi nguyên tử + audit | **Đã triển khai** | Hợp đồng hiện hành |
| LC-07: giải quyết chỉ từ InProgress | **Đã triển khai**. Bắt buộc nội dung giải quyết công khai: **Mở** | |
| LC-08: đóng chỉ từ Resolved, bởi chủ sở hữu hoặc Admin | **Đã triển khai** (khớp DEC-05) | |
| LC-09: mở lại từ Resolved | **Đã triển khai**. Thời hạn mở lại: **Mở**. Lý do và lịch sử chu kỳ: **Mở** | DEC-05 |
| LC-10: người phụ trách khi mở lại | **Mở** | DEC-05 |
| LC-13: các trạng thái chờ (PendingUser, PendingExternal) | **Mở** | DEC-03, DEC-04 |
| LC-14: Closed terminal, Cancel chỉ từ Submitted | **Đã triển khai**. Không tự động đóng trong MVP: **Đã duyệt** | DEC-05 |
| LC-05: phân loại bắt buộc có Category, Impact, Urgency | **Mở** | ADR-016, DEC-01 |
| LC-12: phân biệt nội dung công khai/riêng trong audit | **Đã triển khai** một phần (timeline) | |

### 1.5 [05-assignment-and-comment-policy.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/05-assignment-and-comment-policy.md) (ASG-01–12, COM-01–14, OPS-DEC-01–07)

| Nhóm quy tắc | Trạng thái | Nơi chuyển hoặc đối chiếu |
| --- | --- | --- |
| ASG-02, 03, 09, 10: tự nhận, chạy đồng thời một người thắng, audit bên backend | **Đã triển khai** (claim, `expectedRevision`, audit hệ thống) | |
| ASG-04–08: gán/gán lại/hủy gán bởi Lead hoặc Admin; điều kiện người nhận thuộc nhóm | **Đã duyệt – chưa triển khai** (hiện mọi Agent gán được) | ADR-016 |
| ASG-11: không phân công lại khi Resolved | **Đã duyệt – chưa triển khai** (mã hiện cho gán khi Resolved) | DEC-05 |
| ASG-12, OPS-DEC-02: chuyển nhóm, lịch sử riêng, phiếu cũ chưa có nhóm | Phiếu cũ chưa có nhóm: **Đã duyệt – chưa triển khai** quyền (migration chuyển phiếu chưa kết thúc vào `MANUAL_TRIAGE` đã có, ADR-016). Chuyển nhóm, lịch sử riêng: **Mở** | DEC-02 |
| COM-01–06, 08, 10, 11: bình luận công khai, ghi chú nội bộ, đọc theo vai trò, bất biến, không viết vào phiếu kết thúc, lọc ở truy vấn | **Đã triển khai** | Ma trận quyền |
| COM-07, OPS-DEC-04: phản hồi khi PendingUser tự tiếp tục hay nhân viên tiếp tục | **Mở** | DEC-03 |
| COM-09, OPS-DEC-06: sửa/xóa bình luận, giới hạn độ dài, tần suất, lưu giữ | **Mở** (chưa có sửa/xóa) | |
| COM-12–14, OPS-DEC-07: lộ nội dung riêng qua kênh khác (tìm kiếm, thông báo, AI, log) | **Mở** cho kênh chưa tồn tại; nguyên tắc bắt buộc khi thêm | |
| OPS-DEC-01: ánh xạ Dispatch vào ba vai trò | **Đã duyệt** (Lead là năng lực của nhóm) | ADR-016 |
| OPS-DEC-05: `/api` hay `/api/v1`, `expectedRevision` | **Đã triển khai**: giữ `/api` và `expectedRevision` | Hợp đồng hiện hành |

### 1.6 [06-attachment-security.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/06-attachment-security.md) (ATT-DEC-01–10, ATT-01–20)

| Nhóm quy tắc | Trạng thái | Nơi chuyển hoặc đối chiếu |
| --- | --- | --- |
| ATT-DEC-01, ATT-06: hạn mức tường minh | Một tệp 10 MiB: **Đã triển khai**. Ứng viên B3 kiểm 10 tệp/phiếu và 50 MiB/phiếu khi xác nhận. Giới hạn grant chờ xác nhận, hết hạn và lưu giữ: **Mở** | DEC-07 |
| ATT-DEC-02, ATT-07: danh sách định dạng | **Lịch sử** cho ứng viên "PNG, JPEG, TXT"; chính sách chốt là pdf/png/jpg/jpeg/docx/txt/zip (**Đã triển khai** theo đuôi tệp) | DEC-07 |
| ATT-08: kiểm tra chữ ký nội dung | **Mở** (mã đối chiếu dung lượng và loại thực tế, chưa kiểm chữ ký) | DEC-07 |
| ATT-DEC-03, ATT-11: truyền tệp qua API (stream) thay URL ký | **Mở** (hiện dùng URL/biểu mẫu ký; chưa duyệt đổi) | DEC-06 |
| ATT-DEC-04, 05, 09, 10: kho riêng, tên khóa do server sinh, đối tượng bất biến, thiếu cấu hình thì ngừng tải lên | **Đã triển khai** một phần (khóa theo tiền tố `tickets/{id}/attachments/`, sao chép sang khóa cuối, 503 khi kho lỗi); cấu hình kho riêng thuộc hạ tầng | Hợp đồng hiện hành, repo hạ tầng |
| ATT-DEC-06, ATT-14: quét mã độc | **Mở** (chưa có) | DEC-07 |
| ATT-DEC-07: tệp nội bộ gắn ghi chú nội bộ | **Mở** (mã chưa có khái niệm tệp nội bộ) | |
| ATT-DEC-08, ATT-16, ATT-18: không sửa/xóa, dọn đối tượng mồ côi, lưu giữ | Không có sửa/xóa: **Đã triển khai**. Dọn mồ côi và lưu giữ: **Mở** | DEC-07 |
| ATT-01–05, 12, 13, 15, 17, 19, 20 | **Đã triển khai** một phần; đối chiếu từng điều khi nghiệm thu tệp | |

## 2. Tám hồ sơ hợp đồng lịch sử (mức tệp)

Chủ dự án chỉ yêu cầu ánh xạ nguồn thay thế ở mức tệp. Các tệp này đã mang nhãn lịch sử và không cấp quyền triển khai. Các điểm còn mở của chúng đã có trong bảng quyết định DEC-01–08; đã được đối chiếu với cột cuối trước khi đưa ra khỏi HEAD.

| Tệp (đã đưa ra khỏi HEAD; bản tại `db756d2`) | Nguồn thay thế hiện hành | Quyết định liên quan |
| --- | --- | --- |
| [01-rbac-and-api-permission-matrix.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/01-rbac-and-api-permission-matrix.md) | [Ma trận quyền](rbac-permission-matrix.md), ADR-016 | DEC-02 |
| [02-ticket-domain-and-field-contract.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/02-ticket-domain-and-field-contract.md) | [Hợp đồng hiện hành](current-backend-contract.md), ADR-016 | DEC-01, DEC-02 |
| [03-ticket-lifecycle-and-visibility-baseline.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/03-ticket-lifecycle-and-visibility-baseline.md) | Hợp đồng hiện hành (không remap enum) | DEC-01, DEC-03–05 |
| [04-ticket-state-transition-and-reopen-matrix.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/04-ticket-state-transition-and-reopen-matrix.md) | Hợp đồng hiện hành, bảng quyết định; 168 giờ/72 giờ không phải quyết định production | DEC-03–05 |
| [05-comment-and-internal-note-privacy-contract.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/05-comment-and-internal-note-privacy-contract.md) | Ma trận quyền (mục ghi chú nội bộ), quy tắc bảo mật | DEC-02, DEC-03 |
| [06-ticket-assignment-policy.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/06-ticket-assignment-policy.md) | ADR-016, ma trận quyền | DEC-02 |
| [07-attachment-security-limits-and-policy.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/07-attachment-security-limits-and-policy.md) | Ma trận quyền (mục tệp đính kèm); 30 MiB/phiếu cũ bị thay bằng 50 MiB/phiếu đã duyệt | DEC-06, DEC-07 |
| [08-sprint-acceptance-and-governance-record.md](https://github.com/supportflow-ai/ai-it-service-desk/blob/db756d27f07819cefca208548d8ecc8155fa0d48/docs/architecture/08-sprint-acceptance-and-governance-record.md) | [Hồ sơ Sprint 1](../sprint-1/README.md); nhãn nghiệm thu cũ không ký nhận mã mới | DEC-08 |

## 3. Điều kiện và trạng thái đưa tệp ra khỏi HEAD

- Tám tệp lịch sử: **đã đưa ra khỏi HEAD** (xóa khỏi cây tệp, vẫn truy cập qua URL ghim `db756d2`). `AGENTS.md` có ngoại lệ lưu trữ hẹp cho đúng tám tệp này; `docs/context-sources.json` và chỉ mục kiến trúc đã cập nhật; trong tài liệu hiện hành và hồ sơ bằng chứng chỉ liên kết tương đối bị gãy được thay bằng URL ghim SHA, nội dung giữ nguyên. Các nhắc tên tệp dạng văn bản thường (không phải liên kết) trong hồ sơ bằng chứng và sáu đề xuất được giữ nguyên.
- Sáu đề xuất: chỉ xét khi các dòng **Mở** và **Đã duyệt – chưa triển khai** đã nằm trong ADR, bảng quyết định hoặc backlog có chủ sở hữu. Bảng 1.x là danh sách để đối chiếu.
- Mỗi PR xóa phải chạy `python scripts/validate_project_docs.py`, `python -m unittest discover -s scripts/tests -p "test_project_docs.py"`, `git diff --check` và kiểm tra `git grep` các đường dẫn cũ.
