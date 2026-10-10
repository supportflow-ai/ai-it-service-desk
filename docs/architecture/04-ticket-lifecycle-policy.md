# Ticket lifecycle policy — TC-261 / TC-299

- Ngày: 2026-10-10 (Asia/Bangkok). Trạng thái: READY FOR REVIEW / Proposed implementation policy.
- Source baseline: origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de; branch docs/ticket-domain-lifecycle.
- [Domain/fields/DEC register](03-ticket-domain-contract.md) và [findings/tests/migration](../reviews/ticket-contract-gap-analysis.md) là phần cùng contract.
- Không có domain state machine hay transition endpoint trên dev hiện tại. Chỉ create Submitted và detail là Verified; mọi command trong ma trận trừ TR-01 là target Proposed hoặc disabled/TBD.
- Các quyết định DEC-01–10 cần Backend/Frontend/Architect review; không coi label APPROVED của frontend reference là chứng cứ rollout/data compatibility.

## 1. State vocabulary, persisted code và serialization

| Semantic state | dev code hiện tại | main / FE hiện tại | Proposed persisted code (DEC-05) | Meaning / constraints |
|---|---|---|---|---|
| Draft | 0 | 0 | 0 | Personal draft; riêng future draft surface; chưa sinh số; không vào staff queue |
| Submitted | 1 | 1 | 1 | Đã gửi; **initial state của current POST /api/tickets**; number/owner/content valid |
| InProgress | 2 | 4 | 2 | Đang xử lý; target phải có active assignee/team và triage fields |
| PendingUser | 3 | 5 | 3 | Chờ requester; public question + PendingUserSince; không đồng nghĩa Assigned |
| Resolved | 4 | 7 | 4 | Có public Resolution + current ResolvedAt; có thể close/reopen theo guard |
| Closed | 5 | 8 | 5 | Terminal, read-only; mọi mutation denied kể cả Admin |
| Cancelled | 6 | không có; FE6=PendingExternal | 6 | Legacy terminal read-only; **không enable cancel command** trong baseline |
| Triaged | không có | 2 | 7 (new) | Category/Impact/Urgency/Priority đã human-validated; chưa cần Agent |
| Assigned | không có | 3 | 8 (new) | Có active SupportTeam và concrete eligible AssignedAgent |
| PendingExternal | không có | 6 | 9 (new) | Chờ bên ngoài; vendor/reference/reason private; optional extension cần sign-off |

Main reference: origin/main@e044f470d38cb9d00a4adca9e59de45abbde3ff5, src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs. Dev/FE evidence: TicketStatus.cs:3–11 và src/frontend/service-desk-web/src/features/tickets/types.ts:3–13, cùng dev SHA. Main enum là primitive cho AI contract, không aggregate workflow implementation.

There are nine active/planned semantic states plus one retained legacy Cancelled = 10 persisted values in this recommendation. Triaged/Assigned/PendingExternal are appended, **not mapped onto2/3/6**. Reopened is an event, not a state. “New/Open” in TC-208 demo is informal Submitted/early queue intent, not a new enum alias.

**Wire policy**:

1. Existing TicketDto.Status is int; keep current numeric representation for existing API. FE first needs an explicit **dev0–6 decoder** rather than the current main-style enum.
2. Proposed additive statusName carries a stable name matching status; contract tests assert both. FE rollout using names must handle old API fallback with correct version mapping.
3. New strings-only contract requires explicit version/content negotiation and corresponding DTO. JsonStringEnumConverter does not convert an int DTO property.
4. Do not change old FE numeric assumptions by changing persisted values. Introduce reader compatibility before new-state writers. Unknown wire names/codes -> Unknown label, no actionable UI controls, telemetry without ticket sensitive content.
5. Unknown persisted values -> quarantine/remediation gate (proposed503 DATA_CONTRACT_UNAVAILABLE on affected operations), not coercion to Draft or another state. Enum casts are not validation. Verify deployed schema provenance: identical raw2 may mean different states across historical writer versions.

## 2. Proposed state graph

