# ADR-016: Nhóm hỗ trợ, định tuyến phiếu và phạm vi đọc của Agent

## Trạng thái và nguồn quyết định

Hướng thiết kế và chính sách ở mục "Đã chốt thêm" được chủ dự án phê duyệt ngày 2026-11-08 qua chỉ dẫn trực tiếp. **Triển khai theo giai đoạn.** B1-PR1 thêm bảng nhóm, thành viên, cột `tickets.support_team_id` và hàng đợi `MANUAL_TRIAGE`. B1-PR2 đưa phiếu mới và phiếu cũ chưa kết thúc vào triage, đồng thời thêm `ISupportTeamMembershipReader`. B1-PR3 áp dụng phạm vi đọc và chặn lệnh ghi vào phiếu ngoài phạm vi. Luồng chuyển nhóm, cấp phép chia sẻ lịch sử ghi chú, quyền điều phối Lead và định tuyến thuộc các đợt sau.

Quan hệ: làm rõ và thay thế phần "chưa cách ly theo nhóm hoặc phân công" của [ADR-014](ADR-014-feature-use-cases-and-published-contracts.md); đóng phần hướng của DEC-02 trong [bảng quyết định còn mở](../architecture/pending-business-decisions.md); ma trận quyền ở [RBAC chính thức](../architecture/rbac-permission-matrix.md); nguồn yêu cầu cũ ở [bảng kiểm kê](../architecture/legacy-requirements-inventory.md).

## Bối cảnh

Tại `dev@db756d2`, Agent/Admin đọc chi tiết, timeline và tệp của mọi phiếu (`TicketReadScope.AllTickets`); mọi Agent đều gán được phiếu cho Agent khác; `Ticket` không có `SupportTeamId`. `Department` là phòng ban của người gửi (`RequesterDepartmentId`, nullable), không phải nơi xử lý. Các đề xuất cũ về team và Lead nằm rải rác trong `docs/architecture/01–08`, có chỗ mâu thuẫn nhau. AI-service chỉ đề xuất Category, Impact, Urgency và `TriagePolicy` cấm AI tự chuyển trạng thái hay chốt độ ưu tiên.

## Quyết định

1. **`SupportTeam` là khái niệm nghiệp vụ độc lập, tách hoàn toàn khỏi `Department`.** `Department` tiếp tục là phòng ban của Requester và được ghi nhận (snapshot) vào `RequesterDepartmentId`. Không suy quyền hay nhóm xử lý từ `Department`.
2. **Dữ liệu.** `Ticket.SupportTeamId` nullable trong schema. `SupportTeamMember(TeamId, UserId, IsLead)` ghi thành viên của nhóm.
3. **Lead không phải vai trò JWT thứ tư.** Vai trò JWT vẫn là Requester, Agent, Admin. "Support Team Lead" là khả năng (capability) sinh ra từ `IsLead` của thành viên active trong nhóm; quyền này chỉ có hiệu lực trong nhóm đó và được kiểm ở phía server, không đọc từ claim hay dữ liệu frontend.
4. **Phạm vi xử lý của Agent.** Agent xử lý phiếu thuộc `SupportTeam` đang hoạt động mà mình là thành viên hoặc phiếu được phân công trực tiếp. Admin giữ phạm vi toàn hệ thống. Chủ phiếu giữ quyền đọc công khai trên phiếu mình tạo, kể cả khi có vai trò Agent. Phiếu ngoài phạm vi của thao tác trả `404`. [Ma trận quyền](../architecture/rbac-permission-matrix.md)
5. **Quyền điều phối của Lead** trong nhóm mình phụ trách: xác nhận định tuyến, phân công, phân công lại và hủy phân công, theo policy được mô tả trong ma trận RBAC.
6. **Hàng đợi phân loại thủ công.** Có đúng một `SupportTeam` đặt `IsTriageQueue = true`. Phiếu chưa định tuyến được hoặc định tuyến không chắc chắn nằm ở đó.
7. **Định tuyến theo bảng.** `RoutingRule` ánh xạ `TicketCategory → SupportTeam`. Backend là nơi duy nhất quyết định định tuyến.
8. **Ranh giới với AI.** AI chỉ đề xuất Category, Impact, Urgency. AI không chọn nhóm bằng quyền riêng và **không chọn Agent cụ thể**. Tự định tuyến (auto-routing) nếu có chỉ là policy của backend, áp dụng điều kiện do backend định nghĩa và ghi audit; không phải quyền trực tiếp của AI-service. Quy tắc SEC trong `TriagePolicy` vẫn buộc phân loại thủ công.
9. **Không mở rộng quyền bằng nguồn không tin cậy.** Không suy quyền nhóm từ `Department`, claim phía client hoặc dữ liệu frontend.

