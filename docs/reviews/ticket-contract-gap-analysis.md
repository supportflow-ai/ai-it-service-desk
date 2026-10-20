# Hồ sơ lịch sử: Đối chiếu khoảng cách hợp đồng phiếu

> **Tài liệu lịch sử.** Phân tích hợp đồng tại mốc nguồn gốc; các thao tác thêm trong PR #28 được ghi riêng trong hợp đồng hiện hành.
> Chú thích tiếng Việt ngày 2026-10-20; phần bản gốc bên dưới giữ nguyên. Nguồn đang áp dụng: [hướng dẫn hiện hành](../architecture/current-backend-contract.md).

<!-- original-record -->
# Ticket contract gap analysis — TC-199 / TC-260 / TC-261 / TC-299

- Audit date: 2026-10-10 (Asia/Bangkok).
- Worktree: ai-it-service-desk-ticket-domain; branch docs/ticket-domain-lifecycle.
- Source HEAD/origin/dev: 6b124e73e9cb95350a505a24a548cb663ab1b7de, pulled --ff-only after fetch --prune; clean before audit.
- Deliverables: [domain contract](../architecture/03-ticket-domain-contract.md), [lifecycle policy](../architecture/04-ticket-lifecycle-policy.md), this report.
- Result: documents READY FOR REVIEW; implementation lifecycle/DB/security acceptance **BLOCKED** pending decision sign-off, code work and real PostgreSQL gates.
- Scope: documentation only. No business code, migration, enum values or authentication changed; no commit/push/merge/PR/Jira write.

## 1. Repository/branch evidence and requirement reconciliation

Snapshot labels below apply to every source finding. Exact file paths/lines are supplied in each GAP. “Missing on dev” is branch-specific, not a repository-wide absence claim.

| Label | Branch/ref | Commit SHA | Ticket implementation |
|---|---|---|---|
| D | origin/dev; audit docs HEAD | 6b124e73e9cb95350a505a24a548cb663ab1b7de | Ticket entity7 states/create/detail/sequence/xmin; no domain lifecycle/priority/assignment/audit commands |
| M | origin/main | e044f470d38cb9d00a4adca9e59de45abbde3ff5 | TicketCategory/Impact/Urgency/Priority/PriorityMatrix and9-state primitive, priority tests; no Ticket aggregate at src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs |
| F | local frontend | 063489b97c1f706cebeaeea1d3038f0e63a39fd7 |9 committed architecture/governance docs; target Draft/nine-state/string API not reconciled with D |
| C | origin/feature/core-ticket-lifecycle | 5425b62b84913964c129b9235744a7dd03bd3a4f | src/backend/ServiceDesk.Domain/Ticketing/.gitkeep only; scaffold, not implemented lifecycle |
| T | origin/feature/tc-200-create-ticket | 9df98c27a5d50b269ce5ba71dacd1e0839f7c46d | Create/detail branch already represented on dev; not future lifecycle implementation |
| AI | origin/feature/tc-275-276-277-ai-triage-contract | b529b2cdd138b273ad5f89efb18ccb9f5a399313 | AI primitive work exists; inspect integration against D enum, not ordinal-copy |
| H | fix/p0-idempotency-isolation worktree | 6b124e73e9cb95350a505a24a548cb663ab1b7de plus existing uncommitted changes | persistent replay isolation candidate/tests in separate worktree; not merged or copied here |
| R | docs/tc-246-247-rbac worktree | 6b124e73e9cb95350a505a24a548cb663ab1b7de plus2 staged documents | current actor/permission proposal used as dependency; index preserved |

Verified merge-base D/M = eceaaa28c9ec377353ae354d92f33b0f1fd40dd1; D has25 unique commits, M has 2 relative to each other. D/C =22/0 ahead/behind; C is an ancestor scaffold. These counts are from fresh git rev-list, not prior audit. No merge/conflict resolution/cherry-pick attempted.

Live Jira read: TC-199/260/261 In Progress, TC-299 To Do; owner Đức Anh. TC-262 To Do and263 In Progress Anh Nguyen; TC-264 Done Đức Anh; TC-208/300/301 To Do Anh Nguyen; TC-302 To Do Nguyễn Thị Thu Hoài; TC-294/303 To Do Đức Anh. Full descriptions and relevant parent/subtask/links read; TC-199's single comment carries no technical decision. No specific Jira approval for100/200 cap, Draft replacement,168h window or72h automation was found in inspected content.