~~~mermaid
stateDiagram-v2
    [*] --> Submitted: existing create
    [*] --> Draft: future draft create
    Draft --> Submitted: submit
    Submitted --> Triaged: triage
    Triaged --> Assigned: assign
    Assigned --> InProgress: start
    Assigned --> Triaged: unassign
    InProgress --> Triaged: release assignment
    InProgress --> PendingUser: request information
    PendingUser --> InProgress: staff resume
    InProgress --> PendingExternal: wait external (extension)
    PendingExternal --> InProgress: staff resume (extension)
    InProgress --> Resolved: resolve
    Resolved --> Closed: confirm close
    Resolved --> InProgress: reopen with valid assignee
    Resolved --> Triaged: reopen requiring reassignment
    Closed --> [*]
~~~

Diagram shows the Proposed graph; it is not evidence of implemented commands. Requester reply is a PendingUser self-loop/comment, never an autonomous resume. Legacy Cancelled has no outgoing edges. Reassignment/routing self-loops are listed in TR rows below. Auto-close, SLA clocks and AI autonomous transitions are not included.

## 3. Actor, scope, error and atomicity rules

R=Requester acting as owner; A=Agent; D=Agent with trusted team Dispatch capability (proposed Lead mapping); M=Admin with explicit management permission. A staff identity can use own-requester actions for its own personal ticket. Assignment is not ownership. D is not a new JWT role.

Staff scope = trusted current valid assignment OR authoritative active team membership; action-specific assigned/dispatch predicate still applies. No membership/profile implementation in dev, so new staff commands must fail closed until dependency implemented. An unrouted Submitted ticket is visible only in an explicitly authorized Admin intake scope until a trusted team route is set; ordinary Agent access cannot become a global fallback. Initial intake/default-team routing needs TC-294 review before Agent workflow release. M can access organization resources by explicit grant, but cannot bypass input/version/state/window constraints. Sensitive management actions depend on the fresh-privilege rule in staged RBAC/TC-248; current JWT alone is not evidence that revocation handling exists.

| Rule ID | Rule / example | Acceptance criteria |
|---|---|---|
| LC-01 | Intent commands, not arbitrary Status patch; client sends targetStatus=Closed | New DTO rejects controlled status/owner/priority400; every command has explicit source/guard/event |
| LC-02 | Authorization before state/version revelation; B tries A's ticket |401 unauthenticated;404 missing/invisible;403 visible-but-forbidden action; no business success event/write |
| LC-03 | Concurrency precedes state after authorization; stale start from now-InProgress |400 missing/invalid expectedRevision;409 stale; valid current revision+wrong state422 |
| LC-04 | Create compatibility; same current payload |201 Submitted1, existing Location/body shape; separate draft endpoint after DEC-01; never silently Draft |
| LC-05 | Target triage requires category/impact/urgency; priority derived | Allowed values validated; compute all9 combinations; incomplete fields422; invalid enum syntax/value400 |
| LC-06 | Requester public reply is comment, not workflow permission | Owner reply while PendingUser keeps status, wait timestamp and assignee; staff resume explicit; old reference auto-resume is unapproved alternative |
| LC-07 | Resolve only from InProgress, public resolution required | PendingUser/PendingExternal resolve422; resume first; no implicit close; resolve atomic event and ResolvedAt |
| LC-08 | Close only Resolved by owner/M; Assigned Agent not automatic closer | Owner/M accepted with acknowledgment/reason; nonowner Agent403; Closed/Cancelled mutations422 for authorized action |
| LC-09 | Reopen from Resolved only, reviewed window, reason, cycle history | Exactly boundary allowed; after boundary422; Closed never reopened; Admin same window/graph |
| LC-10 | Reopen assignment eligibility branch explicit | Active assignee+team ->InProgress; inactive/missing assignee+active team ->Triaged; deficient team/triage data422 remediation |
| LC-11 | One aggregate mutation+version+audit transaction | Rollback yields no partial changes/events; conflict writes none; after-commit delivery only |
| LC-12 | Public vs private audit/content projection | Owner sees safe status/public reason, not vendor/internal note/AI fields/counts/URLs; staff endpoint rechecks scope |
| LC-13 | Wait states are reasoned business actions, not enum setters | PendingUser stores public question atomically; PendingExternal stores staff reason/reference privately; resume closes interval |
| LC-14 | Terminal/cancel and automation scope | Closed/legacy Cancelled read-only; no cancel endpoint; no worker auto-close or SLA clock invented by this docs task |

