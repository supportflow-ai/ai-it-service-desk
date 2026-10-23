# Các quyết định nghiệp vụ còn mở

Trạng thái: các phần chưa chọn là đề xuất chờ quyết định; phần DEC-02 về claim đã được chọn ở cuối tài liệu. Bổ sung ngày 2026-10-23 trên nền `47da281`, thay đổi chưa commit. Mốc đối chiếu trước đó: `dev@47634bc8162c13bd6ae024233ec24a0076738c51` ngày 2026-10-20. [Hợp đồng hiện hành](current-backend-contract.md) và [ranh giới kiến trúc](backend-modules-and-use-cases.md) mô tả phần đã có.

PR #28 đã thêm cập nhật, nhận xử lý, giải quyết, mở lại, đóng và hủy phiếu. Sự tồn tại của các lệnh này chưa chốt mọi lựa chọn trong bảng: nhận xử lý chưa có người phụ trách; mở lại chưa có giới hạn thời gian; đợt củng cố ngày 2026-10-23 đã đồng bộ Agent/Admin cho claim và kiểm tra quyền resolve trước persistence; các chính sách mới còn lại vẫn cần chốt. Ghi nhận mã đang chạy tách biệt với việc chọn chính sách nghiệp vụ.

Người quyết định là chủ dự án; Backend/Frontend/QA hoặc phụ trách vận hành cung cấp đánh giá theo tác động. Bảng này không gán công việc Jira, không xác nhận một phê duyệt bên ngoài chưa được kiểm chứng. Quyết định một phần DEC-02 về claim đã được chủ dự án chọn ngày 2026-10-23: Agent và Admin đều được nhận xử lý. Các lựa chọn còn lại trong bảng tiếp tục chờ quyết định.

| Mã | Cần quyết định | Các nguồn/phương án khác nhau | Điều kiện trước triển khai |
| --- | --- | --- | --- |
| DEC-01 | Draft/Submit và hợp đồng đầu vào | [Field cũ](02-ticket-domain-and-field-contract.md) đổi POST hiện tại sang Draft, category GUID tùy chọn; [đề xuất domain](03-ticket-domain-contract.md) giữ đường cũ và cân nhắc luồng Draft riêng | Chốt API, numbering, department/profile, giới hạn trường; phương án tương thích dữ liệu và caller; không đổi nghĩa enum đã lưu |
| DEC-02 | Quyền nhân viên hỗ trợ | Hiện Agent/Admin đọc detail toàn hệ thống; [RBAC đề xuất](02-rbac-permission-matrix.md) dùng team/assignment/dispatch; thu hẹp theo owner cũng là thay chính sách | Chốt phạm vi đọc/ghi, người nhiều role, Lead, nguồn dữ liệu team/assignment, hiệu lực thu hồi; có test bị từ chối và kế hoạch chuyển đổi |
| DEC-03 | Phản hồi PendingUser | [Privacy cũ](05-comment-and-internal-note-privacy-contract.md) tự InProgress; [đề xuất comment](05-assignment-and-comment-policy.md) giữ trạng thái, staff resume riêng | Chốt quyền chuyển trạng thái, ảnh hưởng thời gian chờ/SLA, audit và retry |
| DEC-04 | Resolve từ PendingExternal | [Ma trận cũ](04-ticket-state-transition-and-reopen-matrix.md) cho resolve trực tiếp; [lifecycle đề xuất](04-ticket-lifecycle-policy.md) yêu cầu resume | Một đồ thị chuyển trạng thái, kiểm thử mọi nguồn bị cấm, không thêm generic status setter |
| DEC-05 | Reopen và tự đóng | Bản cũ: reopen 168 giờ nhưng auto-close 72 giờ và Closed là terminal; bản đề xuất hoãn job, xét assignee/team khi reopen | Chốt cửa sổ thực tế, terminal state, trường hợp assignee mất hiệu lực, lịch sử resolution; chưa tạo job |
| DEC-06 | Cơ chế tải tệp | [Attachment cũ](07-attachment-security-limits-and-policy.md): URL ký sẵn 900 giây; [đề xuất](06-attachment-security.md): API kiểm quyền và truyền nội dung | Chốt khả năng thu hồi/chia sẻ URL, quyền nội bộ, tải từng phần, giới hạn lưu lượng và ngân sách tải |
| DEC-07 | Dung lượng, định dạng, retention/xóa tệp | Bản cũ 10 MiB/file, 30 MiB/ticket, 10 file, xóa trong một giờ; bản đề xuất chưa duyệt giới hạn, chưa mở xóa MVP | Chốt quota khi upload đồng thời, MIME/extension/parser, quét mã độc hoặc chấp nhận rủi ro, lưu giữ và xóa có kiểm soát; không coi 512 byte đầu là chứng nhận tệp an toàn |
| DEC-08 | Biên bản nghiệm thu | [Governance cũ](08-sprint-acceptance-and-governance-record.md) ghi accepted; [TC-285](../sprint-1/TC-285/review.md) ghi blocked tại SHA/ngày khác | QA và chủ dự án xác nhận theo đúng SHA và môi trường; không lấy nhãn cũ thay bằng chứng hiện tại |

## Ghi nhận quyết định

Mỗi quyết định mới phải có: người quyết định và căn cứ yêu cầu/phê duyệt, ngày, phạm vi/phiên bản, phương án được chọn, phương án không chọn, tài liệu được thay thế, tác động API/dữ liệu, kiểm thử và cách chuyển đổi/rollback. Chỉ cập nhật các mục liên quan; không tự sửa lịch sử nghiệm thu hoặc thông số nghiệp vụ ở tất cả tài liệu cũ.

Các nguyên tắc kỹ thuật không chờ chọn chính sách: không lộ tài nguyên ngoài quyền; không đổi nghĩa enum đã lưu; Application sở hữu authorization; ranh giới module được bảo vệ bằng kiểm thử. Xem [quy tắc thiết kế bảo mật](security-design-rules.md). Chốt một dòng trong bảng không tự cho phép triển khai các dòng khác.

## Quyết định một phần DEC-02: quyền nhận xử lý

- Người quyết định: chủ dự án, qua lựa chọn trực tiếp “Agent và Admin” trong phiên làm việc ngày 2026-10-23.
- Phạm vi: `ClaimTicketUseCase`; Agent/Admin được nhận xử lý phiếu Submitted. Chọn đồng bộ Application với policy HTTP hiện tại, không chọn thu hẹp HTTP thành chỉ Agent.
- Tác động: Admin chỉ có vai trò Admin trước đây bị từ chối trong use case, nay được phép; không đổi route, JSON hoặc schema dữ liệu. Không suy ra quyền theo team/assignment hoặc mở rộng quyền các lệnh khác.
- Kiểm chứng: unit/use case và `TicketLifecycleBoundaryTests` bao phủ Agent, Admin, Requester, nhiều vai trò và caller chưa xác thực. Có kiểm thử không đồng nghĩa đã có kết quả nghiệm thu; đối chiếu lần chạy tại đúng mã.
- Chuyển đổi/rollback: không cần migration; khôi phục ứng dụng trước thay đổi sẽ khôi phục giới hạn claim cũ. Các phần khác của DEC-02 vẫn mở.
