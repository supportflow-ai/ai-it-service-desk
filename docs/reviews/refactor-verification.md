# Hồ sơ lịch sử: Kiểm chứng đợt tái cấu trúc

> **Tài liệu lịch sử.** Giữ kết quả kiểm thử và giới hạn đã ghi tại commit gốc; không quy đổi thành bằng chứng kiểm chứng HEAD.
> Chú thích tiếng Việt ngày 2026-10-20; phần bản gốc bên dưới giữ nguyên. Nguồn đang áp dụng: [hướng dẫn hiện hành](../runbooks/backend-hardening-verification.md).

<!-- original-record -->
# Backend refactor verification

The baseline is recorded in `refactor-baseline.md`. No historical migration or Git
commit has been rewritten.

## Validated checkpoints

- Backend commit `3de4b745a739e4cf3d7934f778a7a8dd974a89db`: Linux CI passed 250 tests,
  including 53 PostgreSQL integration tests, OpenAPI baseline, AI contract validation
  and API/Worker/Database Docker builds.
- Evidence: https://github.com/supportflow-ai/ai-it-service-desk/actions/runs/39114943634
- AI contract consumer validation passed against the immutable canonical commit:
  https://github.com/supportflow-ai/ai-service/actions/runs/39115086665
- An initial infra stack exposed an unnecessary Identity registration in Worker
  Development DI validation. Module-specific registration and an actual Worker startup
  regression test were added before accepting the handoff.
- The final acceptance evidence is the latest successful checks on the three linked PRs;
  source commits in the infra lockfile are immutable, and contract checksum is verified.

## Compatibility and upgrade coverage

Existing HTTP paths, schemas, statuses, errors and idempotency responses are covered by
the pre-refactor OpenAPI baseline and endpoint tests. Real PostgreSQL tests cover two
hosts, payload conflicts, original-response replay, receipt rollback, key expiry,
advisory-lock timeout, ownership isolation and JWT rejection. Database upgrades preserve
existing ticket/user IDs and ticket numbers; the new number generator crosses 9999
without truncation and does not reset the sequence. Identity tests cover transactional
registration, persistent lockout, expiry and successful-login reset.

The infrastructure handoff must pass the full disposable stack smoke before removing
the old backend-owned Compose/gateway files. No production deployment, database reset,
live AI connection or automatic branch merge is part of this work.
