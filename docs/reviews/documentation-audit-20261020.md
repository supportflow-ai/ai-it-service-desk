# Rà soát toàn bộ tài liệu và phạm vi kho ngày 2026-10-20

Trạng thái: báo cáo rà soát và khắc phục ngày 2026-10-20 (Asia/Bangkok). Rà soát ban đầu so sánh local `334485ddf46e19812c55b77812d16da20695eafd` với `dev@47634bc8162c13bd6ae024233ec24a0076738c51`. Bản khắc phục được chuẩn bị trên đúng mốc dev này tại nhánh `codex/docs-vi-rebaseline`.

## Kết luận sau khắc phục

Ba điểm DOC-01–03 đã được xử lý trong cùng thay đổi: mã nền theo dev sau PR #29; công cụ Python được công bố nhất quán với quy tắc Git; OpenAPI và kiểm thử đã bao phủ toàn bộ tài liệu API. Các kiểm tra tại bản làm việc này đạt như phần kết quả bên dưới. Trước hợp nhất cần CI tại commit mới và đánh giá các phần thay đổi.

Các khoảng cách hành vi về quyền và ghi đồng thời vẫn còn trong hợp đồng hiện hành; các quyết định sản phẩm còn mở giữ nguyên trạng thái. Việc hoàn thành đồng bộ tài liệu/hợp đồng không xác nhận đã sửa các lỗi nghiệp vụ hoặc nghiệm thu toàn hệ thống.

## Kết luận trước khắc phục

Phần phân loại, liên kết và ngôn ngữ tài liệu đã được sửa ở các điểm rõ ràng. Chưa nên coi toàn bộ gói mã/tài liệu đã sẵn sàng tích hợp: mã local chưa chứa PR #28/#29, bộ công cụ local xung đột quy tắc bỏ qua ở nhánh đích, và bản chuẩn API chưa bao phủ mã mới.

Đợt này không đổi nhánh, kéo mã vào bản làm việc có thay đổi, sửa mã chức năng, cập nhật ảnh chụp API, xóa hồ sơ hoặc thao tác cơ sở dữ liệu. Các quyết định sản phẩm tương lai giữ trạng thái còn mở; chúng không bắt buộc phải được chọn để hoàn thành một đợt chỉ chuẩn hóa tài liệu.

## Phạm vi đã rà soát

- Ban đầu có 95 Markdown được công bố/dự kiến công bố trong bản làm việc: 24 hiện hành, 13 quyết định (gồm một chỉ mục bị phân loại sai), 7 đề xuất, 17 lịch sử, 34 bằng chứng.
- Đã kiểm tra toàn bộ danh mục/phân loại/liên kết; đọc đối chiếu nguồn hiện hành, quan hệ quyết định–đề xuất–lịch sử, hướng dẫn chạy, mẫu GitHub và các điều kiện nghiệp vụ đang khác nhau.
- Kiểm tra 22 JSON trong `docs/`; ba bản nguồn được manifest ghi SHA-256 khớp nguyên trạng.
- Kiểm tra tên/tập tệp Git và thư mục local; xem thêm 16 tệp trong bộ ngữ cảnh cũ bị bỏ qua. Không đọc cấu hình bí mật hoặc kết nối lại VPS.
- Đối chiếu PR #29, mã phân quyền/bộ ghi liên quan, bản chuẩn API, CI dev và khóa nguồn của kho hạ tầng.
- Không kiểm tra lại mọi URL ngoài mạng hoặc chạy nghiệm thu ứng dụng/toàn hệ thống; đây là đánh giá tài liệu và mã tham chiếu.

## Phát hiện có tác động trước khi tích hợp

