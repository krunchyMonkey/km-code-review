# PR Review Orchestrator — Proof of Concept Build Spec

> **Purpose of this document**
>
> This is an implementation-ready specification intended to be handed directly to Claude Code (or another coding agent) to build a working proof of concept.
>
> The goal is not to build a production-ready SDLC platform. The goal is to prove that a lightweight review-orchestration layer can reduce pull-request review latency by making review work visible, prioritized, routed, and easier to consume.

---

# 1. Instructions to the Coding Agent

Build the application described in this document as a runnable local proof of concept.

Do not stop after scaffolding. Implement the end-to-end happy path with working backend APIs, persistence, seeded data, scoring, queue behavior, reviewer assignment, and the primary UI screens.

Prefer simple, understandable code over abstraction-heavy architecture. This is a PoC, but keep boundaries clean enough that a real Azure DevOps or GitHub provider can replace the mock provider later.

If a requirement is ambiguous, choose the simplest implementation that preserves the intent described here. Document any material assumptions in the repository README rather than blocking on clarification.

## Required output

Create a repository containing:

- ASP.NET Core backend service.
- React + TypeScript frontend.
- SQLite persistence.
- Seeded demo data.
- Mock source-control provider.
- Review queue and prioritization engine.
- Reviewer routing logic.
- AI review brief abstraction with a deterministic mock implementation.
- REST APIs used by the frontend.
- Automated tests for the scoring and reviewer-routing logic.
- Docker Compose support if practical.
- README with setup and run instructions.

The application must run locally without any external credentials.

---

# 2. Product Summary

AI-assisted coding has increased the rate at which engineers can create changes, but human review capacity has not increased at the same rate.

The result is a review bottleneck:

1. Pull requests are opened quickly.
2. Reviewers receive more requests than they can immediately process.
3. PRs become buried in notifications.
4. Authors continue producing additional PRs.
5. Review latency becomes dominated by queue time rather than actual review time.

The **PR Review Orchestrator** provides an explicit review queue on top of an existing source-control platform.

It should answer four questions immediately:

1. **What should be reviewed next?**
2. **Who should review it?**
3. **What parts actually need human attention?**
4. **Which PRs are becoming stale or blocked?**

The PoC should demonstrate those behaviors without replacing Azure DevOps, GitHub, or the team's existing PR workflow.

---

# 3. Core Product Principles

## 3.1 Human attention is the scarce resource

The product should optimize human review attention rather than simply adding another AI reviewer.

The AI review brief should summarize and focus attention. It should not generate dozens of noisy inline comments.

## 3.2 The queue must be explicit

A PR should never depend only on a notification or someone's memory.

Every ready-for-review PR should have:

- a queue position,
- a priority,
- an age,
- a review status,
- and, where possible, an assigned reviewer.

## 3.3 Risk should determine review effort

Not all changes deserve the same review process.

Tests, documentation, and low-risk UI changes should not consume the same review capacity as authentication, permissions, storage isolation, infrastructure, or data migrations.

## 3.4 AI should reduce review surface

AI should help answer:

- What changed?
- What is risky?
- What is mechanical?
- What files should I read first?

It should not replace reviewer judgment.

## 3.5 Avoid notification spam

The system should escalate aging work intentionally instead of repeatedly notifying people.

---

# 4. PoC Scope

The PoC must support the following workflow:

1. A pull request enters the system.
2. The system calculates a priority score.
3. The system calculates a risk tier.
4. The system selects appropriate candidate reviewers.
5. The PR appears in a global review queue.
6. A reviewer opens the PR in the dashboard.
7. The reviewer sees an AI-generated review brief.
8. The reviewer starts the review.
9. The queue records that the PR is actively being reviewed.
10. The reviewer can mark the review approved, request changes, or release the assignment.
11. Queue metrics update accordingly.

The PoC does **not** need to merge real pull requests or modify a real repository.

---

# 5. Non-Goals

Do not spend PoC time implementing:

- full source-control replacement,
- real merge execution,
- full diff rendering,
- production authentication/SSO,
- enterprise RBAC,
- billing,
- multi-tenant architecture,
- a real LLM provider unless it is trivial and completely optional,
- complicated event streaming,
- production Slack bot installation,
- Kubernetes deployment,
- advanced repository administration.

Use interfaces/stubs where those future integrations need a boundary.

---

# 6. Recommended Technology Stack

Use the following stack unless there is a compelling implementation reason not to.

## Backend

- .NET 10
- ASP.NET Core Minimal APIs or controllers
- C#
- Entity Framework Core
- SQLite
- FluentValidation or simple endpoint validation
- xUnit for unit tests

## Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- React Router
- TanStack Query
- Lucide icons

Avoid a large component framework for the PoC unless needed. Build lightweight reusable components that reproduce the supplied dashboard style.

## Repository structure

