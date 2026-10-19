# GitHub Actions operations

The source workflows are `.github/workflows/ci.yml` in the backend, web, AI and infra repositories. Jenkins is superseded by ADR-013.

## Kiểm tra tài liệu trong backend

Job `backend` kiểm tra nguồn/liên kết bằng `scripts/validate_project_docs.py` và chạy các tình huống hồi quy của công cụ trước bước .NET. Công cụ dùng Python đã thiết lập trên runner, không cần database hoặc package bổ sung. Tài liệu mới phải có phân loại trong `docs/context-sources.json` hoặc thuộc nhóm thư mục được khai báo; liên kết tới tệp local bị ignore không được coi là nguồn công bố hợp lệ.

Các bước .NET, PostgreSQL, hợp đồng AI và build ba image vẫn giữ nguyên. Thêm kiểm tra tài liệu không thay trigger, quyền workflow, phát hành image hoặc deploy.

## Validate a change

1. Push a development branch and open a PR. Check the Actions tab and PR checks.
2. Backend: inspect job `backend`; download `backend-tests` for TRX details and the versioned AI contract artifact if needed. PostgreSQL integration runs using Testcontainers on the hosted Linux runner.
3. Web: inspect job `frontend` for lint, tests and image build.
4. Infra: update `sources.lock.json` to the exact reviewed backend/web commit SHAs. Job `smoke` checks out those commits and builds/starts the disposable system, including migration and roles, before gateway smoke checks. Logs are emitted even on failure; CI volumes are removed on normal job teardown. Never copy CI volume cleanup to a real environment.
5. AI: keep the existing contract checksum/version/fixture checks green when changing a pinned contract.

Manual reruns use **Actions → workflow → Run workflow** after the workflow is merged into the default branch, or the rerun button on an existing run. Canceling an obsolete run cannot change production because no deployment job exists.

## Switch over after merge

Merge the reviewed workflow changes and require the new Actions checks in branch rules before removing any required Jenkins check. Verify a fresh PR receives the required successful checks. No repository protection rules are modified by this code change.

Disable external Jenkins jobs/webhooks for these repositories and retire the Jenkins server only after administrators confirm no other projects depend on it. Preserve needed audit records and server volumes; this migration does not delete them or revoke shared credentials automatically.

CI needs `contents: read` and no deployment or registry secrets for the current public repositories. If a source repository becomes private, provide narrowly scoped cross-repository read access to infra checkout in a separate reviewed change.

## Release boundary

Docker builds validate Dockerfiles and dependencies; runner-local images are discarded. There is no GHCR publication or automatic production deployment. Follow the backend rollout and infra production handoff runbooks for an explicit release using immutable images. A registry/deployment pipeline requires a separate design and authorization.
