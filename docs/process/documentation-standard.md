# Quy chuẩn viết và cập nhật tài liệu

Trạng thái: hướng dẫn hiện hành cho kho backend; cập nhật 2026-10-20. Chủ trì nội dung: người thực hiện thay đổi; người đánh giá đối chiếu phạm vi được giao. Quy chuẩn này chuẩn hóa tài liệu, không tự thay quyền thao tác Git, cơ sở dữ liệu hoặc hệ thống bên ngoài.

## Ngôn ngữ và cách diễn đạt

Viết tiêu đề, đoạn giải thích, bảng, chú thích sơ đồ và hướng dẫn bằng tiếng Việt có dấu. Dùng câu ngắn, nêu hành động và điều kiện cụ thể. Ưu tiên cách gọi trong [bảng thuật ngữ](technical-glossary.md).

Giữ nguyên tên lớp/hàm, namespace, tên vai trò, enum, trường JSON, biến môi trường, lệnh, đường dẫn và tên công nghệ. Thuật ngữ tiếng Anh cần thiết được giải thích ở lần xuất hiện đầu; phần sau dùng tiếng Việt nhất quán.

Ví dụ: viết “kiểm tra quyền truy cập”, “hướng dẫn vận hành”, “mốc chuẩn”, “khôi phục phiên bản trước” trong phần diễn giải; giữ nguyên `RequireAuthorization`, `README.md`, `Idempotency-Key` khi chỉ định thành phần kỹ thuật. Không dịch dữ liệu mẫu kiểm chứng, mã băm hoặc nội dung nhật ký gốc.

Giữ đường dẫn tệp hiện có để bảo toàn liên kết. Tệp mới dùng tên ngắn, không dấu, dấu gạch nối; tiêu đề bên trong dùng tiếng Việt. Viết UTF-8; giữ định dạng xuống dòng theo quy định của kho.

## Một nguồn chính cho mỗi chủ đề

| Chủ đề | Nguồn chính | Nội dung các tài liệu khác nên dẫn tới |
| --- | --- | --- |
| Cách bắt đầu và phạm vi kho | [README](../../README.md), [bản đồ tài liệu](../README.md) | Đường dẫn đọc và nguồn sở hữu từng thành phần |
| Cấu trúc đang triển khai | [Mô-đun và luồng xử lý](../architecture/backend-modules-and-use-cases.md) | Chiều phụ thuộc, trách nhiệm, giao diện công khai |
| Hành vi API hiện hành | [Hợp đồng backend](../architecture/current-backend-contract.md) | Đường dẫn, đầu vào, quyền, trạng thái, phản hồi và giới hạn |
| Lý do quyết định kiến trúc | [Danh mục ADR](../adr/README.md) | Bối cảnh, phương án, quyết định và quan hệ thay thế |
| Lựa chọn chưa thống nhất | [Quyết định còn mở](../architecture/pending-business-decisions.md) | Phạm vi cần chọn, người quyết định và tác động |
| Cấu hình/chạy trên máy phát triển | [Hướng dẫn môi trường](../runbooks/environment-runbook.md) | Điều kiện, cấu hình, công cụ và câu lệnh |
| Kiểm chứng | [Quy trình kiểm chứng](../runbooks/backend-hardening-verification.md) | Kiểm tra theo phạm vi, cách ghi bằng chứng |
| Triển khai/khôi phục | [Quy trình triển khai](../runbooks/backend-refactor-rollout.md) | Ghim phiên bản, dữ liệu, kiểm tra và khôi phục |
| Giao diện/hạ tầng/AI độc lập | Các kho được dẫn từ README | Chỉ mô tả ranh giới tích hợp trong kho backend |

Một hướng dẫn nhanh có thể dẫn tới nguồn chính và đưa ví dụ tối thiểu. Khi câu lệnh hoặc điều kiện được lặp ở nhiều nơi, thay đổi phải đối chiếu tất cả các bản còn dùng; không tạo thêm gói sao chép toàn bộ tài liệu.

## Trạng thái và mốc nguồn

Phân loại theo [danh mục nguồn](../context-sources.json):

| Loại | Cách ghi và sử dụng |
| --- | --- |
| Hiện hành (`current`) | Mô tả đúng phạm vi đang áp dụng; ghi ngày và commit đã đối chiếu khi mô tả mã |
| Quyết định (`decision`) | Giữ ngày quyết định, phạm vi, lý do và quan hệ bổ sung/thay thế |
| Đề xuất (`proposal`) | Nêu lựa chọn còn mở; dẫn tới quyết định cần chốt; không coi là hành vi đã có |
| Lịch sử (`historical`) | Nhãn đầu tệp, phạm vi gốc, liên kết nguồn hiện hành; giữ nội dung và kết luận gốc |
| Bằng chứng (`evidence`) | Chỉ chứng minh lệnh/kết quả tại ngày, commit và môi trường đã ghi |