```text
/pr-review-orchestrator
  /src
    /ReviewOrchestrator.Api
    /ReviewOrchestrator.Core
    /ReviewOrchestrator.Infrastructure
    /ReviewOrchestrator.Web
  /tests
    /ReviewOrchestrator.Core.Tests
  /docs
  docker-compose.yml
  README.md
```

A smaller backend structure is acceptable if it materially speeds up the PoC, but keep domain logic out of endpoint handlers.

---

# 7. High-Level Architecture

```text
                  +-----------------------+
                  | Azure DevOps / GitHub |
                  |   Future Integration  |
                  +-----------+-----------+
                              |
                              | webhook / polling
                              v
                    +--------------------+
                    | SourceControl      |
                    | Provider Interface |
                    +---------+----------+
                              |
                              v
+----------------+    +-------------------------+
| Review Brief   |--->| PR Review Orchestrator  |
| Provider       |    | API                     |
+----------------+    +-----------+-------------+
                                  |
          +-----------------------+-----------------------+
          |                       |                       |
          v                       v                       v
+------------------+   +-------------------+   +-------------------+
| Priority Engine  |   | Reviewer Router   |   | Review Queue      |
+------------------+   +-------------------+   +-------------------+
          |                       |                       |
          +-----------------------+-----------------------+
                                  |
                                  v
                           +-------------+
                           | SQLite / EF |
                           +------+------+ 
                                  |
                                  v
                           +-------------+
                           | React UI    |
                           +-------------+
```

---

# 8. Domain Model

## 8.1 PullRequest

```csharp
public sealed class PullRequest
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ReadyForReviewAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public PullRequestStatus Status { get; set; }
    public ReviewRisk Risk { get; set; }
    public double PriorityScore { get; set; }

    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int ChangedFiles { get; set; }
    public int MeaningfulChangedLines { get; set; }

    public bool IsReleaseBlocker { get; set; }
    public bool IsProductionFix { get; set; }
    public bool BlocksOtherWork { get; set; }
    public bool IsSecuritySensitive { get; set; }
    public bool IntentionallyLarge { get; set; }

    public string SourceBranch { get; set; } = string.Empty;
    public string TargetBranch { get; set; } = "main";
    public string ExternalUrl { get; set; } = string.Empty;

    public List<string> Labels { get; set; } = [];
    public List<PullRequestFile> Files { get; set; } = [];
    public List<ReviewAssignment> Assignments { get; set; } = [];
}
```

## 8.2 PullRequestFile

```csharp
public sealed class PullRequestFile
{
    public Guid Id { get; set; }
    public Guid PullRequestId { get; set; }
    public string Path { get; set; } = string.Empty;
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public bool IsGenerated { get; set; }
    public bool IsTestFile { get; set; }
    public bool IsSecuritySensitive { get; set; }
    public string? Component { get; set; }
}
```

## 8.3 Reviewer

```csharp
public sealed class Reviewer
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int MaxConcurrentReviews { get; set; } = 3;
    public List<string> Components { get; set; } = [];
    public List<string> Specialties { get; set; } = [];
}
```

Example specialties:

```text
auth
security
frontend
backend
storage
infra
database
ai
```

## 8.4 ReviewAssignment

```csharp
public sealed class ReviewAssignment
{
    public Guid Id { get; set; }
    public Guid PullRequestId { get; set; }
    public Guid ReviewerId { get; set; }
    public ReviewAssignmentStatus Status { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset LeaseExpiresAt { get; set; }
}
```

## 8.5 ReviewBrief

```csharp
public sealed class ReviewBrief
{
    public Guid Id { get; set; }
    public Guid PullRequestId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }

    public List<string> WhatChanged { get; set; } = [];
    public List<string> HumanAttentionAreas { get; set; } = [];
    public List<string> MostlyMechanical { get; set; } = [];
    public List<ReviewFileRecommendation> RecommendedReviewOrder { get; set; } = [];
}
```

## 8.6 ReviewFileRecommendation

```csharp
public sealed class ReviewFileRecommendation
{
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int Rank { get; set; }
}
```

## 8.7 Enums

```csharp
public enum PullRequestStatus
{
    Draft,
    WaitingForReview,
    InReview,
    ChangesRequested,
    Approved,
    Merged,
    Closed
}

public enum ReviewRisk
{
    Low,
    Medium,
    High
}

public enum ReviewAssignmentStatus
{
    Assigned,
    Acknowledged,
    Released,
    Completed,
    Expired
}
```

---

# 9. Priority Scoring

Implement the priority engine as deterministic domain logic.

The exact numbers are intentionally tunable. Use these defaults for the PoC.

## Base score

```text
age_hours * 1.0
```

Cap age contribution at 72 points.

## Priority modifiers

```text
+40 release blocker
+35 production fix
+25 blocks another PR or active work
+20 high risk
+10 medium risk
+10 PR has already waited > 1 business day
+8  small PR bonus when meaningful changed lines <= 150
-10 very large PR when meaningful changed lines > 1000
```