## Đã chốt thêm (2026-11-08, chủ dự án)

- **Phiếu cũ chưa có nhóm.** Phiếu có `SupportTeamId = null` và trạng thái khác Closed/Cancelled (theo giá trị số đã lưu: mọi trạng thái trừ 5 và 6) được chuyển vào `MANUAL_TRIAGE` bằng migration dữ liệu riêng; Closed/Cancelled lịch sử giữ null. Phiếu mới luôn vào `MANUAL_TRIAGE`. Không suy nhóm từ `RequesterDepartmentId`, danh mục hay `AssignedToId`; phiếu có người phụ trách vẫn có thể thuộc hàng đợi phân loại (nhóm sở hữu và người phụ trách là hai chiều khác nhau). Cột vẫn nullable để biểu diễn dữ liệu lịch sử chưa định tuyến. Việc ánh xạ danh mục sang nhóm là `RoutingRule` của B2, không phải migration dữ liệu.
- **Agent thuộc nhiều nhóm.** Quyền đọc là hợp của mọi nhóm đang hoạt động mà Agent là thành viên, cộng với phiếu được phân công trực tiếp. Việc chọn "nhóm đang làm việc" trên giao diện chỉ là bộ lọc, không phải ngữ cảnh bảo mật; không lưu nhóm hiện tại trong phiên hay JWT. Lead được xét riêng từng nhóm.
- **Thành viên hợp lệ** = có dòng trong `SupportTeamMember` và `SupportTeam.IsActive = true`. Thu hồi tư cách là xóa dòng thành viên; quyền thành viên được đọc từ cơ sở dữ liệu, không từ JWT. Chưa thêm `IsActive`, ngày bắt đầu/kết thúc hay lịch sử thành viên (chỉ nâng cấp thành bảng membership đầy đủ khi cần audit). Không tự thêm Agent nào vào `MANUAL_TRIAGE`; thành viên là dữ liệu cấp quyền có chủ đích.

- **Ghi chú nội bộ khi chuyển nhóm.** Ghi chú đi cùng phiếu để nhóm mới có đủ lịch sử xử lý. Agent thấy ghi chú theo quyền đọc hiện tại trên phiếu: là thành viên của một nhóm đang hoạt động đang sở hữu phiếu, hoặc đang được phân công trực tiếp. Agent mất quyền xem ghi chú nếu quyền trước đó chỉ đến từ nhóm cũ; không có cơ chế chia sẻ riêng. Admin xem toàn hệ thống. Requester, kể cả khi đồng thời là Agent nhưng chỉ có quyền chủ sở hữu, chỉ thấy nội dung công khai.
- **Thu hồi quyền nhóm.** Mỗi request cần quyết định quyền theo nhóm kiểm tra PostgreSQL qua `ISupportTeamMembershipReader`, không đọc từ JWT. Xóa thành viên, vô hiệu hóa nhóm (`is_active = false`) hoặc gỡ `IsLead` có hiệu lực từ request kế tiếp sau khi giao dịch thay đổi đã commit, kể cả khi access token còn hạn. Agent còn được phân công trực tiếp vẫn đọc phiếu theo quyền phân công. Gỡ Lead không xóa quyền của thành viên còn hợp lệ. Quy tắc này áp dụng cho quyền theo nhóm; vòng đời các vai trò toàn hệ thống trong JWT là quyết định riêng (xem [hợp đồng hiện hành](../architecture/current-backend-contract.md)).

