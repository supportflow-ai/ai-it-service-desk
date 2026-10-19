# Auth and Ticketing MVP Design

**Date:** 2026-10-20
**Status:** Proposed
**Scope:** `Identity & Auth` and `Ticketing Lifecycle` in `ai-it-service-desk`.

## Goal

Deliver one usable internal-support flow without AI runtime integration:

1. A person self-registers as a `Requester`.
2. The requester signs in and creates, updates, cancels, reads, and comments on tickets.
3. An `Agent` claims an unassigned ticket, resolves it, and comments on it.
4. The requester can reopen or close a resolved ticket.
5. An `Admin` can assign/reassign tickets and promote users to `Agent` or `Admin`.

Knowledge Base, SLA/Analytics, attachments, email verification, internal notes, queues, RAG, embeddings, and `ai-service` integration are out of scope.

## Existing Constraints

- Keep Modular Monolith and Clean Architecture boundaries.
- `Domain` has no ASP.NET Core, EF Core, or infrastructure dependencies.
- `Application` depends only on `Domain` and declares ports/use cases.
- `Infrastructure` implements persistence, Identity, JWT, clock, and configured adapters.
- `Api` maps HTTP to application use cases.
- AI services remain registered as existing `NoOp` implementations.
- Preserve existing untracked user files: `HUONG-DAN.md`, `scripts/`, `supportflow_backend_ed25519`, and `supportflow_backend_ed25519.pub`.

## Roles

| Role | Capabilities |
|---|---|
| `Requester` | Register, log in, read own tickets, create/update/cancel own submitted tickets, add public comments, reopen/close own resolved tickets. |
| `Agent` | Read workspace tickets, filter workspace tickets, claim an unassigned submitted ticket, resolve own assigned ticket, add public comments. |
| `Admin` | All workspace ticket access, assign/reassign tickets, perform allowed ticket transitions, add public comments, change a user's role. |

New registrations always get `Requester`. Startup creates one configurable `Admin` account. Agents are promoted by an Admin; no Agent account is seeded.

## Authentication

### Environment

Add ignored local configuration values only; do not commit secrets:

```text
ADMIN_EMAIL
ADMIN_PASSWORD
```

At startup, seed roles `Requester`, `Agent`, and `Admin`. If `ADMIN_EMAIL` and `ADMIN_PASSWORD` are configured, create that user when absent and ensure role `Admin`. Reject startup configuration with an actionable error when exactly one admin variable is supplied or password violates Identity requirements.

### Endpoints

| Method | Route | Access | Result |
|---|---|---|---|
| `POST` | `/auth/register` | Anonymous | Create a `Requester`; return JWT and current user. |
| `POST` | `/auth/login` | Anonymous | Validate credentials; return JWT and current user. |
| `GET` | `/auth/me` | Authenticated | Return current user and roles. |
| `PUT` | `/admin/users/{id}/role` | Admin | Replace target user's application role with `Requester`, `Agent`, or `Admin`. |

Registration input contains `email` and `password`. Login input contains `email` and `password`. JWT claims include stable user ID, email, and roles. Do not expose Identity errors beyond controlled validation/authentication messages.

## Ticket Aggregate

### Data

`Ticket` is the aggregate root. Persist:

```text
Id
RequesterId
AssigneeId? 
Title
Description
Category
Impact
Urgency
Priority
Status
CreatedAt
UpdatedAt
ResolvedAt?
ClosedAt?
CancelledAt?
```

`Priority` is derived on create/update from `Impact × Urgency` through existing `PriorityMatrix`. Client input never sets `Priority`.

`TicketComment` is child data:

```text
Id
TicketId
AuthorId
Content
CreatedAt
```

Comments are immutable. No edit/delete endpoint.

### Ticket State Machine

```text
Submitted --claim--> InProgress --resolve--> Resolved --close--> Closed
                                           ^                 
                                           |----reopen-------|
Submitted --cancel--> Cancelled
```

Allowed transitions:

| From | To | Actor |
|---|---|---|
| `Submitted` | `InProgress` | Claiming Agent, or Admin assignment/transition. |
| `InProgress` | `Resolved` | Assigned Agent or Admin. |
| `Resolved` | `InProgress` | Requester or Admin. |
| `Resolved` | `Closed` | Requester or Admin. |
| `Submitted` | `Cancelled` | Requester owner or Admin. |

`Cancelled` and `Closed` are terminal. Requester updates are only valid in `Submitted`. A claim requires `Submitted` with no assignee and assigns the current Agent. Reopening does not clear current assignee. Admin assignment targets an `Agent` or `Admin` account and sets status to `InProgress` when assigning a submitted ticket.

Public comments are valid in `Submitted`, `InProgress`, and `Resolved`; reject comments on `Cancelled` and `Closed`.

## Ticket API

