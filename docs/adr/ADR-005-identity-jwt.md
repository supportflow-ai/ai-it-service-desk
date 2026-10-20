# ADR-005: ASP.NET Core Identity và JWT Bearer

## Trạng thái

Đã chấp nhận. Bản diễn giải tiếng Việt cập nhật 2026-10-20.

## Bối cảnh

API của công cụ hỗ trợ CNTT nội bộ cần nền tảng xác thực và quản lý tài khoản.

## Quyết định

Dùng ASP.NET Core Identity để quản lý tài khoản, băm mật khẩu và lưu dữ liệu người dùng; dùng JWT Bearer để xác thực các yêu cầu API.

## Phương án đã cân nhắc

- Xác thực bằng cookie: không phải lựa chọn ban đầu cho kiến trúc API phục vụ giao diện độc lập.
- Nhà cung cấp OAuth2/OIDC bên ngoài: tăng độ phức tạp vượt nhu cầu tại thời điểm quyết định.

## Hệ quả

Tận dụng chức năng quản lý tài khoản có sẵn; token JWT giúp API xác thực mà không giữ phiên đăng nhập trên máy chủ. Việc quản lý khóa ký và cơ chế làm mới token cần được thiết kế phù hợp.

Hiện trạng đăng ký, khóa đăng nhập và hiệu lực quyền trong JWT nằm trong [hợp đồng backend](../architecture/current-backend-contract.md). Mục “cơ chế làm mới token” là trách nhiệm cần xem xét, chưa xác nhận có API làm mới token.

Nguồn quyết định trước khi Việt hóa: [bản ghi tại mốc đối chiếu](https://github.com/supportflow-ai/ai-it-service-desk/blob/260f37b5c415d0aa69818dc306411ab5a4f467f5/docs/adr/ADR-005-identity-jwt.md).
