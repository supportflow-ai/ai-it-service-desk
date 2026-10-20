# ADR-013: GitHub Actions cho kiểm tra từng kho và toàn hệ thống

## Trạng thái

Đã chấp nhận ngày 2026-10-18, thay [ADR-009](ADR-009-jenkins-ci-cd.md) theo yêu cầu chủ dự án. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Bối cảnh

Backend, giao diện, hợp đồng AI và hạ tầng đã tách thành các kho GitHub. Duy trì Jenkinsfile song song với GitHub Actions làm trùng các bước kiểm tra và cần thêm máy chủ CI.

## Quyết định

Dùng GitHub Actions làm nền tảng tích hợp liên tục đang áp dụng. Loại Jenkinsfile, cấu hình Jenkins trong Compose/máy chủ và công cụ lấy mã chỉ phục vụ Jenkins khỏi nguồn hiện hành; giữ lịch sử Git và bằng chứng đánh giá cũ.

Phạm vi được chọn là kiểm thử, tạo ảnh ứng dụng và kiểm tra nhanh toàn hệ thống. Ảnh được tạo trên máy chạy kiểm tra tạm thời, chưa được phát hành. Quyết định này không bổ sung triển khai production, thông tin xác thực môi trường, bí mật kho ảnh hoặc quyền ghi gói.

| Kho | Kiểm tra |
| --- | --- |
| Backend | Khôi phục gói theo khóa, biên dịch, kiểm thử đơn vị/kiến trúc/tương thích không cần cơ sở dữ liệu, tích hợp PostgreSQL thật, hợp đồng AI và tạo ảnh API/Worker/Database |
| Giao diện | Cài gói theo khóa, kiểm tra quy cách mã, biên dịch, kiểm thử và tạo ảnh |
| AI | OpenAPI, checksum/phiên bản và dữ liệu mẫu không gọi AI thật |
| Hạ tầng | Ghim commit backend/giao diện, kiểm tra Compose, migration/vai trò, hệ thống tạm và luồng người yêu cầu qua cổng chung |

Backend/giao diện kiểm tra khi có PR và khi đẩy lên các nhánh đã cấu hình. Chạy thủ công có khi quy trình nằm trên nhánh mặc định. Hạ tầng kiểm tra khi đẩy mã, có PR hoặc chạy thủ công. Các lượt cũ của cùng quy trình/nhánh được hủy theo cấu hình đồng thời. Giữ định danh công việc `backend`, `frontend`, `smoke` để ổn định tên kết quả kiểm tra.

## Hệ quả

Không cần máy chủ Jenkins, phần mở rộng Jenkins, mật khẩu quản trị hoặc gắn Docker socket vào Jenkins. Máy chạy Linux do GitHub cung cấp có Docker để dùng Testcontainers/Compose.

Quy tắc bảo vệ nhánh và máy chủ Jenkins bên ngoài cần được quản trị viên thay đổi riêng. Xóa cấu hình trong mã nguồn không xóa máy chủ hoặc vùng dữ liệu đang tồn tại.

Phát hành ảnh và tự động triển khai tiếp tục là quyết định tương lai. Hướng dẫn triển khai hiện tại dùng ảnh ghim phiên bản và lệnh Database có chủ đích. Xem [hướng dẫn GitHub Actions](../runbooks/github-actions.md).

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-013-github-actions.md).
