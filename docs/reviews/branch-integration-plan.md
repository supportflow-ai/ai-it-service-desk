# Branch Integration Plan — feature/* → dev → staging → main

Ngày audit: 09/10/2026 (Asia/Bangkok, UTC+07). Repository: https://github.com/supportflow-ai/ai-it-service-desk.

Baseline được fetch/pull mới: **origin/dev @ 6b124e73e9cb95350a505a24a548cb663ab1b7de**. Worktree audit: `D:/Users/Documents/HUCE/HK1_2026-2027/8. ThapCam/Code/ai-it-service-desk-audit`. Nhánh: `audit/repository-audit-20261009`.

Quy ước SHA dùng xuyên suốt (mọi viết tắt dưới đây trỏ chính xác SHA đầy đủ):

| Nhãn | Branch | Commit SHA |
|---|---|---|
| D | origin/dev | 6b124e73e9cb95350a505a24a548cb663ab1b7de |
| M | origin/main | e044f470d38cb9d00a4adca9e59de45abbde3ff5 |
| A | origin/feature/tc-275-276-277-ai-triage-contract | b529b2cdd138b273ad5f89efb18ccb9f5a399313 |
| Q | origin/feature/tc-203-quality-gate-and-integration-environment | e834e74a50ecc4986ca329bc2a217ffc14af8cf4 |
| F | origin/frontend | 106909cca3ed09e4687dda667a213c6d60841a8f |
| C | origin/feature/core-ticket-lifecycle | 5425b62b84913964c129b9235744a7dd03bd3a4f |
| T | origin/feature/tc-200-create-ticket | 9df98c27a5d50b269ce5ba71dacd1e0839f7c46d |
| B | common baseline; local main/dev trước audit | eceaaa28c9ec377353ae354d92f33b0f1fd40dd1 |

Paths đều relative repository tại SHA/branch ghi kèm, số sau dấu `:` là line của snapshot đó. Path thiếu ở một branch được ghi rõ là absence evidence qua tree/route scan. Owner là **đề xuất**, không thay đổi assignee Jira. P0 = chặn đưa ra môi trường có dữ liệu/thành viên thật; P1 = chặn tích hợp/release hoặc nghiệm thu feature liên quan; P2 = debt/planned scope. Không coi mọi planned feature missing là bug.

## Mục tiêu và giới hạn

Đồng bộ toàn bộ lịch sử đã tồn tại, bổ sung quality gates và tạo release candidate có thể rollback. Đây là **kế hoạch đề xuất, chưa thực hiện merge**. Audit chỉ tạo worktree/branch audit và reports. Checkout frontend cùng 9 staged documents được giữ nguyên; không đưa tài liệu đó vào branch audit, không commit thay chủ sở hữu. Source/bugs chi tiết: [repository-audit.md](repository-audit.md), [technical-debt-register.md](technical-debt-register.md).

## 1. Baseline và nhánh cần đồng bộ

| Compared with origin/dev | Branch-only (ahead dev) | Dev-only (behind dev) | Merge-base SHA label | Kết luận |
|---|---:|---:|---|---|
| origin/main | 2 | 25 | B | Diverged; AI thiếu ở dev |
| origin/frontend | 0 | 15 | F | Fully integrated ancestor |
| origin/feature/core-ticket-lifecycle | 0 | 22 | C | Fully integrated ancestor |
| origin/feature/tc-200-create-ticket | 0 | 12 | T | Fully integrated ancestor |
| origin/feature/tc-275-276-277-ai-triage-contract | 1 | 25 | B | Unintegrated into dev; integrated main |
| origin/feature/tc-203-quality-gate-and-integration-environment | 4 | 25 | B | Contains main's 2 + 2 unique TC-203 commits |

`git rev-list --left-right --count origin/main...origin/dev` trả **2 25**, tính lại trong audit này. Những con số giống audit cũ là kết quả xác minh mới. `git merge-base` trả B. `git log origin/main..Q` chỉ có 9010486 và e834e74; Q chứa M, vì thế không merge lặp A riêng và main riêng.