Jira sources: [TC-199](https://evolving-kg-drift.atlassian.net/browse/TC-199), [TC-260](https://evolving-kg-drift.atlassian.net/browse/TC-260), [TC-261](https://evolving-kg-drift.atlassian.net/browse/TC-261), [TC-299](https://evolving-kg-drift.atlassian.net/browse/TC-299), [TC-208](https://evolving-kg-drift.atlassian.net/browse/TC-208), [TC-294](https://evolving-kg-drift.atlassian.net/browse/TC-294), [TC-303](https://evolving-kg-drift.atlassian.net/browse/TC-303). Read using search-company-knowledge skill/Atlassian read APIs; no comments/messages/issue edits sent. Rovo reports search source coverage may be incomplete.

ADR-001/002/003/004/005/010 read. They confirm architecture/persistence/test approach, not concrete field limits/state codes/reopen window. Legacy references F: docs/architecture/02-ticket-domain-and-field-contract.md, 03-ticket-lifecycle-and-visibility-baseline.md, 04-ticket-state-transition-and-reopen-matrix.md were read in full via git show. Their APPROVED label is documentary history; sign-off and deployed schema not independently verified.

## 2. Findings, priority and reproducibility

Severity P0/P1/P2 = security/release blocker or material contract/integration risk or consistency debt; planned absent features are not automatically security bugs. Source-confirmed code path is distinguished from executed runtime reproduction. All D findings use the full D SHA above; M/F exceptions explicit.

### GAP-01 — P0, confirmed unsafe create replay scope; fresh HTTP reproduction NOT RUN

- Branch/SHA: D. Source: src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:33–50.
- Evidence: cache key idempotency:ticket:{key.Trim()}, no requester/method/resource/payload partition; cache hit returns stored TicketDto before service ownership check. Query owner filtering in TicketService:79–82 never runs on this branch.
- Reproduction procedure: authenticated A POST valid payload key K, then B POST same K while cache live on same API process; inspect returned id/title/description. Parallel same-owner misses and alternating instances additionally test duplicate/replay inconsistency. Not executed anew here due Docker fixture unavailable.
- Expected: B receives its own distinct ticket; A same key/same payload same result; A changed payload409; concurrent/multi-instance single durable ticket per scope.
- Actual source path: B can receive A DTO; get/set is non-atomic/local cache with10-minute TTL.
- Impact: cross-Requester data disclosure and unsafe duplicate creation; release blocker already tracked separately. Owner/action: Anh Nguyen complete H isolated hotfix, Đức Anh review, Kiệt PostgreSQL multi-instance tests before integrating.
- Tests: TCON-47; persistent unique scope+fingerprint+same transaction, failure rollback/expiry policy. Adding RequesterId to memory key alone insufficient. This docs branch does not implement or merge the hotfix.

### GAP-02 — P1, confirmed number generator truncation; DB boundary reproduction NOT RUN

- Branch/SHA: D. Source: src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:74–79; Migrations/20260918_AddTicketTable.cs:26–29; tests/ServiceDesk.Application.Tests/Ticketing/TicketServiceTests.cs:43,66; tests/ServiceDesk.IntegrationTests/TicketEndpointTests.cs:43.
- Evidence: default uses LPAD(nextval(...)::text,4,'0'); comment incorrectly claims numbers>=10000 remain full. [PostgreSQL17 string functions](https://www.postgresql.org/docs/17/functions-string.html) specify truncation when input exceeds target length.
- Isolated SQL reproduction: SELECT lpad('10000',4,'0'), lpad('10001',4,'0'); expected corrected generator10000/10001, current expression1000/1000. Do not reset shared/live sequence to reproduce.
- Expected: IT-year-10000 etc unique, varchar30 sufficient. Actual expression: collision with prior1000 or consecutive long values under same year; unique constraint should reject insert. Endpoint catches ArgumentException only, so database failure may surface500. Actual PostgreSQL execution and response not verified here.
- Impact: create availability at sequence threshold; not a claim duplicate rows bypass unique constraint. Sequence is global, not reset each year.
- Owner/action: Anh Nguyen additive ALTER DEFAULT using one nextval evaluation and width=max(4,length(n)); preserve all existing numbers; Kiệt boundary/parallel/rollback/year tests TCON-17/18. Avoid destructive edit of already-applied migration.

### GAP-03 — P1, confirmed enum contract mismatch; UI runtime end-to-end NOT RUN

- Branch/SHA: D paths src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:5–11; Application/Ticketing/Dtos/TicketDto.cs:9; Infrastructure/Ticketing/TicketService.cs:69,94; src/frontend/service-desk-web/src/features/tickets/types.ts:3–13; components/TicketDetailPage.tsx:46–57 and AgentTicketWorkspace.tsx:110–120,207–238. M enum primitive also follows FE nine-state order.
- Repro from source mappings: backend status2=InProgress renders FE Triaged;3=PendingUser renders Assigned;4=Resolved renders InProgress;5=Closed renders PendingUser;6=Cancelled renders PendingExternal.0/1 align. Source comparison script executed for0–8.
- Expected: every number resolves to same semantic label/action or explicit unknown. Actual: contradictory labels/action gating for2–6. No supported update API is required to demonstrate source-level mismatch; live stored rows/status distribution not inspected.
- Impact: wrong labels/actions and potential workflow misuse upon implementing commands.
- Owner/action: Thu Hoài correct dev decoder first; Anh Nguyen/Đức Anh approve DEC-05 append-only codes/statusName/version and reconcile M enum before integration. Tests TCON-12/13/14,48.

### GAP-04 — P1 integration gap, confirmed missing lifecycle behavior/routes on dev

- Branch/SHA: D. Source: src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs:3–15; Application/Ticketing/Interfaces/ITicketService.cs:7–8; Api/Endpoints/TicketEndpoints.cs:20,66; FE api/ticketApi.ts:48–76.
- Procedure: inspect endpoint registration; after server available call /triage,/assign,/start,/resolve using authorized ticket. Expected target approved command graph with authorization/guards/audit; actual D has no routes/behavior, route requests should404 (HTTP reproduction not run).
- FE /tickets/me is also absent; timeline helper at ticketApi.ts:24–35 is mock, not persistence.
- Impact: TC-199/208 consumers not integrated; no claim missing endpoint exposes a transition exploit.
- Owner/action: Anh Nguyen TC-262/300/301 after DEC approval; Thu Hoài TC-302 consume agreed command names and DTO; remove mock-as-production behavior. Tests TCON-21–40/48.

### GAP-05 — P1 target gap/risk, concurrency and audit contract not implemented

- Branch/SHA: D. Source: Infrastructure/Persistence/ApplicationDbContext.cs:83–86; Domain/Ticketing/Ticket.cs:5–15; Application/Ticketing/Dtos/TicketDto.cs:3–11; FE api/ticketApi.ts:49–76.
- Evidence: shadow xmin mapped, but no input expected version, logical revision, update command handler, stored audit or transition events. SQLite test mapping omits xmin.
- Procedure: after commands implemented, two independently scoped clients mutate version4; inject audit persistence failure. Expected one accepted/one409, all mutation/event/child rollback on failure. Actual concurrent update behavior unknown because commands absent.
- Impact: future lost-update/partial-audit risk; not a confirmed race in a non-existent update endpoint.
- Owner/action: Anh Nguyen atomic transaction+DB CAS+version contract; Kiệt real PostgreSQL race/fault injection. Tests TCON-41/42/43/46. Domain pure; Application auth/time; Infrastructure transaction; no external call in transaction.

### GAP-06 — P1 contract gap, Agent detail scope broader than Proposed RBAC

- Branch/SHA: D. Source: Infrastructure/Ticketing/TicketService.cs:75–84; Domain/Ticketing/Ticket.cs; Application/Common/Interfaces/ICurrentUser.cs:11–18.
- Repro procedure: Agent outside team/assignment GET another Requester's ticket. Actual source skips owner predicate for any Agent/Admin; no team/assignment model. Runtime request not run.
- Expected target: current authoritative Agent assignment/team predicates; explicit Admin organization access. Current broad Agent access is source-confirmed but target scoped requirement still needs TC-248/261/294 acceptance.
- Impact: resource isolation gap relative to staged RBAC, not false claim cross-Requester owner query is broken; requester-to-requester service test passes.
- Owner/action: Anh Nguyen model scope/handlers; Đức Anh permission decisions; Kiệt TCON-06/07/08. Unknown scope fail closed, snapshot department never authorization grant.

### GAP-07 — P1 integration/data-model gap, field/taxonomy/projection contract drift

- Branch/SHA: D src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs; TicketCategories.cs; Application/Ticketing/Dtos/TicketDto.cs; FE features/tickets/types.ts:22–35; M Ticketing/Impact.cs/Urgency.cs/Priority.cs/PriorityMatrix.cs; F field contract reference.
- Procedure: compare DTO/entity/mapping with30-field target. Expected triage/assignment/resolution/lifecycle timestamps/version separate and nullable until appropriate state. Actual D lacks those fields, FE optional declarations do not persist them; category string codes vs reference GUID, main enums zero-based vs reference1-based.
- Impact: incompatible GUID/category/enum contracts, incorrect display/projections if copied; features on M exist but not integrated into D.
- Owner/action: Đức Anh DEC-04/09/10; Anh Nguyen additive migrations/explicit wire mapping; Thu Hoài runtime decoder/detail types. Tests TCON-04/09/15/16/19/20/44. No new field mandatory/backfilled without provenance.

### GAP-08 — P2 confirmed validation drift; policy decision required, not automatically DB bug

- Branch/SHA: D. Source: Infrastructure/Ticketing/TicketService.cs:25–41,49–54; Persistence/ApplicationDbContext.cs:46–58; Domain/Common/DomainDefaults.cs; FE CreateTicketPage.tsx:50–83; F field contract reference.
- Procedure: submit raw title101; service rejects400 though physical DB allows200. Compare padded title and description spaces: title checks raw before trim; description checks trimmed min10, UI raw min10. Submitted field limits differ from reference trimmed title5–200 and description max4000.
- Expected baseline: current100 maintained until reviewed; target all layers agree approved normalization/count/limits. Actual: intentionally wider DB may be valid layering, but no reconciled contract; Ticket description4000 generic constant is not used.
- Impact: inconsistent user feedback/invariants; do not silently widen or impose new caps on accepted input.
- Owner/action: Đức Anh DEC-02/03, Backend+FE coordinated validation; QA boundary/Unicode/null/trim scenarios TCON-02/03/10. No schema shrink/data truncate.

### GAP-09 — P1 migration validation gap; fresh PostgreSQL startup NOT VERIFIED

- Branch/SHA: D. Source: Persistence/Migrations/20260918_AddTicketTable.cs; ApplicationDbContext.cs:15,39,83; Infrastructure/Identity/IdentityDataSeeder.cs:30–45; tests/ServiceDesk.IntegrationTests/DatabaseTests.cs:27–38.
- Evidence: only Ticket migration; no Identity table migration or model snapshot. Requester uuid not directly type-compatible FK to IdentityUser.Id string. Seeder calls Migrate before Identity role/user queries. DatabaseTests calls EnsureCreated and merely checks connectivity, not full migration schema correctness.
- Procedure: isolated PostgreSQL blank DB -> migrations -> seeder -> inspect tables/FKs/index/default/xmin -> startup/create/detail; repeat from existing migration history. Expected complete schema and identity association; actual runtime unknown, all selected fixtures Docker-blocked.
- Do not claim emitted xmin user-column DDL fails without inspecting provider SQL: mapping/migration mentions xmin, generator handling and runtime must be checked.
- Impact: rollout risk; no genuine migration evidence from SQLite EnsureCreated. Owner/action: Anh Nguyen valid baseline/snapshot/identity decision; Kiệt fresh+upgrade+rollback dry-run TCON-19/45.

### GAP-10 — P1 confirmed Frontend build blocker

- Branch/SHA: D. Source: src/frontend/service-desk-web/src/features/tickets/components/AgentTicketWorkspace.tsx:10 (unused Option),225 (unsupported Button.success); TicketListPage.tsx:2 (unused Space).
- Reproduction EXECUTED: npm ci --no-audit --no-fund then npm run build on Node24.13.1/npm11.8.0.
- Expected TypeScript/Vite build0; actual exit2: TS6133 ×2, TS2322 ×1; no Vite production build. README Node22 CI runtime was not tested here.
- Impact: Frontend delivery gate blocked; docs unchanged cannot fix. Owner/action Thu Hoài separate frontend repair/verification; Kiệt supported Node22 CI build plus contract decoder tests. No source edits performed.

### GAP-11 — P1 contract/governance blocker, legacy decisions not reconciled

- Branch/SHA: F field/lifecycle/reopen reference docs; D current endpoints/status/tests; R staged RBAC proposal.
- Evidence: F labels APPROVED, Draft/create replacement, /api/v1, TK numbering, mandatory department snapshot,9 numeric states + string serialization,168h reopen/72h auto-close. D uses /api, Submitted,IT, null dept,7 states/int DTO. R says Requester reply does not auto-transition, whereas F reply auto-resumes. Main has9 states but no Ticket aggregate to justify deployed-data mapping.
- Expected: a reviewed version/migration/ownership/state policy with deployment evidence. Actual: sources conflict, live Jira inspections don't prove these precise choices signed off; reference “P0 Drift” labels alone aren't confirmed implementation security defects.
- Impact: copying contracts/code could corrupt stored status meaning or break accepted create behavior;72h close makes nominal168h reopen ineffective after closure.
- Owner/action Đức Anh DEC-01–10 and staged RBAC extension review (Resume/WaitExternal/Cancelled terminal); Anh Nguyen/Thu Hoài consume signed target; keep automation outside TC-208. Tests TCON-12/14/21/30/37/39.

### GAP-12 — P1 validation blocker, planned tests not executed

- Branch/SHA: D. Source: tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:19–23,60–63; tests/ServiceDesk.Application.Tests/Ticketing/TicketServiceTests.cs:24–45; tests/ServiceDesk.Domain.Tests/Common/DomainDefaultsTests.cs; src/frontend/service-desk-web/package.json.
- Actual EXECUTED:21 nonIntegration PASS;10 selected integration fail during Docker fixture initialization at npipe://./pipe/docker_engine,0 pass / 0 skipped. No lifecycle Domain invariant tests or FE automated test runner/script on D.
- Expected: real PG sequence/xmin/transaction/migration tests, domain state allowlist and API ownership/role grid, FE enum schema tests. Actual required cases missing/not run; no percentage coverage inferred.
- Owner/action Kiệt infrastructure/CI + Anh Nguyen tests TC-263/300/301 + Thu Hoài test harness/decoder. TCON matrix below is acceptance design, not claimed test implementation.

## 3. Security/contract test scenarios — positive and negative

Fixtures: Requester A/B; assigned Agent X; team Agent Y without assignment; outside-team Agent Z; dispatcher L (trusted grant, not JWT fourth role); Admin M; valid/invalid active teams/agents; controlled clock; current and stale revisions separately. Future tests are NOT RUN unless existing evidence column explicitly says source test executed. All mutation tests assert exact row counts, field values, Revision increment and event types/counts; denial/error asserts no business mutation/audit success. “P/N” is positive/negative scenario, not production status.

| Test ID | Rule/transition | Positive scenario | Negative scenario / expected result | Current evidence |
|---|---|---|---|---|
| TCON-01 | DOM-01, LC-04/TR-01 | Current3-field create ->201 Submitted1 + Location; owner A | no token →401, invalid principal401 | SQLite create passes; HTTP blocked |
| TCON-02 | DOM-03, DEC-02 | raw100 title accepted; target approved boundary tested | raw101 rejects baseline400; spaces/null/Unicode/raw-vs-trim boundaries | existing101/empty/trim service tests PASS; new boundaries NOT RUN |
| TCON-03 | DOM-03, DEC-03 | trimmed10 description accepted baseline | trimmed9/whitespace rejects400; very long accepted baseline vs approved future cap explicit | short/trim tests PASS; new cap cases NOT RUN |
| TCON-04 | DOM-04 | all11 codes roundtrip | unknown/lowercase/UUID/null category400 baseline | invalid category test PASS; grid NOT RUN |
| TCON-05 | DOM-01 | API sets owner from A even unknown requesterId field | new strict DTO owner/status/priority injection400; internal service Guid.Empty cannot persist | API/new tests NOT RUN |
| TCON-06 | DOM-06 | A reads own detail | B reads A detail404, matching missing404; no hidden snippets/counts | SQLite owner/missing/other PASS; HTTP foreign test missing |
| TCON-07 | DOM-06 | X assigned or Y current team reads permitted projection | Z outside scope404; invalid/revoked membership fails closed | no Agent scope test/model on dev; NOT RUN |
| TCON-08 | DOM-06, LC-02 | M explicit org read, management grant with reason | authenticated unknownrole403; M still state/version guards | SQLite Admin read PASS; target grid NOT RUN |
| TCON-09 | DOM-07 | all valid persisted states restore semantic name | invalid stored99 quarantined503 target; request undefined enum400 | NOT RUN |
| TCON-10 | DOM-03 | complete approved required command fields | missing title/description/category400 baseline; missing resolution/reopenreason422 target; invalid version400 | partial source tests PASS; target NOT RUN |
| TCON-11 | LC-01 | allowlisted per-action inputs accepted | targetStatus/owner/internal flag/priority injection400; caller UUID never body actor | NOT RUN |
| TCON-12 | DOM-07 | dev numeric0–6 each match FE labels/actions |2 must not Triaged;6 must not PendingExternal; unknown label/no actions | source mapping executed; FE automated test NOT RUN |
| TCON-13 | DOM-07 | additive statusName and number agree; old fallback explicit | disagreement fails decoder; string-only route version cannot mutate legacy body silently | NOT RUN |
| TCON-14 | DEC-05 | representative dev0–6 migration retains meaning; proposed7–9 additive | main-style ordinals imported only via reviewed provenance; Cancelled never coerced | NOT RUN; source compare done |
| TCON-15 | DOM-04/05 | main classifier types mapped by stable names | zero-based Low0 vs referenceLow1 cannot be cast interchangeably | main source read only, tests NOT RUN on main |
| TCON-16 | DOM-05 | all9 impact/urgency pairs match approved matrix | undefined enums400; direct priority override400; AI never state transition | main tests exist but not run; dev target NOT RUN |
| TCON-17 | DOM-02 | isolated sequence9999/10000/10001 yields unique untruncated numbers | old LPAD expression collision reproduced isolated; unique reject no duplicate row | authoritative PostgreSQL doc/source evidence; DB NOT RUN |
| TCON-18 | DOM-02/08 | parallel numbers unique; UTC year rollover; gaps after rollback permitted | no annual reset/reuse/MAX+1; client number injection400 target | NOT RUN |
| TCON-19 | DEC-09 | valid principal-Identity association uses compatible types | unknown/empty/orphan user handling rejects; no UUID-to-text FK guess | NOT RUN |
| TCON-20 | DEC-09/10 | trusted department snapshot retained through later profile changes | missing profile stays documented nullable baseline; no invented ID; not scope grant | NOT RUN; current null source confirmed |
| TCON-21 | LC-04/TR-02 | future draft create returnsDraft0/nullNumber via new surface | legacy POST must remainSubmitted; draft field requirements cannot change old DTO | NOT RUN |
| TCON-22 | TR-03 | own valid draft submit once ->Submitted + number/time/event | foreign404; current-version wrongstate422; stale409; failed submit no partial number persisted | NOT RUN |
| TCON-23 | LC-05/TR-04 | D/M authorized valid triage ->Triaged7 + deterministicPriority | Requester403; out-ofscope404; wrongstate422; invalid enum400 | NOT RUN |
| TCON-24 | TR-05 | eligible X selfassign Triaged ->Assigned8 | second concurrent claimant409; inactive/nonAgent422; no 'me' UUID sent | NOT RUN |
| TCON-25 | TR-06 | L/M assigns active team Agent | ordinary non-dispatch Agent403; wrong-team/inactive target422 | NOT RUN |
| TCON-26 | TR-07 | valid reassign Assigned/IP preserves source; updatesAssignedAt | pending/Resolved422; target invalid422; stale409 | NOT RUN |
| TCON-27 | TR-08/09 | authorized release Assigned/IP ->Triaged; clearsAgent/currentAssignedAt | unrelated visible Agent403; PendingUser/Resolved422 | NOT RUN |
| TCON-28 | TR-10 | X start Assigned ->InProgress; firstStartedAt recorded | Requester403, unassigned/wrongstate422; M/D intervention withoutreason422 | NOT RUN |
| TCON-29 | TR-11 | public question + PendingUser transition atomic | blankquestion422; private note content cannot be put in public projection; child save failure rolls back | NOT RUN |
| TCON-30 | LC-06/TR-12 | owner reply keepsPendingUser/startOfWait; public comment/event | B404; isInternal escalation400; no silent resume; Closed/Cancelled422 | NOT RUN |
| TCON-31 | TR-13 | assigned/dispatch/M resumePendingUser ->InProgress, clearswait | requester403 even owner; inactiveassignment422; old version409 | NOT RUN |
| TCON-32 | TR-14/15 | staff wait-external then resume; private reason/reference; interval closed | missing reference422; public requester no private bytes; direct resolve fromPendingExternal422 | NOT RUN; optional extension |
| TCON-33 | LC-07/TR-16 | approved resolution InProgress ->Resolved4, noClosedAt | short/blank resolution422; PendingUser/External resolve422; unauthorized403 | NOT RUN |
| TCON-34 | LC-08/TR-17 | owner acknowledgment or reasonedM closeResolved ->Closed5 | assigned nonowner Agent403; current-version wrongstate422; no automatic worker close | NOT RUN |
| TCON-35 | LC-09/10/TR-18 | within-window reopen validassignment ->InProgress; count+1 | Closed422, unrelatedAgent403, missingreason422; wrongscope404 | NOT RUN |
| TCON-36 | LC-10/TR-19 | missing/inactive Agent + activeTeam/triage ->Triaged; assignment cleared | missingTeam/triage422 remediation; no arbitrary assignee or globalfallback | NOT RUN |
| TCON-37 | LC-09/DEC-07 | at lower/upper UTCwindow bounds accepted | upper+tick expired422; now<ResolvedAt422; missingconfig failclosed; disabled72h automation | NOT RUN |
| TCON-38 | DOM-08/TR-18/19 | reopen oldresolution cleared/currentcount increment; nextresolve newcycle | oldcycle time/reason retainedhistory; no rewrittenSubmitted/Created/TicketNumber; duplicate retry no doublecount | NOT RUN |
| TCON-39 | LC-14/TR-20 | legacyCancelled correct label and scopedread | no cancel route404; all terminal mutation commands422 for authorized action | NOT RUN |
| TCON-40 | TR-21/22 | scoped route preservesvalidagent/state or downgradesTriaged ifinvalid | no targetgrant403; no IP with missingagent; stale409; Pending*/Resolved route422 | NOT RUN |
| TCON-41 | DOM-09 | two independent contexts/instances expectedrevision4: exactly onecommit | losingwriter409, no duplicateevent/version increment; finalassignment matches winner | NOT RUN — needs real PostgreSQL |
| TCON-42 | LC-03/09 | simultaneous close/reopenResolved: one accepted | loser stale409 beforestate422; terminal outcome consistent; subsequent wrongstate currentversion422 | NOT RUN — real PostgreSQL |
| TCON-43 | LC-11 | transaction includes ticket/comment/audit | forced DB/audit failure rollbackall; no aftercommit notification onfailure; retry no false replay | NOT RUN — real PostgreSQL fault injection |
| TCON-44 | DOM-10/LC-12 | owner public detail/timeline, staff permittedhistory | hidden notes/vendor/AI metadata/snippets/attachmentURLs/counts never included; parent-child mismatch404 | NOT RUN — models/API absent |
| TCON-45 | DEC-10 | blank + upgrade PostgreSQL schema/migrations/seeder/constraints/xmin verified | malformedhistory/reserved-column/Identitymissing issues caught; rollback never drops populatedaudit | HTTP/DB fixture BLOCKED |
| TCON-46 | DOM-08/09 | one UTCnow/createtimestamps, revision1 then+1; response andexpectedRevision agree | missing/invalid/stale version400/409; UTCoffset roundtrip; int32 overflow reject withoutwrite | NOT RUN |
| TCON-47 | DOM-11 | A replay samekey/payload same201/id acrossinstances, no extra row | B samekey distinctowner; A changedpayload409; failure/TTL/auth tests; nosecretlogging | existing same-process happy HTTP test blocked; H candidate tests not rerun here |
| TCON-48 | LC-01/03/14 | exhaustive10 states×all enabledcommands accept onlyTR allowlist | every other source denies422 with currentversion; role403/scope404/stale409 independent | NOT RUN; implementation handoff |

Test matrix has48 scenario IDs; do not report48 tests passed. Future PendingExternal/draft/routing actions remain disabled until DEC sign-off and prerequisite models. Main11 priority testcases exist (9 theory cases+2 invalid facts), not included in dev21 result.

## 4. Code changes required — no implementation in this branch

| Priority/order | Backend change / affected path | Frontend change / affected path | Dependency/owner | Migration/API compatibility |
|---|---|---|---|---|
|0 P0 | integrate reviewed persistent idempotency H candidate; Api/Endpoints/TicketEndpoints.cs, Infrastructure/Ticketing/TicketService.cs/persistence | reuse key per logical create attempt only after agreed contract | Anh Nguyen + Đức Anh + Kiệt | additive durable records; same201 create; payloadconflict409 explained; real PostgreSQL/multiinstance gate |
|1 P1 | fix sequence default via new migration; Persistence/ApplicationDbContext.cs and Migrations | number regex allows4+ digits; don't hardcode TK | Anh Nguyen/Kiệt | preserve IDs/numbers/max30; no renumber; forward fix |
|2 P1 | DEC-05 explicit status mapping/version/schema | features/tickets/types.ts and all3 displays/action panels; fix3 TS errors | Đức Anh/Anh Nguyen/Thu Hoài | keep0–6; readerfirst; new7–9 behind deployment gate |
|3 P1 | approve DEC-01–10; domain factory/value types/state guards; Domain/Ticketing/Ticket.cs | approved title/category/DTO validation and unknown-state UI | TC-262/263 Backend; Architect | new limitations versioned; no Draft rewrite or GUIDcategory surprise |
|4 P1 | compatible Identity/team/membership + additive field/logicalversion/audit schema; Persistence/ApplicationDbContext.cs/Migrations | response revision handling; separate public/staff DTOs | Backend TC-248/294 + QA | deployedschema survey; nullable expansion/backfill provenance; no destructiveDown |
|5 P1 | Application explicit commands/interfaces; Infrastructure transaction/CAS/events; Api peraction auth; TC-300/301 | ticketApi namedactions; replace /assign+'me'; capability-awareUI; TC-302 | Anh Nguyen/Thu Hoài | actionpaths approved /api;422/409 typederrors; new commands additive |
|6 P1 | public/private comment/history projection/atomicchildren; TC-303 dependency | replace mockedtimeline with realserver; separate notespublic; typed error/reloadfeedback | Backend/FE/Architect | privacy/versioned DTOs; don't release private optionalfields via genericresponse |
|7 gate | meaningful unit/API/real PostgreSQL tests TCON matrix, fresh/upgrade migrations | test runner/decoder/schema + Node22 build | Kiệt + Backend + FE | CI pass required; mainprioritytests need reviewedintegration, notcopiedenum |

No generic transition endpoint, implicit Admin state override, new auto-close feature, SLA timer or authentication rewrite is authorized in this docs scope. Assignment rows only specify dependencies/semantics; advanced assignment remains outside TC-199.

## 5. Migration and rollout risks / rollback

1. Inventory actual schema/data/writer versions before migrations; no DB accessed in this audit. If old main-style numeric values were ever persisted, infer meaning only from writer/schema provenance, not ordinal.
2. Preserve current0–6 and public createSubmitted; append status codes with readercompatibility first. Never set2=Triaged or6=PendingExternal on existing dev data.
3. New fields initiallynullable; logicalversion seeded only with defined rollout; classifier/assignment/time gaps produce exceptions/remediation report, not invented records. ClosedAt required onlyClosed, ResolvedAt may need vetted provenance.
4. Title/reference caps need scans; do not shrink200->100 or truncate descriptions. Per-command new input caps cannot invalidate unchanged legacy fields. No global description4000 DB CHECK while legacy create is unbounded; require coordinated versioned writer rollout first. UUID FK to string Identity Id needs deliberate type/bridge design, not SQL cast on unknown IDs. Missingdepartment remains knownnullable until profile decision.
5. Sequencefix changes future default only; one sequencevalue evaluation; backup prior definition/data for reviewedrollback. Olddefault restored would restoreoverflowbug, so preferforwardfix.
6. Transactional Ticket+audit+children tests first, then endpoints/FE. Old reader binaries must not receive newcodes7–9; rolling backwriterrequiresfeatureoff and compatible reader retained.
7. Rollbackapplication/disablecommands preservesaudit/history/newcolumns. No Down-drop populatedtickets/audit/version fields; data backups and rehearsal required. Irreversible historical ambiguity is a blocker requiringmanualreview.

These are plans, not executed migrations/deployments.

## 6. Verification actually executed

Environment .NET SDK10.0.300, Node24.13.1, npm11.8.0. Commands ran only in new ticket-domain worktree; dependencies/build/logs are ignored files. Root frontend/audit/hotfix/RBAC not built or edited.

| Command | Result | Evidence / limits |
|---|---|---|
| git fetch origin --prune; new worktree from origin/dev; git pull --ff-only origin dev | PASS | already up-to-date; HEAD matchesD SHA; branchcorrect, initialclean |
| dotnet build src/backend/ServiceDesk.sln -c Release | PASS exit0 |0 warnings / 0 errors; .cache/ticket-build.log |
| dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category!=Integration" | PASS exit0 |21=Domain 4 + Application 11 + Architecture 6; .cache/ticket-unit.log; no lifecycle tests; TRX reused filename by runner, perassembly summaries in log are count evidence |
| dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj -c Release --no-build --filter "Category=Integration&(FullyQualifiedName~TicketEndpointTests\|FullyQualifiedName~DatabaseTests)" | FAIL exit1; ENVIRONMENT BLOCKED |10 failures during Docker fixture setup,0 pass / 0 skipped; npipe://./pipe/docker_engine unavailable; .cache/ticket-integration.log and TRX |
| npm ci --no-audit --no-fund | PASS exit0 |272 packages, lock/manifests untouched |
| npm run build | FAIL exit2 |3 existing TypeScript errors GAP-10; no productionbundle |
| source enum comparison0–8 | PASS comparison, confirms GAP-03 | printed Backend/FE mapping; not HTTP/UI regressiontest |
| TCON future tests / realDB sequence/migration/concurrency / frontend decoder tests | NOT RUN | proposed/missing implementation andDockerblocked; no claimcoverage/securityacceptance |
| document structure/ID/path/self-review checks + git diff --check | PASS | IDs/cross-references/UTF-8/no trailing whitespace checked; not proof of future command behavior |

Re-run integration command with **literal pipe inside quoted filter**, not escaped pipe; Markdown table escaping above is display only:

~~~powershell
dotnet build src/backend/ServiceDesk.sln -c Release
dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category!=Integration"
dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj -c Release --no-build --filter "Category=Integration&(FullyQualifiedName~TicketEndpointTests|FullyQualifiedName~DatabaseTests)"
~~~

For proposed domain/API gates implement TCON cases first; then real PostgreSQL CI on supported runtime. Do not re-label fixturefailure as 10 product bugs or silently skipIntegration.

Read-only source rechecks:

~~~powershell
git rev-parse HEAD origin/dev origin/main origin/feature/core-ticket-lifecycle
git rev-list --left-right --count origin/dev...origin/main
git rev-list --left-right --count origin/dev...origin/feature/core-ticket-lifecycle
git show origin/dev:src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs
git show origin/main:src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs
git show frontend:docs/architecture/02-ticket-domain-and-field-contract.md
git show frontend:docs/architecture/04-ticket-state-transition-and-reopen-matrix.md
git status --short
git diff --cached --name-status
~~~

## 7. Self-review, changed files and preservation

Only requested docs are changed/new:
- docs/architecture/03-ticket-domain-contract.md
- docs/architecture/04-ticket-lifecycle-policy.md
- docs/reviews/ticket-contract-gap-analysis.md

Checked scope/current vs Proposed/TBD, 30 target fields, 10 DEC decisions, 22 transition rows/default deny, ownership/role/resource/state separation, no enum renumber, baseline create compatibility, window/auto-close ambiguity, each gap severity/path/SHA/impact/repro/owner/tests, full positive/negative matrix. Draft/Resume/WaitExternal/route/version policies require stakeholder approval; documents do not invent sign-off.

Protected baseline (hash/index/branch/head/status verified before work):
| Worktree | Branch / HEAD | Initial state | Final verification |
|---|---|---|---|
| ai-it-service-desk | frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7 | clean; 9 user-committed docs intact; 154 files | PASS — branch/HEAD/status/index hash, tracked+untracked content hashes unchanged |
| ai-it-service-desk-audit | audit/repository-audit-20261009@6b124e73e9cb95350a505a24a548cb663ab1b7de |3 untracked reports; 148 files | PASS — branch/HEAD/status/index hash, tracked+untracked content hashes unchanged |
| ai-it-service-desk-p0-idempotency | fix/p0-idempotency-isolation@6b124e73e9cb95350a505a24a548cb663ab1b7de | existing 18 pending changed files; 157 files | PASS — branch/HEAD/status/index hash, tracked+untracked content hashes unchanged |
| ai-it-service-desk-rbac | docs/tc-246-247-rbac@6b124e73e9cb95350a505a24a548cb663ab1b7de |2 staged RBAC docs; 147 files | PASS — branch/HEAD/status/index hash, tracked+untracked content hashes unchanged |

Shared remote refs/worktree registration changed by authorized fetch/new worktree only. No original worktree switch/stash/reset/stage/commit; no audit/hotfix/RBAC content/index touched. Hash comparison covers all tracked/nonignored untracked files and index entries, not volatile ignored cache metadata.

Remaining risk: business decisions, actual data provenance, PostgreSQL migration/sequence/xmin/race/replay evidence and Frontend build/decoder gate. Documentation task delivers reviewable contract; release/security gates remain blocked.
