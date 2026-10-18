# ADR-013: GitHub Actions for repository and system validation

## Status
Accepted, 2026-10-18. Supersedes ADR-009 at the project owner's request.

## Context
The backend, frontend, AI contract and infrastructure are separate GitHub repositories. Maintaining Jenkinsfiles alongside working Actions workflows duplicates the same quality gates and requires a separate CI server.

## Decision
Use GitHub Actions as the only active CI/CD platform. Remove Jenkinsfiles, Jenkins Compose/server configuration and the Jenkins-only checkout helper. Preserve Git history and historical audit evidence.

The agreed scope is test, image build and whole-system smoke. Images are built on disposable hosted runners as validation; they are not published. No production deployment, environment credentials, registry secrets or package write permission are introduced.

| Repository | Required validation |
|---|---|
| Backend | Locked restore, build, unit/architecture/offline compatibility, real PostgreSQL integration, AI contract/fixtures, API/Worker/Database image builds |
| Web | Locked npm install, lint, build, tests, frontend image build |
| AI | OpenAPI, checksum/version and offline fixtures; existing Actions workflow remains |
| Infra | Immutable backend/web source SHAs, Compose validation, migration/seed roles, disposable stack, requester smoke through gateway and health/FE checks |

Backend/web run on PRs and configured development/release branch pushes, with manual dispatch available once the workflow is on the default branch. Infra runs on pushes, PRs and manual dispatch. Concurrency cancels obsolete runs for the same workflow/ref. Keep existing job IDs (`backend`, `frontend`, `smoke`) so check names remain stable.

## Consequences
No Jenkins server, plugins, admin password or Docker socket mounted into Jenkins is required. GitHub-hosted Linux runners provide Docker for Testcontainers and Compose. Branch protection and existing external Jenkins installations require a repository/infrastructure administrator's separate change; deleting source configuration does not remove an external server or its volumes.

Registry publication and deployment, including any staging automation mentioned by historical plans, remain future decisions. Production uses the documented manual handoff with immutable images and explicit database commands.