| Method | Route | Access | Notes |
|---|---|---|---|
| `POST` | `/tickets` | Requester, Agent, Admin | Creates submitted ticket; actor becomes requester. |
| `GET` | `/tickets/me` | Authenticated | Requester sees own tickets. Agent/Admin see workspace. Filters: `status`, `assigneeId`, `requesterId`, `page`, `pageSize`. |
| `GET` | `/tickets/{id}` | Owner, Agent, Admin | Returns ticket and applicable metadata. |
| `PATCH` | `/tickets/{id}` | Owner requester | Submitted only. Updates title, description, category, impact, urgency. |
| `POST` | `/tickets/{id}/claim` | Agent | Claims an unassigned submitted ticket. |
| `POST` | `/tickets/{id}/assign` | Admin | Body contains target `assigneeId`. |
| `POST` | `/tickets/{id}/resolve` | Assigned Agent, Admin | Transitions `InProgress` to `Resolved`. |
| `POST` | `/tickets/{id}/reopen` | Owner requester, Admin | Transitions `Resolved` to `InProgress`. |
| `POST` | `/tickets/{id}/close` | Owner requester, Admin | Transitions `Resolved` to `Closed`. |
| `POST` | `/tickets/{id}/cancel` | Owner requester, Admin | Transitions `Submitted` to `Cancelled`. |
| `POST` | `/tickets/{id}/comments` | Owner, Agent, Admin | Adds public comment if ticket is non-terminal. |
| `GET` | `/tickets/{id}/comments` | Owner, Agent, Admin | Returns comments in ascending creation time. |

Use controlled RFC 7807 Problem Details responses:

- `400`: malformed input or invalid state transition.
- `401`: missing/invalid authentication.
- `403`: authenticated caller lacks role or ticket access.
- `404`: inaccessible ticket/user appears not found.
- `409`: claim/assignment/state changed concurrently.

Pagination defaults and maximum page size are defined once in application code. Response returns items, `page`, `pageSize`, and `totalCount`.

## Layer Design

### Domain

Add `Ticket` and `TicketComment` to `ServiceDesk.Domain.Ticketing`. Put state transitions, priority recalculation, ownership invariants, assignee invariants, and comment validity inside domain methods.

Do not put HTTP roles or database queries in domain entities. Application authorizes actor role/ownership before calling domain methods; domain still guards invalid state/assignee inputs.

### Application

Create vertical feature folders for `Auth` and `Ticketing` with commands, queries, request/response DTOs, validators, and handlers. Define narrow ports for user management/token issuance and ticket persistence. Handlers enforce role checks, owner checks, query scope, pagination, and transaction boundaries.

Avoid generic repository/service abstractions. One ticket data port and one identity/token port are enough for this scope.

### Infrastructure

Configure EF Core mappings for Tickets and TicketComments, add DbSets, generate migration, and add indexes for:

- requester + created time;
- assignee + status;
- status + created time;
- comments by ticket + created time.

Implement Identity user lookup/role assignment, JWT issuance, role seeding, admin seeding, and application ports. Keep existing PostgreSQL, MinIO, and AI registrations.

### API

Add route groups/controllers following current API style. Bind request DTOs, invoke application handlers, map result/error types to Problem Details, and require authorization policies by role. Swagger documents authenticated routes and Bearer JWT.

## Data Flow

```text
HTTP request
  -> API endpoint validates transport shape
  -> Application handler checks caller access and loads aggregate
  -> Domain method checks state invariant and changes aggregate
  -> Infrastructure persists transaction through EF Core
  -> API returns DTO or Problem Details
```

## Verification

### Domain tests

- Priority recalculates after impact/urgency changes.
- Every allowed transition succeeds.
- Every disallowed and terminal transition fails.
- Claim/assignment invariants hold.
- Comments reject terminal tickets.

### Application tests

- Register creates `Requester`.
- Role change requires Admin and validates target role.
- Requester cannot access another requester's ticket.
- Agent can claim only eligible tickets and resolve only own assigned ticket.
- Admin can assign/reassign and transition permitted tickets.
- Workspace filters and pagination scope results correctly.

### Integration tests

Use existing PostgreSQL Testcontainers fixture:

- Migration applies to clean database.
- Register/login produce accepted JWT.
- `/auth/me` reads JWT principal.
- Full ticket flow: register requester, create, admin promotes agent, agent claims/resolves, requester reopens/closes.
- `Cancelled` and `Closed` reject comments.
- Existing `/api`, `/health`, and Swagger tests remain passing.

## Delivery Order

1. Add domain Ticket/TicketComment and domain tests.
2. Add EF mappings, migrations, and persistence tests.
3. Add auth application/infrastructure/API flow and integration tests.
4. Add ticket command/query handlers and application tests.
5. Add ticket endpoints and full integration workflow tests.
6. Run build, unit tests, integration tests, and Docker Compose smoke after environment image issue is fixed separately.

## Explicit Deferrals

- Knowledge Base
- SLA and Analytics
- AI provider, RAG, embeddings, and `ai-service` client
- Attachments and MinIO upload APIs
- Email verification, password reset, refresh tokens, rate limiting
- Internal notes, audit trail, notifications
- Business-hour SLA calendars