## Formula

Pseudo-code:

```text
score = min(ageHours, 72)

if releaseBlocker: score += 40
if productionFix: score += 35
if blocksOtherWork: score += 25

if risk == High: score += 20
if risk == Medium: score += 10

if ageHours >= 8: score += 10

if meaningfulChangedLines <= 150: score += 8
if meaningfulChangedLines > 1000: score -= 10
```

Sort queue by:

1. priority score descending,
2. ReadyForReviewAt ascending.

Expose the score and the major score reasons through the API so the UI can explain why a PR is ranked where it is.

Example:

```json
{
  "priorityScore": 82,
  "priorityReasons": [
    "+35 production fix",
    "+25 blocks other work",
    "+14 waiting time",
    "+8 small PR"
  ]
}
```

---

# 10. Risk Classification

Implement a deterministic PoC risk engine.

## High risk

A PR is High risk if any of the following are true:

- label contains `security`,
- label contains `auth`,
- label contains `permissions`,
- label contains `infrastructure`,
- label contains `migration`,
- a changed path contains `/auth/`,
- a changed path contains `/security/`,
- a changed path contains `/permissions/`,
- a changed path contains `/terraform/`,
- a changed path contains `/migrations/`,
- `IsSecuritySensitive == true`.

## Medium risk

A PR is Medium risk if it is not High and any of the following are true:

- backend behavior changes,
- persistence/database changes,
- API contract changes,
- more than 500 meaningful changed lines,
- more than 10 production files changed.

## Low risk

Everything else is Low risk.

Typical low-risk examples:

- documentation,
- tests only,
- telemetry-only changes,
- formatting,
- dependency cleanup,
- small presentation-only UI changes.

The UI should display the calculated risk tier and the reasons.

---

# 11. Large PR / Review Surface Detection

Add a `ReviewSurfaceWarning` when either condition is met:

```text
MeaningfulChangedLines > 600
OR
production files changed > 15
```

Generated files, lock files, snapshots, minified bundles, and common package-lock files should not count toward meaningful changed lines.

The warning text should be approximately:

```text
Large review surface

1,247 meaningful lines changed across 22 production files.

Consider splitting the change or providing a review map.
```

Do not block the PR in the PoC.

---

# 12. Reviewer Routing

The reviewer router should select reviewers based on ownership, specialty, and current load.

## Inputs

- changed components,
- PR labels,
- risk tier,
- reviewer component ownership,
- reviewer specialties,
- active review count,
- reviewer concurrency limit.

## Candidate scoring

Start each reviewer at zero.

```text
+40 owns a changed component
+30 specialty matches a PR label/risk area
+15 has reviewed the repository before (seeded/mock flag is acceptable)
-15 per active assigned or acknowledged review
-100 if inactive
-100 if at MaxConcurrentReviews
```

For High-risk PRs:

- require at least one reviewer whose specialty matches `security`, `auth`, `infra`, `database`, or the identified risk reason when available.

For the PoC, automatically assign the highest-scoring eligible reviewer.

Return the top three candidates in the API response so the UI can show why someone was selected.

Example:

```json
{
  "assignedReviewer": {
    "id": "...",
    "name": "Bob Smith"
  },
  "candidates": [
    {
      "name": "Bob Smith",
      "score": 55,
      "reasons": ["owns backend", "1 active review"]
    },
    {
      "name": "Kent Jones",
      "score": 25,
      "reasons": ["owns backend", "3 active reviews"]
    }
  ]
}
```

---

# 13. Reviewer Lease

An assignment should have a lease.

Default:

```text
4 business hours
```

For the PoC, normal clock hours are acceptable. Do not implement a business-calendar engine.

If the assignment remains `Assigned` past `LeaseExpiresAt` without being acknowledged:

1. mark it `Expired`,
2. calculate reviewer routing again,
3. assign the next eligible reviewer.

Provide a background hosted service that checks expired leases every minute.

Make the interval configurable.

---

# 14. Aging States

Expose an aging state on each queue item.

```text
Fresh       < 4 hours
Waiting     >= 4 hours
Aging       >= 8 hours
Stale       >= 24 hours
Critical    >= 48 hours
```

The UI should make Aging, Stale, and Critical visually obvious.

This state should not replace priority score. It is an additional signal.

---

# 15. WIP Signal

The product should help reveal when authors are generating work faster than it is being reviewed.

For each author calculate:

```text
openReviewCount = number of PRs in WaitingForReview + InReview + ChangesRequested
```

Default warning threshold:

```text
3
```

When an author has 3 or more open review items, include a soft warning:

```text
This author currently has 4 PRs in the review pipeline.
```

This is informational only. Do not block submission.

---

# 16. Review Brief Provider

Create the following interface:

```csharp
public interface IReviewBriefProvider
{
    Task<ReviewBrief> GenerateAsync(
        PullRequest pullRequest,
        CancellationToken cancellationToken);
}
```