Target error mapping (not current implementation):401 principal;404 hidden/missing;403 forbidden role/function;400 malformed/unknown controlled field/invalid enum encoding/invalid revision;409 stale/version or occupied self-assign;422 invalid transition/semantic guard;503 quarantined data/contract prerequisite unavailable. Legacy create service required-field errors stay400. New semantic required resolution/reopen reason is422 when JSON shape/version is valid.

## 4. Transition matrix — executable decision table after approval

Common guard G: authenticated valid principal; resource visibility; action authorization; current expectedRevision; data contract/config ready. Input limits follow DEC-03. Every accepted mutation increments Revision once and sets UpdatedAt to one command UTC now, persists corresponding immutable event in the same DB transaction. Retry of an already successful command cannot create a second success event: planned commandId/replay behavior must be reviewed, not assumed from current global create cache.

| ID | From | To | Actor / policy | Guard beyond G | Required input | State mutation / timestamps | Error (after authorization) | Audit event |
|---|---|---|---|---|---|---|---|---|
| TR-01 Verified create; audit P | no resource | Submitted1 | R/A/M own identity; current RequireRequester | current3 field validation; replay isolation DOM-11 target | title,description,categoryId; optional current Idempotency-Key | server UUID/number/owner; current CreatedAt/UpdatedAt; target SubmittedAt+revision1 | current400/401; target payload replay409 | target TicketSubmitted(event creation=true); current no audit |
| TR-02 P DEC-01 | no resource | Draft0 | R/A/M own; Ticket.CreateDraft | future DTO fields; owner principal | title,description | no number until submit; Create/Update same now; revision1 |400 validation | TicketDraftCreated |
| TR-03 P DEC-01 | Draft0 | Submitted1 | owner personal scope; Ticket.SubmitOwnDraft | fields satisfy approved new draft submit input policy including category; not already submitted | expectedRevision; categoryId required by this baseline candidate | issue number once; snapshot if approved/available; SubmittedAt; owner immutable |422 wrongstate;400 invalid approved fields;409 stale | TicketSubmitted |
| TR-04 P | Submitted1 | Triaged7 | A assigned or D; M; Ticket.Triage + RequireAgent | valid current staff scope; allowlisted classifier inputs | categoryId,impact,urgency,expectedRevision | set classifier fields; computePriority; TriagedAt first now |400 invalid enum/code syntax;422 incomplete business input/state | TicketTriaged |
| TR-05 P | Triaged7 | Assigned8 | eligible A in Team; eligible M also Agent for self; Ticket.SelfAssign | no current assignee; active team; caller active Agent member | expectedRevision (caller selected server-side) | AssignedAgent=principal; AssignedAt=now |409 occupied/stale;422 noteligible/wrongstate | TicketAssigned(mode=self) |
| TR-06 P | Triaged7 | Assigned8 | D/M; Ticket.AssignOther | target current active team Agent; team active | assignedAgentId UUID,reason,expectedRevision | setAgent+AssignedAt |422 invalidtarget/state;403 noDispatch | TicketAssigned(mode=dispatch) |
| TR-07 P | Assigned8 or InProgress2 | same source state | D/M; Ticket.AssignOther | replacement active Agent in same team; classification valid | assignedAgentId,reason,expectedRevision | replaceAgent; AssignedAt=now; StartedAt first retained |422 target/state;409 stale | TicketReassigned |
| TR-08 P | Assigned8 | Triaged7 | assigned A or D/M; Ticket.Unassign | release only current assignment; active team/classification | reason,expectedRevision | clearAgent/AssignedAt |403 unrelatedassignee;422 state | TicketUnassigned |
| TR-09 P | InProgress2 | Triaged7 | assigned A or D/M; Ticket.Unassign | explicit release reason; leave active processing | reason,expectedRevision | clearAgent/AssignedAt; keep historical StartedAt |422 state;409 stale | TicketUnassigned(from=InProgress) |
| TR-10 P | Assigned8 | InProgress2 | assigned A or D/M; Ticket.Start | valid activeAgent/team; intervention reason for D/M | expectedRevision; reason for intervention | StartedAt set if first start |422 invalidassignment/state | TicketStarted |
| TR-11 P | InProgress2 | PendingUser3 | assigned A or D/M; Ticket.RequestInformation | public-safe question; current assignment valid | message,expectedRevision; intervention reason | create public question; PendingUserSince=now |422 blankquestion/state | InformationRequested |
| TR-12 P dependency TC-303 | PendingUser3 | PendingUser3 (self-loop) | owner R/A/M own; Ticket.AddPublicComment | valid public message; no internal flag elevation | content,expectedRevision | publicComment; status/assignee/wait start unchanged |422 blankcontent;400 privatefields | RequesterReplied (no status change) |
| TR-13 P permission extension | PendingUser3 | InProgress2 | assigned A or D/M; Ticket.Resume | validassignment; review reply or explicit follow-up reason; no automatic resume | reason,expectedRevision | clearPendingUserSince; retainStartedAt |422 state/assignment/input | TicketResumed(source=PendingUser) |
| TR-14 P optional extension | InProgress2 | PendingExternal9 | assigned A or D/M; Ticket.WaitExternal | staff-only reason; vendor OR external reference nonblank | reason, vendorName or externalReference, expectedRevision | private dependency note; PendingExternalSince=now |422 reference/reason/state | ExternalDependencyStarted (private metadata) |
| TR-15 P optional extension | PendingExternal9 | InProgress2 | assigned A or D/M; Ticket.Resume | validassignment; staff-only outcome text | reason,expectedRevision | clearPendingExternalSince |422 state/input/assignment | ExternalDependencyResumed |
| TR-16 P | InProgress2 | Resolved4 | assigned A or D/M; Ticket.Resolve | validassignment; resolution approved min/max/public-safe | resolution,expectedRevision; optional reviewed resolution category | Resolution and ResolvedAt=now; do not close |422 state/blank/shortresolution | TicketResolved |
| TR-17 P | Resolved4 | Closed5 | owner (any role in own scope) or explicit M; Ticket.Close | current ResolvedAt/resolution exists; acknowledgment; M managementreason | acknowledged=true,expectedRevision; M reason | ClosedAt=now; retainResolution+ResolvedAt+assignee history |403 nonownerAgent;422 state/acknowledgment | TicketClosed |
| TR-18 P DEC-07 | Resolved4 | InProgress2 | owner or assigned A/D/M; Ticket.Reopen | within reviewed window; current assignment/team active; triage data valid | reason,expectedRevision | clearcurrentResolution/ResolvedAt; old cycle in audit; ReopenCount++; LastReopenedAt; preserveAgent/team/StartedAt |422 window/state/guard | TicketReopened(target=InProgress) |
| TR-19 P DEC-07 | Resolved4 | Triaged7 | owner or D/M; Ticket.Reopen | withinwindow; assignee missing/inactive but team active, validclassification; action permission stillrequired | reason,expectedRevision | samecycle reset; clearAgent/AssignedAt; count++; queue forassign |422 missingteam/triagedata/window;403 unrelatedAgent | TicketReopened(target=Triaged) |
| TR-20 disabled/TBD | any | Cancelled6 forbidden | no new grant or endpoint | Cancel not in approved baseline | none | no mutation | router404 for nonexistent cancel API; not simulated success422 endpoint | none |
| TR-21 P scoped assignment dependency | Submitted1 or Triaged7 | same source state | D/M; Ticket.RouteTeam | active target; allowed source+target route grants; no existing agent | supportTeamId,reason,expectedRevision | setTeam; status unchanged |403 routegrant;422 target/state | TicketTeamChanged |
| TR-22 P scoped assignment dependency | Assigned8 or InProgress2 | source if target eligible; otherwise Triaged7 | D/M; Ticket.RouteTeam | active target; routing grants; if downgrade classifierdata remains valid | supportTeamId,reason,expectedRevision | retainAgent only if eligible in target; otherwise clearAgent/AssignedAt and leaveprocessing |422 target/data/state;409 stale | TicketTeamChanged(with explicit status/assignment change) |

