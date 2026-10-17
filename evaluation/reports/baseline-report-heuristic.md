# 📊 AI Triage Evaluation Baseline Report (heuristic)

- **Execution Date**: `2026-10-17T10:01:17.749733Z`
- **Provider Under Test**: `heuristic`
- **Total Test Cases**: `43` (Standard: `22`, Edge/Ambiguous: `21`)
- **Total Duration**: `0.001s`

---

## 🎯 Executive Summary & Metrics

| Metric | Target Goal | Score | Status |
|---|:---:|:---:|:---:|
| **Overall Category Accuracy** | $\ge 75\%$ | **90.7%** | ✅ PASS |
| **Standard Cases Accuracy** | $\ge 85\%$ | **90.91%** | ✅ PASS |
| **Edge / Ambiguous Accuracy** | $\ge 60\%$ | **90.48%** | ✅ PASS |
| **Impact Accuracy** | $\ge 70\%$ | **79.07%** | ✅ PASS |
| **Urgency Accuracy** | $\ge 70\%$ | **53.49%** | ⚠️ SUB-PAR |
| **Priority Calculation Accuracy** | $\ge 70\%$ | **62.79%** | ⚠️ SUB-PAR |
| **Security HITL Violations (SEC Missed)** | **0** | **2** | 🚨 CRITICAL RISK |

---

## 🔍 Detailed Analysis per Category & Case