Implement:

```text
MockReviewBriefProvider
```

The mock provider should deterministically generate useful output based on file paths, labels, size, and PR metadata.

Example logic:

- persistence files -> mention persistence behavior,
- auth files -> mention authentication/security review,
- DTO/model files -> classify as potentially mechanical,
- test files -> classify as mechanical,
- largest production files -> place near the top of recommended review order,
- security-sensitive files -> always place in recommended review order.

Do not require an LLM API key.

Also define, but do not fully implement:

```csharp
public sealed class OpenAiReviewBriefProvider : IReviewBriefProvider
```

It can throw `NotSupportedException` or be excluded behind configuration. The purpose is simply to establish the future integration boundary.

---

# 17. Source Control Provider

Create:

```csharp
public interface ISourceControlProvider
{
    Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(CancellationToken cancellationToken);
    Task<PullRequest?> GetPullRequestAsync(string externalId, CancellationToken cancellationToken);
}
```

Implement:

```text
MockSourceControlProvider
```

The mock provider should seed or return demo pull requests matching the UI scenarios in this specification.

Optionally add empty/stub implementations for:

```text
AzureDevOpsSourceControlProvider
GitHubSourceControlProvider
```

Do not spend meaningful PoC time implementing live provider authentication unless everything else is complete.

---

# 18. API Endpoints

Base route:

```text
/api
```

## 18.1 Queue

### GET `/api/review-queue`

Query parameters:

```text
repository
priority
risk
status
reviewerId
authorId
agingState
search
sort
```

Default sort:

```text
priority-desc
```

Response should contain enough data to render the queue without requesting every PR individually.

Example:

```json
{
  "items": [
    {
      "id": "guid",
      "externalId": "217",
      "title": "Add pending skill lifecycle handling",
      "repositoryName": "ai-assistant",
      "authorName": "Alex Chen",
      "readyForReviewAt": "2026-10-05T18:00:00Z",
      "waitMinutes": 47,
      "priorityScore": 82,
      "priorityBand": "High",
      "risk": "Medium",
      "agingState": "Fresh",
      "status": "WaitingForReview",
      "labels": ["backend", "skills", "state-management"],
      "additions": 342,
      "deletions": 98,
      "changedFiles": 12,
      "assignedReviewer": null
    }
  ],
  "summary": {
    "total": 12,
    "high": 3,
    "medium": 6,
    "low": 3,
    "aging": 4
  }
}
```

## 18.2 PR Detail

### GET `/api/pull-requests/{id}`

Return:

- PR metadata,
- priority explanation,
- risk explanation,
- assignments,
- reviewer candidates,
- review brief,
- files,
- WIP warning,
- review-surface warning.

## 18.3 Start Review

### POST `/api/pull-requests/{id}/start-review`

Body:

```json
{
  "reviewerId": "guid"
}
```

Behavior:

- create or acknowledge assignment,
- set PR status to `InReview`,
- set `AcknowledgedAt`,
- return updated PR detail.

## 18.4 Release Review

### POST `/api/pull-requests/{id}/release-review`

Behavior:

- mark current assignment Released,
- set PR to WaitingForReview,
- rerun reviewer routing.

## 18.5 Complete Review

### POST `/api/pull-requests/{id}/complete-review`

Body:

```json
{
  "outcome": "Approved"
}
```

Allowed outcomes:

```text
Approved
ChangesRequested
```

Behavior:

- mark assignment completed,
- update PR status.

## 18.6 Recalculate

### POST `/api/pull-requests/{id}/recalculate`

Recalculate:

- risk,
- priority,
- reviewer candidates,
- review brief.

Useful for the PoC demo.

## 18.7 Reviewers

### GET `/api/reviewers`

Return reviewers with:

- active review count,
- capacity,
- components,
- specialties.

## 18.8 Dashboard Metrics

### GET `/api/metrics/summary`

Return:

```json
{
  "waitingForReview": 12,
  "inReview": 5,
  "aging": 4,
  "averageWaitMinutes": 312,
  "medianWaitMinutes": 148,
  "averageTimeToFirstReviewMinutes": 187,
  "reviewerUtilization": [
    {
      "reviewerName": "Bob Smith",
      "active": 1,
      "capacity": 3
    }
  ]
}
```

---

# 19. UI Requirements

The UI should closely follow the supplied dashboard mockup.

Desktop-first is acceptable for the PoC, but the layout should degrade reasonably at smaller widths.

Use a modern application shell with:

- dark left navigation,
- white/light main content,
- restrained borders,
- subtle shadows,
- rounded cards,
- clear colored priority/risk badges,
- dense but readable information hierarchy.

Do not make it look like an admin template from 2014.

---

# 20. Main Navigation

Left sidebar:

```text
ReviewHub
PR Review Orchestrator

Review Queue
My Reviews
Assigned to Me
Needs Attention

Analytics
Team
Settings

Integrations
Azure DevOps / GitHub
Slack

Current user
```

