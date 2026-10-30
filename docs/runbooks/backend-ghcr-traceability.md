# Backend GHCR Artifact Traceability — Round 3

Tài liệu ghi nhận hiện vật GHCR và bằng chứng kiểm thử của Backend cho Round 3.

## 1. Thông tin nhánh và Commit

- Baseline branch: `dev` (`44ba0b4b4fc1ad6f515f8485b83736eccb127491`)
- Round 2 branch: `devops/cicd-github-actions` (`adb43ae4de5f575b4bfd9559ae96105034a74022`)
- Round 3 corrective branch: `devops/cicd-round3-backend`
- Pull Request Round 2: [PR #34](https://github.com/supportflow-ai/ai-it-service-desk/pull/34) (Base: `dev`, Head: `devops/cicd-github-actions`)

## 2. Kết quả kiểm thử và Workflow

- Workflow Run: [Run 41115110483](https://github.com/supportflow-ai/ai-it-service-desk/actions/runs/41115110483)
- Kết quả kiểm thử: 265 passed, 0 failed (Domain: 118, Application: 80, Architecture: 26, Integration Offline: 41).
- PostgreSQL Testcontainers: 17 integration tests pass.
- AI Contract Validation: Pass.

## 3. Danh mục Container Images và Digests đã xuất bản

| Image | Tag | Digest SHA256 | Mục đích / Runtime |
| :--- | :--- | :--- | :--- |
| `ghcr.io/supportflow-ai/servicedesk-api` | `sha-adb43ae4de5f575b4bfd9559ae96105034a74022` | `sha256:f28277ad51197f7974e29ff944b0e259d22420ffa49726419a85dec32d8ef334` | ASP.NET Core API runtime (`USER $APP_UID`) |
| `ghcr.io/supportflow-ai/servicedesk-worker` | `sha-adb43ae4de5f575b4bfd9559ae96105034a74022` | `sha256:c10abca647303570a0589fe531a215c95b3c4673d884c8ef399337e163eba90b` | Background processing worker (`USER $APP_UID`) |
| `ghcr.io/supportflow-ai/servicedesk-database` | `sha-adb43ae4de5f575b4bfd9559ae96105034a74022` | `sha256:f635c5790c4a526f7a640c02ef8911f61c746edbca67ce56a66fffd3cf1c8c01` | CLI tool migration và seeding (`USER $APP_UID`), không phải PostgreSQL server |
