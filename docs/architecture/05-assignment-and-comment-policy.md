> **Đề xuất — chưa cấp phép triển khai.**
> Các mô tả “hiện tại/dev” và kết quả kiểm tra bên dưới chỉ thuộc ngày/SHA nguồn trong bản gốc, không mô tả HEAD mới.
> Đối chiếu [hợp đồng hiện hành](current-backend-contract.md), [quyết định còn mở](pending-business-decisions.md) và [quy tắc bảo mật](security-design-rules.md). Chú thích bổ sung ngày 2026-10-19; giữ bản gốc để truy vết.

<!-- original-record -->
# Ticket assignment, public comment và internal note policy

Ngày audit: 2026-10-10. Branch tài liệu: docs/ticket-operations-security.
Baseline D = origin/dev, SHA 6b124e73e9cb95350a505a24a548cb663ab1b7de.
Reference F = origin/frontend, SHA 063489b97c1f706cebeaeea1d3038f0e63a39fd7.
Trạng thái: **Proposed contract / implementation chưa có trên D**. Không phải xác nhận nghiệm thu Jira.

## 1. Evidence và phạm vi

| Nguồn | Branch / SHA | Evidence |
|---|---|---|
| Ticket entity | D | src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs:3–15 chỉ có requester, nội dung, status và timestamps; chưa có assignee/team/comment/audit |
| API | D | src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:20,66 chỉ map create và detail |
| Authorization | D | src/backend/ServiceDesk.Api/Program.cs:50–55 role policies; Infrastructure/Ticketing/TicketService.cs:75–84 owner filter cho Requester, Agent/Admin đọc toàn bộ |
| Concurrency | D | Infrastructure/Persistence/ApplicationDbContext.cs:83–86 shadow xmin; DTO không có revision |
| Frontend | D | src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:24–35 timeline mock,54–76 gọi commands chưa có; components/AgentTicketWorkspace.tsx:76 gửi assigneeId = me |
| Reference policies | F | docs/architecture/05-comment-and-internal-note-privacy-contract.md; 06-ticket-assignment-policy.md tự ghi APPROVED, nhưng các rule chưa tích hợp D |
| Jira trực tiếp | Đọc 2026-10-10 | [TC-294](https://evolving-kg-drift.atlassian.net/browse/TC-294), [TC-303](https://evolving-kg-drift.atlassian.net/browse/TC-303) To Do, Đức Anh; không có comments/sign-off trên các issue này |

[TC-207](https://evolving-kg-drift.atlassian.net/browse/TC-207) yêu cầu assignment/auth/audit, loại skill-based auto routing. [TC-209](https://evolving-kg-drift.atlassian.net/browse/TC-209) yêu cầu public/internal/timeline/leakage tests, loại email/chat realtime. Quy tắc cho export/notification/search/AI là constraint trước khi bật các channel đó; không triển khai các channel trong scope này.

Tài liệu domain/RBAC ở các branch docs/ticket-domain-lifecycle và docs/tc-246-247-rbac cũng là Proposed, chưa được merge. Các dependencies dưới đây phải được chốt đồng bộ, không coi tồn tại tài liệu đồng nghĩa đã có code.

## 2. Actor, ownership và trusted grants

| Ký hiệu | Định nghĩa / điều kiện |
|---|---|
| R | Requester trên requester surface, ticket.RequesterId bằng authenticated subject; UUID hợp lệ từ JWT đã validate |
| A | Agent active, có trusted membership của team đang phụ trách; assignment hiện tại chỉ có hiệu lực nếu membership còn active |
| S | A và là assigned agent hiện tại của ticket |
| Dp | A có permission Ticket.Dispatch cho team đó; “Lead” là capability từ dữ liệu trusted, không tự thêm JWT role Lead |
| M | Admin active, được kiểm tra quyền quản trị hiện thời; override không bỏ state, revision, target eligibility hoặc privacy guards |
| ReadPublic | R đối với ticket của mình; A/Dp trong team; M toàn hệ thống |
| ReadInternal | A/Dp trong team hoặc M, chỉ trên staff surface; không cấp bằng requester ownership |
| WriteStaff | S, Dp hoặc M; ghi note và phản hồi public của staff |
| Invisible | Ticket không tồn tại hoặc ngoài scope của actor → 404, không lộ owner, assignee, version hoặc note count |

Role là quyền chức năng, ownership là quan hệ với resource, team membership/Dispatch là phạm vi, status là eligibility. Client không được gửi ownerId, actorId, team grant, authorRole hay timestamp để backend tin dùng. Dữ liệu team/active/grants hiện chưa có: implementation phải bổ sung và fail closed khi thiếu; không fallback “mọi Agent đều được”.

Người có nhiều role dùng requester route vẫn chỉ nhận public projection. Staff route cần ReadInternal độc lập; token có role Agent nhưng ngoài team không được xem note trên ticket tự tạo. Ví dụ R-A có UUID a; đổi ticketId sang ticket của R-B trả 404 dù biết URL.

## 3. Common command contract

Proposed route prefix giữ /api/tickets như code D; không âm thầm thêm /v1 chỉ vì reference F sử dụng /api/v1. Các policy mới dưới đây là tên đề xuất, chưa có trong PolicyNames.cs.

Mutation nhận expectedRevision: positive int32; backend expose revision logical trên DTO và dùng DB compare-and-swap kết hợp xmin. Không expose PostgreSQL xmin làm business version. Tăng revision và UpdatedAt một lần mỗi thay đổi thực tế. Revision overflow → 409 VERSION_EXHAUSTED, không wrap. Cùng nguyên tắc ở domain draft.

Thứ tự quyết định: authentication → syntax → visible scope → action permission → revision → state → target/content guard → atomic persist. Gate role endpoint có thể trả 403 sớm trước load. Không trả 409/version của ticket ngoài scope.

| Điều kiện | HTTP / code đề xuất | Database và response |
|---|---|---|
| Missing/invalid/expired JWT hoặc subject không hợp lệ | 401 | Không query replay, không ghi |
| Invisible resource | 404 NOT_FOUND | Body generic, không current revision |
| Visible nhưng thiếu function permission | 403 FORBIDDEN | Không thay đổi |
| JSON/UUID/expectedRevision sai, field actor/owner/visibility bị tiêm | 400 INVALID_REQUEST | Strict allowlist input |
| Version stale, losing CAS hoặc occupied assignment | 409 VERSION_CONFLICT / ALREADY_ASSIGNED | Không success event; client refetch, không tự retry overwrite |
| Trạng thái/target không hợp lệ | 422 INVALID_STATE / INVALID_ASSIGNEE | Không tiết lộ thông tin account ngoài scope |
| Membership/config bắt buộc thiếu, resource bị quarantine | 503 POLICY_UNAVAILABLE | Fail closed |
| Mutation thực tế thành công | 200 assignment; 201 comment/note | DTO authorized, revision mới |
| Bounded rate limit | 429 | Chính sách ngưỡng TBD, không dùng limit tùy ý |

Authentication hiện validate issuer/audience/lifetime/signature, role claim URI (Program.cs:36–47); CurrentUser.cs:20–31 đọc NameIdentifier và role. Không triển khai lại auth. Nâng object policies theo actor và resource, không dựa vào ProtectedRoute hoặc ẩn nút.

## 4. Assignment decision table

Trạng thái dùng tên semantic. Backend hiện Draft0, Submitted1, InProgress2, PendingUser3, Resolved4, Closed5, Cancelled6; frontend 0–8 có nghĩa khác. Triaged/Assigned/PendingExternal là **Proposed**, chưa có D. Domain draft đề xuất append Triaged7/Assigned8/PendingExternal9, giữ 0–6; cần approval/migration/enum contract gate trước khi triển khai. Bảng này không renumber enum đang lưu.

| Rule | Actor / action | Preconditions / input | Expected state mutation và ví dụ | Negative / AC |
|---|---|---|---|---|
| ASG-01 | R: mọi assignment | Ticket của R, kể cả R muốn chọn Agent | Deny; requester ownership không phải Dispatch | R gọi self-assign/assign/reassign/unassign → 403, DB/event không đổi |
| ASG-02 | A: self-assign | Triaged, chưa assignee, active team/member, expectedRevision | AssignedAgentId = subject; AssignedAt = UTC; Triaged → Assigned | Target body/role giả → 400; ngoài team → 404; inactive → 403; đúng scope tạo một event |
| ASG-03 | S: self-assign lại | Assigned cùng assignee, fresh revision | 200 no-op; không tăng revision/AssignedAt hoặc phát thêm event | Stale revision → 409; đang assigned người khác → 409; không dùng self-assign để override |
| ASG-04 | Dp/M: assign Agent khác | Triaged, empty assignee; target active Agent, active membership trong team; targetAgentId + revision | Assigned + Agent target, AssignedAt; owner giữ nguyên | Ordinary A → 403; target Requester/inactive/outside team → 422 INVALID_ASSIGNEE; 1 event |
| ASG-05 | Dp/M: reassign | Assigned hoặc InProgress, có old assignee; target khác hợp lệ; reason nonblank + revision | Giữ source status; đổi assignee, AssignedAt; không reset work timestamps | A không Dispatch dù là S → 403; same target → 422 NO_CHANGE; Pending/Resolved/Closed → 422 |
| ASG-06 | S/Dp/M: unassign | Assigned hoặc InProgress, có assignee; reason + revision | Clear Agent/AssignedAt; → Triaged; giữ lịch sử xử lý và team | R/Agent khác không Dispatch → 403; empty assignment → 422 NO_ASSIGNEE; 1 event |
| ASG-07 | Staff: eligibility | Tất cả commands trước khi mutate | Draft/Submitted chưa eligible assign; PendingUser/PendingExternal/Resolved/Closed/Cancelled không được đổi assignment | M không bypass; mỗi state bị cấm → 422 không event; route/triage/reopen là commands riêng |
| ASG-08 | A/Dp/M: target eligibility | Trusted account role/active/lockout và team đều hợp lệ tại commit | Admin self-assign chỉ nếu có eligibility Agent của team; Admin role đơn lẻ không thành assignee | Nhận me/client role flag → 400; lockout/deactivate giữa check và commit không tạo stale assignment |
| ASG-09 | Concurrent A/Dp/M | 2+ commands cùng revision trên 2 instances | Một CAS winner; losers → 409; ticket/audit/assignment đồng nhất | Barrier race 2 Agent: đúng 1 assignee, revision +1, đúng 1 success event |
| ASG-10 | Backend audit writer | Assignment thực tế thành công; trusted actor/time | Persist oldAssignee/newAssignee/actor/time/reason/oldStatus/newStatus/revisions/correlationId | Rollback ticket khi insert audit fail; denied/stale/no-op không thành success audit |
| ASG-11 | Backend lifecycle / owner | Assignment changes | Không đổi RequesterId/department; Start/Resolve không tự chạy | Inject requesterId/Status → 400; sau assign owner và CreatedAt giữ nguyên |
| ASG-12 | Dp/M: team transfer boundary | Routing command được phê duyệt riêng, không dùng reassign để chuyển team | Nếu team đổi phải revalidate/clear assignee; version-fence với assignment | Target ở team khác không chuyển team ngầm; đổi team đồng thời assign → chỉ một valid commit |

Ví dụ: ticket T ở Network, Triaged, revision 8. Agent A nhận bằng subject a → Assigned, revision 9. Agent B gửi revision 8 → 409. Dp muốn chuyển sang B phải gọi reassign với revision 9 và reason, không retry self-assign.

### Transaction và race policy

Đọc ticket trong visible scope; khóa/version-fence cả eligibility rows (team, membership, account active) với ticket mutation. Tất cả writer deactivate/membership/remove/route phải dùng cùng giao thức, theo thứ tự khóa thống nhất để tránh deadlock. Check-then-save trên ticket đơn lẻ không bảo vệ membership race.

Một PostgreSQL transaction gồm CAS ticket, assignment event và outbox nếu cần. Xử lý DbUpdateConcurrencyException thành 409, rollback toàn bộ; không phát notification trước commit. Retry deadlock có giới hạn và chạy lại auth/guards, không overwrite revision người dùng. Multi-instance không dùng in-process mutex làm correctness guarantee. [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## 5. Public comment / internal note decision table

Public nghĩa là visible cho các bên được phép của ticket, không phải anonymous/public Internet. Nội dung plain text, trim, không rỗng; min/max length còn cần chốt. Reference F có 2/4000 chars; lấy làm candidate TC-303, không tự ghi approved. Nếu thiếu config giới hạn thì deny write bằng 503, không silently unlimited. Author subject và UTC server; display name là dữ liệu đã xác minh, không nhận từ client.

| Rule | Actor / action | Precondition | Expected behavior / ví dụ | Negative / AC |
|---|---|---|---|---|
| COM-01 | R: tạo public | Own ticket; Submitted/Triaged/Assigned/InProgress/PendingUser/PendingExternal/Resolved; revision; content | 201 public comment, author R, revision +1, audit | R-B đổi ticketId của R-A → 404; empty/unknown fields → 400; không record |
| COM-02 | S/Dp/M: public reply | ReadPublic + WriteStaff; cùng write states COM-01 | 201 public, không tự đổi status; đúng actor/time | A cùng team chưa assigned/Dispatch → 403 dù đọc được; ngoài team → 404 |
| COM-03 | S/Dp/M: tạo internal note | ReadInternal + WriteStaff; cùng write states | 201 internal record; server cố định visibility Internal | R own gọi notes → 403; ngoài scope → 404; giả isInternal=false không hạ sensitivity |
| COM-04 | R và staff: đọc public | ReadPublic; kể cả Closed/Cancelled | Public-only DTO, không noteId/count/author/time/placeholder | R query includeInternal=true/view=staff → 400 ở public route; direct note → 404 |
| COM-05 | A/Dp/M: đọc internal | ReadInternal trên staff endpoint, scope hiện thời | Staff-only query; A mất membership mất quyền ngay ở request sau | Role Agent đơn lẻ ngoài team → 404; R-only note list → 403, note detail → 404 |
| COM-06 | Mọi actor: visibility immutability | Public endpoint hoặc internal endpoint riêng | DTO allowlist; reject client isInternal/visibility/ticketId/authorId/createdAt/status | Inject isInternal=true vào comments → 400; không downgrade note thành public |
| COM-07 | R: PendingUser reply | Own PendingUser ticket, valid public input/version | Status giữ PendingUser; event RequesterReplied với visibility Public; staff resume bằng command riêng | Không tự chuyển InProgress hoặc làm mất assignee; duplicate retry không nhân đôi |
| COM-08 | Mọi actor: Closed/Cancelled/Draft write | Resource visible, đúng function permission | 422 INVALID_STATE cho create comment/note/attach; reads còn được theo ACL | Admin không bypass; comment racing Close cùng revision chỉ một commit |
| COM-09 | Author/Agent/Admin: edit/delete | MVP chưa có contract lịch sử/redaction/retention được duyệt | Không cung cấp PATCH/DELETE comment/note; author cũng không có quyền ngầm | Authenticated caller dùng API không map → 404; không đổi visibility; sửa sai bằng comment mới |
| COM-10 | Backend queries và serializers | CanReadPublic/Internal xác định trước projection | Filter visibility tại query, trước paginate/count/DTO; public stable order by CreatedAt,Id | Public cursor/count/search snippets không lộ internal; token cursor không lấy từ staff view |
| COM-11 | Backend command writer | Content+actor+revision hợp lệ | Comment/note + ticket revision + event atomic; persistent retry token scoped subject/method/resource/key + payload hash | Replay khác user → không nhận dữ liệu; same key khác payload → 409; DB failure rollback all |
| COM-12 | Các downstream channels | Public audience hoặc private staff scope | Propagate visibility/ticket ACL; requester cache, export, notification, search, AI chỉ lấy public-safe context | Internal sentinel không xuất hiện ở output, diagnostics, logs hay prompt gửi provider public |
| COM-13 | Frontend / QA | Authorized response đã được server filter | Escape plain text; public/internal composers riêng; capability UI + refetch409 | Đổi localStorage/route/body không qua API guard; raw script hiển thị text, không execute |
| COM-14 | Backend logs / telemetry | Bất kỳ success/denial/error | Chỉ eventId, correlation, action/status, actor opaqueId trong audit hạn chế; không content/token/raw URL | Inject password/note sentinel; toàn bộ log/APM/client console không chứa sentinel/Authorization |

Internal read cả ticket phải kiểm tra hai cấp: TicketId thuộc authorized scope và noteId thuộc đúng ticket. UUID khó đoán không phải authorization. [OWASP BOLA](https://api-security.owasp.org/editions/2023/en/0xa1-broken-object-level-authorization/).

### Visibility ở mọi channel

| Channel | Public projection | Staff projection / guard |
|---|---|---|
| Detail, timeline, comments list/detail | Không chứa internal record hoặc metadata; default timeline luôn public | /workspace/timeline và /internal-notes riêng, RequireAgent + ReadInternal |
| Export / notifications (deferred) | DTO riêng, recipient ACL check lúc delivery; không event note cho Requester | Chỉ staff authorized tại delivery, không auto-copy note text vào public message |
| Search / autocomplete (deferred) | ACL + visibility trước search/count/snippet; không index internal vào public collection | Private index/label + scope, permission change invalidate indexes/caches |
| AI suggestions / RAG (deferred) | Context builder public-safe, không mixed note, prompt/output/log đều có sensitivity | MVP không đưa internal notes vào external AI; sau này cần privacy approval, scoped context và restricted results |
| Cache / browser | Key scope subject + view + authz version; Cache-Control private,no-store cho sensitive responses | Logout/role change clear UI state; không dùng cache chung public/staff |
| Audit / logs | Public event projection không reason nhạy cảm của reassign/unassign | Full old/new/reason ở restricted audit, không serialize sang requester timeline |

Assignment events mặc định Internal; requester timeline chỉ có safe summary “assignment updated” nếu được duyệt riêng, không reason/agent-private metadata. Timestamp/cursor không phản ánh count của hidden events. Reassignment không xóa note cũ; quyền team thay đổi theo ticket current scope, cross-team note history visibility cần quyết định OPS-DEC-02 trước khi bật team transfer. Conservative gate: chưa chốt thì chặn team transfer có private history.

## 6. Proposed API inventory

Tất cả endpoints sau **Missing trên D**, trừ GET ticket và POST create hiện có. Không đổi accepted create/detail contract trong docs-only task.

| Endpoint | Actor / policy cần triển khai | Request / result |
|---|---|---|
| POST /api/tickets/{id}/self-assign | RequireAgent + CanSelfAssign | expectedRevision → 200 authorized TicketDto |
| POST /api/tickets/{id}/assign | RequireAgent + CanDispatch | targetAgentId,expectedRevision → 200 |
| POST /api/tickets/{id}/reassign | RequireAgent + CanDispatch | targetAgentId,reason,expectedRevision → 200 |
| POST /api/tickets/{id}/unassign | RequireAgent + CanUnassign | reason,expectedRevision → 200 |
| POST /api/tickets/{id}/comments | RequireRequester + CanCommentPublic | content,expectedRevision; required scoped Idempotency-Key → 201 public DTO |
| GET /api/tickets/{id}/comments | RequireRequester + ReadPublic | Authorized public page; no internal count |
| GET /api/tickets/{id}/comments/{commentId} | RequireRequester + ReadPublic | Cross-parent/invisible → 404 |
| POST /api/tickets/{id}/internal-notes | RequireAgent + CanWriteInternal | content,expectedRevision; Idempotency-Key → 201 internal DTO |
| GET /api/tickets/{id}/internal-notes | RequireAgent + ReadInternal | Staff-only authorized page |
| GET /api/tickets/{id}/internal-notes/{noteId} | RequireAgent + ReadInternal | Requester-only detail denied 404; scope before data |
| GET /api/tickets/{id}/timeline | RequireRequester + ReadPublic | Public-only; query flags cannot add private fields |
| GET /api/tickets/{id}/workspace/timeline | RequireAgent + ReadInternal | Staff timeline, scoped cursor |
| PATCH/DELETE comment/note | Not mapped for MVP | 404 route, no edit/delete permission implied |

For note-detail concealment, endpoint may use authenticated coarse gate plus ReadInternal handler returning 404 for R; list/write coarse RequireAgent returns 403 for R. Explicitly implement this distinction; don't annotate note detail RequireAgent and expect 404 from a handler that won't run.

## 7. Database / implementation handoff

Backend Core (Anh Nguyen, TC-295/296/304/305): add SupportTeams, TeamMemberships(active/version), scoped Dispatch grants and authoritative active/lockout checks. Identity user.Id is string today, RequesterId Guid; standardize FK strategy with migration/backfill validation, don't silently reinterpret IDs.

Ticket additive fields: SupportTeamId nullable until routed, AssignedAgentId nullable, AssignedAt UTC, logical Revision, indexes team/status/assignee; invariants Assigned implies eligible assignee, Triaged unassigned. Quarantine/backfill old InProgress rows lacking routing/assignment; not guess team or claim all legacy data valid.

TicketComments: Id, TicketId FK, AuthorId authoritative Identity FK/type, Visibility constrained Public/Internal, Content with reviewed limit, CreatedAt UTC; immutable visibility/content for MVP; index (TicketId,Visibility,CreatedAt,Id). TicketAuditEvents immutable: eventId/ticketId/type/oldAssignee/newAssignee/actorId/occurredAtUTC/old-new status+revision/reason/correlationId/visibility. Client timestamp/role excluded. Outbox only when consumers introduced. Persisted replay constraints reusable P0 foundation, no IMemoryCache correctness guarantee.

Application: plain domain methods and authorization ports; no ICurrentUser/EF/HTTP inside Domain. Infrastructure: EF transaction/CAS, membership fencing, constrained persistence. API: parse/validate/authorize/ProblemDetails. Frontend (Thu Hoài, TC-297/306): align enums, self-assign subject route, use targetAgentId for others, separate notes endpoint, expectedRevision, public/staff projection, safe errors, no mock timeline as evidence.

Migration: expand nullable columns/tables first; validate/backfill via reviewed job; enforce constraints after legacy audit; update writers/readers coordinated. Rollback app only while compatible, disable writes if reverting; don't drop audit/comments or overwrite enums. Down migration that removes populated tables requires archived recovery and approval, not an automatic release rollback.

## 8. Positive / negative regression matrix (NOT RUN; proposed tests)

All negative writes assert zero row/event/outbox changes; all successful mutations assert subject/time/visibility/version and unchanged ownership. Integration uses real PostgreSQL and two independent API hosts; fake frontend-only tests cannot prove enforcement.

| Test | Rule | Scenario / mandatory assertion |
|---|---|---|
| OPS-A01 | ASG-02,10,11 | Eligible self-assign → 200 one assignment/event/version increment, same owner |
| OPS-A02 | ASG-01 | R own assignment commands → 403; R foreign → 404, no writes |
| OPS-A03 | ASG-04,05 | Dp/M assign/reassign eligible target; old/new/time/reason complete; ordinary A403 |
| OPS-A04 | ASG-08 | Missing/inactive/locked/Requester target/wrong team → 422 generic; me spoof400 |
| OPS-A05 | ASG-06 | S unassign → Triaged/nullAgent; other A403; no assignee422 |
| OPS-A06 | ASG-07 | Each prohibited state for each command incl M → 422; no mutation |
| OPS-A07 | ASG-09 | Same-revision race two Agents across hosts → one200/one409, one event |
| OPS-A08 | ASG-08,12 | Membership deactivate or route races assign; serializable valid outcome, no invalid target |
| OPS-A09 | ASG-10 | Fail audit insert → ticket rollback, no notification |
| OPS-A10 | ASG-03 | Same assignee fresh revision no-op; stale409; other assignee409 |
| OPS-A11 | ASG-01..12 | No/expired/forged JWT401, injected client grants denied; zero replay data |
| OPS-C01 | COM-01,02 | R own and S/Dp/M public create; author trusted; unassigned A403 |
| OPS-C02 | COM-03,05 | Staff note positive; R write/list403; R detail404; foreign staff404 |
| OPS-C03 | COM-04,10,12 | Internal sentinel absent from public detail/list/timeline/count/cursor/export/search |
| OPS-C04 | COM-06 | isInternal/authorId/time/status tampering400; no visibility conversion |
| OPS-C05 | COM-07 | R reply PendingUser keeps status; one event; no automatic Start |
| OPS-C06 | COM-08 | Closed/Cancelled/Draft writes422; terminal read ACL still applies |
| OPS-C07 | COM-09 | Author/M PATCH/DELETE →404; immutable data stays intact |
| OPS-C08 | COM-10 | Mixed-time pages/ties hide all internal data; forged staff cursor rejected |
| OPS-C09 | COM-11 | Same-user/key/payload retry one comment; different payload409; users never share DTO |
| OPS-C10 | COM-08,11 | Comment vs Close/Unassign race one version winner; failed transaction no comment |
| OPS-C11 | COM-12 | Notification/export/search/AI mocked recipient switch excludes private context; no public cache mixing |
| OPS-C12 | COM-13 | HTML/JS content escaped; UI body/role tampering doesn't bypass server |
| OPS-C13 | COM-14 | Capture server/client/APM errors; no token/note/password/raw signed URL |
| OPS-C14 | COM-04,05,12 | Dual-role user requester route public-only; staff scope checked independently |
| OPS-C15 | COM-03,05 | NoteId of T-B under T-A path →404; revoked team membership loses read |
| OPS-C16 | COM-01,06,11 | Empty/invalid length/missing config, clock/client author, DB failure, rate limit deny safely |

QA owner: VÕ NGUYỄN ANH KIỆT, [TC-298](https://evolving-kg-drift.atlassian.net/browse/TC-298), [TC-307](https://evolving-kg-drift.atlassian.net/browse/TC-307). Backend owns unit/transaction tests; QA owns direct HTTP/leakage/race evidence, Frontend owns UX/error tests. Deferred channels must stay disabled until corresponding sentinel tests exist.

## 9. Decision gates / self-review

| Decision | Proposed choice / conflict requiring Đức Anh review |
|---|---|
| OPS-DEC-01 | Mapping team Dispatch into 3-role RBAC; exact team directory/active authority required |
| OPS-DEC-02 | Team transfer private-history access and legacy routing quarantine; deny transfer until approved |
| OPS-DEC-03 | F allows self-assign override/Assigned empty and uncertain Unassign state; this draft uses explicit reassign and Triaged unassign to maintain invariants |
| OPS-DEC-04 | F auto-resumes PendingUser on requester comment; this draft/domain draft keeps state and requires staff resume; choose one before implementation |
| OPS-DEC-05 | /api vs /api/v1, expectedRevision vs revision, logical version/enum expansion compatibility |
| OPS-DEC-06 | Comment min/max/rate limit/retention/redaction; edit/delete deferred, no arbitrary approved values |
| OPS-DEC-07 | Full assignment reason/audit Internal; safe public summary optional; downstream visibility cannot be inferred from UI |

Self-review: every ASG/COM row has actor/action/precondition/result/negative assertion; ownership immutable, denied requests no writes, all concurrency dependencies described, Closed/Cancelled explicit, dual-role projection and all requested leakage channels covered. Source audit confirms create/detail only; proposed tests are not claims of execution. See ticket-security-findings.md for actual build/test results and blockers.