Only Review Queue needs complete functionality for the PoC.

Other routes can be implemented as lightweight placeholder screens, except Analytics should display basic metrics if time allows.

---

# 21. Review Queue Screen

Route:

```text
/
/reviews
```

## Header

```text
PR Review Queue
Prioritized pull requests needing review
```

Controls:

- search,
- repository dropdown,
- priority dropdown,
- status dropdown,
- sort dropdown.

Tabs:

```text
All
High
Medium
Low
Aging
```

Each tab should show a count.

## Queue item

Each row/card should show:

- priority badge,
- PR title,
- PR number,
- repository,
- author,
- wait duration,
- queue/review status,
- tags,
- additions,
- deletions,
- changed files.

Example:

```text
HIGH
Add pending skill lifecycle handling
#217 in ai-assistant · opened by Alex Chen

backend   skills   state-management

+342  -98   12 files
                              47m
                              waiting for review
```

Selected item should have a visible accent border or background.

Clicking an item loads its detail panel without requiring a full page navigation if convenient.

---

# 22. PR Detail Screen / Panel

The mockup uses a two-pane layout:

```text
| queue | PR detail |
```

Maintain this layout on wide screens.

## PR header

Show:

- title,
- PR number,
- repository,
- priority badge,
- Start Review button,
- optional overflow menu.

Tabs:

```text
Overview
Files
Commits
Checks
Discussion
```

Only Overview must be complete.

Files may show a simple file list.

The other tabs can be placeholder content.

---

# 23. AI Review Brief Card

This is one of the most important parts of the PoC.

Header:

```text
AI Review Brief
A concise summary to help you review faster
Generated X minutes ago
```

Display three columns/cards:

## What changed

Example:

```text
- Added pending skill lifecycle handling.
- Modified thread persistence logic.
- Added one new API response field.
- Updated state management for skill execution.
```

## Areas worth human attention

Example:

```text
- Pending state can now be cleared in two different paths.
- Changes persistence behavior in EndpointHandler.
- New failure handling logic may affect retries.
```

## Mostly mechanical

Example:

```text
- DTO changes
- Test updates
- Renames
- Formatting
- Import organization
```

Use visual distinctions but avoid scary red styling unless there is an actual warning.

---

# 24. Recommended Review Order

Below the three review brief columns, show a ranked list.

Example:

```text
1. PendingSkillCoordinator.cs
   Core lifecycle logic for pending skills

2. EndpointHandler.cs
   Changes to persistence behavior

3. ThreadState.cs
   State-management updates
```

Also show line additions/deletions when available.

The intent is to tell the reviewer where to spend attention first.

---

# 25. PR Details Card

Show:

- author,
- age,
- description,
- labels,
- risk badge,
- additions,
- deletions,
- changed files,
- source branch,
- linked work item if present.

Add a small section explaining priority:

```text
Why this is ranked highly
+25 blocks dependent work
+20 high-risk area
+18 waiting time
```

Do not display an unexplained magic score without the explanation.

---

# 26. Risk Assessment Card

Show:

```text
Risk Assessment    MEDIUM RISK
```

Then list:

- affected areas,
- detected risk reasons,
- reassuring signals where useful.

Example:

```text
Areas touched
Backend Logic
API Changes
Data Persistence

✓ No authentication changes
✓ No security-sensitive files
⚠ Requires component-owner review
⚠ Affects core lifecycle behavior
```

---

# 27. Reviewer Assignment Card

Add a card to the Overview screen beneath risk assessment.

Example:

```text
Review Assignment

Assigned to: Bob Smith
Lease remaining: 2h 17m
Active reviews: 1 / 3

Why Bob?
- Owns backend component
- Experience with state-management changes
- Lowest current load among qualified reviewers
```

Also display the next two candidate reviewers in a compact list.

Provide buttons:

```text
Start Review
Release
```

Once started:

```text
Approve
Request Changes
Release
```

---

# 28. Large Review Surface Warning

When triggered, display a warning near the top of the Overview:

```text
Large review surface

1,247 meaningful lines changed across 22 production files.
Consider splitting the change or providing a review map.
```

This should be noticeable but not block the workflow.

---

# 29. Author WIP Warning

When triggered:

```text
Review pipeline load

Alex currently has 4 PRs waiting for or undergoing review.
```

Keep this neutral. The feature is intended to expose system load, not shame an engineer.

---

# 30. Analytics Screen

Route:

```text
/analytics
```

Implement a lightweight dashboard if practical.

Show cards for:

- Waiting for review
- In review
- Aging PRs
- Median review wait
- Average time to first review

Add a table:

```text
Reviewer         Active   Capacity
Bob Smith        1        3
Kent Jones       3        3
Priya Singh      2        3
```

Optional chart:

```text
PRs by aging bucket
```

Do not let analytics work delay the primary review workflow.

---