- `origin/main@M` → integration branch từ dev → PR dev: đưa AI histories vào dev, resolve TicketStatus có migration/test. Không merge A lần nữa vì A đã là ancestor M.
- `origin/feature/tc-203-quality-gate-and-integration-environment@Q` → integration branch từ dev đã chứa M → PR dev: chỉ 2 unique commits TC-203 sau loại ancestors; review phần CI, fixture, process, runbooks và smoke.
- C, T, F đã ancestor D; **không cần merge thêm**. Giữ branches cho đến khi owner xác nhận archive; không delete trong audit.
- Local dev/main đều B và thuộc worktrees khác. Khi owner của từng checkout có working tree sạch, họ fetch và pull --ff-only branch tương ứng. Agent audit không switch/pull trên checkout đó.
- Local frontend@D đi trước origin/frontend@F 15 commits, không có remote-only commits sau fetch. Đó là local history có sẵn; không force-reset local để khớp F, không push D lên frontend thay owner.
- staging local/remote chưa có. Tạo staging tại tested candidate sau gates; không lấy M scaffold làm staging candidate. First bootstrap branch có thể cùng SHA dev nên không tạo PR rỗng; ghi release record. Các đợt sau PR dev→staging.

## 2. Evidence conflict và dependencies

| Integration | Baselines / source paths | Đã xác nhận | Semantic review bắt buộc | Owner |
|---|---|---|---|---|
| main → dev | D/M; src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs | merge-tree exit1 add/add | Enum wire/persisted data, category string vs enum, priority, revision/xmin; preserve Auth/Ticket implementations dev | Đức Anh + Anh Nguyen + Hương Nguyễn + Nguyễn Thị Thu Hoài |
| AI feature → dev trực tiếp (không chọn làm đường chuẩn) | D/A; cùng TicketStatus.cs | merge-tree exit1 | M đã chứa A; tránh duplicate integration paths | Đức Anh |
| TC-203 → dev tại baseline cũ | D/Q; TicketStatus.cs, frontend package.json/lock | merge-tree exit1 ở 3 files | Program auth/health/DB fixture auto-merge cũng phải review; regenerate lock từ merged manifests | VÕ NGUYỄN ANH KIỆT + Nguyễn Thị Thu Hoài + Anh Nguyen |
| TC-203 sau main integration | dev mới + Q; Jenkinsfile/.github/workflows/ci.yml/deploy/smoke | Chưa simulation vì chưa tạo integration commit trong audit | Recompute merge-base/counts, lock conflicts, smoke prefix/auth/payload, branch config cũ | VÕ NGUYỄN ANH KIỆT |
| Docs local frontend → docs feature/dev | Original frontend@D; docs/architecture/01–08 + README | 9 files staged, chưa commit | Labels APPROVED cần reviewer; Draft/Submitted, roles/lifecycle/privacy/attachments mapping | Đức Anh + module owners |
| dev → staging → main | Candidate SHAs chưa có; README.md:326/Jenkinsfile:2 tại D | Chưa có staging/tag; main/divergence có | Không promote khi còn P0/P1 trong scope, no fake/mock UI as accepted feature | Đức Anh + VÕ NGUYỄN ANH KIỆT |

Cần duyệt contract TC-246/247/260/261/299/303/294/308 trước stateful implementation. DB migrations/concurrency phải ổn trước operations/HITL. main/A có AI domain contract nhưng chưa provider/API/storage; merge history không đồng nghĩa feature complete.

## 3. Thứ tự tích hợp đề xuất

