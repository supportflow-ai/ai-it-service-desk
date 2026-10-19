# Auth and Ticketing MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver self-registration, JWT authentication, role management, and end-to-end Ticket lifecycle for Requester, Agent, and Admin users.

**Architecture:** Add domain-first `Ticket` and `TicketComment` aggregates, then expose them through narrow Application ports and vertical Auth/Ticketing handlers. Infrastructure owns EF Core mappings, ASP.NET Identity, JWT and startup seeding; minimal API route groups only bind HTTP, invoke handlers, and map controlled errors to Problem Details.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, ASP.NET Core Identity, EF Core 10, Npgsql/PostgreSQL, JWT Bearer, xUnit, FluentAssertions, Testcontainers PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-10-20-auth-ticketing-mvp-design.md`

## Global Constraints

- Keep `Domain` free of NuGet, ASP.NET Core, EF Core, HTTP, and infrastructure dependencies.
- `Application` only depends on `Domain`; do not add MediatR, FluentValidation, generic repositories, or new packages.
- Keep AI services as existing `NoOp` implementations. Do not call `ai-service`.
- Do not change `HUONG-DAN.md`, `scripts/`, `supportflow_backend_ed25519`, or `supportflow_backend_ed25519.pub`.
- New accounts always receive `Requester`; roles are exactly `Requester`, `Agent`, `Admin`.
- `Priority` derives from `Impact × Urgency`; never accept priority from client input.
- Ticket terminal states are `Cancelled` and `Closed`; public comments reject terminal tickets.
- Do not implement Knowledge Base, SLA/Analytics, attachments, verification email, refresh tokens, internal notes, notifications, RAG, or queue work.
- `.env` remains ignored; never log values from `ADMIN_EMAIL`, `ADMIN_PASSWORD`, JWT secret, or database credentials.

## Review Focus

- Concurrent claims: second Agent receives `409 Conflict` and ticket remains assigned only once.
- Role mutation: caller cannot use role endpoint unless JWT contains `Admin`; invalid target role receives `400 Bad Request`.
- Tenant visibility: Requester receives `404 Not Found` for another Requester's ticket/comments, not ticket data or a `403` existence leak.
- Startup configuration: only one of `ADMIN_EMAIL`/`ADMIN_PASSWORD` causes explicit startup failure; both absent does not block local tests.
- Terminal comments: `Cancelled` and `Closed` tickets reject comments with `400 Bad Request`.

---

## File Structure

| Path | Responsibility |
|---|---|
| `src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs` | Ticket aggregate, state transitions, priority recomputation, assignee and comment invariants. |
| `src/backend/ServiceDesk.Domain/Ticketing/TicketComment.cs` | Immutable public ticket comment. |
| `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs` | MVP ticket status enum including `Cancelled`. |
| `src/backend/ServiceDesk.Application/Common/Interfaces/ITicketStore.cs` | Narrow persistence operations for ticket aggregate and listing. |
| `src/backend/ServiceDesk.Application/Common/Interfaces/IIdentityService.cs` | Identity creation, login lookup, roles, and role assignment port. |
| `src/backend/ServiceDesk.Application/Common/Interfaces/ITokenService.cs` | JWT issuance port. |
| `src/backend/ServiceDesk.Application/Auth/*` | Auth request/result types and handlers. |
| `src/backend/ServiceDesk.Application/Ticketing/*` | Ticket commands, queries, DTOs, access checks, pagination constants and handlers. |
| `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs` | DbSets plus Identity database context. |
| `src/backend/ServiceDesk.Infrastructure/Persistence/Configurations/*` | EF mappings/indexes for Ticket and TicketComment. |
| `src/backend/ServiceDesk.Infrastructure/Persistence/EfTicketStore.cs` | EF implementation of ticket store. |
| `src/backend/ServiceDesk.Infrastructure/Identity/IdentityService.cs` | User/role implementation of application identity port. |
| `src/backend/ServiceDesk.Infrastructure/Identity/JwtTokenService.cs` | Signed JWT implementation. |
| `src/backend/ServiceDesk.Infrastructure/Identity/IdentitySeeder.cs` | Roles and optional env Admin startup seed. |
| `src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs` | Auth route group. |
| `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs` | Ticket route group and HTTP result mapping. |
| `src/backend/ServiceDesk.Api/Endpoints/ApiErrors.cs` | Shared controlled validation/not-found/conflict Problem Details mapping. |
| `src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/*` | EF migration generated after mappings are final. |
| `tests/ServiceDesk.Domain.Tests/Ticketing/*` | Aggregate and state-machine tests. |
| `tests/ServiceDesk.Application.Tests/Auth/*` | Auth and role authorization handler tests with in-memory fakes. |
| `tests/ServiceDesk.Application.Tests/Ticketing/*` | Ticket access, commands, filters, pagination handler tests with in-memory fakes. |
| `tests/ServiceDesk.IntegrationTests/AuthEndpointsTests.cs` | JWT register/login/me/role endpoint tests. |
| `tests/ServiceDesk.IntegrationTests/TicketEndpointsTests.cs` | PostgreSQL-backed API workflow, access, conflict, and terminal-comment tests. |

### Task 1: Define Ticket domain model and state machine

**Files:**
- Create: `src/backend/ServiceDesk.Domain/Ticketing/Ticket.cs`
- Create: `src/backend/ServiceDesk.Domain/Ticketing/TicketComment.cs`
- Modify: `src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs:1-20`
- Test: `tests/ServiceDesk.Domain.Tests/Ticketing/TicketTests.cs`
- Test: `tests/ServiceDesk.Domain.Tests/Ticketing/TicketCommentTests.cs`

**Interfaces:**
- Produces: `Ticket.Create`, `Ticket.Update`, `Ticket.Claim`, `Ticket.Assign`, `Ticket.Resolve`, `Ticket.Reopen`, `Ticket.Close`, `Ticket.Cancel`, `Ticket.AddComment`.
- Produces: `TicketComment.Create`.
- Consumed later by `ITicketStore`, EF mappings, ticket handlers, and endpoint DTO mapping.

- [ ] **Step 1: Write failing creation/priority tests**

```csharp
[Fact]
public void Create_ValidInput_CalculatesPriorityAndStartsSubmitted()
{
    var createdAt = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    var ticket = Ticket.Create("requester-1", "VPN unavailable", "Cannot connect", "Network", Impact.High, Urgency.Medium, createdAt);

    ticket.Status.Should().Be(TicketStatus.Submitted);
    ticket.Priority.Should().Be(Priority.P2);
    ticket.RequesterId.Should().Be("requester-1");
    ticket.AssigneeId.Should().BeNull();
}

[Fact]
public void Update_Submitted_RecalculatesPriority()
{
    var ticket = CreateSubmittedTicket();
    ticket.Update("New title", "New description", "Hardware", Impact.Low, Urgency.Low, DateTimeOffset.UtcNow);
    ticket.Priority.Should().Be(Priority.P4);
}
```

- [ ] **Step 2: Run domain creation tests and verify failure**

Run: `dotnet test tests/ServiceDesk.Domain.Tests/ServiceDesk.Domain.Tests.csproj --filter FullyQualifiedName~TicketTests`

Expected: FAIL because `Ticket` does not exist.

- [ ] **Step 3: Write failing workflow and terminal tests**

```csharp
[Fact]
public void Claim_SubmittedWithoutAssignee_AssignsAgentAndStartsWork()
{
    var ticket = CreateSubmittedTicket();
    ticket.Claim("agent-1", DateTimeOffset.UtcNow);
    ticket.Status.Should().Be(TicketStatus.InProgress);
    ticket.AssigneeId.Should().Be("agent-1");
}

[Fact]
public void Resolve_Reopen_Close_UsesAllowedWorkflow()
{
    var ticket = CreateClaimedTicket();
    ticket.Resolve(DateTimeOffset.UtcNow);
    ticket.Reopen(DateTimeOffset.UtcNow);
    ticket.Resolve(DateTimeOffset.UtcNow);
    ticket.Close(DateTimeOffset.UtcNow);
    ticket.Status.Should().Be(TicketStatus.Closed);
}

[Theory]
[InlineData(TicketStatus.Closed)]
[InlineData(TicketStatus.Cancelled)]
public void AddComment_TerminalTicket_Throws(TicketStatus terminalStatus)
{
    var ticket = terminalStatus == TicketStatus.Closed ? CreateClosedTicket() : CreateCancelledTicket();
    var action = () => ticket.AddComment("requester-1", "More detail", DateTimeOffset.UtcNow);
    action.Should().Throw<InvalidOperationException>();
}
```

- [ ] **Step 4: Run workflow tests and verify failure**

Run: `dotnet test tests/ServiceDesk.Domain.Tests/ServiceDesk.Domain.Tests.csproj --filter FullyQualifiedName~TicketTests`

Expected: FAIL because workflow methods and `Cancelled` state do not exist.

- [ ] **Step 5: Implement minimal aggregate and status enum**

```csharp
public enum TicketStatus
{
    Submitted,
    InProgress,
    Resolved,
    Closed,
    Cancelled
}

public void Claim(string agentId, DateTimeOffset now)
{
    EnsureStatus(TicketStatus.Submitted);
    if (!string.IsNullOrWhiteSpace(AssigneeId)) throw new InvalidOperationException("Ticket is already assigned.");
    AssigneeId = RequireValue(agentId, nameof(agentId));
    Status = TicketStatus.InProgress;
    UpdatedAt = now;
}

public void AddComment(string authorId, string content, DateTimeOffset now)
{
    if (Status is TicketStatus.Cancelled or TicketStatus.Closed) throw new InvalidOperationException("Terminal tickets cannot receive comments.");
    _comments.Add(TicketComment.Create(Id, authorId, content, now));
}
```

Implement `Create`, `Update`, `Assign`, `Resolve`, `Reopen`, `Close`, and `Cancel` with exact transitions from spec. Keep comment collection private and expose `IReadOnlyList<TicketComment>`. Validate required strings and timestamps. Do not add role checks in Domain.

- [ ] **Step 6: Run domain ticket tests and verify pass**

Run: `dotnet test tests/ServiceDesk.Domain.Tests/ServiceDesk.Domain.Tests.csproj --filter FullyQualifiedName~Ticketing`

Expected: PASS.

- [ ] **Step 7: Commit domain model**

```bash
git add src/backend/ServiceDesk.Domain/Ticketing tests/ServiceDesk.Domain.Tests/Ticketing
git commit -m "feat: add ticket domain workflow"
```

### Task 2: Persist tickets with EF Core migration

**Files:**
- Modify: `src/backend/ServiceDesk.Infrastructure/Persistence/ApplicationDbContext.cs:13-30`
- Create: `src/backend/ServiceDesk.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`
- Create: `src/backend/ServiceDesk.Infrastructure/Persistence/Configurations/TicketCommentConfiguration.cs`
- Create: `src/backend/ServiceDesk.Infrastructure/Persistence/Migrations/<timestamp>_AddTicketing.cs`
- Create: `src/backend/ServiceDesk.Infrastructure/Persistence/EfTicketStore.cs`
- Create: `src/backend/ServiceDesk.Application/Common/Interfaces/ITicketStore.cs`
- Modify: `src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs:23-76`
- Test: `tests/ServiceDesk.IntegrationTests/TicketPersistenceTests.cs`

**Interfaces:**
- Consumes: `Ticket`, `TicketComment` from Task 1.
- Produces: `ITicketStore` methods `AddAsync`, `GetByIdAsync`, `GetCommentsAsync`, `ListAsync`, and `SaveChangesAsync`.
- Produces: PostgreSQL tables `Tickets` and `TicketComments` with optimistic concurrency `Version` token.

- [ ] **Step 1: Write failing PostgreSQL persistence tests**

```csharp
[Fact]
public async Task SaveTicket_RoundTripsCommentsAndDerivedPriority()
{
    await using var scope = _factory.Services.CreateAsyncScope();
    var store = scope.ServiceProvider.GetRequiredService<ITicketStore>();
    var ticket = Ticket.Create("requester-1", "Laptop", "Won't start", "Hardware", Impact.High, Urgency.High, DateTimeOffset.UtcNow);
    ticket.AddComment("requester-1", "Started this morning", DateTimeOffset.UtcNow);

    await store.AddAsync(ticket, CancellationToken.None);
    await store.SaveChangesAsync(CancellationToken.None);

    var saved = await store.GetByIdAsync(ticket.Id, CancellationToken.None);
    saved!.Priority.Should().Be(Priority.P1);
    (await store.GetCommentsAsync(ticket.Id, CancellationToken.None)).Should().ContainSingle();
}
```

- [ ] **Step 2: Run persistence test and verify failure**

Run: `dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj --filter FullyQualifiedName~TicketPersistenceTests`

Expected: FAIL because `ITicketStore` and `DbSet<Ticket>` do not exist.

- [ ] **Step 3: Define application ticket persistence port**

```csharp
public interface ITicketStore
{
    Task AddAsync(Ticket ticket, CancellationToken cancellationToken);
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TicketComment>> GetCommentsAsync(Guid ticketId, CancellationToken cancellationToken);
    Task<PagedTickets> ListAsync(TicketListFilter filter, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record TicketListFilter(
    string? RequesterId,
    string? AssigneeId,
    TicketStatus? Status,
    int Page,
    int PageSize);

public sealed record PagedTickets(IReadOnlyList<Ticket> Items, int TotalCount);
```

Place `TicketListFilter` and `PagedTickets` beside `ITicketStore`. Page validation stays in application handlers, not EF store.

- [ ] **Step 4: Add DbSets and fluent mappings**

```csharp
public DbSet<Ticket> Tickets => Set<Ticket>();
public DbSet<TicketComment> TicketComments => Set<TicketComment>();

builder.HasIndex(ticket => new { ticket.RequesterId, ticket.CreatedAt });
builder.HasIndex(ticket => new { ticket.AssigneeId, ticket.Status });
builder.HasIndex(ticket => new { ticket.Status, ticket.CreatedAt });
```

Map required string lengths: title 200, category 100, description 8000, comment content 4000, user IDs 450. Store enum values as strings. Configure `Version` as concurrency token. Configure `Ticket` comments as one-to-many, cascade delete only at database level because tickets are never hard-deleted by use cases.

- [ ] **Step 5: Implement `EfTicketStore` and register it**

```csharp
public async Task<PagedTickets> ListAsync(TicketListFilter filter, CancellationToken cancellationToken)
{
    var query = _db.Tickets.AsNoTracking().AsQueryable();
    if (filter.RequesterId is not null) query = query.Where(ticket => ticket.RequesterId == filter.RequesterId);
    if (filter.AssigneeId is not null) query = query.Where(ticket => ticket.AssigneeId == filter.AssigneeId);
    if (filter.Status.HasValue) query = query.Where(ticket => ticket.Status == filter.Status.Value);
    var totalCount = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(ticket => ticket.CreatedAt)
        .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(cancellationToken);
    return new PagedTickets(items, totalCount);
}
```

Register `ITicketStore` scoped. `GetByIdAsync` loads comments only when a caller needs aggregate comment mutation; query comments separately for GET comments endpoint.

- [ ] **Step 6: Generate migration and apply through integration fixture**

Run: `dotnet ef migrations add AddTicketing --project src/backend/ServiceDesk.Infrastructure/ServiceDesk.Infrastructure.csproj --startup-project src/backend/ServiceDesk.Api/ServiceDesk.Api.csproj --output-dir Persistence/Migrations`

Then modify `ServiceDeskWebApplicationFactory.InitializeAsync` to create scope and call `Database.MigrateAsync()` after container starts.

- [ ] **Step 7: Run persistence and existing integration tests**

Run: `dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj --filter "FullyQualifiedName~TicketPersistenceTests|FullyQualifiedName~HealthCheckTests|FullyQualifiedName~DatabaseTests"`

Expected: PASS.

- [ ] **Step 8: Commit ticket persistence**

```bash
git add src/backend/ServiceDesk.Application/Common/Interfaces/ITicketStore.cs src/backend/ServiceDesk.Infrastructure/Persistence src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs tests/ServiceDesk.IntegrationTests
git commit -m "feat: persist tickets and comments"
```

### Task 3: Add application identity, token, and auth handlers

**Files:**
- Create: `src/backend/ServiceDesk.Application/Common/Interfaces/IIdentityService.cs`
- Create: `src/backend/ServiceDesk.Application/Common/Interfaces/ITokenService.cs`
- Create: `src/backend/ServiceDesk.Application/Auth/AuthModels.cs`
- Create: `src/backend/ServiceDesk.Application/Auth/AuthHandlers.cs`
- Modify: `src/backend/ServiceDesk.Application/DependencyInjection.cs:9-18`
- Create: `src/backend/ServiceDesk.Infrastructure/Identity/IdentityService.cs`
- Create: `src/backend/ServiceDesk.Infrastructure/Identity/JwtTokenService.cs`
- Create: `src/backend/ServiceDesk.Infrastructure/Identity/IdentitySeeder.cs`
- Modify: `src/backend/ServiceDesk.Infrastructure/DependencyInjection.cs:40-76`
- Modify: `src/backend/ServiceDesk.Api/Program.cs:21-45`
- Test: `tests/ServiceDesk.Application.Tests/Auth/AuthHandlersTests.cs`

**Interfaces:**
- Produces: `RegisterAsync(RegisterRequest)`, `LoginAsync(LoginRequest)`, `GetCurrentUserAsync(string)`, `ChangeRoleAsync(string actorId, string targetId, string role)`.
- Produces: `IdentityUserRecord(Id, Email, Roles)` and `AuthResult(Token, User)`.
- Consumed later by auth endpoints, ticket authorization, and integration tests.

- [ ] **Step 1: Write failing auth handler tests with fakes**

```csharp
[Fact]
public async Task Register_AssignsRequesterAndReturnsToken()
{
    var identity = new FakeIdentityService();
    var handler = new AuthHandlers(identity, new FakeTokenService("token"), new FakeCurrentUser());

    var result = await handler.RegisterAsync(new RegisterRequest("user@example.com", "ValidPass1"), CancellationToken.None);

    result.Token.Should().Be("token");
    result.User.Roles.Should().ContainSingle("Requester");
}

[Fact]
public async Task ChangeRole_NonAdminActor_ThrowsForbidden()
{
    var handler = CreateHandler(currentUserId: "requester-1", roles: ["Requester"]);
    var action = () => handler.ChangeRoleAsync("target-1", "Agent", CancellationToken.None);
    await action.Should().ThrowAsync<ForbiddenException>();
}

[Fact]
public async Task ChangeRole_UnknownRole_ThrowsValidation()
{
    var handler = CreateHandler(currentUserId: "admin-1", roles: ["Admin"]);
    var action = () => handler.ChangeRoleAsync("target-1", "Owner", CancellationToken.None);
    await action.Should().ThrowAsync<ValidationException>();
}
```

- [ ] **Step 2: Run auth handler tests and verify failure**

Run: `dotnet test tests/ServiceDesk.Application.Tests/ServiceDesk.Application.Tests.csproj --filter FullyQualifiedName~AuthHandlersTests`

Expected: FAIL because auth handlers and ports do not exist.

- [ ] **Step 3: Define ports, request/response records, and controlled application exceptions**

```csharp
public interface IIdentityService
{
    Task<IdentityUserRecord> RegisterRequesterAsync(string email, string password, CancellationToken cancellationToken);
    Task<IdentityUserRecord?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    Task<IdentityUserRecord?> GetUserAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken);
    Task SetRoleAsync(string userId, string role, CancellationToken cancellationToken);
}

public interface ITokenService
{
    string Create(IdentityUserRecord user);
}

public sealed record IdentityUserRecord(string Id, string Email, IReadOnlyList<string> Roles);
public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResult(string Token, AuthUser User);
public sealed record AuthUser(string Id, string Email, IReadOnlyList<string> Roles);
```

Create `ValidationException`, `ForbiddenException`, `NotFoundException`, and `ConflictException` in `Application/Common/Exceptions`. Extend `ICurrentUser` with `IReadOnlyList<string> Roles` and `bool IsInRole(string role)`; update infrastructure `CurrentUser` to read `ClaimTypes.Role`.

- [ ] **Step 4: Implement application handlers and test fakes**

`RegisterAsync` calls `RegisterRequesterAsync`, creates token, returns controlled user data. `LoginAsync` returns `ForbiddenException` for invalid credentials. `GetMeAsync` requires authenticated current user and returns a fresh identity record. `ChangeRoleAsync` requires Admin role, accepts only `Requester`, `Agent`, `Admin`, loads target, then calls `SetRoleAsync`.

Register `AuthHandlers` scoped in `AddApplication`.

- [ ] **Step 5: Implement infrastructure Identity/JWT/seeding**

```csharp
public string Create(IdentityUserRecord user)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id),
        new(ClaimTypes.Email, user.Email)
    };
    claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
    // create signed JWT with JwtOptions issuer, audience, secret, expiry
}
```

Use `UserManager<IdentityUser>` and `RoleManager<IdentityRole>` in `IdentityService`. Collapse Identity validation errors to a controlled validation error; never return raw password or Identity error details. `IdentitySeeder.SeedAsync(IServiceProvider, IConfiguration, CancellationToken)` ensures all three roles, then validates admin env pair and creates/promotes the configured account.

In `Program.cs`, call `await IdentitySeeder.SeedAsync(app.Services, app.Configuration, app.Lifetime.ApplicationStopping);` before `app.Run()`.

- [ ] **Step 6: Run auth application tests and build**

Run: `dotnet test tests/ServiceDesk.Application.Tests/ServiceDesk.Application.Tests.csproj --filter FullyQualifiedName~AuthHandlersTests && dotnet build src/backend/ServiceDesk.sln --no-restore`

Expected: PASS.

- [ ] **Step 7: Commit auth application and infrastructure**

```bash
git add src/backend/ServiceDesk.Application src/backend/ServiceDesk.Infrastructure src/backend/ServiceDesk.Api/Program.cs tests/ServiceDesk.Application.Tests/Auth
git commit -m "feat: add auth services and role seeding"
```

### Task 4: Expose authenticated Auth HTTP endpoints

**Files:**
- Create: `src/backend/ServiceDesk.Api/Endpoints/AuthEndpoints.cs`
- Create: `src/backend/ServiceDesk.Api/Endpoints/ApiErrors.cs`
- Modify: `src/backend/ServiceDesk.Api/Program.cs:88-144`
- Modify: `tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:25-58`
- Create: `tests/ServiceDesk.IntegrationTests/AuthEndpointsTests.cs`

**Interfaces:**
- Consumes: `AuthHandlers` and models from Task 3.
- Produces: `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `PUT /admin/users/{id}/role`.
- Produces: exact HTTP mappings: validation `400`, unauthenticated `401`, forbidden `403`, inaccessible resource `404`, conflict `409`.

- [ ] **Step 1: Write failing endpoint tests**

```csharp
[Fact]
public async Task Register_Login_AndMe_ReturnJwtPrincipal()
{
    var registered = await _client.PostAsJsonAsync("/auth/register", new { email = "requester@example.com", password = "ValidPass1" });
    registered.StatusCode.Should().Be(HttpStatusCode.OK);

    var auth = await registered.Content.ReadFromJsonAsync<AuthResult>();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

    var me = await _client.GetAsync("/auth/me");
    me.StatusCode.Should().Be(HttpStatusCode.OK);
    (await me.Content.ReadAsStringAsync()).Should().Contain("Requester");
}

[Fact]
public async Task ChangeRole_Anonymous_ReturnsUnauthorized()
{
    var response = await _client.PutAsJsonAsync($"/admin/users/{Guid.NewGuid()}/role", new { role = "Agent" });
    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
```

- [ ] **Step 2: Run auth endpoint tests and verify failure**

Run: `dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj --filter FullyQualifiedName~AuthEndpointsTests`

Expected: FAIL with 404 because routes do not exist.

- [ ] **Step 3: Map auth endpoints and exception handling**

```csharp
public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
{
    var auth = endpoints.MapGroup("/auth");
    auth.MapPost("/register", async (RegisterRequest request, AuthHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.RegisterAsync(request, ct))).AllowAnonymous();
    auth.MapPost("/login", async (LoginRequest request, AuthHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.LoginAsync(request, ct))).AllowAnonymous();
    auth.MapGet("/me", async (AuthHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.GetMeAsync(ct))).RequireAuthorization();
    return auth;
}
```

Map Admin role endpoint under `/admin/users/{id}/role` with `.RequireAuthorization(policy => policy.RequireRole("Admin"))`. Add endpoint filter or `IExceptionHandler` mapping application exceptions to exact Problem Details statuses. Never surface internal exception messages to HTTP responses.

- [ ] **Step 4: Make integration fixture isolated and migrated**

In `ConfigureWebHost`, set in-memory `Jwt:SecretKey`, `Jwt:Issuer`, `Jwt:Audience`, `ADMIN_EMAIL`, and `ADMIN_PASSWORD`; preserve Testcontainers database override. Call migrations and seeding in fixture initialization. Add a `CreateAuthenticatedClientAsync(email, password)` helper only if used by more than two endpoint tests.

- [ ] **Step 5: Run auth endpoint tests and existing API checks**

Run: `dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj --filter "FullyQualifiedName~AuthEndpointsTests|FullyQualifiedName~HealthCheckTests"`

Expected: PASS.

- [ ] **Step 6: Commit auth API**

```bash
git add src/backend/ServiceDesk.Api src/backend/ServiceDesk.Infrastructure tests/ServiceDesk.IntegrationTests
git commit -m "feat: expose auth endpoints"
```

### Task 5: Add ticket application handlers and authorization

**Files:**
- Create: `src/backend/ServiceDesk.Application/Ticketing/TicketModels.cs`
- Create: `src/backend/ServiceDesk.Application/Ticketing/TicketHandlers.cs`
- Modify: `src/backend/ServiceDesk.Application/DependencyInjection.cs:9-18`
- Test: `tests/ServiceDesk.Application.Tests/Ticketing/TicketHandlersTests.cs`

**Interfaces:**
- Consumes: `ITicketStore`, `IIdentityService`, `IClock`, `ICurrentUser`, `Ticket` from Tasks 1-3.
- Produces: `CreateAsync`, `GetAsync`, `ListAsync`, `UpdateAsync`, `ClaimAsync`, `AssignAsync`, `ResolveAsync`, `ReopenAsync`, `CloseAsync`, `CancelAsync`, `AddCommentAsync`, `ListCommentsAsync`.
- Produces: `TicketResponse`, `TicketCommentResponse`, `PageResponse<T>`.

- [ ] **Step 1: Write failing application tests for owner and agent policy**

```csharp
[Fact]
public async Task Get_RequesterDoesNotOwnTicket_ThrowsNotFound()
{
    var handlers = CreateHandlers(userId: "requester-2", roles: ["Requester"]);
    await FluentActions.Invoking(() => handlers.GetAsync(_otherRequesterTicket.Id, CancellationToken.None))
        .Should().ThrowAsync<NotFoundException>();
}

[Fact]
public async Task Claim_SecondAgentAfterFirstClaim_ThrowsConflict()
{
    var store = new FakeTicketStore(CreateSubmittedTicket());
    var first = CreateHandlers(store, userId: "agent-1", roles: ["Agent"]);
    await first.ClaimAsync(store.SingleTicket.Id, CancellationToken.None);

    var second = CreateHandlers(store, userId: "agent-2", roles: ["Agent"]);
    await FluentActions.Invoking(() => second.ClaimAsync(store.SingleTicket.Id, CancellationToken.None))
        .Should().ThrowAsync<ConflictException>();
}

[Fact]
public async Task List_AgentGetsWorkspaceAndFilterIsApplied()
{
    var handlers = CreateHandlers(userId: "agent-1", roles: ["Agent"]);
    var page = await handlers.ListAsync(new TicketListRequest(TicketStatus.Submitted, null, null, 1, 20), CancellationToken.None);
    page.Items.Should().OnlyContain(ticket => ticket.Status == TicketStatus.Submitted);
}
```

- [ ] **Step 2: Run ticket handler tests and verify failure**

Run: `dotnet test tests/ServiceDesk.Application.Tests/ServiceDesk.Application.Tests.csproj --filter FullyQualifiedName~TicketHandlersTests`

Expected: FAIL because ticket handlers and request records do not exist.

- [ ] **Step 3: Define transport-neutral application models**

```csharp
public sealed record CreateTicketRequest(string Title, string Description, string Category, Impact Impact, Urgency Urgency);
public sealed record UpdateTicketRequest(string Title, string Description, string Category, Impact Impact, Urgency Urgency);
public sealed record AssignTicketRequest(string AssigneeId);
public sealed record AddCommentRequest(string Content);
public sealed record TicketListRequest(TicketStatus? Status, string? AssigneeId, string? RequesterId, int Page, int PageSize);
public sealed record TicketResponse(Guid Id, string RequesterId, string? AssigneeId, string Title, string Description, string Category, Impact Impact, Urgency Urgency, Priority Priority, TicketStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ResolvedAt, DateTimeOffset? ClosedAt, DateTimeOffset? CancelledAt);
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

Define `DefaultPageSize = 20` and `MaximumPageSize = 100` in `TicketHandlers`; reject pages below 1 and page sizes below 1 or above 100 with `ValidationException`.

- [ ] **Step 4: Implement authorization and lifecycle handlers**

Implement caller helpers:

```csharp
private void RequireAuthenticated();
private bool IsAdmin() => _currentUser.IsInRole("Admin");
private bool IsAgent() => _currentUser.IsInRole("Agent");
private static void RequireOwner(Ticket ticket, string userId);
private static TicketResponse ToResponse(Ticket ticket);
```

Requester list scope always sets `RequesterId = current user ID`, ignoring incoming requester/assignee filters. Agent/Admin list preserves valid filters and sees workspace. Ticket details/comments allow owner requester, Agent, Admin. For unauthorized requester access, throw `NotFoundException`.

Use domain methods for transitions. Application ensures: claim requires Agent; resolving Agent owns assignee; assignment is Admin-only and target role is Agent or Admin; requester updates/cancels/reopens/closes only own ticket. Convert `InvalidOperationException` from valid aggregate races/transitions into `ConflictException` only where stale concurrent state is plausible; use `ValidationException` for bad request shape or known invalid current state.

- [ ] **Step 5: Add EF concurrency translation test and implementation**

Add test that configures two handler scopes, claims same ticket, then verifies second operation becomes `ConflictException`. In `EfTicketStore.SaveChangesAsync`, catch `DbUpdateConcurrencyException` and throw `ConflictException("Ticket changed by another request.")`.

- [ ] **Step 6: Run all application tests**

Run: `dotnet test tests/ServiceDesk.Application.Tests/ServiceDesk.Application.Tests.csproj`

Expected: PASS.

- [ ] **Step 7: Commit ticket application layer**

```bash
git add src/backend/ServiceDesk.Application tests/ServiceDesk.Application.Tests/Ticketing src/backend/ServiceDesk.Infrastructure/Persistence/EfTicketStore.cs
git commit -m "feat: add ticket application workflows"
```

### Task 6: Expose Ticket HTTP endpoints and end-to-end integration tests

**Files:**
- Create: `src/backend/ServiceDesk.Api/Endpoints/TicketEndpoints.cs`
- Modify: `src/backend/ServiceDesk.Api/Program.cs:88-144`
- Create: `tests/ServiceDesk.IntegrationTests/TicketEndpointsTests.cs`
- Modify: `tests/ServiceDesk.IntegrationTests/ServiceDeskWebApplicationFactory.cs:25-58`

**Interfaces:**
- Consumes: `TicketHandlers` and models from Task 5.
- Produces: all ticket routes in approved API contract.

- [ ] **Step 1: Write failing end-to-end workflow test**

```csharp
[Fact]
public async Task RequesterAgentTicketFlow_ReopenAndClose_Succeeds()
{
    var requester = await RegisterAsync("requester@example.com");
    var agent = await CreateAndPromoteAgentAsync("agent@example.com");

    using var requesterClient = CreateBearerClient(requester.Token);
    var created = await requesterClient.PostAsJsonAsync("/tickets", new { title = "VPN", description = "Cannot connect", category = "Network", impact = "High", urgency = "Medium" });
    created.StatusCode.Should().Be(HttpStatusCode.OK);
    var ticket = await created.Content.ReadFromJsonAsync<TicketResponse>();

    using var agentClient = CreateBearerClient(agent.Token);
    (await agentClient.PostAsync($"/tickets/{ticket!.Id}/claim", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    (await agentClient.PostAsync($"/tickets/{ticket.Id}/resolve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    (await requesterClient.PostAsync($"/tickets/{ticket.Id}/reopen", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    (await agentClient.PostAsync($"/tickets/{ticket.Id}/resolve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    (await requesterClient.PostAsync($"/tickets/{ticket.Id}/close", null)).StatusCode.Should().Be(HttpStatusCode.OK);
}
```

- [ ] **Step 2: Write failing access, pagination, and terminal-comment tests**

```csharp
[Fact]
public async Task RequesterCannotReadAnotherRequesterTicket_ReturnsNotFound()
{
    var response = await otherRequesterClient.GetAsync($"/tickets/{ticketId}");
    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
}

[Fact]
public async Task ClosedTicketComment_ReturnsBadRequest()
{
    var response = await requesterClient.PostAsJsonAsync($"/tickets/{closedTicketId}/comments", new { content = "Please reopen" });
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task AgentListWorkspace_AppliesStatusAndPagination()
{
    var response = await agentClient.GetAsync("/tickets/me?status=Submitted&page=1&pageSize=1");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var page = await response.Content.ReadFromJsonAsync<PageResponse<TicketResponse>>();
    page!.Items.Should().HaveCount(1);
    page.Items.Single().Status.Should().Be(TicketStatus.Submitted);
}
```

- [ ] **Step 3: Run ticket endpoint tests and verify failure**

Run: `dotnet test tests/ServiceDesk.IntegrationTests/ServiceDesk.IntegrationTests.csproj --filter FullyQualifiedName~TicketEndpointsTests`

Expected: FAIL with 404 because ticket routes do not exist.

- [ ] **Step 4: Map exact ticket routes**

```csharp
var tickets = endpoints.MapGroup("/tickets").RequireAuthorization();
tickets.MapPost("/", async (CreateTicketRequest request, TicketHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.CreateAsync(request, ct)));
tickets.MapGet("/me", async ([AsParameters] TicketListRequest request, TicketHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.ListAsync(request, ct)));
tickets.MapGet("/{id:guid}", async (Guid id, TicketHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.GetAsync(id, ct)));
tickets.MapPatch("/{id:guid}", async (Guid id, UpdateTicketRequest request, TicketHandlers handlers, CancellationToken ct) => Results.Ok(await handlers.UpdateAsync(id, request, ct)));
```

Map remaining routes exactly:

```text
POST /tickets/{id}/claim
POST /tickets/{id}/assign
POST /tickets/{id}/resolve
POST /tickets/{id}/reopen
POST /tickets/{id}/close
POST /tickets/{id}/cancel
POST /tickets/{id}/comments
GET  /tickets/{id}/comments
```

Use endpoint role requirements for `claim` (Agent), `assign` (Admin), and handler checks for ownership/assignment. Ensure Swagger includes Bearer security definition and operation requirements.

- [ ] **Step 5: Run ticket integration tests and all backend tests**

Run: `dotnet test src/backend/ServiceDesk.sln --no-restore`

Expected: all unit, architecture, and integration tests PASS when Docker Testcontainers is available.

- [ ] **Step 6: Build backend and verify working-tree scope**

Run:

```bash
dotnet build src/backend/ServiceDesk.sln --no-restore
git status --short
git diff --check
```

Expected: build succeeds; no whitespace errors; only planned source/tests/migration/docs changes plus pre-existing untracked user files.

- [ ] **Step 7: Commit ticket API and tests**

```bash
git add src/backend/ServiceDesk.Api tests/ServiceDesk.IntegrationTests
git commit -m "feat: expose ticket lifecycle API"
```

### Task 7: Final verification and documentation alignment

**Files:**
- Modify: `README.md:171-283`
- Modify: `docs/superpowers/specs/2026-10-20-auth-ticketing-mvp-design.md:1-239` only if implementation materially changes approved contract; otherwise do not modify.
- Test: existing solution test projects.

**Interfaces:**
- Consumes: finished API from Tasks 3-6.
- Produces: accurate local setup/API documentation without secrets.

- [ ] **Step 1: Update README API usage**

Document environment keys without values:

```text
ADMIN_EMAIL=admin@example.test
ADMIN_PASSWORD=ValidAdminPass1
```

Document endpoints and lifecycle exactly as implemented. State that Docker full-stack smoke remains blocked by existing `minio/minio:latest` image pull failure; do not change Compose in this task.

- [ ] **Step 2: Run focused full verification**

Run:

```bash
dotnet test src/backend/ServiceDesk.sln --no-restore
dotnet build src/backend/ServiceDesk.sln --no-restore
git diff --check
```

Expected: tests/build pass; `git diff --check` exits 0. If Testcontainers cannot run because Docker is unavailable, report exact failed command and leave no claim that integration suite passed.

- [ ] **Step 3: Attempt unchanged Docker Compose smoke only after test verification**

Run: `docker compose up --build -d`

Expected current baseline may fail at `minio/minio:latest`; record result only. Do not edit Compose or substitute image.

- [ ] **Step 4: Commit documentation**

```bash
git add README.md
git commit -m "docs: document auth and ticket API"
```

## Plan Self-Review

- **Spec coverage:** Task 1 covers aggregate/data/workflow/comments. Task 2 covers mappings, indexes, migration, store, PostgreSQL. Tasks 3-4 cover registration, login, JWT, current-user, roles, admin seed, and auth endpoints. Tasks 5-6 cover authorization, workspace scope, filters/pagination, transitions, comment rules, HTTP errors, Swagger, and end-to-end flow. Task 7 covers docs and environment/Compose limitation.
- **No placeholders:** No incomplete implementation references remain; each task contains exact paths, tests, commands, and required signatures.
- **Type consistency:** `Ticket`, `TicketComment`, `ITicketStore`, `IdentityUserRecord`, `AuthHandlers`, `TicketHandlers`, request models, and response models retain the same names across tasks.
- **Review focus coverage:** Concurrent claim is Task 5; invalid role/role authorization is Tasks 3-4; requester privacy is Tasks 5-6; partial Admin env is Task 3; terminal comments are Tasks 1 and 6.