TR-22 deterministic branch: validate the existing assignee against target membership in the same transaction/snapshot used for mutation; valid -> preserve From, invalid -> Triaged. No InProgress without active assignee is created. Routing is only dependency documentation for TC-294, not advanced assignment implementation added in TC-199.

Policies are target action names, not PolicyNames.cs constants already implemented. Proposed D/M interventions always reasoned/audited; R cannot triage/assign/start/resolve by owning the ticket. A read grant does not grant a transition. TR-13/14/15 add actions absent from staged RBAC42 matrix and need explicit TC-248/247 review; no implicit RequireAgent blanket substitute.

Suggested endpoint set:
- Existing POST /api/tickets, GET /api/tickets/{id}.
- P: POST /api/tickets/drafts; POST /api/tickets/{id}/submit, /triage, /self-assign, /assign-agent, /unassign, /assign-team, /start, /request-information, /resume, /wait-external, /resolve, /close, /reopen; POST /comments and distinct /internal-notes per TC-303.
- No generic /transition or arbitrary status PATCH. Frontend current /assign and literal assignedAgentId="me" require reviewed adapter or update to /self-assign; no GUID guessing. Reference /request-info and current FE /request-information need one agreed naming, recommended current FE /request-information.

## 5. Allowed/disallowed semantics and completeness