| Mã | Mức | Phát hiện và bằng chứng | Xử lý/trạng thái |
| --- | --- | --- | --- |
| DOC-01 | P1 | README/hợp đồng ghi 12 luồng từ dev, trong khi HEAD local chỉ có sáu; các liên kết mã tương đối mở bản local | Đã thêm [bảng mốc nguồn](../architecture/source-baseline.md) và cảnh báo ở các điểm vào. Trước tích hợp vẫn cần đưa mã/tài liệu về cùng mốc |
| DOC-02 | P1 | Hướng dẫn dùng hai công cụ khởi chạy/kết nối chưa được Git quản lý; [gitignore của dev](https://github.com/supportflow-ai/ai-it-service-desk/blob/47634bc8162c13bd6ae024233ec24a0076738c51/.gitignore) lại bỏ qua đúng hai đường dẫn đó | Đã ghi giới hạn và điều kiện công bố. Cần thống nhất phạm vi công cụ/quy tắc Git trên nhánh đích; chưa tự đưa tệp vào index |
| DOC-03 | P1 | Bản chuẩn OpenAPI có 10 đường dẫn, thiếu sáu thao tác mới và `UpdateTicketRequest`; kiểm thử chỉ duyệt mục đã có. PR #29 đổi mô tả Swagger nhưng phần này cũng chưa được so sánh đầy đủ | Đã mô tả đúng khoảng cách. Cập nhật bản chuẩn và kiểm tra phạm vi API cùng mã tương ứng; không xuất lại trên mã local cũ |
| DOC-04 | P2 | `local-startup-verification.md` là kết quả ngày 2026-10-19 nhưng bị nhóm runbooks phân loại hiện hành | Đã khai báo riêng là bằng chứng và gắn nguồn kiểm chứng thay thế; giữ số liệu cũ |
| DOC-05 | P2 | `docs/adr/README.md` là chỉ mục nhưng bị nhóm ADR phân loại thành quyết định | Đã khai báo riêng là chỉ mục hiện hành |
| DOC-06 | P2 | Hai mẫu báo lỗi/đề xuất GitHub còn tiếng Anh | Đã Việt hóa phần người dùng đọc; giữ nhãn/định danh kỹ thuật |
| DOC-07 | P2 | Hướng dẫn VPS chứa thông tin máy đã thiết lập; dễ hiểu số liệu/mật khẩu local/khả năng kết nối là hiện trạng mọi máy | Đã phân biệt mốc bàn giao với hướng dẫn mới; làm rõ mỗi máy phải nhận cấu hình và đường quản trị DB dùng chung khác DB riêng local |
| DOC-08 | P2 | PR #29 gọi Ticket là “vé”, receipt là “hóa đơn”; phần mô tả lỗi có chuỗi đã đổi ngôn ngữ trong khi một số chỗ nói chung “giữ lỗi hiện tại” | Đã làm rõ thuật ngữ và khác biệt hình dạng phản hồi/nội dung thông báo. Chú thích mã dev cần thống nhất trong thay đổi mã riêng |

P1: cần xử lý trước khi khẳng định gói tích hợp mã/tài liệu nhất quán. P2: sai lệch về phân loại, ngôn ngữ hoặc độ rõ của hướng dẫn.

## Mâu thuẫn đã được khoanh theo nguồn

Các hồ sơ cũ có chú thích lịch sử và các đề xuất có trạng thái chờ quyết định. Các cặp dưới đây không còn được xem là nhiều yêu cầu hiện hành cùng có hiệu lực.

| Phạm vi | Các nguồn khác nhau | Phần áp dụng tại mốc dev / quyết định còn mở |
| --- | --- | --- |
| Tạo phiếu và dữ liệu | Hợp đồng trường cũ yêu cầu Draft, danh mục GUID, mã TK, tiêu đề 200; đề xuất khác và mã hiện tại giữ cách tạo cũ | Tạo Submitted, danh mục chuỗi, mã IT, tiêu đề đầu vào 100; DEC-01 quyết định thay đổi tương lai |
| Quyền nhân viên hỗ trợ | Phạm vi đọc toàn hệ thống so với nhóm/phân công/Lead | Quyền đọc hiện hành được ghi rõ; DEC-02 còn mở. Riêng lỗi Agent/Admin ở nhận xử lý vẫn cần sửa mã |
| Phản hồi khi chờ người yêu cầu | Tự chuyển InProgress so với nhân viên thực hiện lệnh tiếp tục riêng | Chưa triển khai bình luận; DEC-03 |
| Giải quyết khi chờ bên ngoài | Cho giải quyết từ PendingExternal so với phải tiếp tục xử lý trước | Mã hiện chỉ giải quyết từ InProgress; DEC-04 |
| Mở lại/tự đóng | Mở lại 168 giờ nhưng tự đóng 72 giờ và Closed kết thúc; đề xuất khác hoãn tác vụ | Mã hiện mở lại Resolved không kiểm tra thời gian, chưa tự đóng; DEC-05 |
| Tải tệp | URL ký sẵn 900 giây so với API kiểm quyền và truyền nội dung | Chưa có nghiệp vụ tệp; DEC-06 |
| Giới hạn/lưu giữ tệp | Các giới hạn dung lượng, số lượng và thời gian xóa cũ so với đề xuất chưa duyệt | Chưa chọn giới hạn nghiệp vụ mới; DEC-07 |
| Nghiệm thu | Biên bản cũ ghi chấp thuận so với hồ sơ Sprint 1 có kết luận bị chặn tại mốc khác | Giữ nguyên cả hai mốc; CI mới không thay xác nhận QA/nghiệm thu; DEC-08 |
| Số dự án/mô-đun/luồng | Bốn tầng/năm mô-đun mục tiêu/sáu luồng tại đợt tái cấu trúc so với mã dev mới | Sáu dự án, ba mô-đun đã triển khai, 12 luồng ở dev; các con số lịch sử giữ đúng phạm vi |
| CI và kết quả kiểm thử | Jenkins hoặc báo cáo kiểm thử lỗi cũ so với GitHub Actions hiện tại | ADR-013 thay Jenkins; kết quả theo đúng commit/môi trường, không ghi đè lịch sử |

Nguồn chọn chính sách là [bảng quyết định còn mở](../architecture/pending-business-decisions.md); nguồn mô tả mã là [hợp đồng hiện hành](../architecture/current-backend-contract.md). Mã enum đã lưu không được đổi nghĩa để khớp bảng lịch sử.

## Tệp và thư mục còn liên quan hay có thể dọn

| Tệp/thư mục | Tình trạng thực tế | Kết luận |
| --- | --- | --- |
| `src/frontend/`, tệp package Node, Jenkinsfile, Compose triển khai trong tập Git backend | Không có trong tập tệp đang được quản lý; cũng không có trong cây dev đã kiểm tra | Không phát hiện mã frontend hoặc cấu hình triển khai toàn hệ thống bị sót trong tập này |
| `ai-it-service-desk-claude-context-pack/` | 16 tệp local, 0 tệp công bố; chứa bối cảnh nguyên khối cả giao diện/backend, Jenkins và giới hạn dựng khung cũ | Không phải nguồn hiện hành; ứng viên lưu trữ ra ngoài hoặc xóa khi dọn local. Ba bản nguồn cần truy vết đã có trong Sprint 1 và khớp mã băm |
| `planning/` | Thư mục trống, không có tệp Git | Có thể dọn khỏi máy; không ảnh hưởng mã đang công bố |
| `evaluation/datasets/`, `evaluation/reports/` | Chỉ có hai tệp giữ thư mục, chưa có quy trình đánh giá AI hoạt động | Chỗ giữ cho phạm vi tương lai, chưa dùng để phát triển hiện tại; cân nhắc bỏ hoặc bổ sung hướng dẫn khi mở phạm vi AI |
| `Application/Knowledge/`, `Application/Analytics/` | Mỗi thư mục chỉ có tệp giữ thư mục | Chưa có mô-đun triển khai; không tính vào ba mô-đun hiện có. Việc dọn cần nhất quán với định hướng sản phẩm |
| `artifacts/`, `docs/reviews/`, `docs/sprint-1/` và tám hợp đồng lịch sử | Có bằng chứng/kết luận được dẫn để truy vết | Còn liên quan về lịch sử; giữ nguyên bản gốc, không dùng làm hướng dẫn triển khai mới |
| Hai công cụ Python và kiểm thử local | Chưa được Git quản lý nhưng phục vụ trực tiếp cấu hình/kết nối backend | Còn liên quan; xử lý quy tắc công bố, không xếp vào tệp rác |
| Đoạn giao diện/NGINX/AI trong README và ADR | Mô tả ranh giới tích hợp giữa các kho | Còn liên quan; mã và hướng dẫn chi tiết của từng thành phần nằm ở kho sở hữu |

Không xóa tệp/thư mục trong đợt review này. Không có Markdown trùng nội dung hoàn toàn trong 95 tệp ban đầu; nhiều hồ sơ nói cùng chủ đề là các mốc khác nhau.

## Bằng chứng và giới hạn kiểm chứng

Trước chỉnh sửa bổ sung: công cụ tài liệu đạt 95 tệp/498 liên kết; toàn bộ 28 kiểm thử Python đang có đạt.

Kết quả cuối sau chỉnh sửa:

| Kiểm tra | Kết quả |
| --- | --- |
| Nguồn, chú thích và liên kết cục bộ | 97 Markdown, 518 liên kết đạt |
| Phân loại | 25 hiện hành, 12 quyết định, 7 đề xuất, 17 lịch sử, 36 bằng chứng |
| Liên kết tới tiêu đề Markdown | 52 liên kết hợp lệ, không có đích thiếu |
| Kiểm thử Python đang có | 28/28 đạt |
| Bản gốc dưới dấu `original-record` | 32/32 phần giữ nguyên nội dung so với đầu lần review |
| Markdown trùng toàn bộ / dấu xung đột Git | Không phát hiện |
| Tệp ngoài tài liệu và danh mục nguồn | Không phát sinh thay đổi trong lần review này |
| Định dạng phần chênh lệch tài liệu | `git diff --check` đạt |
| HEAD / index | HEAD giữ nguyên, chưa đưa thay đổi vào index hoặc commit |

22 JSON hợp lệ; ba SHA-256 bản nguồn trong manifest khớp. Phần bản gốc của hồ sơ lịch sử và đề xuất được giữ; chỉ bổ sung diễn giải ở phần ngoài hoặc chỉ mục.

CI dev tại [lượt 39424511333](https://github.com/supportflow-ai/ai-it-service-desk/actions/runs/39424511333) thành công cho commit dev mới, không bao gồm thay đổi local. Kho hạ tầng ở nhánh `codex/infra-handoff` vẫn ghim backend `9dd0746…`, nên kiểm tra theo khóa đó không xác nhận dev mới.

Không chạy .NET/PostgreSQL/Docker/toàn hệ thống cho thay đổi chỉ tài liệu, không xác minh lại VPS hoặc trạng thái Jira. Các điểm quyền và ghi đồng thời được xác nhận bằng đọc mã mới, chưa tái hiện HTTP trong lần này.


## Kết quả khắc phục ba điểm trước tích hợp

| Mã | Cách xử lý trong nhánh chuẩn hóa | Trạng thái |
| --- | --- | --- |
| DOC-01 | Dựng bản làm việc từ đúng cây dev `cdb664cc…` gồm 313 tệp, xác minh mã băm từng tệp; giữ lại các thay đổi tài liệu/công cụ local trước khi sửa tiếp | Đã đồng bộ mã nền và tài liệu |
| DOC-02 | Gỡ hai dòng bỏ qua `run_backend.py`/`start_database_tunnel.py`; đưa hai công cụ, kiểm thử, cấu hình mẫu và hướng dẫn vào cùng tập công bố | Đã thống nhất phạm vi Git |
| DOC-03 | Xuất OpenAPI từ ứng dụng sau PR #29; thêm sáu đường dẫn và `UpdateTicketRequest`, đồng bộ mô tả/bảo mật; kiểm thử so sánh toàn bộ OpenAPI | Đã hoàn thiện bản chuẩn và phạm vi kiểm tra |

Không thay đổi các đường dẫn hoặc lược đồ đã có trong bản chuẩn cũ. Thử tạm bỏ `/api/tickets/{id}/claim` khỏi bản chuẩn khiến kiểm thử hợp đồng thất bại đúng tại phép so sánh toàn bộ; đã khôi phục bản chuẩn sau phép thử. Lần chạy xác nhận tắt `EXPORT_API_BASELINE`.

| Kiểm tra trên bản khắc phục | Kết quả |
| --- | --- |
| Khôi phục phụ thuộc theo tệp khóa | 10 dự án đạt, dùng gói đã có tại máy |
| Build Release | 10 dự án đạt |
| .NET ngoài nhóm PostgreSQL | 254/254 đạt: Domain 118, Application 69, Architecture 26, kiểm thử offline trong IntegrationTests 41 |
| Python tài liệu và cấu hình | 28/28 đạt |
| Tài liệu và liên kết | 97 Markdown, 521 liên kết cục bộ đạt |
| Liên kết đến tiêu đề | 52/52 hợp lệ |
| Hồ sơ gốc / JSON | 32 phần bản gốc giữ nguyên; 22 JSON tài liệu hợp lệ |
| Dấu xung đột Git / định dạng chênh lệch | Không có dấu xung đột; `git diff --check` đạt |

Máy hiện tại chặn bước kiểm tra workload của lệnh .NET CLI; đã dùng cùng SDK `10.0.300` qua `MSBuild.dll` để restore/build và `vstest.console.dll` để chạy kiểm thử. Restore giữ chế độ khóa, dùng bộ nhớ đệm và tắt riêng `NuGetAudit` trong lệnh local vì không truy cập được dữ liệu lỗ hổng qua mạng. Không sửa cấu hình dự án hoặc chốt chặn CI để né lỗi môi trường; lần này không xác minh lại dữ liệu lỗ hổng phụ thuộc.

Chưa chạy PostgreSQL integration, build ảnh Docker hoặc smoke toàn hệ thống trong lần khắc phục này. Không đổi mã persistence, migration, cấu hình ứng dụng hay nghiệp vụ; kiểm thử hợp đồng dùng host trong bộ nhớ và không thao tác database. CI vẫn giữ các bước PostgreSQL, hợp đồng AI và build ba ảnh tại commit mới. Các bằng chứng ở phần rà soát ban đầu giữ nguyên phạm vi thời điểm của chúng.
