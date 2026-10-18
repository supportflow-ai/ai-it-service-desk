# Environment ownership after repository separation

Whole-system Compose, gateway NGINX and GitHub Actions smoke tests are owned by
https://github.com/supportflow-ai/supportflow-ai-infra.

Use that repository for local stack setup. Backend build/test commands are in the root
README. Database initialization, existing-volume preservation, production preflight and
rollback are documented in [backend rollout](backend-refactor-rollout.md).

Historical sprint artifacts are not current deployment instructions. API/Worker no longer
migrate or seed on startup; explicitly run the database tool before starting applications.
