> **Đề xuất — chưa cấp phép triển khai.**
> Các mô tả “hiện tại/dev” và kết quả kiểm tra bên dưới chỉ thuộc ngày/SHA nguồn trong bản gốc, không mô tả HEAD mới.
> Đối chiếu [hợp đồng hiện hành](current-backend-contract.md), [quyết định còn mở](pending-business-decisions.md) và [quy tắc bảo mật](security-design-rules.md). Chú thích bổ sung ngày 2026-10-19; giữ bản gốc để truy vết.

<!-- original-record -->
# Attachment security contract và kế hoạch triển khai

Ngày audit: 2026-10-10. Branch: docs/ticket-operations-security.
Baseline D = origin/dev, SHA 6b124e73e9cb95350a505a24a548cb663ab1b7de.
Reference F = origin/frontend, SHA 063489b97c1f706cebeaeea1d3038f0e63a39fd7.
**Proposed/TBD; không có attachment vertical slice trên D.** Chưa phê duyệt size cap, MIME set, retention hay signed URL TTL.

## 1. Evidence hiện tại và Jira

| Nguồn | Branch/SHA | Điều đã xác minh |
|---|---|---|
| ADR-006 | D, docs/adr/ADR-006-minio-object-storage.md:9–20 | Accepted MinIO/S3 qua application-owned IObjectStorageService, không quyết định limits/privacy/scanning |
| Port | D, src/backend/ServiceDesk.Application/Common/Interfaces/IObjectStorageService.cs:12–39 | Presigned PUT/GET nhận bucket/key, default 3600s; bucket exists/create; chưa có stream/stat/delete |
| Adapter | D, src/backend/ServiceDesk.Infrastructure/Storage/MinioObjectStorageService.cs:23–61 | SDK signing, bucket check/create; không metadata/ticket auth/content check; method signing không dùng token của port |
| DI | D, src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs:61–72 | Endpoint/credentials/SSL config, singleton adapter; chưa có validate-on-start attachment policy |
| Compose | D, docker-compose.yml:48–52,71–75,119–134 | API dùng endpoint minio:9000, HTTP, root credentials từ env với defaults; MinIO **không publish host ports** trong compose này |
| Gateway | D, deploy/nginx/nginx.conf:10–11 | client_max_body_size 50M là hạ tầng, không phải approved business policy |
| Entity/API | D, Domain/Ticketing/Ticket.cs:3–15; Api/Endpoints/TicketEndpoints.cs:20,66 | Không attachment model, metadata DbSet hoặc attachment routes |
| Tests | D, tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:40–54,74–93 | Thay MinIO bằng stub URL, không chứng minh bucket/persistence/signature/privacy |
| Historical attachment spec | F, docs/architecture/07-attachment-security-limits-and-policy.md | Tự ghi APPROVED: 10 MiB/file, 30 MiB/ticket, 10 files, 900s URL, ingress25M; chưa tích hợp và khác live Jira handoff |

