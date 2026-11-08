# ADR-016: Nhóm hỗ trợ, định tuyến phiếu và phạm vi đọc của Agent

## Trạng thái và nguồn quyết định

Hướng thiết kế đã được chủ dự án phê duyệt ngày 2026-11-08 qua chỉ dẫn trực tiếp trong phiên làm việc với trợ lý; chưa có biên bản riêng trong repo và chưa có mã Jira. **Chưa triển khai**: không có bảng, migration, endpoint hay thay đổi quyền nào trong mã tại `dev@db756d2`. Bốn chi tiết ở mục "Còn mở" chưa được chốt. Khi triển khai, từng thay đổi dùng nhiệm vụ riêng và thêm kiểm thử từ chối.

Quan hệ: làm rõ và thay thế phần "chưa cách ly theo nhóm hoặc phân công" của [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md); đóng phần hướng của DEC-02 trong [bảng quyết định còn mở](../architecture/pending-business-decisions.md); ma trận quyền ở [RBAC chính thức](../architecture/rbac-permission-matrix.md); nguồn yêu cầu cũ ở [bảng kiểm kê](../architecture/legacy-requirements-inventory.md).

## Bối cảnh

Tại `dev@db756d2`, Agent/Admin đọc chi tiết, timeline và tệp của mọi phiếu (`TicketReadScope.AllTickets`); mọi Agent đều gán được phiếu cho Agent khác; `Ticket` không có `SupportTeamId`. `Department` là phòng ban của người gửi (`RequesterDepartmentId`, nullable), không phải nơi xử lý. Các đề xuất cũ về team và Lead nằm rải rác trong `docs/architecture/01–08`, có chỗ mâu thuẫn nhau. AI-service chỉ đề xuất Category, Impact, Urgency và `TriagePolicy` cấm AI tự chuyển trạng thái hay chốt độ ưu tiên.

## Quyết định

1. **`SupportTeam` là khái niệm nghiệp vụ độc lập, tách hoàn toàn khỏi `Department`.** `Department` tiếp tục là phòng ban của Requester và được ghi nhận (snapshot) vào `RequesterDepartmentId`. Không suy quyền hay nhóm xử lý từ `Department`.
2. **Dữ liệu.** `Ticket.SupportTeamId` nullable trong schema. `SupportTeamMember(TeamId, UserId, IsLead)` ghi thành viên của nhóm.
3. **Lead không phải vai trò JWT thứ tư.** Vai trò JWT vẫn là Requester, Agent, Admin. "Support Team Lead" là khả năng (capability) sinh ra từ `IsLead` của thành viên active trong nhóm; quyền này chỉ có hiệu lực trong nhóm đó và được kiểm ở phía server, không đọc từ claim hay dữ liệu frontend.
4. **Phạm vi đọc của Agent.** Agent chỉ đọc phiếu khi (a) phiếu thuộc một `SupportTeam` mà Agent là thành viên active, hoặc (b) phiếu được phân công trực tiếp cho Agent. Admin giữ phạm vi toàn hệ thống. Requester chỉ đọc phiếu của mình. Phiếu ngoài phạm vi trả 404 như hiện nay.
5. **Quyền điều phối của Lead** trong nhóm mình phụ trách: xác nhận định tuyến, phân công, phân công lại và hủy phân công, theo policy được mô tả trong ma trận RBAC.
6. **Hàng đợi phân loại thủ công.** Có đúng một `SupportTeam` đặt `IsTriageQueue = true`. Phiếu chưa định tuyến được hoặc định tuyến không chắc chắn nằm ở đó.
7. **Định tuyến theo bảng.** `RoutingRule` ánh xạ `TicketCategory → SupportTeam`. Backend là nơi duy nhất quyết định định tuyến.
8. **Ranh giới với AI.** AI chỉ đề xuất Category, Impact, Urgency. AI không chọn nhóm bằng quyền riêng và **không chọn Agent cụ thể**. Tự định tuyến (auto-routing) nếu có chỉ là policy của backend, áp dụng điều kiện do backend định nghĩa và ghi audit; không phải quyền trực tiếp của AI-service. Quy tắc SEC trong `TriagePolicy` vẫn buộc phân loại thủ công.
9. **Không mở rộng quyền bằng nguồn không tin cậy.** Không suy quyền nhóm từ `Department`, claim phía client hoặc dữ liệu frontend.

## Còn mở (chưa quyết định, thuộc DEC-02)

- Agent thuộc nhiều nhóm: gộp quyền đọc hay bắt chọn nhóm làm việc.
- Quyền xem lịch sử ghi chú nội bộ sau khi phiếu chuyển nhóm.
- Xử lý phiếu cũ chưa có `SupportTeamId`.
- Hiệu lực tức thời khi bị gỡ thành viên hoặc gỡ quyền Lead, khi access token đã phát vẫn còn hạn (xem giới hạn thu hồi quyền trong [hợp đồng hiện hành](../architecture/current-backend-contract.md)).

Không đặt giá trị mặc định cho bốn điểm này trong mã trước khi chủ dự án chốt.

## Phương án đã cân nhắc

- **Dùng `Department` làm nhóm xử lý:** bị loại; trộn nơi người gửi làm việc với nơi xử lý và làm quyền đọc lệ thuộc dữ liệu hồ sơ.
- **Thêm vai trò JWT "Lead":** bị loại; quyền Lead phụ thuộc từng nhóm nên không thể là vai trò toàn hệ thống, và claim cũ trong token không thu hồi tức thời.
- **Để AI tự gán nhóm hoặc Agent:** bị loại; backend giữ mọi quyết định ghi và audit.
- **Giữ phạm vi đọc toàn hệ thống cho Agent:** bị loại cho đích dài hạn vì lộ phiếu ngoài nhóm; giữ nguyên cho tới khi triển khai.

## Hệ quả và hướng triển khai

- Cần migration thêm bảng và cột (không đổi enum đã lưu), kế hoạch gán nhóm cho phiếu cũ, lọc phạm vi đọc ở truy vấn Ticketing, và kiểm thử PostgreSQL cho từ chối, người nhiều vai trò, thu hồi thành viên.
- OpenAPI thay đổi có chủ đích ở endpoint nhóm và gán nhóm; giữ tương thích với API hiện có.
- Các lệnh ghi hiện cho mọi Agent/Admin (nhận, giải quyết, bình luận) sẽ cần quyết định riêng về phạm vi theo nhóm; điều kiện đọc ở mục 4 là điều kiện cần.
- Triển khai theo thứ tự: bảng nhóm và dữ liệu seed; gán nhóm và định tuyến thủ công; lọc đọc; Lead điều phối; auto-routing dưới policy backend và độ tin cậy do chủ dự án chọn.
