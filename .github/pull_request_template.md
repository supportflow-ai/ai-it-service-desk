## 1. Thông tin theo dõi

- Công việc Jira: `TC-XXXX`
- Mã theo dõi: `TC-XXXX` / `PR-XXXX`
- Loại thay đổi: [ ] Tính năng  [ ] Sửa lỗi  [ ] Bảo trì  [ ] Tái cấu trúc
- Thành phần chịu ảnh hưởng:
  - [ ] API / Worker / công cụ Database
  - [ ] Domain / Application
  - [ ] Infrastructure / PostgreSQL / MinIO
  - [ ] Kiểm thử / GitHub Actions / Dockerfile backend
  - [ ] Tài liệu

## 2. Nội dung và lý do thay đổi

<!-- Nêu vấn đề, hành vi sau thay đổi và tác động tới các thành phần liên quan. -->

## 3. Bằng chứng kiểm thử

<!-- Ghi lệnh thực sự đã chạy, kết quả và SHA được kiểm chứng. Không chỉ đánh dấu ô. -->

### 3.1 Kiểm tra backend

```sh
dotnet restore src/backend/ServiceDesk.sln --locked-mode
dotnet build src/backend/ServiceDesk.sln -c Release --no-restore
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --no-build --filter "Category=Integration"
```

- SHA đã kiểm chứng:
- Kết quả, số kiểm thử đạt/thất bại/bỏ qua:
- Liên kết GitHub Actions hoặc tệp TRX:
- Kiểm tra không chạy và lý do, nếu có:

### 3.2 Kiểm tra bổ sung theo phạm vi

- Hợp đồng HTTP / hợp đồng AI:
- Migration và bảo toàn dữ liệu:
- Build image API / Worker / Database:
- Kiểm tra toàn hệ thống, nếu cần: dẫn kết quả từ kho
  [supportflow-ai-infra](https://github.com/supportflow-ai/supportflow-ai-infra),
  ghi rõ SHA backend được ghim. Các lệnh Compose và kiểm tra qua cổng chung nằm ở kho đó.

## 4. Kiểm tra bảo mật và dữ liệu

- [ ] Không đưa mật khẩu, token, khóa bí mật hoặc chứng chỉ thật vào Git.
- [ ] Đầu vào và quyền truy cập được kiểm tra đúng phạm vi thay đổi.
- [ ] Không ghi dữ liệu nhạy cảm vào nhật ký.
- [ ] Hợp đồng API và dữ liệu cũ được giữ tương thích, hoặc thay đổi đã được nêu rõ.

## 5. Điều kiện hoàn thành

### Tài liệu chịu ảnh hưởng

- Tài liệu đã cập nhật, gồm chỉ mục/liên kết liên quan:
- Nếu không cần cập nhật tài liệu, nêu lý do cụ thể:
- Commit mã nguồn đã dùng để đối chiếu nội dung:
- Phần đã triển khai, giới hạn và quyết định còn mở:
- Nếu thay API: đường dẫn/DTO/quyền/phản hồi, chênh lệch OpenAPI và kiểm thử tương thích:

Đối chiếu [quy chuẩn tài liệu](../docs/process/documentation-standard.md) và [bảng thuật ngữ](../docs/process/technical-glossary.md). Bằng chứng kiểm thử cũ giữ nguyên ngày/commit, kết quả mới được ghi riêng.

### Kiểm tra hoàn thành

- [ ] Công việc Jira, mã theo dõi và phạm vi được ghi rõ.
- [ ] Các kiểm thử liên quan đạt; đã cung cấp bằng chứng thực tế.
- [ ] GitHub Actions của backend thành công trên đúng SHA.
- [ ] Tài liệu liên quan đã cập nhật bằng tiếng Việt.
- [ ] Có ít nhất một người khác đánh giá và chấp thuận.