## Phương án SupportTeam đã chốt

Chủ dự án chốt ngày 21/11/2026 `TEAM-01` và `TEAM-04`. Đây là danh mục và luồng mục tiêu. Tại Backend `dev@3640c33`, phiếu mới vào `MANUAL_TRIAGE` và B1-PR3 áp dụng quyền đọc, hàng đợi và lệnh của nhân viên. Quy tắc định tuyến, xác nhận đội, chuyển đội và điều phối của Lead còn chờ triển khai. [Ma trận quyền hiện hành](../architecture/rbac-permission-matrix.md)

| Đội mục tiêu | Phạm vi xử lý |
| --- | --- |
| IT Helpdesk | Hỗ trợ cơ bản, tài khoản, đăng nhập và hướng dẫn sử dụng |
| Network Support | LAN, Wi-Fi, VPN và kết nối nội bộ |
| Software Support | Phần mềm, ứng dụng và email |
| Hardware Support | Máy tính, máy in, thiết bị ngoại vi và hỗ trợ thiết bị |
| Access Support | Cấp, đổi và thu hồi quyền truy cập |
| Security & Infrastructure Support | Bảo mật, máy chủ và hạ tầng |

Kích hoạt từng đội theo nhân sự trực thực tế. `Department` vẫn là phòng ban người gửi. `SupportTeam` là đơn vị xử lý. Ranh giới mã Category và ánh xạ sang đội cần chốt theo `TEAM-03`.

1. Backend dùng Category để đề xuất đội. Phiếu `SEC` tiếp tục phân loại thủ công.
2. Người có thẩm quyền xác nhận đội nhận trước khi gán Agent. Chưa xác định được đội thì giữ phiếu ở `MANUAL_TRIAGE`. Đã xác nhận đội nhưng chưa gán người thì lưu `SupportTeamId` của đội nhận và để `AssignedToId` trống.
3. Khi chuyển đội, kiểm lại người đang phụ trách theo điều kiện của đội đích rồi giữ, gỡ hoặc gán lại. Quyền từ phân công trực tiếp phải được xét cùng quyền theo nhóm. Ghi chú nội bộ đi cùng phiếu.
4. Chỉ đưa Tier vào luật khi cấp kỹ thuật làm thay đổi đội nhận hoặc điều kiện chọn Agent. Tiêu chí L1/L2/L3 thuộc `TEAM-02`. Quyền Lead trong nhóm đã có hướng ở trên. Người xác nhận liên đội, quy tắc cụ thể cho người phụ trách cũ và thứ tự phối hợp Backend G1 với AI Service P1 còn mở theo `TEAM-05/06`.

### Ca kiểm chứng quyền và bàn giao

Phương án cần chốt tại `TEAM-05/06` là kiểm riêng quyền người chuyển và điều kiện người nhận ở đội đích. Giữ người phụ trách cũ khi chính sách cho phép và họ vẫn đủ điều kiện. Các ca còn lại chuyển sang người đủ điều kiện hoặc để `AssignedToId` trống. Ghi đội, người phụ trách, người xác nhận, lý do và audit trong cùng giao dịch.

