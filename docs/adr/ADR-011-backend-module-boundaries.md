# ADR-011: Application-owned use cases and module boundaries

Status: accepted for the backend refactor.

Keep Domain, Application, Infrastructure, API and Worker in one backend repository.
Identity, Ticketing and AIAssistance are modules within these layers. Domain/Application
cannot depend on EF Core, PostgreSQL, ASP.NET Core or Infrastructure. Application owns
use-case interfaces, authorization and coordination; adapters own technical operations.

`TicketService` uses `ITicketQueries` and `ITicketWriter`, never `DbContext` or `IQueryable`.
The writer owns the complete PostgreSQL transaction, advisory lock, receipt and ticket.
Moving only part of this transaction into the application would weaken atomicity.
Receipt hashes, scope, ten-minute lifetime, five-second lock timeout and exact replay
remain compatible. Database time governs receipt expiry; `IClock` governs domain time.

`AuthService` orchestrates application-owned account/token ports. Account creation and
Requester role assignment are atomic in the Identity adapter. Client-supplied role is
ignored. HTTP claims are adapted only by API; Worker has an anonymous actor and cannot
implicitly execute user-authorized use cases.

Cross-module access is through published contracts. AI can use Ticketing category,
impact and urgency value types, but cannot reference the Ticket aggregate, Identity
or another module's persistence. Shared DbContext is retained; configurations are split
by module without renaming schema. Architecture tests enforce these boundaries.

No generic repository, MediatR, message broker or per-module project is introduced.
The next lifecycle/state-machine and Human-in-the-Loop features remain backend-owned.

## Explicit initialization

API/Worker do not mutate schema or seed accounts on startup. A separate database tool
owns migration, role seed, explicit demo seed and new-account-only admin bootstrap.
Production JWT configuration is validated before accepting traffic. Account lockout is
persisted by Identity (five failures, fifteen minutes); endpoints preserve generic 401.

## Deferred work

AI integration, full ticket transitions/SLA, profile persistence, receipt cleanup,
audit trail and telemetry are separate increments. They must preserve these boundaries.