# 31. Seed Data

Seed at least 10 pull requests so the dashboard looks meaningful immediately.

Use these scenarios.

## PR 217

```text
Title: Add pending skill lifecycle handling
Repository: ai-assistant
Author: Alex Chen
Priority intent: High
Risk: Medium
Age: 47 minutes
Additions: 342
Deletions: 98
Files: 12
Labels: backend, skills, state-management
```

Files should include:

```text
src/Skills/PendingSkillCoordinator.cs
src/Endpoints/EndpointHandler.cs
src/Threads/ThreadState.cs
src/Contracts/SkillResponseDto.cs
tests/PendingSkillCoordinatorTests.cs
```

## PR 205

```text
Title: Update authentication token refresh logic
Repository: platform
Author: Priya Singh
Risk: High
Age: 3h 12m
Labels: auth, security, infra
Additions: 128
Deletions: 46
Files: 8
```

## PR 212

```text
Title: Improve threading persistence layer
Repository: ai-assistant
Author: Marcus Lee
Risk: Medium
Age: 5h 3m
Labels: backend, database, performance
Additions: 287
Deletions: 121
Files: 14
```

## PR 209

```text
Title: Add analytics events for skill execution
Repository: ai-assistant
Author: Jordan Park
Risk: Medium or Low
Age: 6h 18m
Labels: telemetry, analytics, observability
Additions: 96
Deletions: 12
Files: 6
```

## PR 198

```text
Title: Refactor UI components to new design system
Repository: web
Author: Casey Nguyen
Risk: Medium
Age: 8h 41m
Labels: frontend, ui, refactor
Additions: 421
Deletions: 203
Files: 18
```

## PR 190

```text
Title: Update documentation for skill APIs
Repository: docs
Author: Sam Rivera
Risk: Low
Age: 12h 2m
Labels: docs, api
Additions: 62
Deletions: 8
Files: 4
```

## PR 188

```text
Title: Remove unused dependencies
Repository: web
Author: Riley Kim
Risk: Low
Age: 1 day
Labels: chore, dependencies
Additions: 24
Deletions: 312
Files: 3
```

## PR 183

```text
Title: Fix race condition in cache invalidation
Repository: platform
Author: Dana Wu
Risk: High
Age: 1 day 4 hours
Labels: bug, cache, performance, production-fix
Additions: 76
Deletions: 31
Files: 7
IsProductionFix: true
```

Add at least two more PRs of your choosing to demonstrate:

- a large-review-surface warning,
- a release blocker,
- an author WIP warning.

---

# 32. Seed Reviewers

Seed at least six reviewers.

Example:

```text
Bob Smith
Components: backend, skills
Specialties: backend, ai
Capacity: 3
Active reviews: 1

Kent Jones
Components: backend, skills
Specialties: backend, architecture
Capacity: 3
Active reviews: 3

Priya Singh
Components: platform
Specialties: auth, security, infra
Capacity: 3
Active reviews: 2

Sarah Miller
Components: web
Specialties: frontend, ui
Capacity: 3
Active reviews: 1

Dana Wu
Components: platform
Specialties: performance, database
Capacity: 3
Active reviews: 2

Jordan Park
Components: observability
Specialties: telemetry, analytics
Capacity: 3
Active reviews: 0
```

The routing demo should visibly prefer qualified reviewers with available capacity rather than simply assigning the first owner.

---

# 33. Frontend State and Query Behavior

Use TanStack Query for server state.

Recommended query keys:

```text
['review-queue', filters]
['pull-request', id]
['reviewers']
['metrics-summary']
```

After review mutations:

- invalidate the selected PR,
- invalidate review queue,
- invalidate metrics.

Avoid building unnecessary global state infrastructure.

Local UI state is sufficient for:

- filters,
- selected queue item,
- open menus,
- current tab.

---

# 34. Visual System

Use the supplied mockup as visual direction.

Suggested tokens:

```text
Sidebar background: near-black / deep navy
Application background: very light cool gray
Cards: white
Primary accent: indigo / blue
High: red
Medium: amber
Low: blue
Success: green
Muted text: slate gray
```

Use accessible contrast.

The exact colors do not need to match pixel-for-pixel.

## Components

Build reusable components for:

```text
Badge
MetricCard
QueueItem
RiskBadge
PriorityBadge
AgingBadge
ReviewBriefCard
ReviewerAvatar
ReviewerCapacity
FilterDropdown
EmptyState
```

---

# 35. Demo Interaction

A reviewer should be able to perform this complete demo:

1. Open the application.
2. See a populated review queue.
3. Observe high-priority and aging PRs at the top.
4. Select `Add pending skill lifecycle handling`.
5. See the AI Review Brief.
6. See why the PR has its priority/risk.
7. See recommended file review order.
8. See assigned reviewer and reviewer load.
9. Click `Start Review`.
10. Observe status change to `In Review`.
11. Click `Approve`.
12. Observe the PR leave the waiting queue or update to Approved.
13. Observe metrics update.