| Mốc | Ca | Kết quả cần kiểm chứng |
| --- | --- | --- |
| Phạm vi đang có | Agent thuộc đội đang hoạt động của phiếu | Thấy phiếu trong hàng đợi, đọc chi tiết, timeline có ghi chú nội bộ và xin URL tải tệp theo quyền hiện tại |
| Phạm vi đang có | Agent được gán trực tiếp và không thuộc đội | Có quyền đọc phiếu từ phân công trực tiếp |
| Phạm vi đang có | Agent ngoài phạm vi, không phải chủ phiếu | Hàng đợi loại phiếu khỏi danh sách và số đếm. Chi tiết, timeline và xin URL tải trả `404` |
| Phạm vi đang có | Requester hoặc Agent chỉ có quyền chủ phiếu | Xem chi tiết, timeline công khai và tệp của phiếu mình tạo. Timeline loại ghi chú nội bộ |
| Phạm vi đang có | Admin và Agent trực triage | Admin thấy phiếu toàn hệ thống. Agent trực đã được cấp thành viên `MANUAL_TRIAGE` thấy phiếu mới chưa gán |
| Phạm vi đang có | Agent đủ quyền thao tác hoặc Admin gán phiếu cho Admin | API hiện chấp nhận người nhận có vai trò Agent hoặc Admin. Danh bạ mặc định lọc Agent. Điều kiện thành viên đội đích thuộc thiết kế chuyển đội |
| Khi có luồng định tuyến | Chưa có đích hoặc đã chọn đội nhưng thiếu Agent | Đối chiếu hai trường hợp `MANUAL_TRIAGE` và đội đích có `AssignedToId` trống |
| Khi có luồng chuyển đội | Người phụ trách cũ mất cả quyền theo nhóm và phân công | Phiếu vắng trong hàng đợi và thao tác gửi ghi chú nội bộ bị từ chối, kể cả khi Agent cũ là chủ phiếu. Chủ phiếu giữ quyền công khai, với timeline loại ghi chú nội bộ. Nếu Agent cũ không phải chủ phiếu, đọc chi tiết, timeline và xin URL tải trả `404`. Kiểm riêng quyền của người vẫn hợp lệ ở đội đích và audit của lần chuyển |

Phần chuyển đội kiểm trên bản ứng viên có luồng tương ứng. Quyền xin URL được kiểm ở từng yêu cầu. Thời hạn hoặc thu hồi URL đã cấp thuộc `DEC-06`. [Quyết định nghiệp vụ](../architecture/pending-business-decisions.md)

## Phương án đã cân nhắc

- **Dùng `Department` làm nhóm xử lý:** bị loại; trộn nơi người gửi làm việc với nơi xử lý và làm quyền đọc lệ thuộc dữ liệu hồ sơ.
- **Thêm vai trò JWT "Lead":** bị loại; quyền Lead phụ thuộc từng nhóm nên không thể là vai trò toàn hệ thống, và claim cũ trong token không thu hồi tức thời.
- **Để AI tự gán nhóm hoặc Agent:** bị loại; backend giữ mọi quyết định ghi và audit.
- **Giữ phạm vi đọc toàn hệ thống cho Agent:** bị loại cho đích dài hạn vì lộ phiếu ngoài nhóm; giữ nguyên cho tới khi triển khai.

## Hệ quả và hướng triển khai

- Cần migration thêm bảng và cột (không đổi enum đã lưu), kế hoạch gán nhóm cho phiếu cũ, lọc phạm vi đọc ở truy vấn Ticketing, và kiểm thử PostgreSQL cho từ chối, người nhiều vai trò, thu hồi thành viên.
- OpenAPI thay đổi có chủ đích ở endpoint nhóm và gán nhóm; giữ tương thích với API hiện có.
- B1-PR3 áp dụng phạm vi nhóm/phân công cho lệnh của nhân viên trước khi kiểm trạng thái và revision. Chủ sở hữu vẫn dùng các thao tác Requester trên phiếu của mình. Agent chỉ thấy timeline công khai nếu truy cập qua quyền chủ sở hữu.
- B1 đã bổ sung bảng nhóm, dữ liệu seed và phạm vi đọc. Thứ tự mở định tuyến, chuyển đội và điều phối Lead chờ `TEAM-06`. Tự định tuyến, nếu được duyệt sau này, thuộc policy của Backend.
