# PR, Branch, and Jira Linkage Convention

**Standard:** Enterprise DevOps & QA Process  
**Applicability:** All contributors, developers, and QA engineers working on `ai-it-service-desk`  
**Status:** Approved & Enforced  

---

## 1. Branch Naming Convention

All branches created in the repository MUST follow the structured format:

```text
<type>/<jira-key>-<short-description>
```

### 1.1 Types
* `feature/` or `feat/`: New feature implementation (e.g., `feature/TC-101-jwt-authentication`)
* `bugfix/` or `fix/`: Defect fix against dev/staging/main (e.g., `bugfix/TC-142-fix-cors-origin`)
* `chore/`: Maintenance, build, dependencies, or DevOps scaffolding (e.g., `chore/TC-203-quality-gate-integration-environment`)
* `refactor/`: Code restructuring without functional change (e.g., `refactor/TC-115-clean-domain-events`)
* `hotfix/`: Critical production patch branched directly off `main` (e.g., `hotfix/TC-999-db-connection-leak`)
* `release/`: Release staging candidate (e.g., `release/v1.0.0`)

### 1.2 Format Rules
1. `<jira-key>` is mandatory: Must match the project Jira key format `TC-[0-9]+`.
2. `<short-description>`: 2–5 lowercase words separated by hyphens (`-`), no underscores or special characters.
3. Examples of valid branch names:
   * `feature/TC-105-ticket-crud-api`
   * `fix/TC-188-token-expiration-parsing`
   * `chore/TC-203-quality-gate-integration-environment`

---

## 2. Commit Message Convention

Commit messages must follow **Conventional Commits** augmented with the Jira issue prefix:

```text
<type>(<scope>): [TC-<id>] <description>

[optional body]

[optional footer(s)]
```

### Examples:
* `feat(auth): [TC-101] implement JWT bearer token issue and refresh endpoints`
* `fix(persistence): [TC-142] resolve PostgreSQL health check connection string binding`
* `chore(ci): [TC-203] add GitHub Actions workflow with backend and frontend quality gates`
* `test(ticketing): [TC-105] add unit tests for priority calculation matrix`

---

## 3. Pull Request (PR) Convention

### 3.1 PR Title Format
```text
[TC-<id>] <type>(<scope>): <summary>
```
Example:
* `[TC-203] chore(devops): standardize PR/branch conventions, CI quality gates, and smoke hooks`

### 3.2 PR Mandatory Metadata
Every PR must fill out the repository's `.github/pull_request_template.md`:
1. **Jira Issue Link:** Direct URL or markdown link: `[TC-XXXX](https://jira.company.internal/browse/TC-XXXX)`.
2. **Local ID:** An internal or pull-request identifier (e.g., `PR-#`, `DEV-01`, `LOCAL-TC203`) to maintain continuity across offline reviews.
3. **Scope:** One or more components impacted (`Backend-Api`, `Backend-Worker`, `Domain`, `Frontend`, `DevOps/Infra`, `Docs`).
4. **Summary & Changes:** Clear technical explanation of what was changed and why.
5. **Test Evidence:** **MANDATORY.** A PR without actual test logs or CLI execution snippets will be immediately blocked and rejected by reviewers.

---

## 4. Test Evidence Rules

Checkboxes alone (e.g. `[x] Tests pass`) are **NOT** sufficient evidence for PR approval.

Every author must provide:
1. **Command Executed:** Full command string executed locally (e.g., `dotnet test src/backend/ServiceDesk.sln -c Release`).
2. **Execution Log Snippet:** Actual console output demonstrating that the test suites ran and passed (e.g., total tests, passed, failed, duration).
3. **Artifact / TRX Attachment:** For integration and E2E tests, attach TRX reports or console logs to the PR comments.
4. **Screenshot:** Required for any user-facing frontend UI change.