This complete path must work without refreshing the browser manually.

---

# 36. API Error Handling

Use Problem Details for API errors.

Examples:

```text
404 PR not found
400 invalid outcome
409 PR is already actively assigned to another reviewer
409 reviewer is at capacity
```

Frontend should show concise toast notifications for mutation errors.

---

# 37. Logging

Add structured logging for important orchestration events.

Examples:

```text
PullRequestScored
RiskCalculated
ReviewerAssigned
ReviewerLeaseExpired
ReviewStarted
ReviewReleased
ReviewCompleted
ReviewBriefGenerated
```

Log useful IDs and outcomes without logging giant serialized objects.

---

# 38. Configuration

Use `appsettings.json` / environment variables for:

```text
ReviewRouting:DefaultLeaseHours = 4
ReviewRouting:MaxAuthorOpenReviewsBeforeWarning = 3
Queue:AgingHours = 8
Queue:StaleHours = 24
Queue:CriticalHours = 48
ReviewSurface:MeaningfulLineThreshold = 600
ReviewSurface:ProductionFileThreshold = 15
SourceControl:Provider = Mock
ReviewBrief:Provider = Mock
```

---

# 39. Automated Tests

At minimum implement unit tests for the following.

## Priority engine

- older PR scores higher when otherwise equal,
- production fix receives bonus,
- release blocker receives bonus,
- dependency blocker receives bonus,
- small PR receives bonus,
- very large PR receives penalty,
- age contribution caps correctly.

## Risk engine

- auth file => High,
- security label => High,
- migration file => High,
- persistence change => Medium,
- tests-only PR => Low.

## Reviewer router

- component owner is preferred,
- overloaded reviewer is penalized,
- reviewer at capacity is not selected,
- inactive reviewer is not selected,
- security-sensitive PR prefers qualified security reviewer.

## Review surface

- generated/lock files excluded,
- line threshold triggers warning,
- production-file threshold triggers warning.

---

# 40. Optional Integration: Azure DevOps

Only implement after the local PoC works.

Create a provider abstraction capable of mapping an Azure DevOps pull request into the internal `PullRequest` model.

Expected future data sources:

- Pull Requests REST API,
- changed files / iterations API,
- reviewer identities,
- repository metadata,
- work-item links,
- status / vote information.

Do not couple the domain model to Azure DevOps SDK types.

The orchestration service owns its calculated fields:

```text
PriorityScore
Risk
AgingState
ReviewBrief
AssignmentLease
ReviewerCandidateScores
```

Azure DevOps remains the source of truth for the underlying PR itself.

---

# 41. Optional Integration: GitHub

Use the same provider abstraction.

Future mapping may come from:

- Pull Request API,
- changed files,
- requested reviewers,
- CODEOWNERS,
- labels,
- checks.

Again, domain logic must remain provider-independent.

---

# 42. Optional Integration: Slack

Do not implement a full Slack app for the core PoC.

Define an interface:

```csharp
public interface IReviewNotificationService
{
    Task NotifyAssignedAsync(ReviewAssignment assignment, CancellationToken cancellationToken);
    Task NotifyAgingAsync(PullRequest pullRequest, CancellationToken cancellationToken);
    Task NotifyReassignedAsync(ReviewAssignment assignment, CancellationToken cancellationToken);
}
```

Implement:

```text
LoggingReviewNotificationService
```

It should simply log notification payloads.

This preserves the future boundary without consuming PoC time.

---

# 43. Future Notification Policy

Document but do not fully implement this policy:

```text
0h       assigned normally
4h       quiet reviewer reminder / lease expiration
8h       PR enters Aging state
24h      PR enters Stale state and may be rerouted
48h      PR enters Critical state / team escalation
```

The system should avoid sending repetitive notifications at every polling interval.

---

# 44. Security Considerations

Even though this is a PoC:

- never commit secrets,
- use environment variables for provider tokens,
- do not expose provider tokens to the frontend,
- sanitize repository URLs shown in logs if they could contain credentials,
- assume PR descriptions and titles are untrusted display text,
- do not render arbitrary HTML from PR content.

Future LLM integration must treat repository content as untrusted input and defend against prompt-injection-like content contained inside source files or PR text.

---

# 45. README Requirements

The README must contain:

## What this is

A short description of the review bottleneck and the PoC.

## Architecture

Include a small Mermaid diagram.

## Running locally

Prefer something similar to:

```bash
dotnet restore
dotnet ef database update --project src/ReviewOrchestrator.Infrastructure --startup-project src/ReviewOrchestrator.Api
dotnet run --project src/ReviewOrchestrator.Api
```

and:

```bash
cd src/ReviewOrchestrator.Web
npm install
npm run dev
```

If migrations are automatically applied on startup for the PoC, simplify the instructions accordingly.

## Demo flow

