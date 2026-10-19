> **Đề xuất — chưa cấp phép triển khai.**
> Các mô tả “hiện tại/dev” và kết quả kiểm tra bên dưới chỉ thuộc ngày/SHA nguồn trong bản gốc, không mô tả HEAD mới.
> Đối chiếu [hợp đồng hiện hành](current-backend-contract.md), [quyết định còn mở](pending-business-decisions.md) và [quy tắc bảo mật](security-design-rules.md). Chú thích bổ sung ngày 2026-10-19; giữ bản gốc để truy vết.

<!-- original-record -->
# API RBAC permission matrix & security implementation review — TC-247 / TC-196

Ngày: 2026-10-09. Status: **READY FOR REVIEW**; backend/frontend/QA acceptance chưa ký.
Docs branch: `docs/tc-246-247-rbac`; audited source: `origin/dev@6b124e73e9cb95350a505a24a548cb663ab1b7de`.
Actor/use-case, rule examples và Jira evidence: [01-actor-use-cases.md](01-actor-use-cases.md).
Chỉ thêm hai tài liệu; không implement/rewrite authentication, resource handlers hoặc business lifecycle.

## 1. Claim, role và policy mapping hiện có

| Thành phần | Current implementation / source ở audited SHA |
|---|---|
| Role strings | Requester, Agent, Admin; src/backend/ServiceDesk.Domain/Identity/RoleNames.cs:9 |
| Coarse policies | RequireRequester = Requester OR Agent OR Admin; RequireAgent = Agent OR Admin; RequireAdmin = Admin; PolicyNames.cs:5 và src/backend/ServiceDesk.Api/Program.cs:50 |
| JWT issuing | IdentityUser.Id -> NameIdentifier và sub; role claims -> ClaimTypes.Role; jti random; HMAC-SHA256; expiry options. src/backend/ServiceDesk.Infrastructure/Identity/JwtTokenGenerator.cs:25 |
| JWT validation | issuer/audience/lifetime/signing key true; RoleClaimType=ClaimTypes.Role. Program.cs:34. ClockSkew/algorithm allowlist/business identity consistency không được đặt tường minh |
| Current actor | CurrentUser.cs:20 đọc NameIdentifier, IsAuthenticated từ principal; UserId cần parse GUID ở TicketEndpoints.cs:28/72. ICurrentUser không có TeamIds/assignment/capability fields |
| Register | RegisterRequest có Role optional nhưng AuthService.cs:46 luôn gán Requester; profile FullName response không phải permission. Không xác nhận self-register privilege escalation |
| Role freshness | JWT chứa roles lúc mint; AuthService.GetCurrentUserAsync đọc DB roles cho /me. JWT authorization không query current roles/security stamp mỗi request; JwtOptions default expiry60 phút |
| Frontend | AuthContext restore /auth/me, localStorage token; ProtectedRoute dùng roles từ user; AppRoutes explicit allowlists. Sửa UI state không đổi signed JWT và không được đổi API quyền |

Full paths các file identity trên:
`src/backend/ServiceDesk.Infrastructure/Identity/{AuthService,CurrentUser,JwtTokenGenerator}.cs`,
`src/backend/ServiceDesk.Application/Common/Interfaces/ICurrentUser.cs`,
`src/backend/ServiceDesk.Application/Identity/Models/RegisterRequest.cs`,
`src/backend/ServiceDesk.Infrastructure/Options/JwtOptions.cs`.
Frontend full paths nằm ở section UI map.

## 2. Common authorization/HTTP rules

| Rule ID | Rule / ví dụ | Acceptance |
|---|---|---|
| RBAC-G01 | Chỉ trust validated principal, known claims; missing/invalid JWT tới protected function |401; không gọi use case. Known role không thay identity check; subject không hợp lệ tới business ticket API401 |
| RBAC-G02 | Coarse function authorization trước object lookup; Requester gọi internal-note/admin function |403, không response tiết lộ resource. Object không visible ở read/shared function ->404; explicit list/analytics scope ngoài quyền ->403 |
| RBAC-G03 | Visible object nhưng forbidden action ->403; authorized action sai state ->422; stale revision/version ->409; invalid syntax ->400 | Kiểm role/scope **trước** state/revision để không làm oracle cho object ngoài quyền; no writes/events on denial |
| RBAC-G04 | DTO/query projection server-side; parent ticket AND child relationship AND child visibility | ID attachment/note thuộc ticket khác404; Requester public DTO/search/timeline/cache không internal fields/metadata |
| RBAC-G05 | Privilege freshness phải chốt; proposed sensitive role changes invalidate old privilege ở request kế tiếp | Positive new role và negative old token/security-stamp test. Current dev chưa enforce; không claim automatic logout UI đã revoke JWT |
| RBAC-G06 | Admin permission chỉ là explicit grant; union multi-role vẫn phải resource/state/revision | Admin không override lifecycle graph hoặc replay owner; không tin client chọn role/team/owner/status |
| RBAC-G07 | Mutations binding từ allowlist fields; comments public route không cho nâng isInternal; create không cho override owner/roles | Existing register ignores requested Role và existing create ignores unknown owner/status fields; planned mutation DTOs reject forbidden fields400; private write riêng explicit policy; audit caller identity không token |
| RBAC-G08 | Mọi business endpoint mới có explicit authorization + resource handler/query predicate; public endpoints theo allowlist | Direct API bypass UI vẫn bị deny; missing handler fail closed. Default authenticated policy không đủ object/action-level access |
| RBAC-G09 | Query, count, pagination, export và AI retrieval cùng scope/filter | RequesterA không thấy counts/snippets củaB; AgentTeamX không aggregate TeamZ dù đổi query/body |
| RBAC-G10 | Replay không ngoại lệ của owner guard; requester/method/resource/key/payload/atomic DB state | Cross-user key no leak; changed body409; concurrency/two-node test. Dev còn gap SEC-01 |

