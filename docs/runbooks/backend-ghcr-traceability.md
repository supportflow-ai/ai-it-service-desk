# Quy trình Đối soát và Truy vết Hiện vật GHCR Backend

Tài liệu ghi nhận kiến trúc xuất bản hiện vật container image, cấu hình ma trận (matrix publish) và cơ chế bảo mật chuỗi cung ứng (SBOM & Provenance) của phân hệ Backend.

## 1. Cơ chế Xuất bản GHCR trên nhánh `dev` và `main`

Quy trình CI/CD của Backend (`.github/workflows/ci.yml`) sử dụng chiến lược ma trận (Matrix Strategy) để đóng gói và xuất bản 3 container image độc lập lên GitHub Container Registry (`ghcr.io`):

| Image Name Canonical | Dockerfile | Mục đích / Thành phần |
| :--- | :--- | :--- |
| `ghcr.io/supportflow-ai/servicedesk-api` | `src/backend/ServiceDesk.Api/Dockerfile` | ASP.NET Core Web API runtime (`USER $APP_UID`) |
| `ghcr.io/supportflow-ai/servicedesk-worker` | `src/backend/ServiceDesk.Worker/Dockerfile` | Background Worker xử lý hàng đợi vé (`USER $APP_UID`) |
| `ghcr.io/supportflow-ai/servicedesk-database` | `src/backend/ServiceDesk.Migrator/Dockerfile` | Công cụ CLI migration và seeding dữ liệu (`USER $APP_UID`) |

## 2. Chính sách Phát hành An toàn (Publish Security Policy)

Job `publish` được bảo vệ nghiêm ngặt bằng điều kiện kiểm soát:
```yaml
if: github.event_name == 'workflow_dispatch' && inputs.publish_images && github.repository_owner == 'supportflow-ai' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/dev')
```
- **Kích hoạt thủ công có kiểm soát (`workflow_dispatch`):** Yêu cầu người vận hành xác nhận rõ cờ `publish_images: true`.
- **Ràng buộc nhánh chính thức:** Chỉ cho phép thực thi khi chạy trên nhánh `dev` hoặc `main`.
- **Cách ly kiểm thử và phát hành:** Các sự kiện `push` và `pull_request` chỉ chạy kiểm thử và build kiểm chứng, tuyệt đối không xuất bản image lên GHCR.
- **Phân quyền tối thiểu (Least Privilege):** Quyền `packages: write` chỉ được cấp cục bộ cho job `publish`; job `backend` chỉ mang quyền `contents: read`.

## 3. Bảo mật Chuỗi Cung Ứng (Supply Chain Security)

Mỗi image được xuất bản thông qua `docker/build-push-action@v6` đều tự động tích hợp:
- **SBOM:** `sbom: true` (Tạo Software Bill of Materials gắn kèm image).
- **Provenance:** `provenance: mode=max` (Ghi nhận toàn bộ thông tin nguồn gốc bản build BuildKit cấp cao nhất).
- **Định danh bất biến:** Gắn tag theo commit SHA `sha-${{ github.sha }}` và định danh bằng SHA-256 Digest `sha256:[0-9a-f]{64}`.

## 4. Cấu trúc Hiện vật Bằng chứng Digest (Artifact JSON)

Sau khi đẩy image thành công, workflow sinh 3 tệp JSON độc lập khớp hoàn toàn với đặc tả của công cụ ghép bản phát hành hạ tầng (`supportflow-ai-infra/scripts/assemble_release.py`):

1. `artifacts/servicedesk-api.json`
2. `artifacts/servicedesk-worker.json`
3. `artifacts/servicedesk-database.json`

Cấu trúc nội dung chuẩn của từng tệp:
```json
{
  "repository": "supportflow-ai/ai-it-service-desk",
  "commit": "<FULL_40_HEX_COMMIT_SHA>",
  "images": {
    "<IMAGE_NAME>": "ghcr.io/supportflow-ai/<IMAGE_NAME>@sha256:<64_HEX_DIGEST>"
  }
}
```
Ba tệp JSON này được lưu trữ thành artifacts: `ghcr-${{ matrix.image }}-${{ github.sha }}` để phục vụ đối soát và tự động lắp ráp Release Manifest.
