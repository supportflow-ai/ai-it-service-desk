# TC-277 — Review AI scope và HITL boundary

Ngày rà soát: **2026-10-15**, timezone **Asia/Bangkok**. Assignee: **Đức Anh**. Jira snapshot: **Done**; review hồ sơ mới: **PENDING REVIEW**.

Scope: AI advisory, human decision, fallback, privacy, stale protection và branch parity. Base: [dev@249608e](https://github.com/supportflow-ai/ai-it-service-desk/commit/249608e24b1bf4c64773f18bcb734fd529c1d554). Đây là snapshot/review bổ sung trên nhánh riêng; không thay đổi trạng thái Jira hoặc xác nhận approval thay reviewer.

## 1. Branch parity và lịch sử

PR #1 đã merge vào main, merge SHA e044f470d38cb9d00a4adca9e59de45abbde3ff5; dev baseline 249608e24b1bf4c64773f18bcb734fd529c1d554. Git ancestry: dev có 45 commit riêng, main có 2 commit riêng. Không dùng Git compare diff từ merge-base như toàn bộ runtime parity; review cây source của từng SHA và actual tests riêng.

| Capability | main | dev | Kết luận |
|---|---|---|---|
| AIAnalysis/AISuggestion/ConfidencePolicy/TriagePolicy | Có domain models | Domain/AIAssistance models này không có | Integration gap |
| TriageRequest/TriageResult/projections | Có contract mới | Interface còn TicketClassificationResult cũ | Contract mismatch |
| NoOp | Controlled TriageResult.Unavailable | TicketClassificationResult.Empty | Fallback semantics khác nhau |
| AI tests | 74 Domain + 13 Application filtered tests PASS | Legacy Empty-result test nằm trong 12 Application tests | Không thay dev acceptance bằng main pass |
| TicketStatus | Nine-state0–8 | Persisted0–6, Cancelled6 | Cần explicit compatibility mapping trước integration |

Main contract: [TriageRequest:L9](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Application/AIAssistance/Models/TriageRequest.cs#L9); dev contract: [Dev classifier interface:L13](https://github.com/supportflow-ai/ai-it-service-desk/blob/249608e24b1bf4c64773f18bcb734fd529c1d554/src/backend/ServiceDesk.Application/AIAssistance/Interfaces/ITicketClassificationService.cs#L13).

## 2. AI/HITL boundary review

| Rule | Source / tests | Verified boundary và giới hạn |
|---|---|---|
| AI chỉ advisory Category/Impact/Urgency | [SuggestionType:L7](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/SuggestionType.cs#L7) | Không thấy core Ticket mutation trong models; ForbiddenAIActions strings là documentation, không phải runtime authorization |
| Priority do business rule | [PriorityMatrix:L19](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/Ticketing/PriorityMatrix.cs#L19) | Deterministic matrix hiện có; chưa chứng minh wiring Apply-to-ticket trên dev |
| Human accept/override/reject | [AISuggestion:L82](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/AISuggestion.cs#L82) | EnsurePending + nonblank reviewer; method không xác thực Agent/team/resource, không nhận current revision/hash |
| Terminal decision không đổi | [Terminal guard:L142](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/AISuggestion.cs#L142) | Existing tests PASS; no HTTP authorization proof |
| Fallback | [Controlled fallback outcomes:L100](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Application/AIAssistance/Models/TriageResult.cs#L100); NoOp tests | Models có Unavailable/Timeout/InvalidResponse; actual provider timeout resilience chưa được run |
| Requester privacy | [Requester projection:L19](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Application/AIAssistance/Models/TriageViewProjections.cs#L19) | Excludes Impact/Urgency/raw scores; includes CategoryConfidence/FailureReason. Không gọi đây là category-only; failure text cần allowlist/sanitize tại adapter |
| SEC manual review | [SEC policy:L17](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/TriagePolicy.cs#L17) | Rule/test exists; cần Application gate để không auto-apply SEC |
| Stale protection | [Snapshot revision/hash:L28](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/AIAnalysis.cs#L28); [Explicit expiry:L128](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/AISuggestion.cs#L128) | Có snapshot/hash và expiry method; không có comparison/atomic apply fence trong methods. Stored fields không đủ proof stale protection |

## 3. Findings không được che bởi green tests

TriagePolicy dùng Enum.TryParse không kèm Enum.IsDefined: [Category parser:L33](https://github.com/supportflow-ai/ai-it-service-desk/blob/e044f470d38cb9d00a4adca9e59de45abbde3ff5/src/backend/ServiceDesk.Domain/AIAssistance/TriagePolicy.cs#L33). Source-level risk: numeric strings có thể parse thành enum undefined; tests hiện kiểm unknown names/casing, chưa có numeric undefined scenario. Đây là finding/handoff, chưa có execution reproduction trong run này.

Reviewer ID chỉ nonblank không chứng minh authenticated actor. Việc MarkExpired không tự so current TicketRevision/InputHash. Future apply handler cần trusted principal + ticket resource scope + pending decision + current snapshot/version trong một persistence operation; unauthorized/stale request không được apply values hoặc đổi core workflow.

## 4. Acceptance handoff

Backend AI phụ trách tích hợp reviewed contracts, numeric parsing/fallback/projections; Backend Core chốt enum/version and ticket concurrency; QA kiểm authorized/foreign/revoked actor, stale revision/hash race, terminal decisions, SEC manual-only và degraded provider. Không cherry-pick whole PR #1/main vào dev trong PR review này.

## 5. Decision note

**Advisory/HITL contract READY FOR REVIEW; dev integration BLOCKED.** Các model tests trên main PASS ở phạm vi đã chạy, nhưng integrated privacy/auth/stale enforcement chưa được chứng minh. Không xác nhận false claim “AI/HITL đã sẵn sàng trên dev”.

## Lịch sử và Evidence Manifest

- [PR #1](https://github.com/supportflow-ai/ai-it-service-desk/pull/1): merged, base main, head feature/tc-275-276-277-ai-triage-contract; merge SHA e044f470d38cb9d00a4adca9e59de45abbde3ff5.
- [PR #6](https://github.com/supportflow-ai/ai-it-service-desk/pull/6): merged, base dev, head frontend; merge SHA 164ae02e29c9498d08acbe11587389c470d79245.

Các PR gộp trong lịch sử không được trình bày thành development branches độc lập. Branch audit/tc-277-ai-hitl-review chỉ bổ sung deliverable hiện tại của TC-277.

[Evidence Manifest](evidence-manifest.json) · [Verification snapshot](verification.md) · [Per-test results](verification-results.json).
