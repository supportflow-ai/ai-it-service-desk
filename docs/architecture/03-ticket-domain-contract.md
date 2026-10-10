# Ticket domain contract — TC-199 / TC-260 / TC-261

- Ngày audit: 2026-10-10 (Asia/Bangkok). Trạng thái: READY FOR REVIEW; không phải sign-off triển khai.
- Checkout: docs/ticket-domain-lifecycle, từ origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de; fetch và pull --ff-only thành công, checkout sạch trước audit.
- Phạm vi: đặc tả Ticket, ownership, persistence/API compatibility và handoff cho Backend/Frontend. Không sửa business code, enum, migration hoặc authentication.
- Quy ước: **Verified** = đọc source/chạy kiểm chứng nêu rõ; **Proposed** = target đề xuất; **TBD** = quyết định nghiệp vụ chưa có evidence phê duyệt. Rule mới chỉ dùng triển khai sau review các DEC tương ứng.
- Tài liệu đi cùng: [lifecycle](04-ticket-lifecycle-policy.md), [gap analysis](../reviews/ticket-contract-gap-analysis.md).
- RBAC tham chiếu: docs/tc-246-247-rbac@6b124e73, docs/architecture/01-actor-use-cases.md và 02-rbac-permission-matrix.md hiện đang staged ở worktree riêng, chưa thuộc dev; không copy hoặc thay đổi index đó.

## 1. Evidence và Jira

Mọi đường dẫn source trong tài liệu, trừ chỗ ghi branch khác, dùng snapshot origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de.

| Evidence ID | Source path / dòng | Xác minh |
|---|---|---|
| SRC-01 | src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs:3–15 | Entity 10 scalar fields, public setters; chưa có factory/transition behavior |
| SRC-02 | src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:3–11 | 7 enum values explicit 0–6 |
| SRC-03 | src/backend/ServiceDesk.Domain/Ticketing/TicketCategories.cs:5–17 | 11 string codes, không phải category GUID |
| SRC-04 | src/backend/ServiceDesk.Application/Ticketing/Commands/CreateTicketCommand.cs:3–6; Dtos/TicketDto.cs:3–11 | Create chỉ 3 input fields; DTO status int, không có RequesterId/UpdatedAt/version |
| SRC-05 | src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs:23–72,75–97 | Create Submitted; title raw <=100; description trimmed >=10; owner qua parameter; detail owner filter cho nonstaff |
| SRC-06 | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:18–86; Program.cs:52–54 | POST /api/tickets và GET /api/tickets/{id}; policy cho Requester/Agent/Admin; global replay cache |
| SRC-07 | src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:30–96 | PostgreSQL mapping, title200, unique number, shadow xmin; SQLite bỏ xmin |
| SRC-08 | src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs:16–62 | Sequence và tickets; thiếu Identity migration/model snapshot trong dev |
| SRC-09 | src/frontend/service-desk-web/src/features/tickets/types.ts:1–47; api/ticketApi.ts:8–77 | FE enum9, optional staff fields, mock timeline, commands chưa có server |
| SRC-10 | src/frontend/service-desk-web/src/features/tickets/components/CreateTicketPage.tsx:47–84 | UI title max100, description min10; không trim validator |
| SRC-11 | tests/ServiceDesk.Application.Tests/Ticketing/TicketServiceTests.cs:22–216; tests/ServiceDesk.IntegrationTests/TicketEndpointTests.cs:22–227 | 10 SQLite service tests, 9 HTTP tests; chưa có lifecycle tests |
| SRC-12 | src/backend/ServiceDesk.Application/Common/Interfaces/IClock.cs:8–13; Domain/Common/DomainDefaults.cs | Clock abstraction có sẵn nhưng service dùng DateTime.UtcNow; 4000 là generic constant, không phải Ticket enforcement |

