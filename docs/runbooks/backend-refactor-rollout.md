# Triển khai backend sau tái cấu trúc và khôi phục phiên bản

Trạng thái: hướng dẫn vận hành hiện hành; cập nhật 2026-10-20. Đối chiếu mã `dev` tại `47634bc8162c13bd6ae024233ec24a0076738c51`. Quy trình không xác nhận đã nghiệm thu hoặc đã triển khai commit này. Người vận hành phải có nhiệm vụ và đích môi trường được xác định rõ.

## Phạm vi và nguồn cấu hình

| Thành phần | Nguồn sở hữu |
| --- | --- |
| Backend | https://github.com/supportflow-ai/ai-it-service-desk |
| Giao diện | https://github.com/supportflow-ai/ai-it-service-desk-web |
| Dịch vụ sử dụng hợp đồng AI | https://github.com/supportflow-ai/ai-service |
| Compose, cổng truy cập và kiểm tra toàn hệ thống | https://github.com/supportflow-ai/supportflow-ai-infra |

Câu lệnh Database và tạo ảnh thuộc backend; ghép hệ thống và triển khai thuộc kho hạ tầng. Lệnh chạy trên máy phát triển nằm trong [hướng dẫn môi trường](environment-runbook.md).

## Chuẩn bị và bảo toàn dữ liệu

Ghi lại commit backend/giao diện/hạ tầng, kết quả CI, tên dự án Compose, ảnh đang chạy và tên vùng dữ liệu thực tế. Giữ dữ liệu PostgreSQL và kho đối tượng hiện có.

Khi chuyển kho hoặc tên dự án Compose, cấu hình vùng dữ liệu ngoài trỏ tới đúng vùng cũ; kiểm tra để tránh vô tình tạo vùng trống thay thế. Sao lưu cơ sở dữ liệu và xác minh phục hồi ở môi trường thử nghiệm. Lệnh `down -v` chỉ dành cho hệ thống CI được xác nhận dùng một lần.

Chuẩn bị cửa sổ bảo trì, người phụ trách, phiên bản khôi phục và tiêu chí dừng. Quyền ứng dụng chỉ gồm quyền đọc/ghi dữ liệu, sequence/hàm cần thiết; tài khoản sở hữu cấu trúc dùng riêng cho migration.

## Các bước ở môi trường thử nghiệm hoặc production

1. Đối chiếu các commit đã ghim với kết quả kiểm chứng đúng phiên bản. Các điểm quyền/ghi đồng thời còn mở trong [hợp đồng backend](../architecture/current-backend-contract.md#các-điểm-chưa-đạt-cần-xử-lý) phải có kết luận trước khi nghiệm thu phạm vi liên quan.
2. Cấp cấu hình cơ sở dữ liệu/kho đối tượng và khóa ký JWT riêng ít nhất 32 byte UTF-8. Giữ khóa mạnh, issuer và audience đang dùng; thay khóa mẫu theo kế hoạch quản trị.
3. Tạo ảnh API, Worker và Database từ cùng commit backend. Ghim ảnh bất biến trong cấu hình hạ tầng.
4. Trong cửa sổ đã thống nhất, xác minh đúng đích rồi chạy Database `migrate`, sau đó `seed-roles`. Migration sửa số phiếu chỉ đổi cách cấp số tiếp theo, giữ số cũ và sequence.
5. Chạy Database `check-production` trong phạm vi được giao. Nếu phát hiện tài khoản mẫu còn mật khẩu mẫu, dừng nghiệm thu, đổi mật khẩu qua quy trình quản trị và xem xét vai trò. Hành vi và giới hạn của lệnh nằm trong [hướng dẫn môi trường](environment-runbook.md#các-lệnh-tài-khoản-và-check-production).
6. Nếu cần tạo quản trị viên đầu tiên, dùng `bootstrap-admin` với `BootstrapAdmin__Email`/`BootstrapAdmin__Password` chỉ trên tiến trình một lần. Lệnh từ chối tài khoản đã tồn tại. Giữ các giá trị này ngoài tiến trình API/Worker.
7. Khởi động các bản API và Worker. Kiểm tra `/health/live`, `/health/ready`, `/health`; thử đăng ký, đăng nhập, tạo, liệt kê và xem phiếu qua cổng chung. Kiểm tra phát lại cùng khóa qua hai bản API và xem nhật ký lỗi.
8. Với phiên bản có sáu thao tác PR #28, kiểm tra thêm cập nhật/nhận xử lý/giải quyết/mở lại/đóng/hủy, quyền bị từ chối và trạng thái không hợp lệ theo kết luận được chọn. Ghi rõ phần kiểm thử tự động của kho hạ tầng chưa bao phủ; không suy từ luồng kiểm tra cũ sang phần mới.

Công cụ Database trả 0 khi thành công, 1 khi lỗi thao tác/cấu hình và 2 khi cách gọi không hợp lệ. Đầu ra không chứa thông tin xác thực hoặc chi tiết ngoại lệ thô. `seed-demo` yêu cầu Development/Test, `SeedDemo__Enabled=true` và `SeedDemo__Password` được cấp rõ ràng; không nằm trong trình tự khởi động production.

## Kết quả cần ghi nhận và điều kiện dừng

Biên bản ghi commit/ảnh, đích môi trường không bí mật, thời điểm, lệnh thực chạy, kết quả kiểm tra sức khỏe, các luồng đã thử và phần chưa thử. Thành công của lệnh `check-production` chỉ xác nhận phạm vi hẹp được mô tả trong hướng dẫn môi trường.

Khi migration, kiểm tra sức khỏe hoặc nghiệp vụ thất bại, giữ nhật ký đã che bí mật và dừng bước tiếp theo. Quyết định khôi phục hoặc đưa bản sửa tiếp dựa trên trạng thái dữ liệu và kế hoạch đã chuẩn bị.

## Khôi phục phiên bản trước

Khôi phục ảnh ứng dụng bằng commit đã giữ lại. Với migration bổ sung hàm cấp số phiếu đang tương thích mã cũ, giữ cấu trúc/dữ liệu và khôi phục ứng dụng; không chạy migration Down trên dữ liệu thật theo quy trình này.

Ảnh cũ có thể còn tự tạo tài khoản khi khởi động. Sau sửa bảo mật, ưu tiên đưa bản sửa tiếp; chỉ chọn ảnh khôi phục có biện pháp chặn tạo dữ liệu mẫu và kiểm tra lại tài khoản trước nghiệm thu. Việc khôi phục ảnh không hoàn tác thay đổi mật khẩu/khóa ký.

Thay đổi cấu trúc tương lai cần phương án tương thích và khôi phục riêng theo migration thực tế.

## Các phần phát triển tiếp

Dọn biên nhận có phối hợp, AI thật và thông tin xác thực dịch vụ, phiên bản nghiệp vụ/dữ liệu đề xuất AI, phân công, phần vòng đời còn thiếu, SLA, lịch sử nghiệp vụ, theo dõi vận hành và hồ sơ người dùng tiếp tục là phạm vi riêng. Sáu thao tác PR #28 chưa hoàn tất các phần này.
