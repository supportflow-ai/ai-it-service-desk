# Taxonomy & Gold-Label Annotation Guideline for AI Triage

## 1. Overview & Objective
Tài liệu này xác lập tiêu chuẩn phân loại vàng (**Gold-Label Standard**) và nguyên tắc dán nhãn độc lập (**Ground Truth Annotation Guideline**) cho bài toán tự động phân loại yêu cầu hỗ trợ IT (**AI Triage**).

Mục tiêu chính:
- Loại bỏ các đánh giá chủ quan, cảm tính đối với chất lượng mô hình AI.
- Định nghĩa rõ ràng 11 Category kinh điển, ranh giới phân định danh mục, cùng mức độ Ảnh hưởng (**Impact**) & Độ khẩn cấp (**Urgency**).
- Thiết lập quy tắc gán nhãn ma trận ưu tiên chuẩn xác theo quy tắc ITIL (Domain PriorityMatrix).
- Xác lập ranh giới can thiệp con người (**Human-in-the-Loop - HITL**) và danh mục bắt buộc kiểm duyệt thủ công (Security Incident `SEC`).

---

## 2. Canonical Category Taxonomy

Hệ thống tuân thủ nghiêm ngặt 11 danh mục theo `ServiceDesk.Domain.Ticketing.TicketCategory`:

| Code | Danh mục | Phạm vi định nghĩa (Scope) | Ví dụ điển hình | Loại trừ / Không thuộc phạm vi |
|---|---|---|---|---|
| **`ACC`** | Account Issues | Các sự cố liên quan tài khoản người dùng, đăng nhập Windows AD/SSO, quên mật khẩu, khóa tài khoản, hồ sơ cá nhân. | "Quên mật khẩu đăng nhập máy tính", "Tài khoản SSO bị khóa do gõ sai 5 lần". | Xin cấp quyền truy cập folder mới $\rightarrow$ `ACCESS`. |
| **`NET`** | Network Connectivity | Kết nối mạng LAN dây, Wi-Fi nội bộ văn phòng, rớt mạng, mất DNS, cấp phát IP DHCP. | "Mất kết nối Wi-Fi tầng 3", "Cắm dây mạng LAN không nhận IP". | Kết nối từ xa tại nhà qua mạng công ty $\rightarrow$ `VPN`. |
| **`VPN`** | Remote Access / VPN | Kết nối mạng riêng ảo từ xa (Cisco AnyConnect, OpenVPN, WireGuard), lỗi xác thực MFA khi vào VPN. | "Không thể kết nối VPN từ nhà", "VPN báo lỗi Certificate expired". | Rớt Wi-Fi tại công ty $\rightarrow$ `NET`. |
| **`MAIL`** | Email & Messaging | Outlook, Exchange, gửi/nhận email bị chậm, dung lượng hòm thư đầy, phần mềm chat công ty (Slack, Teams). | "Hộp thư Outlook bị đầy không nhận được mail mới", "Teams không gửi được file". | Máy tính không kết nối được mạng $\rightarrow$ `NET`. |
| **`SW`** | Software & Applications | Cài đặt phần mềm nghiệp vụ, bản quyền/license, ứng dụng văn phòng lỗi crash, cập nhật OS/Driver. | "Cần cài đặt bản quyền JetBrains Rider", "Excel bị crash liên tục khi mở file lớn". | Lỗi truy cập phân quyền hệ thống $\rightarrow$ `ACCESS`. |
| **`HW`** | Hardware Issues | Hỏng hóc phần cứng hiện có: bàn phím liệt phím, màn hình sọc, quạt kêu to, máy không lên nguồn, chai pin. | "Bàn phím laptop bị liệt phím Enter", "Màn hình ngoài bị chớp nháy và sọc ngang". | Cần mượn thêm màn hình làm việc $\rightarrow$ `EQUIP`. |
| **`PRINT`** | Printing & Scanning | Máy in kẹt giấy, hết mực, không kết nối được máy in mạng, máy scan không gửi file về máy tính. | "Máy in tầng 4 bị kẹt giấy", "Không kết nối được máy in mạng HP qua IP". | Mất mạng toàn văn phòng $\rightarrow$ `NET`. |
| **`ACCESS`** | Permissions & Access Control | Yêu cầu cấp/sửa quyền truy cập thư mục mạng, phân quyền database, cấp quyền admin máy tính, add group email. | "Xin cấp quyền truy cập thư mục Kế toán trên NAS", "Cần quyền đọc database Staging". | Quên mật khẩu đăng nhập cá nhân $\rightarrow$ `ACC`. |
| **`EQUIP`** | Equipment Requests | Đề xuất cấp mới hoặc đổi thiết bị: laptop cho nhân viên mới, chuột, bàn phím, tai nghe, màn hình phụ. | "Yêu cầu cấp thêm 01 màn hình phụ 27 inch", "Xin cấp laptop cho nhân sự mới phòng Marketing". | Laptop hiện tại bị hỏng màn hình $\rightarrow$ `HW`. |
| **`SEC`** | Security Incidents | Các nghi vấn hoặc sự cố an ninh thông tin: email lừa đảo (phishing), mã độc ransomware, nghi lộ mật khẩu, mất laptop. | "Nhận được email lạ có file nén chứa mã độc", "Nghi ngờ lộ mật khẩu email công ty". | Quên mật khẩu thông thường $\rightarrow$ `ACC`. |
| **`OTHER`** | Uncategorized / Miscellaneous | Các thắc mắc, yêu cầu trợ giúp thủ tục IT hoặc các vấn đề nằm ngoài 10 danh mục trên. | "Hỏi về lịch bảo trì định kỳ hệ thống máy chủ", "Tư vấn quy trình hủy thiết bị cũ". | Bất kỳ vấn đề nào khớp rõ 10 danh mục trên. |