| Wave | Thay đổi / target | Dependency | Findings liên quan | Test gate và owner |
|---|---|---|---|---|
| 0 | Freeze release; snapshot refs, branch permissions, API data/DB schema | Fresh fetch, clean disposable checkout | AUD-09/10 | Đức Anh ghi SHA/branch/reviewer; không freeze công việc đang stage của owner |
| 1 | Contract PR độc lập về dev | Review 9 docs do Đức Anh làm khi sẵn sàng; không chạm docs gốc trong audit | AUD-06/07/14/16/18 | Đức Anh + Anh Nguyen/Thu Hoài/Hương; canonical lifecycle/actors/wire format/limits được chốt; QA negative scenarios |
| 2a | Security/idempotency fix PRs từ dev | actor/key/tenant/operation/payload hash contract | AUD-01/02/03 | Anh Nguyen + Kiệt: cross-user isolation, concurrency, 2-node replay, production seeder/secret fail-fast |
| 2b | Persistence/migration/numbering fixes từ dev | Kiểm kê current DB/history, canonical mapping strategy | AUD-04/12/13 | Anh Nguyen + Kiệt: clean DB migrate, upgrade snapshot, auth/create/get, sequence >=10000, fixture health isolated |
| 2c | Frontend build fix PR từ dev | Current API/type baseline, không tự quyết enum mới | AUD-05 | Thu Hoài + Kiệt: npm ci/lint/build Node22, không disable gates |
| 3 | main@M → integration/sync-main-dev branch từ updated dev; PR → dev | Wave1; security/schema baseline; contract/datamapping | AUD-06/09/15 | Đức Anh + Backend/AI/FE: resolve code+data, unit/architecture/integration/contract tests; whole-file conflict choice bị cấm |
| 4 | Q → integration/tc203 branch từ dev mới; repair CI/config/smoke; PR → dev | M ancestor dev sau Wave3; build và DB gate ổn | AUD-08/10/11/12 | Kiệt: isolated stack up/readiness, smoke authenticated, correct TRX reporting, lint/integration gates; required checks + reviewer enforced |
| 5a | feature/ticket-list-and-operations → dev | contracts + migration/revision + CI | AUD-07/14/16 | Anh Nguyen + Thu Hoài + Kiệt: own list/detail, assignment/status/audit/timeline/public/internal ACL, attachment limits, 409 race |
| 5b | feature/ai-runtime-hitl → dev | M contracts integrated, visibility/DB/revision contract + CI | AUD-15 | Hương + Anh Nguyen + Thu Hoài + Kiệt: NoOp/provider timeout/SEC/stale decisions/projections/authorization; AI không tự transition |
| 6 | Knowledge/SLA/Analytics PRs theo release scope | article lifecycle/permissions, SLA targets/clock/glossary được Đức Anh chốt | AUD-17 | Anh Nguyen/Hương/Thu Hoài/Kiệt: article versions/ACL/RAG và SLA pause/reopen/UTC/KPI. Nếu ngoài RC scope, ghi limitation và vô hiệu UI tương ứng |
| 7 | Bootstrap staging / future dev→staging PR at candidate SHA | 0 P0; 0 unresolved in-scope P1; immutable build artifacts | AUD-18/20 | Kiệt + Đức Anh: production topology, secret/proxy/TLS/readiness/backup/restore/UAT + Jira acceptance evidence |
| 8 | staging→main reviewed merge + release tag/artifact record | candidate UAT/go-no-go, main ancestor check vẫn đúng sau fresh fetch | AUD-09/10/18 | Đức Anh approve; Kiệt verify exact SHA/images/database migration version và rollback |

Wave2 security/migrations/build là các PR độc lập có thể do owners thực hiện đồng thời, nhưng trình tự merge phải theo dependencies và CI. Kế hoạch không tự giao việc/message cho thành viên. Mọi owner trong bảng là đề xuất, đối chiếu Jira assignee hiện tại.

## 4. Merge policy giữ lịch sử

1. Shared branches dev/staging/main dùng merge commits (no squash/rebase cho nhánh reconcile main hoặc TC-203 để giữ ancestry). Feature nhỏ có thể dùng policy team, nhưng audit đề xuất giữ merge commits để truy vết Jira/rollback. Không sửa commits đã shared.
2. Không merge dev thẳng main ở trạng thái audit. main phải được integrate ngược vào dev trên nhánh riêng, resolve/test rồi PR về dev. Chỉ promote tested staging candidate.
3. Refresh remote refs và counts trước từng bước, pin source tip ở SHA review. Nếu remote đổi, cập nhật PR bằng merge target vào branch integrator; chạy checks lại. Không dùng kết quả conflict cũ như bảo đảm.
4. Conflict resolution review cả side D và M/Q và state contract, không copy feature tree lên dev. File mất trong tip diff do ancestry không được coi intentional deletion.
5. Khi dev đã chứa M, verify `git merge-base --is-ancestor origin/main origin/dev`; khi candidate chứa Q, verify Q ancestor. M/A/C/T/F phải còn reachable trong lịch sử candidate. Run `git log --graph` và diff business semantics.
6. Commit reports chỉ trên audit branch sau self-review nếu có yêu cầu commit; báo cáo đợt này để uncommitted cho người dùng review. Không tự push, mở PR, merge/settings.

Lệnh sau là **mẫu cho đợt integration được cho phép sau audit**, chưa chạy ở đợt này:

```powershell
# Trong checkout/worktree riêng sạch, sau fresh fetch và approval hợp lệ:
git switch -c integration/sync-main-dev origin/dev
git merge --no-ff origin/main
# Resolve theo contract + migration, review, build/test; commit resolution ở integration branch.
# Sau PR này vào dev, dùng một checkout riêng sạch cho TC-203:
git switch -c integration/tc203 origin/dev
git merge --no-ff origin/feature/tc-203-quality-gate-and-integration-environment
# Repair pipeline/smoke/fixture + lockfiles, rồi run gates trước PR dev.
```

