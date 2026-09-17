# Definition of Ready (DoR), Definition of Done (DoD), and PR Review Rules

**Repository:** `Enterprise-Grade AI-Assisted Internal IT Service Request & Knowledge Management Platform`  
**Governance:** Engineering, DevOps, and QA Standards  
**Status:** Approved & Mandatory  

---

## 1. Definition of Ready (DoR)

A Jira Issue / Backlog item MUST NOT be pulled into an active sprint or transitioned to `In Progress` unless ALL of the following criteria are satisfied:

* [ ] **Clear User Story & Business Objective:** Explains the persona, action, and expected business value.
* [ ] **Unambiguous Acceptance Criteria (AC):** Written using Gherkin format (`Given-When-Then`) or clear testable statements.
* [ ] **Defined Scope & Technical Boundaries:** Explicitly outlines what is in-scope and out-of-scope (no open-ended tasks).
* [ ] **Identified Dependencies:** Database schemas, third-party APIs, frontend-backend contracts, or prerequisite tickets documented.
* [ ] **Architecture Alignment:** Verified against ADRs (e.g., Clean Architecture layers, Domain Entity encapsulation).
* [ ] **Test Strategy Defined:** Identifies required unit, architecture, integration, or smoke tests.
* [ ] **Security & Data Privacy Evaluated:** Flags any impact on RBAC, credentials, PII, or data storage.

---

## 2. Definition of Done (DoD)

A task or Pull Request is considered `Done` ONLY when ALL of the following conditions are met:

* [ ] **Code Complete:** Feature/bugfix implemented according to acceptance criteria without temporary debug hacks or commented-out code.
* [ ] **Automated Tests Passing:**
  * 100% of Domain unit tests pass (`dotnet test tests/ServiceDesk.Domain.Tests`).
  * 100% of Application unit tests pass (`dotnet test tests/ServiceDesk.Application.Tests`).
  * 100% of Architecture tests pass (`dotnet test tests/ServiceDesk.ArchitectureTests`).
  * 100% of Integration tests pass (`dotnet test tests/ServiceDesk.IntegrationTests`).
* [ ] **Frontend Quality Passed:**
  * Clean lint run without errors (`npm run lint`).
  * Clean production build without type errors (`npm run build`).
* [ ] **CI Quality Gate Green:** GitHub Actions workflow completes with exit code `0`.
* [ ] **No Secrets Committed:** Verified that no tokens, API keys, passwords, or connection strings are checked into Git.
* [ ] **Test Evidence Attached:** Actual execution logs and commands pasted into the PR description.
* [ ] **Peer & QA Review Approved:** Minimum required approvals obtained; all review comments marked resolved.
* [ ] **Documentation Updated:** Runbooks, Swagger documentation, ADRs, or README updated where appropriate.
* [ ] **Jira Issue Updated:** Jira issue status synced and linked to the PR.

---

## 3. Pull Request Review Rules

To ensure high engineering velocity while preventing regressions, the following merge rules are strictly enforced:

### 3.1 Approvals Required
1. **At least ONE peer review approval** from a senior engineer or code owner.
2. **At least ONE QA / DevOps sign-off** for any PR impacting infrastructure, Docker, CI pipelines, or core database migrations.
3. **No Self-Approval:** Authors are prohibited from approving their own PRs.

### 3.2 Reviewer Verification Checklist
Reviewers must verify before approving:
1. **Jira & Local ID Linkage:** PR title and metadata contain valid `[TC-XXXX]` and Local ID.
2. **Real Test Evidence:** Verify that console output or TRX artifact exists in the PR. Reject PRs with only empty checkboxes.
3. **Fail-Fast Compliance:** Ensure error paths are not bypassed using `catch {}`, `|| true`, or ignore directives.
4. **Zero Regression:** Existing test coverage is maintained; new behavior is backed by new test cases.
5. **No Lingering Comments:** All discussion threads must be marked `Resolved` prior to merge.

---

## 4. Jira & Local ID Linking Rules

1. **Jira Key Format:** Must match `TC-[0-9]+` (e.g., `TC-203`).
2. **Branch Mapping:** Branch name MUST begin with `<type>/TC-<id>-<slug>`.
3. **Commit Mapping:** Commit message must include `[TC-<id>]`.
4. **PR Mapping:** PR title must begin with `[TC-<id>]`.
5. **Local ID Format:** Use `TC-<id>` or `PR-<number>` in the PR template header to enable offline traceability during audits.