The TR table is the allowlist. For every exposed command, every source not listed is forbidden; source state and current revision must be controlled independently in tests.

| Current state | Allowed graph-changing commands after G | Explicit disallowed examples |
|---|---|---|
| Draft | Submit (future) | Triage, assign, start, resolve, close, reopen |
| Submitted | Triage; route self-loop | Start, resolve, close, reopen |
| Triaged | Assign/self-assign; route self-loop | Direct start without Assigned, resolve, close |
| Assigned | Start, unassign, reassign/route | Resolve, request-info, reopen |
| InProgress | Request-info, optional wait-external, resolve, release, reassign/route | Close, reopen, repeated Start |
| PendingUser | Staff resume; owner reply self-loop | Requester autonomous transition; resolve/assign/route/reopen |
| PendingExternal | Staff resume extension | Direct resolve, close, owner resume, reassign |
| Resolved | Close/reopen | Edit, start, triage/assign/route, resolve again |
| Closed | none | All mutations; Admin override/reopen |
| legacy Cancelled | none | All mutations; covert PendingExternal interpretation |

Assignment-free legacy records in processing/resolved states cannot satisfy target invariants by pretending AssignedAgent exists. Data remediation DEC-10 precedes enabling commands. A proposed Unassign InProgress intentionally changes state to Triaged; it is not a null assignment self-loop.

Public comments/internal notes follow separate permissions; pending reply remains a comment operation. Under staged RBAC, public comments after Resolved are possible but do not reopen, alter resolution or restart window. No comments/attachments on Closed/Cancelled; historical reads allowed under visibility. This clarifies the legacy Cancelled omission in staged RBAC wording and requires synchronized docs review.

## 6. Reopen semantics and terminal states — Proposed, not signed-off

Reference frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7, docs/architecture/04-ticket-state-transition-and-reopen-matrix.md proposes168h reopen,72h auto-close, reason10–1000 and always return InProgress. Live TC-299 only requests matrix/reopen policy and provides no numeric values. These details cannot be presented as approved requirements.

