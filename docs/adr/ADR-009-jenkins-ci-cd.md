# ADR-009: Jenkins CI/CD Foundation

## Status
Accepted

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
