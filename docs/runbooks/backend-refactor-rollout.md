# Backend refactor rollout and rollback

## Repositories and source of truth

- Backend: https://github.com/supportflow-ai/ai-it-service-desk
- Frontend: https://github.com/supportflow-ai/ai-it-service-desk-web
- AI contract consumer: https://github.com/supportflow-ai/ai-service
- Compose/gateway/Jenkins/whole-system tests: https://github.com/supportflow-ai/supportflow-ai-infra

Keep existing database and object-storage contents. For an existing Compose project,
record its project name and actual volume names before changing repositories. Configure
external volumes to those existing volumes; do not let a new project silently create
empty replacements. Back up the database and verify restore in staging. Never run
`down -v` outside disposable CI.

## Staging/production sequence

1. Validate the pinned backend, frontend and infrastructure commits and their CI results.
2. Supply a non-sample JWT key of at least 32 UTF-8 bytes, existing issuer/audience and
   secret database/storage configuration. Retain an existing strong signing key.
3. Build API, Worker and Database images from the same backend SHA. Use immutable image
   references in infra. Restrict runtime database accounts to DML/sequence/function
   privileges; use a separate schema owner for migration.
4. During a coordinated maintenance window, run Database `migrate`, then `seed-roles`.
   The new function changes only future ticket numbers, retaining old rows and sequence.
5. Run Database `check-production`. If a known demo account still has its sample password,
   stop acceptance, reset that password through the controlled administrative process
   and review its roles. Do not silently delete or elevate existing accounts. Rotate
   sample signing keys; existing sample-signed tokens then require login again.
6. To create an initial admin, invoke `bootstrap-admin` with `BootstrapAdmin__Email`
   and `BootstrapAdmin__Password` environment variables on that one-shot process only.
   It refuses existing accounts. Never pass bootstrap credentials to API/Worker.
7. Start the new API replicas and Worker. Confirm `/health/live`, `/health/ready` and
   compatible `/health`; smoke-test registration/login/create/list/detail via gateway.
   Verify two-host same-key replay and inspect logs for application failures.

The database CLI returns 0 on success, 1 on operation/configuration failure and 2 for
invalid usage. It deliberately omits credentials and raw exception details from output.
`seed-demo` requires Development/Test plus `SeedDemo__Enabled=true` and an explicit
`SeedDemo__Password`; it is never part of production startup.

## Rollback

Roll back application images using the retained prior SHA. Do not run migration Down on
real data: the additive ticket-number function/default is compatible with old application
code and preserves data. Older images still have the old automatic seed behavior, so
prefer roll-forward after security changes; only use rollback images with explicit seed
protection, and re-run production account preflight. Resetting credentials/keys is not
undone by application rollback. Keep bootstrap inputs out of long-lived containers.

## Follow-up backlog

Implement a coordinated receipt retention job (without weakening advisory-lock safety),
real AI transport and service credentials, business revision and suggestion persistence,
ticket lifecycle/SLA, full audit trail, telemetry and profile persistence separately.
