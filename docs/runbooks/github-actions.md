# Vận hành GitHub Actions

Trạng thái: hướng dẫn hiện hành; cập nhật 2026-10-20. Đối chiếu các quy trình đã công bố tại mốc backend `47634bc8162c13bd6ae024233ec24a0076738c51`; cấu hình cụ thể lấy từ từng kho và commit đang kiểm tra.

## Nguồn và phạm vi

Nguồn quy trình là `.github/workflows/ci.yml` trong các kho backend, giao diện, AI và hạ tầng. GitHub Actions thay Jenkins theo [ADR-013](../adr/ADR-013-github-actions.md).

Backend kiểm thử và tạo ảnh API/Worker/Database. Giao diện tự kiểm tra mã, kiểm thử và tạo ảnh tại kho riêng. Hạ tầng ghim mã backend/giao diện rồi chạy hệ thống tạm qua cổng truy cập. Kho AI kiểm tra phiên bản, checksum và dữ liệu mẫu hợp đồng.

Ảnh tạo trên máy chạy kiểm tra được thu dọn, chưa phát hành lên kho ảnh. Quy trình hiện chưa tự triển khai production.

## Kiểm tra tài liệu trong backend

Công việc `backend` chạy `scripts/validate_project_docs.py` và các tình huống hồi quy trước bước .NET. Công cụ dùng Python đã thiết lập trên máy chạy CI, không cần cơ sở dữ liệu hoặc gói bổ sung.

Tài liệu mới phải được phân loại trong `docs/context-sources.json` hoặc thuộc nhóm thư mục đã khai báo. Liên kết tới tệp local bị Git bỏ qua không phải nguồn đã công bố. Đối chiếu thêm [quy chuẩn tài liệu](../process/documentation-standard.md), nhất là ngôn ngữ và liên kết tới tiêu đề mà công cụ chưa kiểm tra.

Thay đổi tài liệu không tự thay điều kiện kích hoạt, quyền truy cập, phát hành ảnh hoặc triển khai. CI vẫn chạy đầy đủ các bước được quy trình cấu hình.

## Kiểm tra một thay đổi

1. Khi nhiệm vụ gồm đưa mã lên GitHub và mở PR, kiểm tra kết quả tại thẻ **Actions** và phần kiểm tra của PR. Ghi đúng commit được chạy.
2. Backend: xem công việc `backend`; tải `backend-tests` để đọc TRX và bản hợp đồng AI có phiên bản khi cần. Kiểm thử PostgreSQL chạy bằng Testcontainers trên máy Linux của GitHub.
3. Giao diện: xem công việc `frontend` cho kiểm tra quy cách mã, kiểm thử và tạo ảnh.
4. Hạ tầng: `sources.lock.json` phải ghim chính xác commit backend/giao diện đã được đánh giá. Công việc `smoke` lấy các commit đó, dựng hệ thống tạm, chạy migration/vai trò rồi kiểm tra qua cổng chung. Nhật ký được xuất cả khi thất bại; vùng dữ liệu CI được thu dọn theo quy trình. Không áp dụng cách thu dọn đó vào môi trường có dữ liệu cần giữ.
5. AI: khi thay hợp đồng được ghim, đối chiếu checksum, phiên bản và dữ liệu mẫu của các kho liên quan.

Chạy thủ công qua **Actions → tên quy trình → Run workflow** khi quy trình đã có trên nhánh mặc định; có thể dùng nút chạy lại của lượt hiện hữu. Hủy lượt lỗi thời không thay production vì chưa có công việc triển khai.

## Chuyển kiểm tra bắt buộc và dừng Jenkins cũ

Sau khi thay đổi quy trình được hợp nhất, quản trị viên cấu hình kiểm tra GitHub Actions bắt buộc trước khi bỏ yêu cầu Jenkins tương ứng. Xác minh một PR mới thực sự nhận đủ kết quả thành công. Nội dung quy trình không chứng minh GitHub đã bật bảo vệ nhánh.

Chỉ dừng công việc/webhook/máy chủ Jenkins bên ngoài khi quản trị viên xác nhận không còn dự án khác phụ thuộc. Giữ hồ sơ và vùng dữ liệu cần thiết; việc xóa Jenkinsfile không tự xóa máy chủ hoặc thu hồi thông tin xác thực dùng chung.

## Quyền và phát hành

CI hiện dùng `contents: read`, chưa cần bí mật triển khai hoặc kho ảnh cho các kho công khai. Nếu một kho chuyển riêng tư, thay đổi quyền đọc chéo kho của hạ tầng cần được đánh giá riêng và giới hạn đúng phạm vi.

Phát hành dùng ảnh bất biến theo [hướng dẫn triển khai backend](backend-refactor-rollout.md) và bàn giao của kho hạ tầng. Việc bổ sung kho ảnh hoặc tự động triển khai cần thiết kế và nhiệm vụ riêng.
