# ADR-016: Nhóm hỗ trợ, định tuyến phiếu và phạm vi đọc của Agent

## Trạng thái và nguồn quyết định

Hướng thiết kế đã được chủ dự án phê duyệt ngày 2026-11-08 qua chỉ dẫn trực tiếp trong phiên làm việc với trợ lý; chưa có biên bản riêng trong repo và chưa có mã Jira. **Triển khai theo giai đoạn.** Giai đoạn nền dữ liệu (B1-PR1) chỉ thêm bằng migration cộng thêm `AddSupportTeams`: bảng `support_teams`, `support_team_members`, cột nullable `tickets.support_team_id` và hàng đợi `MANUAL_TRIAGE` có ID cố định; không gán nhóm cho phiếu mới hay cũ. Giai đoạn B1-PR2 thêm: phiếu mới vào `MANUAL_TRIAGE` ngay khi tạo, migration dữ liệu `BackfillLegacyTicketsToManualTriage` và cổng đọc thành viên nhóm (`ISupportTeamMembershipReader`, chưa nối vào phạm vi đọc). Endpoint, định tuyến, phạm vi đọc theo nhóm và quyền Lead **chưa triển khai**: mã vẫn cho Agent/Admin đọc mọi phiếu như mô tả ở mục Bối cảnh (mốc `dev@db756d2`). Hai chi tiết ở mục "Còn mở" chưa được chốt; hai chi tiết còn lại đã được chốt ngày 2026-11-08 (mục "Đã chốt thêm"). Khi triển khai, từng thay đổi dùng nhiệm vụ riêng và thêm kiểm thử từ chối.

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

## Đã chốt thêm (2026-11-08, chủ dự án)

- **Phiếu cũ chưa có nhóm.** Phiếu có `SupportTeamId = null` và trạng thái khác Closed/Cancelled (theo giá trị số đã lưu: mọi trạng thái trừ 5 và 6) được chuyển vào `MANUAL_TRIAGE` bằng migration dữ liệu riêng; Closed/Cancelled lịch sử giữ null. Phiếu mới luôn vào `MANUAL_TRIAGE`. Không suy nhóm từ `RequesterDepartmentId`, danh mục hay `AssignedToId`; phiếu có người phụ trách vẫn có thể thuộc hàng đợi phân loại (nhóm sở hữu và người phụ trách là hai chiều khác nhau). Cột vẫn nullable để biểu diễn dữ liệu lịch sử chưa định tuyến. Việc ánh xạ danh mục sang nhóm là `RoutingRule` của B2, không phải migration dữ liệu.
- **Agent thuộc nhiều nhóm.** Quyền đọc là hợp của mọi nhóm đang hoạt động mà Agent là thành viên, cộng với phiếu được phân công trực tiếp. Việc chọn "nhóm đang làm việc" trên giao diện chỉ là bộ lọc, không phải ngữ cảnh bảo mật; không lưu nhóm hiện tại trong phiên hay JWT. Lead được xét riêng từng nhóm.
- **Thành viên hợp lệ** = có dòng trong `SupportTeamMember` và `SupportTeam.IsActive = true`. Thu hồi tư cách là xóa dòng thành viên; quyền thành viên được đọc từ cơ sở dữ liệu, không từ JWT. Chưa thêm `IsActive`, ngày bắt đầu/kết thúc hay lịch sử thành viên (chỉ nâng cấp thành bảng membership đầy đủ khi cần audit). Không tự thêm Agent nào vào `MANUAL_TRIAGE`; thành viên là dữ liệu cấp quyền có chủ đích.

## Còn mở (chưa quyết định, thuộc DEC-02)

- Quyền xem lịch sử ghi chú nội bộ sau khi phiếu chuyển nhóm.
- Hiệu lực tức thời khi bị gỡ thành viên hoặc gỡ quyền Lead, khi access token đã phát vẫn còn hạn (xem giới hạn thu hồi quyền trong [hợp đồng hiện hành](../architecture/current-backend-contract.md)). Cổng đọc thành viên hiện đọc cơ sở dữ liệu ở mỗi lần gọi nên bản thân việc kiểm thành viên không bị trễ; điểm còn mở là các quyết định dựa trên claim đã phát.

Không đặt giá trị mặc định cho hai điểm này trong mã trước khi chủ dự án chốt.

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
