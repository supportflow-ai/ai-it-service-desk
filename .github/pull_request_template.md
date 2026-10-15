## 1. Tracking & Metadata

* **Jira Issue:** [TC-XXXX](https://jira.company.internal/browse/TC-XXXX)
* **Local ID:** `TC-XXXX` / `PR-XXXX`
* **Change Type:** [ ] Feature  [ ] Bugfix  [ ] Chore  [ ] Refactor  [ ] Hotfix
* **Scope / Components Impacted:**
  * [ ] Backend (API / Worker)
  * [ ] Domain & Application Layer
  * [ ] Infrastructure & Database
  * [ ] Frontend Web App
  * [ ] DevOps, CI/CD, Docker

---

## 2. Summary & Context

<!-- Provide a concise explanation: What does this PR change, and why is it needed? -->

### Key Changes
* 
* 

---

## 3. Test Evidence (MANDATORY)

<!-- Checkboxes alone are NOT sufficient. Provide the command executed and console output snippet. -->

### 3.1 Backend Verification
* Command: `dotnet test src/backend/ServiceDesk.sln -c Release`
* Results:
```text
Passed!  - Failed: 0, Passed: X, Skipped: 0, Total: X - ServiceDesk.Domain.Tests.dll
Passed!  - Failed: 0, Passed: Y, Skipped: 0, Total: Y - ServiceDesk.Application.Tests.dll
Passed!  - Failed: 0, Passed: Z, Skipped: 0, Total: Z - ServiceDesk.ArchitectureTests.dll
Passed!  - Failed: 0, Passed: W, Skipped: 0, Total: W - ServiceDesk.IntegrationTests.dll
```

### 3.2 Frontend Verification
* Lint Command: `npm run lint` (inside `src/frontend/service-desk-web`)
* Build Command: `npm run build`
* Results:
```text
LINT_EXIT: 0
BUILD_EXIT: 0
```

### 3.3 Smoke / Integration Verification (if applicable)
* Script: `deploy/smoke/smoke-test.ps1` or `deploy/smoke/smoke-test.sh`
* Evidence:
```text
<!-- Paste smoke test run log here -->
```

---

## 4. Security & Compliance Checklist

* [ ] No credentials, JWT secrets, passwords, or certificates are committed.
* [ ] Input validation applied to all public endpoints/inputs.
* [ ] RBAC / Tenant isolation respected where applicable.
* [ ] No PII or sensitive data exposed in logs.

---

## 5. Definition of Done (DoD) Checklist

* [ ] Jira Issue is linked and matches PR title.
* [ ] Local ID and Scope are documented.
* [ ] All unit, architecture, and integration tests pass.
* [ ] Frontend lint and type-check build pass without warnings/errors.
* [ ] Actual test evidence (commands + logs) is pasted above.
* [ ] CI Quality Gate is green (exit code 0).
* [ ] Reviewed and approved by at least 1 peer / QA engineer.