| Jira | Nội dung / tình trạng đọc ngày audit | Owner / consumer |
|---|---|---|
| [TC-199](https://evolving-kg-drift.atlassian.net/browse/TC-199) | In Progress; fields/initial/category/ownership/visibility/lifecycle; out of scope advanced assignment, SLA, AI triage behavior | Đức Anh; Backend reviewer; QA |
| [TC-260](https://evolving-kg-drift.atlassian.net/browse/TC-260) | In Progress; field specification | Đức Anh |
| [TC-261](https://evolving-kg-drift.atlassian.net/browse/TC-261) | In Progress; requester own tickets và S1/S2 lifecycle | Đức Anh |
| [TC-299](https://evolving-kg-drift.atlassian.net/browse/TC-299) | To Do; graph, actor, reason/reopen; description không quyết định số ngày cụ thể | Đức Anh |
| [TC-262](https://evolving-kg-drift.atlassian.net/browse/TC-262) / [TC-263](https://evolving-kg-drift.atlassian.net/browse/TC-263) | To Do / In Progress; domain types và invariant tests | Anh Nguyen |
| [TC-264](https://evolving-kg-drift.atlassian.net/browse/TC-264) | Done; architecture review; status không chứng minh dev đã có lifecycle | Đức Anh |
| [TC-208](https://evolving-kg-drift.atlassian.net/browse/TC-208) / [TC-300](https://evolving-kg-drift.atlassian.net/browse/TC-300) / [TC-301](https://evolving-kg-drift.atlassian.net/browse/TC-301) | To Do; domain transition, API/auth/audit; SLA clocks/automation ngoài scope TC-208 | Anh Nguyen |
| [TC-302](https://evolving-kg-drift.atlassian.net/browse/TC-302) | To Do; allowed action controls consume server contract | Nguyễn Thị Thu Hoài |
| [TC-294](https://evolving-kg-drift.atlassian.net/browse/TC-294) / [TC-303](https://evolving-kg-drift.atlassian.net/browse/TC-303) | To Do; assignment permission và note/public visibility dependency | Đức Anh |

Đã đọc description/status/parent/subtasks/issue links của các issue trên; TC-199 có 1 comment, đã đọc toàn bộ, không có quyết định field/lifecycle bổ sung. Không suy ra sign-off từ Jira status hoặc label APPROVED trong tài liệu cũ. Rovo search cảnh báo coverage nguồn có thể chưa đầy đủ; không tuyên bố đã đọc mọi Confluence/PRD/ERD.

ADR-001/002/003/004/005/010 quy định modular monolith, Domain độc lập framework, PostgreSQL, React/API, Identity/JWT và test DB thật. Chúng không chốt Title100/200, Draft flow, enum values hay reopen window. Nhận xét về PRD/ERD trong docs cũ chỉ là reference, chưa xác minh tài liệu gốc ngoài repository.

## 2. Field contract hiện có — Verified

Required là invariant do API/service kiểm tra hay NOT NULL do DB kiểm tra; không đánh đồng hai lớp. DTO camelCase theo cấu hình HTTP mặc định; Status là int đã được cast ở service.

| Field | Data type | Required | Default | Validation | Ownership | Persistence | API representation |
|---|---|---|---|---|---|---|---|
| Id | Guid | yes | Guid.NewGuid ở service | Endpoint route GUID; domain không bảo vệ Guid.Empty | server immutable target | uuid PK; column Id | id UUID string |
| TicketNumber | string | DB yes | DB expression IT-year-sequence | unique, max30; overflow bug GAP-02 | DB/server | ticket_number varchar(30), sequence | ticketNumber string |
| RequesterId | Guid | DB yes | JWT principal GUID từ endpoint | Null/non-GUID principal401; service tự nhận parameter chưa check Empty | authenticated requester | requester_id uuid; index; chưa FK tới Identity | không có trong DTO |
| RequesterDepartmentId | Guid? | no | null | chưa lookup/snapshot validation | server intended snapshot | requester_department_id nullable uuid | requesterDepartmentId UUID/null |
| Title | string | yes | input.Trim() | raw .Length <=100; whitespace rejected; .NET UTF-16 units | requester tại create | title varchar(200) NOT NULL | title string |
| Description | string | yes | input.Trim() | whitespace rejected, trimmed UTF-16 length >=10; không max | requester tại create | description text NOT NULL | description string |
| CategoryId | string | yes | input, không trim/default | exact case-sensitive 11 codes SRC-03 | requester selection | category_id varchar(20) NOT NULL; không FK/check allowlist | categoryId string |
| Status | TicketStatus | DB yes | Submitted=1 tại create; entity default0 | không factory/check enum/state transition | server | status integer NOT NULL; không CHECK whitelist | status JSON number |
| CreatedAt | DateTime | yes | DateTime.UtcNow | UTC từ code, không DB default/order constraint | server | created_at timestamp mapping; schema runtime chưa xác minh | createdAt ISO date-time |
| UpdatedAt | DateTime | yes | lần gọi UtcNow riêng | chưa update commands; không bảo đảm equality create | server | updated_at timestamp mapping | không expose |
| xmin | shadow uint | PostgreSQL-only | system-managed | IsConcurrencyToken; SQLite bỏ mapping | DB-only | system xid; migration generated SQL cần DB gate | không expose/version input |

SRC-04 DTO bắt buộc Description trong C# nhưng FE khai báo optional; FE còn optional priority/resolution/requesterName/assignedAgent*, server chưa gửi. Không dùng optional UI field làm chứng cứ persistence. Create response hiện trả **toàn bộ TicketDto**, FE helper chỉ khai báo id/ticketNumber là subset.

## 3. Proposed target fields và trách nhiệm

Target type shorthand: UTC instant = DateTimeOffset normalized to offset0 in Application/Domain; new database timestamp columns use timestamptz; API uses ISO8601 UTC Z. Existing DateTime columns are inspected for actual type/provenance before any conversion, never blindly shifted. Các dòng có P cần migration/use case mới; không mô tả là đang tồn tại. Required theo state phải được thực thi ở Domain và persistence guard phù hợp. Baseline create hiện có vẫn Submitted với 3 fields; Draft là surface mới theo DEC-01.

| Field | Data type | Required | Default | Validation | Ownership | Persistence | API representation |
|---|---|---|---|---|---|---|---|
| Id | Guid | all states | UUID server | nonempty, immutable | server | uuid PK, unchanged | id UUID |
| TicketNumber | string? | Submitted trở đi; nullable chỉ future Draft | DB-issued on submit, existing create issues once | unique, max30; IT-YYYY-sequence, min4 digits/no truncation DEC-06 | server | keep unique; nullable only with Draft rollout | ticketNumber; null only new draft DTO |
| RequesterId | Guid | all states | authenticated subject | nonempty, immutable; foreign owner forbidden | server principal | uuid; identity association DEC-09 | excluded public unless reviewed need; not writable |
| RequesterDepartmentId | Guid? | optional baseline; policy TBD | trusted profile snapshot if available | absent profile never fabricated; not visibility/team grant | server | nullable UUID until DEC-09 | existing nullable field retained; staff/public review |
| Title | string | create/submit | normalized input | baseline raw1–100 nonblank; aggregate nonblank<=200; proposed new draft input trimmed5–200 DEC-02 | own Draft editor P; immutable submitted baseline | existing varchar200; don't narrow before scan | title string |
| Description | string | create/submit | normalized input | aggregate baseline trimmed>=10, no global max; proposed new input10–4000 DEC-03 | own Draft editor P | text; approved cap/check additive after scan | description string, required detail |
| CategoryId | stable string code | baseline Submitted required; future Draft optional; Triaged required | no guessed category | SRC-03 allowlist; unknown rejected; not GUID DEC-04 | requester baseline; authorized triage P | current varchar20; future nullable Draft/check plan | categoryId code/null in new draft |
| Impact P | nullable Impact enum | Triaged/Assigned/processing/resolution | null before triage | allowlisted Low/Medium/High; no default Low | staff triage | new nullable column/check; do not shift main codes0–2 | proposed names, not enum ordinal |
| Urgency P | nullable Urgency enum | same as Impact | null | Low/Medium/High | staff triage | nullable column/check | Low/Medium/High |
| Priority P | nullable Priority enum | same as Impact | compute only | deterministic matrix; client/AI cannot set directly | server Domain | nullable column; main codes P1=0…P4=3 preserved if used | P1/P2/P3/P4 |
| Status | TicketStatus | all | baseline Submitted1; future Draft0 | allowed values + approved graph, DEC-05 | server commands only | keep0–6, proposed append7–9 | existing status:number; additive statusName proposal |
| SupportTeamId P | Guid? | required before staff queue/Assign | null until explicit route | active authoritative team; source/target grants | dispatcher/Admin | new nullable UUID; membership source dependency | staff supportTeamId; public safe label if approved |
| AssignedAgentId P | Guid? | Assigned/InProgress/Pending*/Resolved | null until assign | active Agent in team; assignment distinct owner | dispatcher/self-assign checked | nullable Identity-compatible association DEC-09 | staff UUID; public display only |
| Resolution P | string? | Resolved/Closed | null until resolve | public-safe, proposed trim10–4000 DEC-03 | assigned/dispatch/Admin | nullable text, state guard | requester resolution string/null |
| ResolutionCategoryId P | string? | optional | null | taxonomy TBD; don't fake GUID FK | authorized resolver | only after taxonomy decision | omit until reviewed |
| CreatedAt | UTC instant | all | one trusted now | never client-controlled, immutable | server | keep existing column; type conversion review | ISO8601 UTC Z |
| UpdatedAt | UTC instant | all | same now as CreatedAt on new create | monotonic with authoritative command clock | server | existing column | proposed updatedAt additive |
| SubmittedAt P | UTC instant? | submitted states | same now on direct create/submit | immutable first submission | server | nullable additive; backfill provenance DEC-10 | submittedAt nullable legacy |
| TriagedAt P | UTC instant? | after triage | command now | first successful triage; corrections have separate events | server | nullable additive | staff; public optional reviewed |
| AssignedAt P | UTC instant? | current assignment exists | now on assignment | clear on unassign, old value in audit | server | nullable additive | staff nullable |
| StartedAt P | UTC instant? | after first start | first start now | retain first start through waits/reopen | server | nullable additive | reviewed public lifecycle field |
| PendingUserSince P | UTC instant? | only PendingUser | request-info now | clear when resume; historical interval in audit | server | nullable additive | safe lifecycle summary |
| PendingExternalSince P | UTC instant? | only PendingExternal | wait-external now | clear on resume; no vendor secrets publicly | server | nullable additive | staff only |
| ResolvedAt P | UTC instant? | current Resolved/Closed | resolve now | clear on reopen; old resolution cycle in audit | server | nullable additive | resolvedAt |
| ClosedAt P | UTC instant? | Closed only | close now | never required merely Resolved; immutable terminal | server | nullable additive | closedAt |
| LastReopenedAt P | UTC instant? | after any reopen | command now | most recent reopen | server | nullable additive | reviewed public summary |
| ReopenCount P | nonnegative int | new allstates; legacy nullable until backfill | 0 for new records | increment exactly once per accepted reopen | server | additive integer/check | proposed reopenCount |
| Revision P | positive int32 | after version rollout | 1 on new ticket | compare expected; +1 per accepted aggregate mutation | server | app revision column; expected input checked with DB token | proposed revision JSON integer for both visible projections; optional ETag, not raw xmin |
| RowVersion | uint shadow xmin | PostgreSQL row | system managed | optimistic DB CAS; not timestamp/revision business count | DB | existing xid, no new byte[] rowversion | internal only; never security authority |
| AuditEvents P | immutable events | successful state/assignment change | none on deny | event identity, actor, from/to, time, correlation, revision, safe reason | server | separate append-only records, same transaction | public projection separate from privileged history |

Public/staff target projection: owner gets id/number/title/description/category/status/statusName, approved lifecycle times/revision and public resolution; staff additionally gets scoped classification/assignment data. Impact, Urgency, vendor/dependency text, AI details/internal notes and full audit are excluded from requester responses. Public Priority/assignee display and the existing nullable department field require explicit review; new permissions never inferred from a DTO field.

Field count: 30, including the separate AuditEvents collection. Lifecycle semantics are in TR rows in doc04; timestamps not inferred merely from enum order. A legacy Resolved row without actual ResolvedAt is not assigned a made-up reopen window.

### Priority matrix reference

Verified on origin/main@e044f470d38cb9d00a4adca9e59de45abbde3ff5, src/backend/ServiceDesk.Domain/Ticketing/PriorityMatrix.cs and tests/ServiceDesk.Domain.Tests/Ticketing/PriorityMatrixTests.cs; **not integrated into dev**, not rerun on main in this task.

| Impact / Urgency | High | Medium | Low |
|---|---|---|---|
| High | P1 | P2 | P3 |
| Medium | P2 | P3 | P4 |
| Low | P3 | P4 | P4 |

Recommendation DEC-04: reuse this deterministic 9-cell matrix rather than invent another one; PM/Backend must approve taxonomy/business meanings. Existing main enums are zero-based (Impact Low0/Medium1/High2, Priority P1=0), while frontend reference docs claim1-based. Reject undefined enums rather than silently accepting numeric casts.

## 4. Rule IDs, examples và acceptance criteria

| Rule | Contract / status | Example | Acceptance criteria |
|---|---|---|---|
| DOM-01 | Owner from authenticated principal; immutable (baseline endpoint Verified, full aggregate P) | A sends requesterId=B as unknown create field; owner remains A today | Direct API+service entry points cannot forge owner; foreign detail404; target future strict mutation DTO rejects owner400 |
| DOM-02 | ID server UUID, number atomic DB issuer (Verified with GAP-02) | 2 callers create simultaneously | Distinct UUID/number; no MAX+1; sequence gaps allowed; no reuse/renumber after retry |
| DOM-03 | Required fields normalized/validated consistently (P DEC-02/03) | 101-character raw title vs DB varchar200 | Current test rejects101; new agreed boundary tests run at API/Domain/DB/FE; no unauthorized breaking cap changes |
| DOM-04 | Category code contract (Verified/P DEC-04) | NET valid; net/unknown/UUID invalid | All11 roundtrip; no string-to-GUID guessing; active catalog changes versioned |
| DOM-05 | Priority computed, missing triage is null (P) | High+Low ->P3; input priority=P1 | Compute9 cells; forbid direct priority override; undefined Impact/Urgency rejected |
| DOM-06 | Role, resource scope, ownership, state are separate predicates (P; detail partly Verified) | Agent outside Team opens ticket | API denies before mutation; unknown scope fail closed; department snapshot not team membership |
| DOM-07 | Enum persistence and wire version explicit (P DEC-05) | persisted2 means InProgress, never Triaged | All legacy0–6 map unchanged; frontend tests each value; unknown wire status displays Unknown and disables actions |
| DOM-08 | One UTC instant per command, lifecycle cycle history (P) | Reopen clears current ResolvedAt but retains prior resolution event | Inject application IClock; Domain takes plain UTC time; DB/API UTC roundtrip, no invented historical timestamps |
| DOM-09 | Atomic mutation/version/audit (P) | Two assignments with expectedVersion=4 | One succeeds, one409; ticket/version/audit same transaction; failure none; DB xmin protects CAS |
| DOM-10 | Public projection prevents private material leakage (P) | Requester detail vs staff internal note | No notes/AI/vendor/internal counts/snippets/URLs in requester DTO/events; parent+child scope rechecked |
| DOM-11 | Replay scoped and payload-bound (P; current GAP-01) | B reuses A's Idempotency-Key | Never receives A response; same owner/payload single ticket, changed payload409, multi-instance transaction safe |

Compatibility rule: new draft input minimum5/title cap200 or description cap4000 must not retroactively invalidate unchanged legacy fields or alter current create validation. Aggregate invariants retain nonblank Title<=200 and Description>=10 with no global upper cap while the unbounded legacy writer exists. New limits are per reviewed new use case/input. Do not add a global description<=4000 DB CHECK while current create still accepts longer descriptions; such enforcement needs coordinated versioned writer rollout and data scan.

No public DTO exposing sensitive new fields is justified by FE optional types. Server authorization precedes fetching a staff projection. Role flags/buttons are UI hints.

## 5. Decision register — no silent contract replacement

| ID | Evidence / tradeoff | Recommendation and status | Approval owner / implementation impact |
|---|---|---|---|
| DEC-01 | dev tests and UI use one-step create Submitted; frontend@063489b reference demands Draft + /api/v1 | **Proposed:** preserve POST /api/tickets existing three-field/201/Submitted contract; separate new /api/tickets/drafts + submit only after approval. Not change existing create to Draft | Đức Anh + Anh Nguyen + Thu Hoài; new draft nullable number/category migration and new DTO; TC-260/261 |
| DEC-02 | raw title<=100 service/UI/test vs DB200 vs legacy reference trimmed5–200 | **Proposed:** compatibility baseline stays nonblank/raw<=100. Consider trimmed5–200 on separately reviewed contract. 200 rationale is existing schema + reference; min5 rationale is old readability intent, not proven Jira approval | Đức Anh/Backend/FE; no shrink or destructive truncate; decide Unicode count and raw-vs-normalized behavior explicitly |
| DEC-03 | description no max today; DomainDefaults4000 generic; reference resolution/reason min10 caps4000/1000 | **Proposed:** new commands description/resolution10–4000, reason10–1000, public question/reply1–4000 after trim; baseline create unchanged. NUL/malformed Unicode rejected400 in new contract | PM+Backend+QA; scan existing long data before constraints; TC-260/299/303; legacy create cap needs version/review |
| DEC-04 | stable11 code strings dev/FE; main enums/9-cell priority; reference category GUID/Impact1-based mismatch | **Proposed:** retain category code strings; explicit nullable triage values; reuse main matrix subject review; wire names for new enums | Đức Anh+Backend; map external AI contracts by names, not ordinal; new DB types/constraints |
| DEC-05 | legacy0–6 differ main/FE0–8 | **Proposed:** keep persisted0–6; append Triaged7, Assigned8, PendingExternal9. Keep Cancelled6 read-only legacy. status numeric baseline retained; add statusName; future string-only API separately versioned | Đức Anh+Backend+FE; do not merge main enum as-is; audit actual deployed versions/data before mapping |
| DEC-06 | DB IT prefix/global sequence max30; reference TK/five digits/max20 | **Proposed:** keep existing IT numbers and varchar30, no annual reset; fix overflow padding without renumbering; year semantics DB UTC reviewed | Backend+QA; ALTER DEFAULT additive; only new rows affected; numbering gaps acceptable |
| DEC-07 | reference168h reopen/72h auto-close; TC-208 excludes automation; inactive assignee not addressed | **Proposed:** explicit reviewed reopen window, candidate168h from reference. No auto-close feature in this scope. Reopen branches per TR-18/19; window value/startup config must be approved before enabling | Đức Anh+Backend+QA; TC-299; full policy in doc04 |
| DEC-08 | Reference Lead vs current3 roles; RBAC staged includes dispatch capability | **Proposed:** Lead=Agent + trusted team Dispatch grant, not role injected by client. Assignment integration only explicit baseline steps, advanced routing scoped separately | Đức Anh TC-294 + Backend TC-248; new team/assignment data needed; Admin obeys graph |
| DEC-09 | requester uuid vs IdentityUser.Id string; no department/profile/membership model | **TBD:** canonical identity association/department availability and deletion semantics. Never direct UUID FK to text Id without compatible schema or stable bridge | Backend+Architect; no snapshot fabrication; deployment/user-ID survey; identity migration prerequisite |
| DEC-10 | entity lacks events/time/version; no DB/data access here | **TBD:** deployment schema/provenance and legacy state-specific data remediation. Add nullable fields first; do not fabricate resolution/triage/time from CreatedAt | Backend+QA; migration dry-run/snapshots, manual exception report; block incompatible commands on deficient rows |

Numeric proposals are explicit and retain dev history. Choosing the old nine-state numbering instead requires a reviewed data migration (2->4,3->5,4->7,5->8) and a decision for legacy6=Cancelled; it must never become PendingExternal automatically. No actual migration is created here.

## 6. Create/detail/API contract and proposed extensions

**Verified baseline**:

~~~json
POST /api/tickets
{"title":"VPN không kết nối","description":"Không thể kết nối VPN từ nhà.","categoryId":"VPN"}

201 Created
Location: /api/tickets/{id}
{"id":"<uuid>","ticketNumber":"IT-2026-0001","title":"VPN không kết nối","description":"Không thể kết nối VPN từ nhà.","categoryId":"VPN","status":1,"createdAt":"<UTC instant>","requesterDepartmentId":null}
~~~

Placeholder UUID/time are schematic, not a captured HTTP response. GET same id returns200 for owner or current unrestricted Agent/Admin,404 for another Requester or missing object; no token401. Wrong service field validation400. DB constraint/unexpected exceptions currently uncaught by TicketEndpoints. Only these two Ticket routes exist; /tickets/me and command calls in ticketApi are planned, not implementation evidence.

**Proposed command shape**, DEC-03/05/08/10 gated:

~~~json
POST /api/tickets/{id}/resolve
{"resolution":"Đã sửa cấu hình VPN và kiểm tra kết nối.","expectedRevision":4}
~~~

Successful new command200 returns authorized public/staff detail plus revision JSON integer (optional representation-specific ETag). Untrusted fields such as owner/status/priority/internal-only flags are rejected400 by new strict command DTOs. Body expectedRevision must match the returned revision JSON integer; missing/invalid400, stale409 after authorization. Revision is positive int32; at int.MaxValue reject409 VERSION_EXHAUSTED without write, never wrap. ETag, if introduced, is representation-specific; clients must not derive revision from xmin. Resource invisible404, visible but action forbidden403; invalid state/semantic missing input422. Current create field errors remain400. No silent raw xmin or arbitrary targetStatus PATCH exposed.

DTO additions (updatedAt, statusName, capability data/version) require JSON schema and FE runtime decoding/contract tests. Proposed string-only status is a distinct version, not solved by JsonStringEnumConverter alone: current TicketDto.Status is int, so that converter would not change it.

## 7. Clean Architecture and persistence plan

Application verifies principal, scope/target eligibility and calls the aggregate with validated domain primitives and one UTC instant; Domain enforces field and lifecycle invariants independent of HTTP/EF/ICurrentUser/IClock from Application. Infrastructure handles transactions, optimistic CAS and append-only audit. A namespace named ServiceDesk.Application on an Infrastructure file is naming debt, not proof that application assembly owns that implementation.

Persistence sequence before implementing target:

1. Inspect deployed __EFMigrationsHistory and actual tickets/Identity schema on authorized staging copy. Record distinct statuses, max lengths, invalid GUID association, number collisions/sequence value and incomplete historical rows. No production queries or mutations performed here.
2. Establish valid baseline Identity + Ticket migrations/model snapshot. PostgreSQL provider-generated xmin SQL must be inspected; do not add a normal user column named xmin blindly or claim fresh DB verified from SQLite.
3. Fix TicketNumber default in a new migration; evaluate nextval once in a reviewed database helper function used as the default (or an explicit INSERT CTE, not an unreviewed replacement default expression), pad to max(4,length(n::text)); retain current unique constraint and values. Test9999,10000,10001 plus concurrency/rollback/year.
4. Add nullable fields/audit table/logical revision; keep existing int enum codes. Make classifier/assignment/time fields mandatory only after approved data remediation and NOT VALID/validate rollout strategy as applicable.
5. Add explicit factory/commands + atomic audit; wire new DTO/version fields; disable commands whose prerequisite data/config is absent. Build/test gates required before integration.
6. Rollback primarily redeploy compatible readers/disable new commands. Do not Down-drop populated Ticket/Audit fields or shrink Title/truncate text. New states7–9 require old binary traffic stopped or compatibility reader first; blindly rolling back to an old reader is unsafe. Default-generator rollback may reintroduce overflow, so forward fix preferred. Preserve backup/snapshot and audit history.

For concurrency, PostgreSQL xmin is a row token, not portable byte[] or elapsed time. [Npgsql concurrency guidance](https://www.npgsql.org/efcore/modeling/concurrency.html) and [EF Core conflict handling](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) support optimistic CAS; expected client version is an additional application check. Source mapping alone does not prove correct concurrent endpoint behavior.

## 8. Handoff and review checklist

- Backend Anh Nguyen: DEC-01–10 review, TC-262/263 invariants, TC-300/301 commands/transaction/audit; integrate reviewed P0 replay fix separately.
- Frontend Nguyễn Thị Thu Hoài: fix enum decoder/build; consume approved DTO/ETag/allowed actions; stop treating mock timeline as stored evidence.
- Architect Đức Anh: decide/create/version/window/category/department semantics; align legacy references and staged RBAC without overwriting them in this branch.
- QA VÕ NGUYỄN ANH KIỆT: test IDs in gap analysis, real PostgreSQL migration and race tests; reviewers sign only after evidence.

Self-review: source/current vs target separated; ownership not assignment; no enum renumber; Title100/200 rationale explicit; all30 fields have required/default/validation/owner/persistence/wire representation; decisions marked; no authentication rewrite. Business sign-off and PostgreSQL release evidence are still pending.
