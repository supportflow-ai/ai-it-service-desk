# ADR-014: Feature use cases and published module contracts

Status: accepted, 2026-10-19. Baseline: 9dd0746450dca4348d6d0fa8a09cfc2a48860020.

Keep the six backend projects and three modules: Identity, Ticketing, AIAssistance.
Application features expose six use-case interfaces: RegisterUser, LoginUser,
GetCurrentUser, CreateTicket, GetMyTickets and GetTicketDetail. Each implementation
is internal, executes one operation and is registered explicitly without a mediator.

Application owns actor validation and access scopes. Requester detail is owner-only;
Agent/Admin detail remains system-wide. This release does not implement team isolation,
assignment or the extended ticket lifecycle. Creation remains Submitted; self-registration
grants Requester. FullName remains response-only until a separate profile increment.
Title input remains limited to 100 characters while the existing column remains 200.

Published use-case contracts and DTOs are distinct from adapter ports. Cross-module
dependencies require an explicit published-type allowlist. Shared code cannot become
a route to module entities; architectural exceptions identify individual technical types.
One DbContext remains, with module-owned mappings and specialized gateways.

Identity serializes password checks and failed-count/reset writes per account using
a PostgreSQL row lock and transaction. Technical write failures cannot silently drop
security state or issue a token. Ticket creation retains its existing atomic PostgreSQL
idempotency implementation and exact replay semantics.

AI wire DTOs/JSON mapping belong to Infrastructure. Application results enforce adapter
postconditions. Domain owns suggestion invariants. Input hash v2 uses length-prefixed
UTF-8 fields; the wire hash is opaque and echoed, not recomputed by a provider.

Baseline verification: build succeeds, 202 offline tests pass; the baseline CI also
passed 54 real PostgreSQL tests and three image builds. Final verification must repeat
those checks against the final implementation. No schema change is intended.