---

## 3. Impact & Urgency Guidelines

Độ ưu tiên không được gán cảm tính mà phải xác định độc lập thông qua **Impact (Mức ảnh hưởng)** và **Urgency (Độ khẩn cấp)** theo ITIL:

### 3.1 Impact (Mức độ ảnh hưởng)
- **`High`**: Ảnh hưởng đến toàn bộ công ty, toàn bộ một chi nhánh/phòng ban lớn, hoặc dịch vụ lõi ngừng hoạt động.
- **`Medium`**: Ảnh hưởng đến một nhóm làm việc (team) hoặc một quy trình nghiệp vụ quan trọng nhưng có giải pháp thay thế tạm thời.
- **`Low`**: Ảnh hưởng cục bộ tới duy nhất 1 cá nhân, không ảnh hưởng đến tiến độ dự án chung.

### 3.2 Urgency (Mức độ khẩn cấp)
- **`High`**: Công việc bị đình trệ hoàn toàn ngay tại thời điểm hiện tại, có hạn chót gấp (deadline trong ngày, sự cố an ninh khẩn cấp).
- **`Medium`**: Hiệu suất làm việc bị giảm sút nhưng vẫn có thể làm việc tạm thời hoặc có phương án thay thế.
- **`Low`**: Yêu cầu có kế hoạch từ trước, có thể xử lý theo lịch trình thông thường mà không gây gián đoạn.

### 3.3 Ma trận ưu tiên xác định (Deterministic Priority Matrix)
Được chuẩn hóa theo `PriorityMatrix.cs`:
$$Priority = f(Impact, Urgency)$$

| Impact \ Urgency | High | Medium | Low |
|---|:---:|:---:|:---:|
| **High** | **P1 (Critical)** | **P2 (High)** | **P3 (Medium)** |
| **Medium** | **P2 (High)** | **P3 (Medium)** | **P4 (Low)** |
| **Low** | **P3 (Medium)** | **P4 (Low)** | **P4 (Low)** |

---

## 4. Human-In-The-Loop (HITL) & Security Guardrails
1. **Mandatory Manual Triage**:
   - Tất cả các ticket thuộc danh mục **`SEC`** (Security Incidents) hoặc nghi ngờ bảo mật bắt buộc phải dán nhãn `requires_manual_triage: true`. AI tuyệt đối không được phép tự động phân luồng hay tự động giải quyết.
2. **Ambiguous / Edge Cases**:
   - Đối với các ticket thiếu thông tin trầm trọng ("máy bị lỗi", "không vào được", "cứu tôi với"), AI phải hạ độ tin cậy (`ConfidenceBand.Low` hoặc `Medium`) và yêu cầu xác nhận từ Agent / Requester trước khi áp dụng.
3. **Forbidden Actions**:
   - AI không được tự đóng ticket, không tự đổi trạng thái workflow, không tự đổi phân quyền hoặc đổi mật khẩu.

---

## 5. Dataset Schema Specification
Mỗi trường hợp trong tập test dataset (`evaluation/datasets/*.json`) phải tuân thủ schema JSON sau:

```json
{
  "id": "TC-EVAL-001",
  "ticket_type": "standard | edge_case | ambiguous",
  "input": {
    "title": "Tiêu đề yêu cầu",
    "description": "Chi tiết mô tả sự cố"
  },
  "gold_label": {
    "category": "NET",
    "impact": "High",
    "urgency": "High",
    "expected_priority": "P1",
    "requires_manual_triage": false,
    "rationale": "Giải thích lý do gán nhãn gold label"
  }
}
```
