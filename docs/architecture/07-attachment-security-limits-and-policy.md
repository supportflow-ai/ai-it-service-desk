# TC-308: Attachment Security Limits & Object Storage Policy Specification

- **Document ID:** `ARCH-CONTRACT-07`
- **Jira Issue:** `TC-308` (Chốt attachment security limits)
- **Author / Architect:** Đức Anh (Leader / Architect / PM)
- **Status:** APPROVED / TECHNICAL CONTRACT
- **Date:** 2026-09-29
- **Target Audience:** Backend Core Team, DevOps Team, Frontend Web Team, Security Team
- **Direct Handoff Recipients:**
  - `TC-309`: MinIO Adapter, Bucket Provisioning & Object Key Management
  - `TC-310`: Attachment Application UseCases & Validation Middleware
  - `TC-311`: Frontend File Upload Component & NGINX Alignment

---

## 1. Bối cảnh & Các Vấn đề Cần Chốt Quyết định (Decision Context)

Trước Sprint 2, tài liệu dự án mới chỉ nêu nguyên tắc chung: *"File lưu tại MinIO, metadata lưu PostgreSQL, có kiểm tra dung lượng"*.
Testing Plan và Implementation Spec ghi nhận việc **chưa khóa các giới hạn cụ thể về file-size, MIME-type, cơ chế presigned URL và đồng bộ cấu hình NGINX**.
Tài liệu này là quyết định kiến trúc chính thức (Architectural Decision) chốt dứt điểm các thông số này.

---

## 2. Bảng Quyết định Giới hạn & An toàn Tệp tin (Attachment Security Decision Table)

| Tiêu chí kỹ thuật | Quyết định chuẩn hóa (Specification) | Cơ chế kiểm soát & Thực thi |
|---|---|---|
| **Kích thước tối đa mỗi tệp (Max File Size)** | **10 MB** (10,485,760 bytes) cho 1 file đơn lẻ. | Kiểm tra ở 3 tầng: NGINX Ingress, ASP.NET Core Middleware, và Frontend Client-side validation. |
| **Tổng dung lượng đính kèm mỗi Ticket** | Tối đa **30 MB** (hoặc tối đa 10 tệp tin) cho một ticket. | Application Service kiểm tra `SUM(FileSize)` của các tệp hiện hữu trên Ticket trước khi nhận tệp mới. |
| **Danh sách MIME Type cho phép (Whitelist)** | - **Văn bản:** `application/pdf`, `.docx`, `.xlsx`, `.txt`, `.csv`<br>- **Hình ảnh chụp lỗi:** `image/png`, `image/jpeg`, `image/webp`<br>- **Tệp nhật ký:** `text/plain` (.log) | Chỉ cho phép các định dạng phục vụ trực tiếp việc mô tả sự cố kỹ thuật. Cấm tuyệt đối `*/*`. |
| **Danh sách Định dạng cấm tuyệt đối (Blacklist)** | `.exe`, `.dll`, `.bat`, `.cmd`, `.sh`, `.ps1`, `.vbs`, `.js`, `.jar`, `.msi`, `.com`, `.scr`, `.zip`, `.rar`, `.7z` | Ngăn chặn nguy cơ phát tán mã độc (Malware/Ransomware) hoặc file nén lồng nhau (Zip Bomb). |
| **Cơ chế xác thực định dạng (File Verification)** | **Kiểm tra Magic Bytes (File Signatures)** tại Server. | Không tin tưởng header `Content-Type` do browser gửi lên. Đọc 512 bytes đầu tiên để xác thực magic bytes thực tế. |
| **Chuẩn hóa tên tệp (Filename Sanitization)** | - Tên file lưu trữ trong MinIO: `tickets/{ticketId}/{attachmentId}.bin`<br>- Tên file gốc: làm sạch ký tự đặc biệt, chống Path Traversal (`../`). | Tránh tấn công ghi đè hệ thống tệp và che giấu thông tin nhạy cảm trên storage bucket. |
| **Mức độ riêng tư của Storage (Bucket Privacy)** | **100% Private Bucket**. MinIO bucket `service-desk-attachments` không mở quyền đọc công khai (Anonymous Read = Denied). | Mọi hành động tải tệp đều phải được chứng thực và cấp phép qua Backend API. |
| **Cơ chế Tải tệp (Download Mechanism)** | **Short-lived Presigned URL** với thời gian sống (TTL) là **15 phút** (900 giây). | Backend kiểm tra Resource Authorization thành công mới sinh URL presigned từ MinIO cho Client tải trực tiếp. Giảm tải băng thông và I/O cho API Gateway. |
| **Đồng bộ NGINX Reverse Proxy** | `client_max_body_size 25M;`<br>`client_body_buffer_size 128k;` | NGINX phải cấu hình lớn hơn giới hạn 10MB để chứa multipart/form-data overhead. Tránh lỗi `413 Request Entity Too Large` từ NGINX. |

---

## 3. Ma trận Phân quyền Thao tác Tệp tin (Attachment Authorization Matrix)

| Hành động | Requester (Chủ sở hữu) | Agent / Lead (Thuộc Team phụ trách) | Admin (Quản trị viên) | User khác (Không liên quan) |
|---|---|---|---|---|
| **Upload tệp công khai** | Được phép (khi ticket chưa Closed) | Được phép (khi ticket chưa Closed) | Được phép | `403 Forbidden` / `404 Not Found` |
| **Upload tệp nội bộ (Internal)** | **Bị cấm** (`403 Forbidden`) | Được phép (tự động gắn cờ `IsInternal = true`) | Được phép | `403 Forbidden` |
| **Download tệp công khai** | Được phép | Được phép | Được phép | `404 Not Found` |
| **Download tệp nội bộ** | **Bị cấm** (`404 Not Found` - không lộ link) | Được phép | Được phép | `404 Not Found` |
| **Xóa tệp đính kèm** | Chỉ được xóa tệp do chính mình upload trong vòng 1 giờ | Được xóa tệp do mình upload, Lead được xóa tệp trong team | Toàn quyền xóa | `403 Forbidden` |

---

## 4. Kịch bản Kiểm thử An toàn Tệp tin Bắt buộc (Negative Test Cases)

1. **Upload vượt dung lượng:** Gửi file dung lượng 11 MB → API trả về `413 Payload Too Large` kèm thông báo lỗi rõ ràng.
2. **Giả mạo phần mở rộng (Extension Spoofing):** Đổi tên file `malware.exe` thành `screenshot.png` rồi upload → Server kiểm tra magic bytes không khớp với PNG header và trả về `415 Unsupported Media Type`.
3. **Tấn công Path Traversal:** Đặt tên file là `../../../../etc/passwd` → Tên file được làm sạch thành `passwd`, lưu trên MinIO bằng Guid ngẫu nhiên, không gây ảnh hưởng đến filesystem.
4. **Truy cập trái phép tệp nội bộ:** Requester gọi endpoint lấy download link của một attachment có `IsInternal = true` → Trả về `404 Not Found`.
5. **Hết hạn Presigned URL:** Client tải file bằng presigned URL sau 16 phút → MinIO trả về `403 Request has expired`.