Mốc nguồn của tài liệu khác với mốc đã chạy kiểm thử. Nếu nhánh local chưa chứa commit được tài liệu mô tả, ghi rõ sự khác biệt khi bàn giao. Chú thích mới và bản tóm tắt tiếng Việt đặt ngoài phần bằng chứng gốc.

Tài liệu hiện hành nên có: mục đích, phạm vi/đối tượng đọc, ngày cập nhật, mốc nguồn nếu liên quan mã, nội dung chính, giới hạn và liên kết kiểm chứng. Hướng dẫn vận hành bổ sung điều kiện chuẩn bị, các bước, kết quả mong đợi, cách dừng/xử lý lỗi và khôi phục khi phù hợp.

## Cập nhật cùng thay đổi chức năng

1. Xác định mã nguồn, API, cấu hình và tài liệu bị ảnh hưởng.
2. Đối chiếu cùng commit; ghi rõ phần triển khai khác với đề xuất hoặc yêu cầu chưa thống nhất.
3. Cập nhật nguồn chính, sau đó chỉ mục, liên kết và tài liệu liên quan.
4. Giữ nguyên lịch sử; phần bổ sung nêu ngày, nguồn và phạm vi thay thế.
5. Chạy kiểm tra phù hợp; ghi lệnh/kết quả thực chạy vào bàn giao.
6. Trong PR, liệt kê tài liệu đã cập nhật hoặc giải thích cụ thể vì sao không cần cập nhật.

Trước khi chốt tập tệp công bố, kiểm tra các công cụ/tệp được tài liệu dẫn tới có thực sự thuộc thay đổi Git dự kiến. Công cụ kiểm tra tài liệu hiện chấp nhận cả tệp mới chưa được Git quản lý; vì vậy kết quả đạt tại máy không bảo đảm checkout sau khi tích hợp có đủ công cụ. Đối chiếu cả `.gitignore` của nhánh đích; ví dụ hai công cụ khởi chạy/kết nối đang được nêu tại [bảng mốc nguồn](../architecture/source-baseline.md#công-cụ-local-và-quy-tắc-git).

Không có mã Jira hoặc nhãn APPROVED nào tự chứng minh chức năng đã được kiểm chứng. Cách làm việc và quyền thực hiện theo nhiệm vụ được giao; quy chuẩn này không tự phát sinh tác vụ trên Jira hoặc tạo PR.

## Khi API hoặc DTO thay đổi

Đối chiếu phương thức/đường dẫn, đầu vào, kiểu dữ liệu, vai trò, phạm vi tài nguyên, chuyển trạng thái, mã phản hồi, header và khả năng tương thích dữ liệu.

Bản chuẩn `contracts/backend/openapi-baseline.json` phải được rà soát trên đúng nhánh chứa mã API tương ứng. Nếu dùng cơ chế xuất `EXPORT_API_BASELINE=1` trong `ApiContractTests`, chỉ thực hiện trong bản làm việc đã chọn rõ, xem từng mục thay đổi và chạy lại sau khi tắt biến xuất. Đây không phải bước thông thường của kiểm thử.

Kiểm thử `PublishedApiMatchesBaseline` so sánh toàn bộ OpenAPI, đồng thời giữ kiểm tra chi tiết các đường dẫn/lược đồ đã công bố. Đường dẫn hoặc schema mới chưa có trong bản chuẩn, mô tả và cấu hình bảo mật thay đổi đều phải được phát hiện. Ảnh chụp OpenAPI không xác nhận thứ tự kiểm tra quyền, che giấu tài nguyên hoặc xử lý ghi đồng thời; các phần đó cần kiểm thử hành vi.

Đợt đồng bộ ngày 2026-10-20 được chuẩn bị trên dev sau PR #29: bản chuẩn có 16 đường dẫn, gồm sáu thao tác vòng đời mới và `UpdateTicketRequest`. Xem mốc nguồn và bằng chứng trong [báo cáo rà soát](../reviews/documentation-audit-20261020.md).

## Kiểm tra trước khi bàn giao

Từ gốc kho, dùng Python phù hợp với workflow:

```sh
python scripts/validate_project_docs.py
python -m unittest discover -s scripts/tests -p "test_project_docs.py"
```

Kiểm tra thêm bằng cách đọc phần chênh lệch: ngôn ngữ dễ hiểu; liên kết đến tiêu đề sau khi dịch; câu lệnh đúng thư mục/cấu hình; nguồn mới đã phân loại; tài liệu lịch sử không bị sửa kết quả.

Công cụ hiện kiểm tra tệp và phân loại nguồn, chưa xác nhận nội dung nghiệp vụ, URL mạng, liên kết đến tiêu đề hoặc thực thi lệnh vận hành. Chỉ tài liệu không cần tự chạy toàn bộ kiểm thử ứng dụng hay thao tác cơ sở dữ liệu. Thay đổi chức năng cần hợp các kiểm tra trong [điều kiện hoàn thành](definition-of-ready-done.md).