| ID | Type | Title | Gold Cat / P | Pred Cat / P | Match | Security HITL | Latency |
|---|---|---|:---:|:---:|:---:|:---:|:---:|
| EVAL-STD-001 | standard | Quên mật khẩu tài khoản Windows và email... | `ACC` / P3 | `ACC` / P4 | ⚠️ | ✅ OK | 0.05ms |
| EVAL-STD-002 | standard | Tài khoản SSO bị khóa sau nhiều lần nhập... | `ACC` / P3 | `ACC` / P4 | ⚠️ | ✅ OK | 0.03ms |
| EVAL-STD-003 | standard | Mất toàn bộ kết nối mạng LAN và Wi-Fi tạ... | `NET` / P1 | `NET` / P1 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-004 | standard | Cáp mạng cắm vào máy bàn không nhận địa ... | `NET` / P4 | `NET` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-005 | standard | Không thể kết nối VPN Cisco AnyConnect k... | `VPN` / P3 | `VPN` / P4 | ⚠️ | ✅ OK | 0.05ms |
| EVAL-STD-006 | standard | VPN Gateway báo chứng chỉ số Certificate... | `VPN` / P1 | `VPN` / P2 | ⚠️ | ✅ OK | 0.02ms |
| EVAL-STD-007 | standard | Hòm thư Outlook bị đầy dung lượng, không... | `MAIL` / P3 | `MAIL` / P3 | ✅ | ✅ OK | 0.02ms |
| EVAL-STD-008 | standard | Ứng dụng Microsoft Teams không tải được ... | `MAIL` / P3 | `MAIL` / P3 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-009 | standard | Yêu cầu cấp bản quyền và kích hoạt JetBr... | `SW` / P4 | `SW` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-STD-010 | standard | Phần mềm kế toán MISA bị treo và tự thoá... | `SW` / P2 | `SW` / P3 | ⚠️ | ✅ OK | 0.02ms |
| EVAL-STD-011 | standard | Bàn phím laptop ThinkPad bị liệt toàn bộ... | `HW` / P3 | `OTHER` / P1 | ❌ | ✅ OK | 0.03ms |
| EVAL-STD-012 | standard | Màn hình ngoài 27 inch Dell bị sọc tím v... | `HW` / P4 | `HW` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-013 | standard | Máy in Canon tầng 3 bị kẹt giấy và phát ... | `PRINT` / P2 | `PRINT` / P3 | ⚠️ | ✅ OK | 0.02ms |
| EVAL-STD-014 | standard | Máy scan phòng Hành chính không gửi file... | `PRINT` / P4 | `PRINT` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-STD-015 | standard | Yêu cầu cấp quyền truy cập thư mục Kế to... | `ACCESS` / P4 | `ACCESS` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-016 | standard | Thêm tài khoản vào nhóm phân quyền Git R... | `ACCESS` / P3 | `ACCESS` / P3 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-017 | standard | Đề xuất cấp mới 01 màn hình phụ 27 inch ... | `EQUIP` / P4 | `EQUIP` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-018 | standard | Đổi dây cáp chuyển đổi USB-C sang Displa... | `EQUIP` / P4 | `EQUIP` / P4 | ✅ | ✅ OK | 0.04ms |
| EVAL-STD-019 | standard | Phát hiện email giả mạo Giám đốc tài chí... | `SEC` / P1 | `MAIL` / P3 | ❌ | 🚨 FAIL | 0.02ms |
| EVAL-STD-020 | standard | Màn hình máy tính hiển thị thông báo đòi... | `SEC` / P1 | `SEC` / P2 | ⚠️ | ✅ OK | 0.01ms |
| EVAL-STD-021 | standard | Xin tư vấn quy trình thanh lý thiết bị c... | `OTHER` / P4 | `OTHER` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-STD-022 | standard | Hỏi thông tin về kế hoạch bảo trì máy ch... | `OTHER` / P4 | `OTHER` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-EDGE-001 | ambiguous | Máy hỏng rồi | `OTHER` / P3 | `OTHER` / P3 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-002 | ambiguous | Cần hỗ trợ gấp | `HW` / P3 | `HW` / P3 | ✅ | ✅ OK | 0.03ms |
| EVAL-EDGE-003 | edge_case | Không vào được mạng khi bật VPN ở quán c... | `VPN` / P3 | `VPN` / P2 | ⚠️ | ✅ OK | 0.01ms |
| EVAL-EDGE-004 | edge_case | Quên mật khẩu giải nén file backup trên ... | `OTHER` / P4 | `OTHER` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-005 | edge_case | Mất điện thoại chứa app xác thực 2 lớp v... | `SEC` / P1 | `SEC` / P4 | ⚠️ | ✅ OK | 0.01ms |
| EVAL-EDGE-006 | edge_case | Outlook không kết nối được máy chủ khi ở... | `VPN` / P4 | `VPN` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-007 | edge_case | Cần mượn máy in màu di động cho sự kiện ... | `EQUIP` / P4 | `EQUIP` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-008 | edge_case | Cấp quyền gửi email dưới danh nghĩa hòm ... | `ACCESS` / P4 | `ACCESS` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-009 | ambiguous | Lỗi 403 Forbidden khi mở hệ thống CRM | `ACCESS` / P3 | `ACCESS` / P4 | ⚠️ | ✅ OK | 0.03ms |
| EVAL-EDGE-010 | edge_case | Laptop làm việc phát ra mùi khét và pin ... | `HW` / P3 | `HW` / P4 | ⚠️ | ✅ OK | 0.03ms |
| EVAL-EDGE-011 | ambiguous | Mạng bị chậm | `NET` / P4 | `OTHER` / P4 | ❌ | ✅ OK | 0.02ms |
| EVAL-EDGE-012 | edge_case | File Excel chứa số liệu khách hàng bị ch... | `SEC` / P1 | `OTHER` / P4 | ❌ | 🚨 FAIL | 0.05ms |
| EVAL-EDGE-013 | edge_case | Cần cài lại máy tính vì máy chạy quá nhi... | `SW` / P4 | `SW` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-EDGE-014 | ambiguous | Help me please | `OTHER` / P4 | `OTHER` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-015 | edge_case | Máy in báo offline nhưng các máy khác tr... | `PRINT` / P4 | `PRINT` / P4 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-016 | edge_case | Bàn phím không gõ được tiếng Việt sau kh... | `SW` / P4 | `SW` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-EDGE-017 | edge_case | Mất thẻ từ ra vào văn phòng và phòng máy... | `SEC` / P1 | `SEC` / P3 | ⚠️ | ✅ OK | 0.01ms |
| EVAL-EDGE-018 | edge_case | Bổ sung RAM cho laptop phục vụ chạy Dock... | `EQUIP` / P4 | `EQUIP` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-EDGE-019 | ambiguous | Đường truyền cáp quang biển AAG bị đứt ả... | `NET` / P2 | `NET` / P2 | ✅ | ✅ OK | 0.02ms |
| EVAL-EDGE-020 | edge_case | Không gõ được mật khẩu trên bàn phím số ... | `HW` / P4 | `HW` / P4 | ✅ | ✅ OK | 0.03ms |
| EVAL-EDGE-021 | ambiguous | Cần người hỗ trợ setup phòng họp Ban Giá... | `OTHER` / P2 | `OTHER` / P4 | ⚠️ | ✅ OK | 0.03ms |
