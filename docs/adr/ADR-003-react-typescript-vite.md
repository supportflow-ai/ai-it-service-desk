# ADR-003: React + TypeScript + Vite Frontend

## Status
Accepted

## Context
Cần frontend framework cho SPA giao tiếp với .NET API backend.

## Decision
Chọn **React + TypeScript + Vite** với:
- **React Router** cho routing
- **TanStack Query** cho server state management
- **Axios** cho HTTP client
- **Ant Design** cho UI components (optional)

## Alternatives Considered
- **Angular**: Opinionated hơn, learning curve cao hơn cho nhóm.
- **Vue.js**: Ít phổ biến trong enterprise context.
- **Next.js**: SSR không cần thiết cho internal tool.

## Consequences
- **Positive**: Build nhanh (Vite), type safety (TypeScript), ecosystem lớn.
- **Negative**: Cần manage state manually (mitigated bằng TanStack Query).