Document the 13-step demo described above.

## Architecture decisions

Briefly explain:

- why the queue is separate from the source-control provider,
- why priority is deterministic,
- why the review brief is provider-based,
- why reviewer routing is isolated domain logic.

## Future work

List:

- Azure DevOps provider,
- GitHub provider,
- real LLM review brief,
- Slack notifications,
- merge queue integration,
- historical analytics,
- team-specific priority configuration.

---

# 46. Acceptance Criteria

The PoC is complete when all of the following are true.

## Application startup

- Backend starts locally.
- Frontend starts locally.
- SQLite database is automatically created or trivially initialized.
- Seed data is available on first run.
- No external credentials are required.

## Review queue

- Queue loads from backend.
- PRs are sorted by calculated priority.
- Search works.
- Priority filter works.
- Status filter works.
- Aging tab works.
- Queue displays age, stats, labels, and risk/priority indicators.

## PR overview

- Selecting a PR loads detail.
- AI review brief is displayed.
- Risk assessment is displayed.
- Priority explanation is displayed.
- Recommended review order is displayed.
- Reviewer assignment and candidate explanation are displayed.

## Review workflow

- Reviewer can start review.
- PR changes to In Review.
- Reviewer can approve.
- Reviewer can request changes.
- Reviewer can release review.
- UI updates immediately after each mutation.

## Orchestration logic

- Priority scoring is deterministic and tested.
- Risk classification is deterministic and tested.
- Reviewer routing is deterministic and tested.
- Reviewer capacity influences assignment.
- Lease expiration can reroute a PR.
- Large PR warnings work.
- Author WIP warnings work.

## Quality

- TypeScript compiles without errors.
- .NET solution builds without errors.
- Tests pass.
- No secrets are checked into the repo.
- README explains how to run the PoC.

---

# 47. Suggested Implementation Order

Follow this order to avoid spending time polishing UI before the workflow works.

## Phase 1 — Backend skeleton

1. Create solution/projects.
2. Add domain models.
3. Configure EF Core + SQLite.
4. Add seed data.
5. Implement queue query endpoint.

## Phase 2 — Orchestration logic

1. Risk engine.
2. Priority engine.
3. Reviewer router.
4. Review-surface evaluator.
5. WIP evaluator.
6. Unit tests.

## Phase 3 — Review workflow

1. Assignment persistence.
2. Start review endpoint.
3. Complete review endpoint.
4. Release review endpoint.
5. Assignment lease background service.

## Phase 4 — Review brief

1. Define provider interface.
2. Implement deterministic mock brief.
3. Add brief to PR detail endpoint.

## Phase 5 — Frontend

1. Application shell/sidebar.
2. Review Queue list.
3. Filters.
4. PR Overview panel.
5. Review Brief.
6. Risk/priority cards.
7. Reviewer assignment controls.

## Phase 6 — Polish

1. Loading states.
2. Error states.
3. Toasts.
4. Analytics summary.
5. README.
6. Docker Compose if useful.

---

# 48. Expected PoC UX

The most important UX outcome is this:

A reviewer should be able to open the application and, within roughly ten seconds, understand:

```text
What should I review next?
Why is it important?
Why was it assigned to me?
What code deserves my attention?
How long has everyone been waiting?
```

The application succeeds if it makes review work feel like a managed queue instead of a pile of notifications.

---

# 49. Design Philosophy

The PoC should intentionally avoid becoming another heavyweight process tool.

It sits beside the existing development workflow and adds orchestration.

Think of the system as a **traffic controller for human review attention**:

```text
AI increases code throughput
          |
          v
More PRs enter the system
          |
          v
PR Review Orchestrator
   |       |       |
Priority  Routing  Review Brief
   |       |       |
   +-------+-------+
           |
           v
Human reviewer spends attention
where it matters most
```

The system should not attempt to make humans review every AI-generated line faster.

It should make fewer lines require human attention and make the important decisions obvious.

---

# 50. Final Coding-Agent Checklist

Before considering the task finished, verify all of the following yourself:

- [ ] The repo builds from a clean checkout.
- [ ] The backend runs.
- [ ] The frontend runs.
- [ ] The database seeds automatically.
- [ ] The queue contains at least 10 believable PRs.
- [ ] Priority ordering visibly works.
- [ ] Risk tiers visibly work.
- [ ] Reviewer load visibly affects assignment.
- [ ] AI Review Brief renders useful deterministic content.
- [ ] Start Review works.
- [ ] Approve works.
- [ ] Request Changes works.
- [ ] Release works.
- [ ] Queue and metrics refresh after mutations.
- [ ] Large-review warning is demonstrable.
- [ ] Author-WIP warning is demonstrable.
- [ ] Lease expiration logic has tests.
- [ ] Core domain logic has tests.
- [ ] README contains exact run commands.
- [ ] No live credentials are necessary for the demo.

Once those items pass, stop expanding scope and deliver the PoC.