[TC-308](https://evolving-kg-drift.atlassian.net/browse/TC-308) To Do, Đức Anh, chốt size/MIME/upload/download/metadata; không chứa numeric limits. [TC-210](https://evolving-kg-drift.atlassian.net/browse/TC-210) To Do, Anh Nguyen: secure attachment slice; **virus scanning và image preview ngoài scope**. Demo đòi Requester khác tải URL/ID bị chặn. [TC-309](https://evolving-kg-drift.atlassian.net/browse/TC-309) Backend metadata/auth/key; [TC-310](https://evolving-kg-drift.atlassian.net/browse/TC-310) Frontend UI; [TC-311](https://evolving-kg-drift.atlassian.net/browse/TC-311) QA security/integration. F ghi sai consumer roles cho TC-310/311; live Jira là handoff hiện thời.

F là reference policy có nhãn approved, không xóa hay phủ nhận lịch sử đó. Tuy nhiên live task vẫn To Do và không có sign-off/comments; cần review reconciliation trước bật feature, không tự lấy numbers làm production defaults. Constraint mới về “khác Requester không tải được URL” cần lựa chọn delivery khác raw signed URL.

## 2. Decisions cần review (ATT-DEC)

| ID | Quyết định đề xuất | Trạng thái / cơ sở / owner |
|---|---|---|
| ATT-DEC-01 | Explicit config MaxFileBytes, MaxTicketBytes, MaxFiles, MaxConcurrentUploads, UploadTimeout/Retention; không default unlimited | TBD Đức Anh + QA + DevOps capacity/abuse test; F 10/30 MiB/10 chỉ là reference để xác nhận |
| ATT-DEC-02 | MVP candidate allowlist PNG, JPEG và UTF-8 plain text; PDF/Office/WebP/CSV/log yêu cầu loại parser/validation và nhu cầu cụ thể | Proposed Đức Anh; không nói các định dạng này malware-free. F có Office/PDF nhưng signature “512 bytes” không đủ xác thực container |
| ATT-DEC-03 | API authenticated upload stream và API authenticated download stream mặc định; **không trả raw MinIO signed URL cho browser** | Proposed Backend/Security, đáp ứng URL/ID ownership và internal secrecy |
| ATT-DEC-04 | Private bucket provisioned by DevOps, app service account least privilege, TLS ngoài trust boundary, deny anonymous Get/List/Put | Proposed; exact bucketName/environment/IAM policy TBD; không auto-create public policy |
| ATT-DEC-05 | Filename chỉ display metadata; storage key sinh server, immutable UUID, không dùng original filename | Proposed; cap display name/chars cụ thể TBD; path/control chars deny400 |
| ATT-DEC-06 | Scanning chưa có và TC-210 loại khỏi scope; ScanStatus=NotScanned, không ghi Clean giả | Scope confirmed Jira; production enable cần explicit risk acceptance hoặc task scanning riêng. Không tự mở rộng triển khai |
| ATT-DEC-07 | Public attachment gắn public comment/ticket; Internal attachment chỉ gắn internal note, visibility immutable từ parent | Proposed Đức Anh/Backend; chưa chốt thì disable internal-upload surface |
| ATT-DEC-08 | Không edit/delete user-facing MVP; admin retention/redaction workflow riêng TBD; cleanup chỉ pending/orphan đã xác minh | Proposed; không áp dụng “author xóa trong1h” từ F khi chưa thống nhất retention/audit |
| ATT-DEC-09 | Object immutable sau publish; uploader không có credentials/signed PUT cho Ready object; pin version/hash, reconcile corruption | Proposed Backend/DevOps; nếu sau này direct-upload thì promote immutable verified object, không verify rồi để PUT URL overwrite |
| ATT-DEC-10 | Policy thiếu/không nhất quán → disable upload/readiness503, không tạo bucket/URL để cố phục hồi request | Proposed; gate trước release, diagnostic không in secrets |

Không tự gọi 50M NGINX là 50MiB business cap. MaxFileBytes chính xác là byte; MaxTicketBytes/count gồm Ready + Pending reserved để tránh bypass concurrent uploads; phí multipart phải tính riêng ở ingress/Kestrel. PM phải phân biệt “MB” decimal và MiB binary khi chọn giá trị.

### So sánh delivery

| Phương án | Ownership/security | Chọn cho MVP |
|---|---|---|
| API stream sau JWT/object ACL | Mỗi request/download/range kiểm tra ticket+parent+visibility; URL có thể chia sẻ nhưng người khác thiếu quyền bị401/404 | **Proposed default**, load budget và timeout cần capacity test |
| Raw signed GET URL sau ACL | URL là bearer capability; người nhận URL có thể tải trước expiry, không kiểm tra Requester JWT; private bucket không khắc phục | Không đáp ứng strict requester isolation của TC-210 nếu raw URL bị copy; không chọn mặc định |
| Signed POST/PUT quarantine + finalize | Có thể giảm tải upload; cần size bound thực sự, immutable promotion/version, expiry, orphan/job/quota/revalidation | Deferred; port hiện chỉ raw PUT URL không đủ; không nói thêm subject vào URL là bảo mật |
| Authenticated gateway grant | Opaque grant ràng subject/attachment/version/expiry, mỗi redeem xác thực và ACL; server tự stream | Option sau này, không biến grant thành anonymous signed URL |

MinIO SDK6.0.5 signing tạo capability URL, không phải ticket authorization và không phải proof object tồn tại. Signing GET/PUT không nhận CancellationToken trong SDK signature. Kiểm tra canceled token trước/after signing và timeout tại boundary; không hứa có thể hủy mọi SDK internal I/O. [MinIO SDK source6.0.5](https://github.com/minio/minio-dotnet/blob/6.0.5/Minio/ApiEndpoints/ObjectOperations.cs).

## 3. Attachment rule matrix

Actor definitions R/A/S/Dp/M và ReadPublic/ReadInternal/WriteStaff theo 05-assignment-and-comment-policy.md. Tất cả scope phải tra trusted ticket/parent metadata, không dùng client object key, filename hoặc uploaderId làm authorization.

| Rule | Actor / action | Preconditions | Expected behavior / example | Negative / acceptance criterion |
|---|---|---|---|---|
| ATT-01 | R: upload Public | Own ticket, writable nonterminal state; policy enabled; public parent cùng ticket; expectedRevision | Author=subject, Public visibility, server attachmentId/key; stream bounded | Foreign ticket404, internal parent404, tampervisibility400, no object/metadata |
| ATT-02 | S/Dp/M: upload | ReadPublic + WriteStaff; Internal còn cần ReadInternal và parent note | Public/internal endpoint riêng; sensitivity lấy parent; valid version | Same-team A chưa WriteStaff403; outside scope404; R internal-write403 |
| ATT-03 | R/staff: list/read metadata | Ticket ReadPublic; staff internal cần ReadInternal | Filter before page/count; public DTO không key/bucket/noteId/internal size/count | Cross-ticket attachmentId404; Requester internal metadata404; no hidden placeholders |
| ATT-04 | R/staff: download | ATT-03 plus Ready/authorized parent, current scope | Stream API, safe Content-Disposition attachment, nosniff, private,no-store; checked each range/new request | Copy API URL to R-B404/noJWT401; raw bucket access denied; no redirect to signed bearer |
| ATT-05 | Backend: immutable visibility | Parent and target ticket verified server-side | Public upload never accepts client Internal flag; Internal tied note; no API visibility conversion | Move parent/ticketId/mark public after commit rejected; private bytes never public |
| ATT-06 | Backend: size/quota | Approved byte/count config, reservation available | Read actual bytes with bounded streaming even chunked/missing Content-Length; per-file over413 | Lie declared size or omit header cannot bypass; per-ticket reservation exhausted409 QUOTA_EXCEEDED; no Ready object |
| ATT-07 | Backend: MIME allowlist | Exact normalized MIME+extension+detected content/parser agree with approved policy | Known approved type; text UTF-8 parser, binary signature+safe decoder bounds | Header image/png + executable415; wildcard/octet-stream/arbitrary extension not accepted |
| ATT-08 | Backend: signature/content | Bounded validator type-specific and parser safe | Verify content, not just extension or first512 bytes; no preview/execution | Polyglot/truncated files/parser bomb rejected415; signature alone doesn't claim antivirus |
| ATT-09 | Backend: filename/key | Input nonempty valid display name under reviewed limit; generated IDs | Reject separators/path traversal/NUL/CRLF/control; normalize display; key tickets/{ticketId}/{attachmentId}.bin | ../../x or Windows ADS/CRLF400; duplicate display names allowed, keys differ; no overwrite |
| ATT-10 | DevOps/backend: bucket/credentials | Provision private bucket and scoped service account; approved config | Deny anonymous Get/List/Put; storage key/client bucket not accepted; secrets in secret store | Anonymous/object guess blocked in real MinIO; app cannot administer other buckets |
| ATT-11 | Backend: signed URL boundary | Alternative path explicitly reviewed, API default no external capability | No URL/signature in API payload/logs/analytics; URL expiration not permission revocation | Raw GET URL bearer transfer acknowledged if enabled; internal URLs never sent Requester |
| ATT-12 | Backend: metadata/audit | Successfully verified upload and atomic DB publish | Trusted author/time, detected MIME/bytes/hash/version, parent visibility, UploadCompleted event; no content logs | Client uploader/time/checksum trusted false; audit insert fail rolls back publish |
| ATT-13 | Backend: terminal state | Closed/Cancelled/Draft or version changes during transfer | Upload denied422; recheck state+ACL+revision immediately before publish | Upload starts then Close wins CAS → upload publish409, orphan cleanup; M no bypass |
| ATT-14 | Backend/DevOps: scanning | No scanner in current code, TC-210 excludes it | NotScanned metadata, UI honest; production approval gate ATT-DEC-06 | Never show “Clean”; scanner unavailable if later mandatory → quarantine, no download |
| ATT-15 | Backend: failures/timeouts | Storage unavailable/auth misconfig/cancel/DBfailure | Safe503 STORAGE_UNAVAILABLE; release quota only after safe cleanup or retain reservation pending reconciliation | Never return internal endpoint/credential/SDK raw errors; no dangling Ready record or unchecked bytes |
| ATT-16 | Worker/DevOps: orphan cleanup | Pending lease expired; bounded age policy approved; no active writer | Version-fenced worker marks Abandoned, deletes exact orphan key, idempotent retries; audit counts | Don't delete active upload/Ready object; DB unreachable → no blind sweep; never delete by user filename |
| ATT-17 | Backend: concurrency | Pending reservation transaction + ticket CAS + membership fencing | Actual bytes/count update atomic, immutable storage publication, one completion | Two uploads exceeding shared ticket quota: at most reserved capacity succeeds; no sum-check race |
| ATT-18 | Author/M: edit/delete | MVP no contract for redaction/history/retention | No PATCH/DELETE published attachment for any user; corrective upload creates new ID | Owning uploader has no implicit delete; Closed no user deletion; cleanup not blanket business delete |
| ATT-19 | Backend/Frontend/AI: leak prevention | Channel public or staff context authorized | No private file names/sizes/hash/text/URLs in requester suggestions/search/export/notifications/logs | Internal file sentinel absent; browser/report/APM no bearer or upload body |
| ATT-20 | Backend: integrity | Metadata Ready has pinned object version/hash/size | Stat/read verify expected storage object, immutable access; detect missing/corrupt object →503 and quarantine | Alter/delete object out-of-band no arbitrary fallback key or another attachment download |

[OWASP file upload guidance](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) supports layered validation and bounded uploads. File signatures do not prove absence of malware. These architecture choices and thresholds require project approval; OWASP supplies no project-specific byte cap.

No MIME claims for formats without natural signatures: plain text must be decoded/checked, CSV semantics and formula risks if exported need specific policy. DOCX/XLSX are ZIP containers requiring bounded entries/uncompressed size and container validation; single ZIP signature cannot distinguish trusted Office document from arbitrary archive. PDF/scripts/archives stay denied unless corresponding validators and approval exist.

## 4. Proposed upload protocol: DB + MinIO are not a distributed transaction

1. Authenticate; validate route/body; load ticket/parent in authorized scope, function permission, state and expectedRevision. Validate policy availability and reserve a server-approved byte ceiling under row lock/unique upload operation. If Content-Length is missing, reserve MaxFileBytes; if a smaller ceiling is reserved, the stream must never exceed that reservation. Count pending reservations to avoid quotas racing. Mint random attachmentId/lease; store Pending metadata with immutable parent/visibility/subject; no download allowed.
2. API stream bytes to quarantine/temp object via port. Enforce both MaxFileBytes and the reserved byte ceiling incrementally, with bounded buffering, cancellation/time budget and hash. A declared-size lie must either be rejected413 when the reservation is exhausted or obtain an atomic additional reservation before writing the next byte; never grow storage first and adjust quota later. Reject oversized/incompatible input and compensate. Client has no raw MinIO PUT URL and cannot overwrite Ready key.
3. Validate detected MIME/parser/content and actual size; pin immutable version/hash. Recheck current ticket ownership/staff grants, note ACL and revision, lease and lifecycle; all authorization-changing writers use same lock/version protocol.
4. One DB transaction CAS ticket revision, mark metadata Ready (ValidationPassed, ScanStatus NotScanned), adjust quota actual bytes, insert audit. Object storage write happened separately: publication becomes visible only after this commit. If this fails, keep object quarantined and cleanup job compensates; never call it full DB+MinIO atomicity.
5. Failure before/after object write: Abandoned/Failed record and DeletePending compensation; retain reserved capacity until object removal accounted. Idempotent cleanup retries, separate transaction; DB unavailable leaves recorded pending lease for reconciliation. No “rollback DB” claim that deletes object automatically.
6. Completion response201 contains authorized metadata only. Request timeout after commit: retry using required Idempotency-Key scoped subject/method/ticket+parent/key and payload fingerprint → same metadata, never duplicate or foreign DTO. Blob digest/detected MIME part of operation binding; key reuse with changed content409.
7. Download authenticates every request/range, joins ticket + attachment + note parent; check Ready/integrity and ACL before stream. Never accept key/bucket from client. ACL revoked mid-stream cannot recover bytes already delivered; stop stream when feasible via revocation signal, residual risk documented. Every subsequent request denied.

Pending states: PendingUpload → Validating → Ready; failure → Abandoned/DeletePending; integrity failure → Quarantined; retry deletion terminal Deleted for abandoned objects. This state machine is attachment technical processing, does not silently add ticket lifecycle transitions. ScanStatus NotScanned is separate from validated type; only a real scanner may emit Clean in future approved scope.

Bucket provisioning belongs to deployment, not HTTP request path. Existing EnsureBucketExistsAsync check-then-create is unsafe as unconditional multi-instance startup logic: handle only known already-owned result after verify policy, fail on foreign bucket/access denied. Do not catch all MinioExceptions and mark success.

## 5. Proposed APIs và port changes

| API (Missing trên D) | Policy / request | Result / denial |
|---|---|---|
| POST /api/tickets/{id}/attachments | RequireRequester + CanUploadPublic; public ticket/comment parent, expectedRevision, multipart file, scoped Idempotency-Key | 201 metadata;401/404/403/400/409/413/415/422/503 |
| POST /api/tickets/{id}/internal-notes/{noteId}/attachments | RequireAgent + CanWriteInternal; same ticket/note, revision, multipart, key | 201 Internal; Requester403; unauthorized parent404 |
| GET /api/tickets/{id}/attachments | RequireRequester + ReadPublic | Public-only page; staff internal list separate |
| GET /api/tickets/{id}/workspace/attachments | RequireAgent + ReadInternal | Staff filtered page, no object keys |
| GET /api/tickets/{id}/attachments/{attachmentId} | Authenticated + object projection ACL | 200 safe metadata; unknown/invisible/internal-for-R404 |
| GET /api/tickets/{id}/attachments/{attachmentId}/content | Authenticated + CanDownloadAttachment | 200 streamed bytes or validated range206; unauthorized404/no token401, storage failure503 |
| PATCH/DELETE published attachment | Not mapped MVP | Authenticated404; retention workflow separate |

AuthorizedAttachmentDto Proposed allowlist: id, publicCommentId (only when public parent), filename, detectedMime, byteSize, createdAtUTC, authorDisplayName, status and scanStatus. Public lists include Ready public attachments only; pending records are visible only through an upload operation bound to its actor, not in global count/timeline. Never include bucket/key/storage version/hash, replay fingerprint or internalNoteId in a public DTO. Staff list still applies scope before projection; parent internalNoteId only on explicit authorized staff surface.

Content disposition built by safe framework API with sanitized display filename, not string concatenate user input into headers. No inline preview; limit ranges/request count/bytes per policy TBD, invalid range416 only after scope check.

Extend application port (Proposed, ADR-006 retained): WriteQuarantinedObjectAsync(stream,bounded metadata,ct), StatObjectAsync, OpenReadObjectAsync, RemoveObjectAsync; return safe typed storage result/integrity fields, not raw SDK errors. Infrastructure SDK implementation, API stream cancellation and policy enforcement, Application authorization/use case orchestration. Domain remains framework independent.

Do not remove existing presign methods in audit or call them exposed vulnerability: they are currently not mapped to API. Deprecate direct browser use in future consumer code with tests; evaluate SDK cancellation constraints rather than adding unsupported token argument. Configure separate externally reachable endpoint only if approved direct delivery later; rewriting signed hostname after signing invalidates signature.

## 6. Proposed data/migration changes

| Entity / data | Fields / constraints | Owner / purpose |
|---|---|---|
| TicketAttachments | UUID id; ticketId FK; optional publicCommentId or internalNoteId, validated same parent ticket; AuthorId Identity-compatible FK; visibility; sanitized filename; detectedMime; actualBytes; sha256; storage bucket/key/version kept private; createdAtUTC; status/scanStatus/policyVersion | Backend TC-309; visibility derives parent; positive bytes; unique bucket+key/version; constrained status/visibility |
| UploadOperations | operationId, subjectId, ticket/parent, scoped key hash, fingerprint, expectedRevision, reservedBytes/count, leaseUntil, technical status, storage identity | DB unique subject/method/resource/key, CAS lease; no raw key in logs; owns compensation |
| Quota reservation | ticketId totals Ready+Pending, active reservations under row lock; reviewed cap policy version | Prevent check SUM then upload race across instances |
| Audit events | attachmentId/ticketId/parent, actor/time/action/visibility, safe size/MIME, integrity outcome, correlationId | Restricted staff audit; public timeline contains only public metadata |
| Cleanup ledger | lease/version, delete state/retry after/last safe error code | Worker can reconcile exact orphan, no secrets/URLs/content |

When operation pending, same-key retry returns409 UPLOAD_IN_PROGRESS with bounded Retry-After; committed matching replay201 same metadata after current ACL check; changed digest/payload409. Expired key record reuse policy must be defined with P0 idempotency foundation; do not delete committed binding while ticket/result still authoritative merely because memory TTL expires.

Add migrations via expand/backfill/enforce; no fake Clean backfill. Existing binary content (if externally stored without DB records) must be inventoried by DevOps, not adopted by filename. Rollback app only with compatible schema and upload disabled; retain metadata/audit/object manifest. Never use compose down -v or drop attachment table to roll back.

## 7. Negative / positive test matrix (NOT RUN; tests to implement)

All cases assert API payload + raw bytes + DB + audit + object state, not just status. Real MinIO/PostgreSQL, two API instances and direct unauthorized clients required for QA.

| Test | Rules | Scenario / expectation |
|---|---|---|
| OPS-T01 | ATT-01,12 | R-A own Public upload201, actual size/hash/author; repeat key same result |
| OPS-T02 | ATT-01,03,04 | R-B guesses ticket/attachment/download API URL →404; noJWT401; no bytes |
| OPS-T03 | ATT-02,05 | S/Dp/M internal upload positive; R write403/read404; same-team unassigned A write403 |
| OPS-T04 | ATT-03,19 | Internal sentinel in filename/body absent public lists/count/detail/search/export/AI/logs |
| OPS-T05 | ATT-06 | Approved limit-1, exact limit accepted; +1 byte413; chunked/misdeclared length still blocked |
| OPS-T06 | ATT-06,17 | Two simultaneous quota reservations across hosts never exceed byte/count cap; losing request409 |
| OPS-T07 | ATT-07,08 | Executable renamed PNG/MIME forged/extension mismatch/polyglot/truncated →415, no Ready object |
| OPS-T08 | ATT-07,08 | Accepted PNG/JPEG/text fixtures validate; invalid UTF-8/unsupported PDF/Office/archive fail by policy |
| OPS-T09 | ATT-09 | Traversal, ADS, NUL, CRLF/control chars400; duplicate display names independent random keys |
| OPS-T10 | ATT-10 | Real private bucket anonymous GET/LIST/PUT denied; app principal cannot administer unrelated bucket |
| OPS-T11 | ATT-04,11 | API download URL copied to R-B denied; assert no raw signed URL in response/log; explicit alternative test verifies bearer transfer risk |
| OPS-T12 | ATT-13 | Close/reassign/revoke permission while upload pending → CAS/ACL rejection, cleanup, no public publish |
| OPS-T13 | ATT-15,16 | MinIO timeout/write/read/delete failure, credentials error, missing bucket; safe503, preserved compensation/reservation |
| OPS-T14 | ATT-12,15 | DB/audit commit fail after object write → hidden orphan; cleanup idempotent; lost response replay safe |
| OPS-T15 | ATT-16,17 | Lease cleanup racing active completion → one owner; Ready object never deleted |
| OPS-T16 | ATT-14 | Scanner absent labeled NotScanned; no fake Clean; production enable gate enforced |
| OPS-T17 | ATT-18 | Author/M delete/edit calls404; published object immutable |
| OPS-T18 | ATT-20 | Out-of-band replacement/deletion detected; wrong key/hash not served; quarantine safe503 |
| OPS-T19 | ATT-10,11,15 | TLS/internal hostname/signature integration when alternative enabled; expiration/config cancellation explicit |
| OPS-T20 | ATT-04,19 | Header filename injection/HTML files/invalid ranges; no inline execution; sanitized attachment/no-store/nosniff |
| OPS-T21 | ATT-01,02,05 | Parent note/comment belongs another ticket, arbitrary key/bucket/uploader/visibility →400/404; no privilege gain |
| OPS-T22 | ATT-01,13 | Closed/Cancelled/Draft upload422 incl Admin; authorized terminal downloads remain readable |
| OPS-T23 | ATT-06,10 | Missing policy/cap, invalid config/misprovisioned privacy → readiness fail, upload503, no issuance |
| OPS-T24 | ATT-12,17 | Same-user key different content409; different-user same key isolated; canceled transfer not Ready; failed cleanup retains accounting |

## 8. Handoff / release gates / self-review

Order: TC-308 decisions → TC-303 parent privacy + domain/RBAC/revision foundations → TC-309 metadata/stream/quotas/compensation → TC-310 client picker/progress/error/authorized download → TC-311 real storage security tests. Backend Anh Nguyen, Frontend Nguyễn Thị Thu Hoài, QA VÕ NGUYỄN ANH KIỆT, architect Đức Anh. DevOps service-account/TLS/backup/cleanup support proposed, no additional Jira assignee invented.

Feature disabled until caps/MIME/ownership delivery/no-scanner acceptance approved, real MinIO tests pass, PostgreSQL concurrency/migration gate pass, P0 cross-user replay fix verified and integrated, frontend build repaired. Code D has no uploads to protect today; audit identifies implementation blockers and future exposure risks, not a confirmed public bucket leak.

Self-review: all20 rules actor/action/preconditions/results/negative; no arbitrary numeric production limit; private download bearer limitation explicit; DB/object compensation not miscalled distributed atomicity; scan scope reconciled with Jira; cleanup non-destructive to Ready data; tests distinguish real MinIO from factory stub. See ticket-security-findings.md for execution evidence.