Candidate recommendation:
- Window W=168 hours from the current successful ResolvedAt, pending PM/Backend review. Runtime configuration W must be explicit/reviewed before enablement; missing config503 CONTRACT_CONFIGURATION_UNAVAILABLE fails closed.
- Valid when ResolvedAt <= now <= ResolvedAt+W; after upper bound422 REOPEN_WINDOW_EXPIRED; clock before resolution/data missing422 DATA_REMEDIATION_REQUIRED. UTC duration, not local calendar dates.
- Reason trimmed10–1000 candidate DEC-03; stored as public safe reopen comment and safe event reason in the same transaction. Requester owner or scoped assigned/dispatch/M grant only. Admin follows window.
- Preserve Ticket.Id/TicketNumber/RequesterId/category/priority; previous resolution+time goes to immutable historical event, current Resolution/ResolvedAt=null. Increase ReopenCount exactly once; LastReopenedAt/UpdatedAt=now; no new ticket number.
- Valid existing assignment ->TR-18; missing/inactive assignee but active team/triage ->TR-19. Missing team/classification or unverifiable role grants ->422 remediation/403 grant. No silent Admin impersonation or guessing assignment.
- Reopen from Closed is forbidden regardless W. Subsequent incident needs a new ticket; linking is outside this implementation scope.
- A later new Resolve sets new ResolvedAt and starts a new candidate window; ReopenCount cumulative. Max business reopen count is TBD; no arbitrary cap invented. Integer overflow is rejected without mutation, never wrapped.
- Auto-close is deferred (TC-208 out of scope). If future72h auto-close retained, it shortens effective168h opportunity because Closed is terminal; PM must resolve this contradiction or choose explicit alternate policy. No worker/service actor added now.

## 7. Atomic side effects, audit trail and replay

Proposed transaction:
1. Authenticate, validate syntax, load only within visible scope; authorize action/target and verify current revision.
2. Capture one trusted UTC instant; reject clock earlier than UpdatedAt422 CLOCK_OUT_OF_RANGE rather than fabricating historical time. Evaluate guard/state, all classifier/assignment invariants, comment visibility and configured window. If a required team/assignee membership can change concurrently, protect eligibility checks using a reviewed transaction/locking or version-fencing strategy; merely reading it once and then saving a ticket is insufficient.
3. Aggregate produces state/fields and domain events; optimistic DB CAS guards lost updates. Application logical Revision increases once per accepted aggregate mutation, including public reply/assignment; no increase on denial or replay.
4. Save Ticket + new public/private child entries + immutable audit + optional notification outbox within one DB transaction. Auditing through a post-commit listener alone is insufficient.
5. Commit, return projection/version. Notification delivery only after commit, retry by eventId; no synchronous external email inside transaction. This is architectural guidance for TC-301, not an implemented notification feature.
6. Any guard/DB/child/audit failure rolls back all business state. Security-denial telemetry can exist separately but never masquerades as successful domain event.

Audit record schema proposal: EventId, TicketId, command/correlationId, EventType, ActorId from principal, authorized actor context, FromStatus/ToStatus (same for reply/assignment), old/new logical revision, OccurredAt UTC, allowlisted changed fields, safe reason, visibility. Full tokens/passwords/raw description/internal note content not duplicated in logs. Resolution history stored in authorized structured history to support cycles, with public-safe projection for owner.

xmin is used for database CAS but not exposed as public audit history/revision identity. See [Npgsql concurrency](https://www.npgsql.org/efcore/modeling/concurrency.html) and [EF concurrency conflict handling](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). The domain model source has mapping but no implemented command conflict handler or expected version DTO; those tests are planned.

## 8. Review and tests handoff

Every TR enabled row needs a valid scenario and a negative scenario (actor/scope/current-version/wrongstate/guard) with actual persisted-state and event-count assertions. Exhaustively enumerate the cross product of10states × exposedcommands; no table membership -> deny. Check auth first, stale/current revisions separately, wrong enum binding400 vs wrong transition422. Fixture DB is PostgreSQL for xmin/sequence/atomicity.

See TCON test matrix and command outcomes in gap analysis. Closed/Cancelled terminal, foreign owner, priority calculation, PendingUser reply, both reopen paths, stale race, rollback and public/private history have dedicated scenarios. Proposed commands/tests are **NOT RUN**, because not implemented. Existing create/detail tests are reported separately; build success does not verify the Proposed graph.

Self-review: all requested Submit/Triage/Assign/Start/PendingUser/Resolve/Close/Reopen are covered; Cancel explicitly disabled; all22 rows identify actors/guards/input/mutation/errors/events; no numeric renumber or arbitrary Admin bypass; reopen fallbacks/terminal/window and auto-close contradiction stated. Review decisions, not source implementation, are the remaining business gates.
