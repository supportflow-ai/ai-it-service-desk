# ADR-005: ASP.NET Core Identity + JWT Bearer

## Status
Accepted

## Context
Cần authentication foundation cho API (internal IT tool).

## Decision
Dùng **ASP.NET Core Identity** cho user management (password hashing, user store) + **JWT Bearer** cho API authentication.

## Alternatives Considered
- **Cookie-based auth**: Không phù hợp API-first SPA architecture.
- **OAuth2/OIDC external provider**: Over-engineering cho internal tool tại thời điểm này.

## Consequences
- **Positive**: Built-in user management, JWT stateless, dễ test, dễ integrate frontend.
- **Negative**: Cần manage token refresh logic, JWT secret management.