Các tên integration branch là đề xuất. Phải kiểm tra chưa tồn tại và không checkout branch ở worktree khác. Không cherry-pick các commits M/A/Q. Nếu merge đang conflict và muốn abort, chỉ `git merge --abort` tại worktree tích hợp riêng sạch trước merge, sau bảo toàn mọi resolution mới; không chạy ở frontend gốc.

## 5. Quality gates

| Gate | Yêu cầu pass / evidence | Chặn |
|---|---|---|
| G0 Scope/contracts | Jira IDs + canonical schema/status/RBAC/privacy/attachment/SLA scope, reviewer độc lập, SHA reviewed | Requirement mismatch hoặc local APPROVED labels chưa review |
| G1 Build/static | backend Release restore/build, FE npm ci/lint/build Node22; dependency versions/SDK recorded | AUD-05 hoặc warning policy chưa rõ |
| G2 Unit/architecture/contract | All existing tests; add meaningful wire enum/validation/domain/AI invariants; validate layer dependencies | Không dùng 21 current tests như proof toàn hệ thống |
| G3 PostgreSQL migrations/auth/ticket | Docker Testcontainers; blank DB + upgrade DB; JWT 401/403/expired/registration role; own/cross-user access; numbering >=10000 | AUD-01/03/04/12/13 chưa chứng minh fixed |
| G4 Workflow/privacy | Actor x state x command matrix, assignment race/409, public/internal note ACL, file MIME/size/ACL; E2E create/list/detail/agent actions | Missing endpoint hoặc timeline mock trong accepted scope |
| G5 AI/KB/SLA nếu in-scope | provider degrade/fallback/manual SEC/no auto actions, stale decisions; published-only RAG; SLA pause/reopen/UTC; KPI glossary | Partial/scaffold đang được trình bày như done |
| G6 CI enforcement | Correct TRX/JUnit publish, integration stage, frontend lint, required statuses exact SHA, independent review; no force-push | No-status/pending-with-zero-checks không pass |
| G7 Deploy/UAT/rollback | Isolated project name/network/volumes; readiness loops, production explicit compose config, 2 nodes, secret/proxy/TLS checks; UAT+demo data; DB/object backup+restore thử | Local unrelated stack health không được coi candidate smoke |

Không đo/đặt coverage% giả định trong audit. Sau khi thiết lập collector, coverage là supplementary evidence, critical negative cases vẫn bắt buộc. Docker daemon hiện không hoạt động nên G3/G7 **chưa pass** tại audit; không có go/no-go tự động suy ra từ `docker compose config`.

## 6. Rollback và bảo toàn dữ liệu

- Trước mỗi integration merge: ghi target/source SHA, PR/Jira/review/check artifacts và deployment image digests; snapshot DB schema/history/data và object storage theo retention policy.
- Candidate lỗi trước merge: dừng PR; dùng worktree integrator để chỉnh resolution hoặc bỏ candidate. Audit report/checkout gốc không cần rollback vì không đổi business code.
- Candidate deployment lỗi: redeploy previous tested image/config digests, không force-reset Git. Không chạy `docker compose down -v` trên môi trường có dữ liệu.
- Merge đã tích hợp gây regression: tạo branch revert mới từ target và PR `git revert -m 1 <merge-sha>` nếu merge parent1 là target đã kiểm; reviewer phải xác nhận mainline. Đây là kế hoạch, không lệnh đã thực hiện. Nếu nhiều merge phụ thuộc, revert ngược dependency order hoặc prefer fix-forward.
- DB enum/default/Identity migration: ưu tiên expand/contract và migration bổ sung, không rewrite history migration đang applied. Không tự Down/drop tables/sequence. Revert source không tự rollback data; chỉ restore backup sau xác nhận compatibility, data-loss window và thử phục hồi.
- Bản ghi idempotency bền vững không xóa khi rollback ứng dụng; unique key/payload state và retry semantics cần tương thích phiên bản cũ.
- Sau rollback, xác minh health/auth/create/list/privacy/sequence và publish incident/known limitation qua quy trình team (audit không gửi messages).

## 7. Đầu ra và release decision

Tại SHA D/M/Q hiện tại: **NO-GO staging/main**. Bắt đầu xử lý AUD-01/02, sau đó AUD-03/04/05/06 cùng integration/gates. Owner Đức Anh chịu trách nhiệm chốt contracts và phê duyệt scope/go-no-go; Backend/Frontend/AI/QA-DevOps cung cấp evidence của module. Kế hoạch giữ cả histories, không mất 25 dev commits hay 2 main commits, không đụng 9 staged docs. Chỉ approve release khi test/deployment evidence của **candidate SHA mới** đã có; báo cáo này không cấp approval deploy.