403 ở coarse role gate áp dụng trước404 cho object. Việc chọn404 cho Agent object ngoài scope là **proposal DEC-04**, cần reconcile legacy frontend doc có Agent403.
Các mã422/409 cho planned commands là target contract, không claim current dev đã trả chúng.
Role/endpoint check không đủ object access; resource handler hoặc scoped query phải enforce riêng, theo
[ASP.NET Core resource authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based?view=aspnetcore-10.0).
Threat categories: [OWASP API1 BOLA](https://api-security.owasp.org/editions/2023/en/0xa1-broken-object-level-authorization/) và
[OWASP API5 function authorization](https://api-security.owasp.org/editions/2023/en/0xa5-broken-function-level-authorization/).

## 3. Actual endpoint inventory trên dev

I=implemented current; P=planned endpoint/policy, **not registered**. Existing business ticket API chỉ create/detail.
Source paths/lines tất cả ở audited SHA trên; không suy từ main.

| Method/path | Current policy | Current behavior / evidence |
|---|---|---|
| POST /api/auth/register | AllowAnonymous | AuthEndpoints.cs:13;201/400, ignores requested role via AuthService.cs:46 |
| POST /api/auth/login | AllowAnonymous | AuthEndpoints.cs:33;200/401 |
| GET /api/auth/me | Default RequireAuthorization() | AuthEndpoints.cs:53; identity self profile200, missing claim401, user not found404 |
| POST /api/tickets | RequireRequester | TicketEndpoints.cs:20/61; all3 roles, GUID from current user;201/400/401; replay global cache security gap |
| GET /api/tickets/{id} | RequireRequester | TicketEndpoints.cs:66/83; Requester query-filter owner, Agent/Admin unrestricted object query at TicketService.cs:79 |
| GET /api/diag/protected | Default authenticated | DiagnosticEndpoints.cs:11;200/401; diagnostic only |
| GET /api/diag/agent-only | RequireAgent | DiagnosticEndpoints.cs:15; Agent/Admin200, Requester403 |
| GET /api/diag/admin-only | RequireAdmin | DiagnosticEndpoints.cs:19; Admin200, Agent/Requester403 |
| GET /api | No policy / no fallback | Program.cs:170; public version/service info; intentional public surface, no ticket data |
| GET /health | No policy / no fallback | Program.cs:148; public dependency health; topology/error descriptions need exposure review for production |

Endpoint files above: `src/backend/ServiceDesk.Api/Endpoints/{AuthEndpoints,TicketEndpoints,DiagnosticEndpoints}.cs`.
Ticket service: `src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs`.
No /api/v1 alias, role-management, ticket list/assignment/transition/comment/internal-note/KB/analytics endpoints registered.
Calls to a P endpoint currently return route404, not evidence a future authorization handler works.

## 4. Permission matrix — target contract

R=Requester; A=Agent; M=Admin. Own=caller is Ticket.RequesterId; Team=ACT-04 server scope; Assigned=valid assigned actor;
Dispatch=trusted team-scoped dispatcher capability (proposed Lead mapping); Fresh=RBAC-G05.
M “yes” is explicit permission, still state/revision/target validation; mutations management require reason/audit.
All rows require401 for missing/invalid JWT except AUTH-01/02;403 for forbidden function,404 for invisible object unless row specifies scope403.
Test ID equals row ID, with positive/negative assertions in section5; UC examples/ACT criteria in document01 apply.

### Auth and ticket core

| ID | Resource/action | R | A | M | Additional conditions | HTTP endpoint / state | Policy (target; current if I) | Denial codes | Required test |
|---|---|---|---|---|---|---|---|---|---|
| AUTH-01 | Identity register | yes | yes | yes | Anonymous entry; server always grants Requester; no requested role elevation | POST /api/auth/register I | AllowAnonymous |400 input; no privilege grant | AUTH-01 P/N |
| AUTH-02 | Identity login | yes | yes | yes | Credentials verified, roles from trusted store | POST /api/auth/login I | AllowAnonymous |401 invalid credentials | AUTH-02 P/N |
| AUTH-03 | Own profile read | yes | yes | yes | Current subject only; valid identity, never URL requesterId | GET /api/auth/me I | Default authenticated; target Identity.ReadSelf |401 identity;404 user missing | AUTH-03 P/N |
| AUTH-04 | User roles manage | no | no | yes | Fresh privilege; role allowlist; target exists; reason/audit; no arbitrary permission injection | PATCH /api/admin/users/{id}/roles P | Identity.ManageRoles + RequireAdmin + Fresh |403 role;404 target;409 revision;400 invalid grant | AUTH-04 P/N |
| TKT-01 | Ticket create own | yes | yes | yes | Owner=subject; baseline create Submitted=1; no client assignee/state/owner; replay ACT-12 | POST /api/tickets I | Current RequireRequester; target Ticket.CreateOwn |400 body;409 payload mismatch target hotfix;503 lock timeout target hotfix | TKT-01 P/N |
| TKT-02 | Own tickets list/count | own | own | own | Query-filter requester at DB; public projection; client requesterId cannot broaden | GET /api/tickets/me P | Ticket.ListOwn |403 explicit foreign filter; no hidden rows | TKT-02 P/N |
| TKT-03 | Staff queue list/count | no | Team | yes | Authoritative team/assignment predicates before paging/count; M organization scope | GET /api/tickets P | Ticket.ListStaff + RequireAgent |403 role/explicit outside team | TKT-03 P/N |
| TKT-04 | Ticket detail | own | Team | yes | ACT-03/04; public or permitted staff projection. Current Agent has no Team predicate | GET /api/tickets/{id} I | Current RequireRequester + owner filter for R; target Ticket.Read(resource) |404 invisible/missing;403 unknown role | TKT-04 P/N |
| TKT-05 | Draft fields update | own | own | own | Draft only; allowlisted title/description/category; owner immutable; revision | PATCH /api/tickets/{id} P | Ticket.EditOwnDraft(resource) |404 foreign;422 notDraft;409 stale | TKT-05 P/N |
| TKT-06 | Draft delete | own | own | own | Draft only; no submitted-ticket delete; own identity | DELETE /api/tickets/{id} P | Ticket.DeleteOwnDraft(resource) |404 foreign;422 notDraft;409 stale | TKT-06 P/N |
| TKT-07 | Draft submit | own | own | own | Draft, valid required fields; ownership/state/revision; separate future flow, not current create rewrite | POST /api/tickets/{id}/submit P | Ticket.SubmitOwnDraft(resource) |404 foreign;422 state;409 stale;400 fields | TKT-07 P/N |


### Ticket operations, collaboration và AI — tất cả P

State names dưới đây là semantic target trong reference TC-260/261/294/299; current dev không có Triaged/Assigned/PendingExternal và chưa có commands.
Không remap integer enum trong task này. Backend chỉ expose command sau approved graph/data mapping; uncertainty fail closed, không fallback current numeric value khác nghĩa.

| ID | Resource/action | R | A | M | Additional conditions | HTTP endpoint | Authorization policy đề xuất | Denial codes | Required test |
|---|---|---|---|---|---|---|---|---|---|
| TKT-08 | Triage | no | Assigned or Dispatch | yes | Visible scope; Submitted; category/impact/urgency valid; revision; audit | POST /api/tickets/{id}/triage | Ticket.Triage(resource) + RequireAgent |403 action;404 scope;422 state;409 stale | TKT-08 P/N |
| TKT-09 | Self-assign | no | Team | eligible Agent identity | Eligible queue state Triaged/Assigned, unassigned; select caller identity, active membership; atomic revision. Replacing existing assignee requires dispatch row10 | POST /api/tickets/{id}/self-assign | Ticket.SelfAssign(resource) + RequireAgent |403 role;404 scope;422 state;409 occupied/stale | TKT-09 P/N |
| TKT-10 | Assign another Agent | no | Dispatch | yes | Current team scope; source Triaged/Assigned/InProgress; target active valid current-team Agent; revision/reason/audit | POST /api/tickets/{id}/assign-agent | Ticket.AssignOther(resource) + RequireAgent |403 noDispatch;404 scope;422 target/state;409 stale | TKT-10 P/N |
| TKT-11 | Route/change support team | no | Dispatch | yes | Source Submitted/Triaged/Assigned/InProgress; target active; dispatcher must have allowed source+target routing grant; reset invalid assignment; audit | POST /api/tickets/{id}/assign-team | Ticket.RouteTeam(resource) + RequireAgent |403 target grant;404 source scope;422 state/target;409 stale | TKT-11 P/N |
| TKT-12 | Unassign | no | Assigned or Dispatch | yes | Assigned/InProgress; assigned Agent only releases own assignment; reason/revision/audit | POST /api/tickets/{id}/unassign | Ticket.Unassign(resource) + RequireAgent |403 unrelated assignee;404 scope;422 state;409 stale | TKT-12 P/N |
| TKT-13 | Start processing | no | Assigned or Dispatch | yes | Assigned; legitimate lead/admin intervention audited; revision | POST /api/tickets/{id}/start | Ticket.Start(resource) + RequireAgent |403 actor;404 scope;422 state;409 stale | TKT-13 P/N |
| TKT-14 | Request information | no | Assigned or Dispatch | yes | InProgress; public question; transitionPendingUser per graph; no internal data in public message | POST /api/tickets/{id}/request-information | Ticket.RequestInformation(resource) + RequireAgent |403 actor;404 scope;422 state;409 stale | TKT-14 P/N |
| TKT-15 | Resolve | no | Assigned or Dispatch | yes | InProgress; resolution required; revision; no silent Closed transition | POST /api/tickets/{id}/resolve | Ticket.Resolve(resource) + RequireAgent |403 actor;404 scope;422 state;409 stale | TKT-15 P/N |
| TKT-16 | Confirm close | own | own | yes | Resolved; owner or explicit Admin; reason/revision/audit; Agent does not close someone else's ticket merely by assignment | POST /api/tickets/{id}/close | Ticket.Close(resource) |403 visible nonowner;404 invisible;422 state;409 stale | TKT-16 P/N |
| TKT-17 | Reopen | own | Assigned or Dispatch | yes | Resolved, approved configurable window, reason+revision; Closed rejected; Admin obeys window/graph unless separate reviewed override exists | POST /api/tickets/{id}/reopen | Ticket.Reopen(resource) |403 actor;404 scope;422 window/state;409 stale | TKT-17 P/N |
| TKT-18 | Public comments read | own | Team | yes | Ticket.Read scope; public filter; read including Closed for history | GET /api/tickets/{id}/comments | Ticket.ReadPublicComments(resource) |404 scope;403 unknown role | TKT-18 P/N |
| TKT-19 | Public comment add / Requester reply | own | Team | yes | NonDraft/nonClosed active states incl Resolved; allowlisted content; internal flag cannot elevate. PendingUser reply is comment, not autonomous transition | POST /api/tickets/{id}/comments | Ticket.AddPublicComment(resource) |404 scope;422 terminal/Draft;400 private flag | TKT-19 P/N |
| TKT-20 | Internal note read | no | Team | yes | Staff scope; closed ticket read allowed; never public timeline/cache/snippets | GET /api/tickets/{id}/internal-notes | Ticket.ReadInternalNotes(resource) + RequireAgent |403 Requester;404 scope | TKT-20 P/N |
| TKT-21 | Internal note add | no | Assigned or Dispatch | yes | Staff scope, nonDraft/nonClosed; author=principal; server marks internal; audit | POST /api/tickets/{id}/internal-notes | Ticket.AddInternalNote(resource) + RequireAgent |403 actor;404 scope;422 state | TKT-21 P/N |
| TKT-22 | Timeline read | own | Team | yes | Scope before query; requester-personal view excludes private events and counts; staff projection only explicit privilege | GET /api/tickets/{id}/timeline | Ticket.ReadTimeline(resource) |404 scope;403 role | TKT-22 P/N |
| TKT-23 | Full audit/history | no | Team | yes | Scope; protected history DTO, redact secrets, privileged read auditable | GET /api/tickets/{id}/history | Ticket.ReadAudit(resource) + RequireAgent |403 Requester;404 scope | TKT-23 P/N |
| TKT-24 | Attachment upload | own | Team | yes | Visible parent, approved mutable state/MIME/size; internal attachment additionally requires TKT-21; server-owned object key | POST /api/tickets/{id}/attachments | Ticket.UploadAttachment(resource) |404 scope;403 private;422 state;413 size;415 MIME | TKT-24 P/N |
| TKT-25 | Attachment download | own public | Team | yes | ParentID-childID relationship; public or permitted internal note; presigned URL only after authorization, bounded expiry | GET /api/tickets/{id}/attachments/{attachmentId} | Ticket.ReadAttachment(resource) |404 foreign/missing/private hidden;403 forbidden role | TKT-25 P/N |
| TKT-26 | Staff AI suggestions read | no | Team | yes | Ticket scope; KB visibility before retrieval; no draft/hidden source leak | GET /api/tickets/{id}/ai-suggestions | Ticket.ReadAISuggestions(resource) + RequireAgent |403 Requester;404 scope | TKT-26 P/N |
| TKT-27 | AI accept/override | no | Assigned or Dispatch | yes | Allowed triage state per approved graph; revision; reason for override; audit human decision; no automatic lifecycle transition | POST /api/tickets/{id}/ai-suggestions/{suggestionId}/decision | Ticket.DecideAISuggestion(resource) + RequireAgent |403 actor;404 parent/child scope;422 state;409 stale | TKT-27 P/N |

Assigned or Dispatch luôn bao gồm valid ACT-04 scope; một claim Agent tự nó không thỏa điều kiện.
Exceptions của mutation-assignment requirement: public comments và public attachment collaboration được cấp Team theo row19/24; workflow/field mutations theo assigned/dispatch rows.
Admin close/reopen/assignment là explicit management grant, không phải impersonation; create/replay vẫn strictly current owner.
Requester-own draft actions áp dụng cho personal action scope: foreign resource404 kể cả user có thêm staff read grant; không administrative delete-draft override trong proposal này.

### Knowledge Base và Analytics — tất cả P, grants cần TC-330/230 review

| ID | Resource/action | R | A | M | Additional conditions | HTTP endpoint | Authorization policy đề xuất | Denial codes | Required test |
|---|---|---|---|---|---|---|---|---|---|
| KB-01 | Article Published read | yes | yes | yes | Published AND ACL-visible; staff draft-edit surface tách riêng; Archived hidden end-user | GET /api/knowledge/articles/{id} | Knowledge.ReadPublished(resource) |404 state/ACL;403 unknown role | KB-01 P/N |
| KB-02 | Article search/RAG | yes | yes | yes | Published+ACL filter trước retrieval/count/snippets; client state/ACL filter không broaden | GET /api/knowledge/articles/search | Knowledge.SearchPublished |403 forbidden explicit scope; no hidden results | KB-02 P/N |
| KB-03 | Article draft create | no | yes | yes | Author=current actor, owned editorial scope; server stateDraft; client statusPublished denied | POST /api/knowledge/articles | Knowledge.CreateDraft + RequireAgent |403 Requester;400 forbidden fields | KB-03 P/N |
| KB-04 | Draft/version edit | no | author or scoped editor | yes | Trusted editorial grant and draft/version; Published edits create controlled draft version, not silent overwrite | PATCH /api/knowledge/articles/{id}/draft | Knowledge.EditDraft(resource) + RequireAgent |403 editor;404 invisible;422 state;409 version | KB-04 P/N |
| KB-05 | Publish approved draft version | no | explicit publisher | yes | Trusted Knowledge.Publish grant, editorial scope, valid content/version; assignment Lead không tự có KB publish | POST /api/knowledge/articles/{id}/publish | Knowledge.Publish(resource) + RequireAgent |403 noGrant;404 scope;422 state;409 version | KB-05 P/N |
| KB-06 | Archive article | no | explicit publisher | yes | Published state; reason/version/audit; disappear from end-user/search/RAG | POST /api/knowledge/articles/{id}/archive | Knowledge.Archive(resource) + RequireAgent |403 noGrant;404 scope;422 state;409 version | KB-06 P/N |
| KB-07 | Editorial draft read | no | author/editor/publisher scope | yes | Trusted editorial scope; current draft version exists; no public endpoint preview bypass | GET /api/knowledge/articles/{id}/draft | Knowledge.ReadDraft(resource) + RequireAgent |403 Requester;404 invisible/noDraft | KB-07 P/N |
| ANA-01 | Personal ticket metrics | own | own | own | Owner predicate before aggregation; public fields; validated UTC date window | GET /api/analytics/me | Analytics.ReadOwn |403 foreign requester filter;400 invalid dates | ANA-01 P/N |
| ANA-02 | Team aggregates | no | Team | yes | Authoritative team scope before aggregate; no raw requester details/internal notes by default | GET /api/analytics/teams/{teamId} | Analytics.ReadTeam(resource) + RequireAgent |403 role/team scope;404 missing team;400 dates | ANA-02 P/N |
| ANA-03 | Organization aggregates | no | no | yes | Managed single-organization scope; approved KPI projection/date window; no automatic raw PII access | GET /api/analytics/overview | Analytics.ReadOrganization + RequireAdmin |403 nonAdmin;400 dates | ANA-03 P/N |
| ANA-04 | Analytics export | no | no | explicit grant | Fresh privilege, trusted Analytics.Export grant, approved columns/scope, reason/audit, bounded export | POST /api/analytics/exports | Analytics.Export + RequireAdmin + Fresh |403 noGrant;400 scope/date/columns | ANA-04 P/N |

Default publish/archive authority thuộc Admin; Agent cần explicit publisher grant, không suy ra từ Agent/Lead alone.
Đây là đề xuất least privilege cho TC-330, không khẳng định Jira đã duyệt granular publisher hoặc server đã lưu grant.
ANA-01 personal dashboard là proposed grant, không khẳng định feature/KPI đã triển khai. Scope denial luôn trước aggregation.


## 5. Positive/negative acceptance test matrix — TC-250 handoff

Fixtures A/B requester, X assigned Agent TeamX, Y same-team unassigned, Z other-team Agent, M Admin; L dispatch-capable Agent TeamX.
P/N dưới đây là **required acceptance**, không executed results. Current test references là partial coverage; actual run status ở section8.
Inherited for every protected row: no token/invalid JWT401, unknown/forbidden role403, no side effects on denial; public AUTH-01/02 exceptions apply.
All allowed HTTP values below là proposed success contract, except I endpoints with current confirmed source contract.

| Test ID | Positive assertion | Negative assertion | Current evidence / missing work |
|---|---|---|---|
| AUTH-01 | Register201; JWT/user roles only Requester | role=Admin/Agent không elevate; duplicate/weak password400 | AuthEndpointTests; AuthNegativeTests:69 checks admin403, cần assert exact claims/roles |
| AUTH-02 | Valid login200; signature/issuer/audience/subject match configuration | Wrong password/nonexistent user401 generic; no credential/token logging | AuthEndpointTests:63/76/87; log/canonical-claim assertions missing |
| AUTH-03 | Valid A me200, only A profile | Missing token401; signed subject missing user404, no B profile | AuthEndpointTests:99/119; invalid-subject/unknown-user cases missing |
| AUTH-04 | Fresh Admin changes allowed role200; audit and revoke previous privilege | Requester/Agent403; unknown grant400; stale revision409; old elevated token denied next sensitive call | Endpoint not implemented |
| TKT-01 | A create201 ownerA; same key/body same ID; B same key owns distinct ID | Existing spoof requester/status fields ignored, owner stillA in DB; changed body409; no-token401;8 concurrent two-node retries count1 | Existing same-key test only same actor; pending hotfix regression suite outside docs branch |
| TKT-02 | A own list200, paging/count/public fields scoped | requesterId=B403; broad filters never return B; no private field | API not implemented; FE calls /tickets/me |
| TKT-03 | X queue200 only TeamX/valid assignment | R403; X explicit TeamZ403; count/total cannot reveal TeamZ | API/scope model not implemented |
| TKT-04 | A own200; X scoped200; M explicit admin200 | B foreign404; Z outside scope404 proposal; unknown ID404; public DTO excludes notes | TicketServiceTests owner/Admin behavior; current Agent broad-access needs negative regression |
| TKT-05 | Own Draft valid revision update200, owner immutable | Foreign404; Submitted422; stale409; owner/status injection400 for planned draft mutation DTO | API not implemented; DTO binding policy needs Backend review |
| TKT-06 | Own Draft delete204 | Foreign404; submitted422; stale409; ticket children not leaked | API not implemented |
| TKT-07 | Own Draft submit200 once with valid input | Foreign404; repeated/nonDraft422 or stale409 by precedence; missing fields400 | API not implemented; approved lifecycle/revision gate needed |
| TKT-08 | Scoped assigned/dispatcher triage200 with audit | R403; Z404; Y without dispatch/assignment403; invalid source422; stale409 | API not implemented |
| TKT-09 | Eligible X self-assign200, assignee=subject; one concurrency winner | R403; Z404; caller asks another assignee400; occupied/stale409; Admin without eligible Agent identity422 | API not implemented |
| TKT-10 | L/M assigns eligible active Agent200 + audit | X without dispatch403; cross-team/inactive target422; stale409 | API not implemented |
| TKT-11 | Authorized routing grant changes team200 and clears invalid old assignment | Unauthorized target-team403; invalid source/target422; stale409 | API not implemented |
| TKT-12 | X releases own assignment200, reason audited | Y releases X without grant403; stale409; invalid state422 | API not implemented |
| TKT-13 | X or trusted scoped intervention starts Assigned200 | Y403; Z404; Closed422; stale409; no mutation after any denial | API not implemented |
| TKT-14 | X request-info200; requester-visible question; PendingUser state per approved graph | Y403; Z404; private content not automatically copied to public question; wrong state422 | API not implemented |
| TKT-15 | X resolve InProgress200 with resolution/audit | Y403; Z404; Closed422; stale409; empty resolution400 | API not implemented |
| TKT-16 | A or authorized Admin closes Resolved200 | Unassigned/assigned nonowner Agent cannot close merely via assignment403 if visible; B404; wrong state422 | API not implemented |
| TKT-17 | A or scoped assigned/dispatcher/Admin reopens Resolved within window200 | Closed/expired window422; foreign B404; stale409; no new Reopened enum implied | API not implemented |
| TKT-18 | A public comments200 only public | Foreign B404; internal IDs/content/count absent; includeInternal cannot broaden | API not implemented |
| TKT-19 | A PendingUser reply201; X allowed team public comment201 | Closed/Draft422; B404; isInternal=true400 at public endpoint, no private-note insert | API not implemented; FE currently includes boolean flag |
| TKT-20 | X internal notes200 for allowed scope | Requester-only token403; Z404; public timeline/detail/search/cache contain no private bytes | API/model/projections not implemented |
| TKT-21 | X/L permitted internal note201; server internal flag + author | Requester-only403; Y without grant403; Z404; Closed422; no public duplication | API not implemented |
| TKT-22 | A public timeline200, X permitted staff timeline200 | B404; hidden note metadata/attachment URLs/aggregate counts absent for A | FE timeline currently mock |
| TKT-23 | X/M scoped audit200, secrets redacted | Requester403; Z404; no audit-success record for failed action | API not implemented |
| TKT-24 | Visible actor valid public upload201 | B404; private upload without TKT-21 denied; Closed422; approved size/MIME413/415 | API not implemented; limits depend TC-308 |
| TKT-25 | A authorized public download200 with bounded URL | Parent/child mismatch404; hidden note attachment404 on generic download; Requester internal function403 | API not implemented |
| TKT-26 | X suggestion200 references only Published visible KB | R403; Z404; exact hidden source search does not disclose snippet/URL/count | NoOp interfaces on dev, no API |
| TKT-27 | X human decision200; revision/reason/audit | R403; Y403; suggestion parent mismatch404; stale409; AI cannot cause autonomous transition | No API; AI contracts on main not integrated into dev |
| KB-01 | All3 allowed actors see ACL-visible Published200 | Draft/Archived/outside ACL404; role alone cannot override read-public lifecycle | Not implemented |
| KB-02 | Allowed search200 filters before retrieval/count | Hidden exact title absent; forbidden explicit ACL scope403; no draft/archived retrieval | Not implemented |
| KB-03 | Agent/M creates Draft201, author=subject | R403; submitted statusPublished400; forged author400 for planned DTO | Not implemented |
| KB-04 | Author/scoped editor/M changes draft/version200 | Agent not editor403 if editorial surface visible, hidden scope404; stale409; no Published overwrite | Not implemented |
| KB-05 | Trusted publisher/M publishes valid draft200 | Default Agent403; stale409; invalid lifecycle/content422 | Not implemented; TC-330 grant decision pending |
| KB-06 | Publisher/M archives Published200, gone from retrieval | Default Agent403; stale409; invalid state422; caches/index cannot keep serving archived content | Not implemented |
| KB-07 | Authorized author/editor/publisher/M reads existing draft200 | R403; unrelated Agent/outside editorial scope404; no draft404; public KB-01 remains404 for unpublished article | Not implemented |
| ANA-01 | A metrics200 reflect only A and approved date window | requesterId=B403; malformed window400; no internal-note/raw B data | Not implemented; grant/KPI review pending |
| ANA-02 | X TeamX aggregate200 | X TeamZ403; R403; no outside-scope count/PII; invalid dates400 | Not implemented |
| ANA-03 | M organization approved aggregates200 | Agent/Requester403; no raw-Person/notes fields unless separately granted | Not implemented |
| ANA-04 | Fresh M with Export grant queues scoped export202 + audit | NonAdmin403; Admin without explicit grant403; disallowed columns/dates400; old revoked token denied | Not implemented |

Binding decision: retain existing register/create behavior (untrusted requested Role/unknown owner/status fields ignored); planned mutation DTOs reject forbidden security fields400. TC-248 reviews this explicit proposal before implementation.
For security-critical public internal flag, contract here chooses **reject400**; author/owner fields never trusted; compatibility exception is limited to the two existing entry points above.
Repeated-submit422 versus409 depends on provided revision: stale revision checked first after authorization; valid revision with wrong state422. QA supplies controlled current/stale versions separately.

### Additional JWT/privilege tests mandatory

| Test | Example / expected | Current coverage |
|---|---|---|
| JWT-01 | Missing/garbage/expired token ->401, with controlled correct key so expiry test does not pass merely because signature is wrong | AuthNegativeTests:23/30/42 exist; real run blocked |
| JWT-02 | Wrong issuer, wrong audience, wrong signing key, unsigned token, unexpected algorithm ->401 | Specific independent cases missing |
| JWT-03 | NameIdentifier/sub conflict or multiple different subject identities ->401 for business contract; canonical subject stable | Generator emits matching values; explicit validation test missing |
| JWT-04 | Known role but malformed/missing business subject ->401; authenticated unknown role ->403 at ticket/staff/admin policy | GUID check present for tickets; JWT/default-policy diagnostics are not substitute business test |
| JWT-05 | Requester->Agent->Admin role/action grid; lower-case/unknown role doesn't gain rights; multiple roles union still scope/state | Diagnostic negative tests exist; positive Agent/Admin and multi-role grid missing |
| JWT-06 | Change/deactivate/delete user then retry old privileged JWT; defined freshness SLA enforced by API, not /me/UI only | Revocation mechanism/test missing; risk SEC-04 |
| JWT-07 | Forge localStorage UI user or call forbidden route/API directly; signed claims/scope decide403; API401 clears FE session, API403 retains session | Frontend automated route/action/E2E cases missing |
| JWT-08 | A/B IDOR via detail/list/child IDs/cache/count/search/export; direct methods and spoof owner/assignee/isInternal fields | SQLite detail-owner check exists; real JWT+DB cross-user regression missing on dev |

No requirement to measure security as “100% coverage” without an instrumented report. Must assert response bytes/IDs/counts and DB side effects, not just status.


## 6. Security gap audit — findings có evidence và reproduction

Common source reference cho SEC-01–07: **branch origin/dev; commit6b124e73e9cb95350a505a24a548cb663ab1b7de**.
Các findings phân biệt bug/source gap đã xác nhận với scenario runtime cần kiểm thử; không dùng Docker fixture failure để đếm product bugs.

### SEC-01 — P0: global idempotency replay bypass ownership

- Source: `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:33`, cache key:36, replay:39, write:49; `src/backend/ServiceDesk.Api/Program.cs:21`.
- Evidence: chỉ key.Trim(), không requester/method/resource/payload; cached DTO trả trực tiếp trước ticket owner lookup. Previous AUD-01 probe endpoint thật với fake service/current-user xác nhận A/B cùng key nhận ticket A; chưa phải JWT/PostgreSQL E2E.
- Repro: trên isolated environment, A POST valid body với keyK; B có identity khác POST cùngK; so returned ID/title/description và DB create count. Thêm A body khác,8 simultaneous retries và two-node routing.
- Expected: B own ticket; A same payload same result; changed body409; concurrent one ticket. Actual baseline: cache hit trả A DTO cho B; lookup/create/set cũng không atomic/multi-instance.
- Impact: ticket private data leakage và duplicates. Owner: Anh Nguyen; Đức Anh security review; Kiệt real DB verification.
- Fix/test: ACT-12 / RBAC-G10 / TKT-01; persistent scope+unique+atomic transaction, strict replay owner. **Hotfix worktree đã có pending implementation/tests nhưng chưa vào dev và integration còn BLOCKED**; không sửa/copy trong task docs này.

### SEC-02 — P0: default privileged seed và JWT secret undermines RBAC

- Source: `src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs:56`; `src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs:70`; `docker-compose.yml:43` và:45/:68.
- Evidence: hosted seeder chạy vô điều kiện, seed privileged test accounts/password literal; Production compose JWT secret có fallback public. Không có guard môi trường/opt-in hoặc fail-fast secret trong các source đã đọc.
- Repro (chưa chạy live): isolated correctly migrated DB + default Production compose config; login seed admin hoặc mint test JWT dùng configured fallback và Admin claim; gọi /api/diag/admin-only. Không ghi actual token/password/secret vào audit doc/log.
- Expected: Production không seed test privileged users; thiếu/known-default secret từ chối startup/token provisioning; forged/unauthorized admin request401/403. Actual source: defaults được cấu hình và seed registered; deployment exploit runtime chưa xác nhận vì Docker.
- Impact: attacker có thể nhận/forge elevated role khi deployment dùng defaults; ownership policies không chống lại forged trusted subject.
- Owner/action: Anh Nguyen + Kiệt gate seed/secret riêng, rotate credentials/key nếu đã dùng; Đức Anh release review. Không rewrite authentication tại đây.
- Tests: JWT-02/05, AUTH-04 và Production seed/fail-fast negative; đây là deployment source-confirmed P0 risk, không claim local exploit thành công.

### SEC-03 — P1: Agent detail authorization quá rộng so với resource contract

- Source: `src/backend/ServiceDesk.Infrastructure/Ticketing/TicketService.cs:75`–84; `src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs:5`; `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:83`.
- Evidence: Agent/Admin bỏ toàn bộ RequesterId predicate; Ticket không có SupportTeamId/AssignedAgentId; không có resource handler ở path này. Requester **có** own filter; không kết luận GET Requester hiện cho đọc chéo.
- Repro (runtime pending): B creates ticket; AgentZ không team/assignment gọi GET ID củaB. Current role check tạo unrestricted query, dự kiến200 nếu ticket tồn tại. Dev chưa có team data để chứng minh model-scoped outcome.
- Expected target: scoped Agent200, outside scope404 proposal DEC-04; current code actual source grants mọi Agent read. Đây là confirmed implementation/contract gap, business scope chưa integrated.
- Impact: staff đọc ngoài scope; owner/department/team bị đồng nhất sai nếu dùng workaround. Owner: Anh Nguyen + Đức Anh contract, Kiệt test.
- Action/tests: server-assigned scope data, query/resource predicate ACT-04; TC-248/294/261 review; TKT-03/04 positive same-team/assigned và negative foreign-team/revocation. Không thay Admin explicit organization grant bằng Requester filter.

### SEC-04 — P1 risk: JWT role freshness khác /me/UI freshness

- Source: `src/backend/ServiceDesk.Api/Program.cs:34`; `src/backend/ServiceDesk.Infrastructure/Identity/JwtTokenGenerator.cs:25`; `src/backend/ServiceDesk.Infrastructure/Identity/AuthService.cs:99`; `src/backend/ServiceDesk.Infrastructure/Options/JwtOptions.cs:13`.
- Evidence: JWT authorization dùng minted role claims; /me trả DB roles mới; không OnTokenValidated/current-user privilege recheck được cấu hình. Default token lifetime60 phút; tolerance chưa đặt tường minh.
- Repro cần controlled DB: mint Agent/Admin JWT, revoke role hoặc delete/deactivate user, gọi me và old JWT vào protected diagnostic/business endpoint. Không có role-management API nên không claim live API revoke feature đã tồn tại.
- Expected proposed: next sensitive management request không dùng revoked privilege (ACT-11/RBAC-G05); exact allowed freshness SLA cần TC-248 sign-off. Actual design: old signed claim có thể còn authorized tới token expiry/tolerance; runtime chưa chạy.
- Impact: UI có thể ẩn menu trong khi token cũ vẫn có quyền; đăng xuất localStorage không server revoke.
- Owner/action: Anh Nguyen + Đức Anh chốt bounded revocation/freshness cho privileged operations; Kiệt JWT-06; Frontend không dùng /me change làm security gate. Không tự triển khai lại auth hoặc mặc định coi JWT stateless là bug nếu chưa duyệt SLA.

### SEC-05 — P1 integration blocker: granular actions/projections chưa có API enforcement

- Source: `src/backend/ServiceDesk.Application/Ticketing/Interfaces/ITicketService.cs:6`; `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs:18`; `src/backend/ServiceDesk.Domain/Identity/PolicyNames.cs:5`;
  `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:8`,:24,:49,:74.
- Evidence: service chỉ create/detail; endpoint scan không list/assign/transition/comment/note/KB/analytics. FE gọi các planned URLs, timeline trả mock data; addComment có isInternal do client gửi.
- Repro: authenticated /tickets/me, POST /tickets/{id}/assign/resolve/comments trên current dev -> route404; UI timeline có synthetic event. Future private write exploit **chưa có endpoint** để xác nhận.
- Expected: mỗi planned action có function+resource+state policy, child/projection checks; visible UI không giả implementation. Actual: missing routes/policies; client flag không là authorization.
- Impact: blocked end-to-end acceptance; future mass assignment/internal note leak risk nếu bind bool và chỉ dùng RequireRequester.
- Owner/action: Anh Nguyen implement reviewed per-action handlers khi task tương ứng; Thu Hoài separate public/internal DTO/actions, remove mock-as-evidence; Kiệt TKT-02–27/KB/ANA tests. Không coi mọi planned missing feature là exploit đang xảy ra.

### SEC-06 — P1 validation blocker: auth/resource integration chưa được chứng minh

- Source: `tests/ServiceDesk.IntegrationTests/{AuthEndpointTests,AuthNegativeTests,TicketEndpointTests,ServiceDeskWebApplicationFactory}.cs`;
  `src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/20260918_AddTicketTable.cs`; `src/backend/ServiceDesk.Infrastructure/Identity/IdentityDataSeeder.cs:34`.
- Evidence:24 selected Auth/Ticket tests fail ở DockerUnavailableException trước assertions. Tree migration baseline chỉ AddTicketTable, không Identity DDL/model snapshot; source schema gap đã xác nhận, fresh-DB startup outcome chưa test.
- Repro: section8 integration command; exact failure npipe docker_engine unavailable. Khi Docker có lại, repeat clean DB migrate + register/login/me/roles/ticket requests, không skip seed/migration để tuyên bố full Auth pass.
- Expected: all required integration cases execute and pass on PostgreSQL17. Actual:0 pass/24 fixture failures; unit constants/SQLite owner checks không chứng minh JWT+DB BOLA.
- Impact: thiếu release evidence; pending scope/claims/role grid cases JWT-02–08 chưa có. Owner: Kiệt + Anh Nguyen.
- Action: Docker-enabled CI, reviewed Identity schema prerequisite riêng, add substantive security cases; không suppress fixture failures hoặc waive integration gate.

### SEC-07 — P1 confirmed frontend build blocker; P2 UI/API drift và future privacy risk

- Source: `src/frontend/service-desk-web/src/routes/AppRoutes.tsx:101`,:107,:112;
  `src/frontend/service-desk-web/src/routes/components/ProtectedRoute.tsx:27`;
  `src/frontend/service-desk-web/src/features/auth/context/AuthContext.tsx:19`;
  `src/frontend/service-desk-web/src/features/tickets/api/ticketApi.ts:74`;
  backend `Program.cs:54`.
- Repro: Agent-only/Admin-only identity navigate /tickets/new hoặc requester detail route -> UI/403, trong khi same roles POST /api/tickets được RequireRequester cho phép. Admin-only không vào Agent workspace route; Admin panel là DemoPage. Direct API bypass UI vẫn theo API policies.
- Fresh frontend verification: npm ci exit0; npm run build exit2,3 TypeScript errors tại src/features/tickets/components/AgentTicketWorkspace.tsx:10 (unused Option),:225 (unsupported Button.success) và TicketListPage.tsx:2 (unused Space). Full repo prefix src/frontend/service-desk-web/; same audited dev SHA. Node24.13.1/npm11.8.0; chưa repeat Node22 CI. Đây là confirmed build blocker, không claim AuthContext hoặc ProtectedRoute có riêng compile error. Không sửa code trong docs task.
- Expected target: capability/persona-consistent entry points, route/action DTO projection mapping,401 session reset và403 session giữ nguyên. Actual: exclusive role routes chưa khớp explicit own-create/read grants; UI boolean internal flag và mock không chứng minh backend privacy.
- Impact: inconsistent UX/acceptance, không phải auth bypass chỉ từ hidden button; stored token XSS exposure là risk, chưa phát hiện XSS exploit ở scope audit.
- Owner/action: Thu Hoài TC-249 fix3 TypeScript build errors in separate implementation task, route/action map + API errors/E2E; Backend enforce private policy; Kiệt JWT-07/TKT-19–25. Không đổi token storage/auth flow ngoài scope.

### SEC-08 — P1 contract/governance drift: Lead, prefix và lifecycle chưa reconcile

- Dev branch/SHA chung: RoleNames.cs:9, Program.cs:54, TicketEndpoints.cs:18, TicketStatus.cs:3, TicketService.cs:52.
- Reference branch: **frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7**;
  `docs/architecture/01-rbac-and-api-permission-matrix.md:19`,
  `docs/architecture/03-ticket-lifecycle-and-visibility-baseline.md:14`,
  `docs/architecture/08-sprint-acceptance-and-governance-record.md`.
- Evidence: reference says Lead, /api/v1, Draft/9-state/string-serialization and APPROVED labels; dev has3 roles, /api, Submitted create,7 numeric states. ADR-005 itself defines Identity/JWT, không quyết định fourth Lead role. Jira TC-196 Done, TC-246/247/248/249/250 To Do.
- Repro: compare immutable branch files + fresh Jira links ở01; no runtime needed. Expected: one reviewed contract + implemented mappings/migration/test links. Actual: divergent artifacts/status claims; no reviewer evidence obtained.
- Impact: mơ hồ scope/states/API; không lấy integer2 ở một branch làm cùng lifecycle nghĩa ở branch khác. Đây là contract discrepancy, không tự kết luận lifecycle hiện có là P0 auth bug.
- Owner/action: Đức Anh + Anh Nguyen + Thu Hoài reconcile DEC-01–06; Kiệt acceptance at exact candidate SHA. Không tự edit old9 docs, thêm fourth role, remap states hoặc update Jira Done.


## 7. Backend/Frontend blockers và implementation handoff

| Priority | Work cần làm | Owner đề xuất / Jira | Gate / thứ tự |
|---|---|---|---|
| P0 | Hoàn tất real-DB regression và review idempotency hotfix riêng; đừng đóng SEC-01 vì docs đã viết | Anh Nguyen + Kiệt; Đức Anh review | Cross-user/key/payload/8-concurrent/two-node/TTL/rollback pass tại candidate SHA |
| P0 | Production seed/secret gate và credential/key remediation nếu đã triển khai defaults | Anh Nguyen + Kiệt | Source fail-fast + Production negative tests; deployment evidence |
| P1 | Chốt DEC-01–06, TC-196/246/247 acceptance evidence; scope/Lead/denial/lifecycle không tự đổi từ labels | Đức Anh + TC-248 Backend + TC-249 FE | Review hai docs trước handler/migration integration |
| P1 | Scope data và per-operation authorization/projections | Anh Nguyen, TC-248/261/294/299/303 | Resource/child/query filtering, identity/owner binding, permission trước state/revision, no hidden bytes |
| P1 | Docker/Identity migration prerequisites + full JWT/DB authorization assertions | Kiệt + Backend, TC-250 | Execute real test matrix, publish reproducible TRX/checks; no fixture skips |
| P1 | Route/menu/action/projection alignment, remove mock-as-acceptance, split public/internal commands | Thu Hoài, TC-249; Backend endpoint owners | React cannot trust user-editable role/team fields; test401 logout,403 keep session, role union and direct API bypass |
| P1 before KB/analytics exposure | Approve KB grants/publication/ACL and own/team/org analytics projection | Đức Anh TC-330/230 + Backend/FE/QA | Scope trước search/RAG/count/export; not based on Admin/Agent UI button |
| P2 | Freeze JWT clock tolerance/algorithm allowlist and privileged freshness SLA, frontend token/XSS posture | Backend/FE + Đức Anh | Specific negative cases; no broad auth rewrite under this docs task |

Mapping plan, chưa implement:
1. API coarse gates/current role constants giữ mapping minh bạch; register/login anonymous allowlist.
2. Application use case resolve caller/resource scope theo trusted provider; resource authorization trước projection/mutation.
3. Infrastructure query scoped DB, assignment/team/grant lookup; no new permission claim issued chỉ vì doc policy có tên.
4. Domain state/revision invariants enforce sau authorization; API map401/403/404/422/409 thống nhất.
5. FE consume approved capability/action map để UX; backend re-check cho mọi HTTP call, export và async use case.
6. QA run test fixtures và assert actual DTO/side effects; candidate code/migration artifact phải cùng SHA.

## 8. Validation tại docs worktree

.NET SDK10.0.300; source HEAD vẫn6b124e73e9cb95350a505a24a548cb663ab1b7de.
Đây là **fresh run trên dev baseline**, không lấy45-unit/41-integration counts từ hotfix branch.

| Command | Result | Evidence / limitation |
|---|---|---|
| dotnet build src/backend/ServiceDesk.sln -c Release | PASS / exit0 |0 warnings,0 errors |
| dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category!=Integration" | PASS / exit0 |21 pass: Domain4 + Application11 + Architecture6;0 skipped |
| dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category=Integration&(FullyQualifiedName~Auth\|FullyQualifiedName~TicketEndpointTests)" | FAIL / exit1, environment BLOCKED |24 fixture failures =15 Auth +9 Ticket tests;0 pass. DockerUnavailableException, cannot connect npipe://./pipe/docker_engine |
| npm ci --no-audit --no-fund (frontend directory) | PASS / exit0 | Locked dependencies installed; no tracked manifest/lockfile changes |
| npm run build (frontend directory) | FAIL / exit2 |3 existing TS errors: AgentTicketWorkspace.tsx:10/:225, TicketListPage.tsx:2; Node24.13.1/npm11.8.0; needs repeat Node22 CI |
| Frontend Auth/ProtectedRoute automated tests | NOT RUN | package.json has dev/build/lint/preview, no test runner/script; E2E/route tests need TC-249/250 |
| New target permission/JWT/FE runtime cases | NOT RUN / mostly not implemented | This task supplies acceptance matrix; no new test/business source added. Current endpoints/schema missing, Docker blocked |
| Live role revoke/user deactivate/Production exploit | NOT RUN | Requires controlled DB/config; source-based risk không là deployed exploit evidence |

Trong command table, escaped `\|` chỉ để render Markdown table; lệnh thực tế/repeat bên dưới dùng plain OR pipe trong quoted filter.

```powershell
dotnet build src/backend/ServiceDesk.sln -c Release
dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category!=Integration"
dotnet test src/backend/ServiceDesk.sln -c Release --filter "Category=Integration&(FullyQualifiedName~Auth|FullyQualifiedName~TicketEndpointTests)"
git diff --check
git status --short
git ls-files --others --exclude-standard
```

Local evidence trong ignored .cache của docs worktree:
rbac-build.log, rbac-unit.log, rbac-security-integration.log, rbac-frontend-install.log, rbac-frontend-build.log, test-results/rbac-*.trx.
Test run added logger/results-directory options; Release/filter semantics như commands trên.
RoleNamesTests2 chỉ constants; TicketServiceTests có4 detail/visibility cases dùng SQLite, không JWT/persistent DB.
Không claim21 pass là full Auth/Authorization acceptance; no measured coverage%.
Actual API current register privileged-role request bị ignore, không tự nâng role; negative test có sẵn chưa pass runtime do fixture.

## 9. Preservation, changed files và self-review

Preflight root clean frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7.
Git fetch origin --prune thành công; docs branch tạo từ origin/dev, pull --ff-only origin dev thành công, fresh checkout clean.
Không switch dev tại root: giữ prior user worktree protection và local dev đã checkout ở worktree khác.
Equivalent synchronization dùng checkout riêng từ latest origin/dev; không --force/stash/reset/cherry-pick/history rewrite.

Changed files chỉ:
- docs/architecture/01-actor-use-cases.md — actor definitions, ACT/UC IDs/examples/criteria, Jira/Lead/contract decisions.
- docs/architecture/02-rbac-permission-matrix.md —42 permission rows +42 positive/negative pairs, common/JWT tests,8 findings, validation/handoff.

Self-review:
- Mỗi permission ID có actor grant/condition/endpoint/policy/denial/test; each ACT/UC có example và acceptance.
- Owner bound to validated identity; Team/Assignment không lấy từ department hoặc UI; không implicit Admin/Lead privilege.
- I/P inventory explicit; planned endpoints không được mô tả là đã return403/422 trên current backend.
- Public/internal projection, parent-child binding, scope-before-aggregation và replay guards included.
- Register RequestedRole ignored được ghi là source-protected; no false confirmed escalation finding.
- Frontend build3 errors reported with paths/runtime and no code fixes; runtime untested risks, missing-feature blockers và confirmed source bugs/gaps được phân biệt; mỗi finding có severity/source/commit/branch/repro/expected/actual/test/owner.
- Legacy frontend labels/prefix/enum/Lead đối chiếu, không silently supersede hoặc tự coi approvals/Jira acceptance tồn tại.
- Không thay Code/DB/JWT/UI/business lifecycle, không update Jira, không push/merge; docs chưa commit để reviewer nhận bản reviewable.
- Worktree frontend, audit và hotfix được đối chiếu toàn bộ branch/HEAD/status/index/diff/untracked và nonignored-file SHA-256 đầu/cuối; exact final result được ghi sau self-review.

Docs nội dung đủ bàn giao review TC-246/247. **Implementation/security acceptance và release vẫn BLOCKED** bởi openP0, scope/schema gaps và Docker integration gate.


### Final preservation result

| Protected worktree | Branch / HEAD | Files checked | Comparison |
|---|---|---|---|
| ai-it-service-desk | frontend@063489b97c1f706cebeaeea1d3038f0e63a39fd7 |154 | Branch/HEAD/status/index/diff/untracked/all SHA-256 unchanged;9 original docs already in prior commit |
| ai-it-service-desk-audit | audit/repository-audit-20261009@6b124e73e9cb95350a505a24a548cb663ab1b7de |148 | Same checks unchanged;3 reports remain untracked/uncommitted |
| ai-it-service-desk-p0-idempotency | fix/p0-idempotency-isolation@6b124e73e9cb95350a505a24a548cb663ab1b7de |157 | Same checks unchanged; all pending hotfix changes preserved |

No source changes in docs worktree. Only the two requested untracked Markdown files; build outputs/logs/dependencies are ignored and confined to this new worktree.
Cross-reference self-review final:32 use cases,13 actor rules,10 common authorization rules,42 permissions and42 paired positive/negative tests,8 findings; all permission IDs resolve.
Main AI-contract reference for TKT-27: origin/main@e044f470d38cb9d00a4adca9e59de45abbde3ff5, src/backend/ServiceDesk.Domain/AIAssistance and tests/ServiceDesk.Domain.Tests/AIAssistance; absent from dev domain tree, no AI HTTP endpoint registered on dev.
