# ADR-009: Quyết định Jenkins đã được thay thế

> **Tài liệu lịch sử.** Đã được thay thế bởi [ADR-013](ADR-013-github-actions.md) ngày 2026-10-18 (Superseded by).
> Tóm tắt tiếng Việt ngày 2026-10-20: bản gốc chọn Jenkins ở giai đoạn trước; nền tảng hiện hành là GitHub Actions. Giữ nguyên bối cảnh, quyết định và kết luận gốc bên dưới.

<!-- original-record -->
# ADR-009: Jenkins CI/CD Foundation

## Status
Superseded by [ADR-013: GitHub Actions](ADR-013-github-actions.md) on 2026-10-18.

The context and decision below are retained as historical evidence. Jenkins is no longer the active CI/CD platform.

## Context
Cần CI/CD pipeline tự động hóa build, test, Docker image build. Pipeline phải chặn deploy khi test fail, và phù hợp với yêu cầu học phần về CI/CD evidence.

## Decision
Dùng **Jenkins** với Jenkinsfile declarative pipeline. Flow: Checkout → Backend Restore/Build → Unit Tests → Frontend Install/Build → Docker Build. Push registry và deploy stages sẽ được thêm khi có infrastructure.

## Alternatives Considered
- **GitHub Actions**: Tốt cho open source, nhưng học phần yêu cầu Jenkins, self-hosted phù hợp hơn cho private infrastructure.
- **GitLab CI**: Cần GitLab server riêng, team dùng GitHub/Jenkins.
- **Không CI/CD**: Không đáp ứng yêu cầu học phần, không đảm bảo quality gate.

## Consequences
- **Positive**: Self-hosted, full control, branch-based pipeline (feature→dev→staging→main), test results tracking, evidence cho học phần.
- **Negative**: Cần maintain Jenkins server, initial setup phức tạp hơn managed CI.
- **Neutral**: Integration tests cần Docker-in-Docker hoặc Docker socket trên Jenkins agent.
