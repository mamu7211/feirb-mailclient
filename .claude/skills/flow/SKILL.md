---
name: flow
description: End-to-end multi-agent workflow for a feature or fix — grill-me design interview, then coder, tester and reviewer agents, ending in a pull request
user_invocable: true
args: issue_number_or_description
---

# Flow — Design, Implement, Test, Review

Orchestrates a change from idea to pull request. The **main session is the orchestrator**: it runs the design interview, plans work packages, spawns the role agents, merges their results and reports to the user. It does not write production code itself.

| Role | Agent (`.claude/agents/`) | Model | Writes |
|------|---------------------------|-------|--------|
| Orchestrator | main session | whatever the user runs | spec, plan, PR text |
| Coder | `coder` | Sonnet | production code + unit tests for it |
| Tester | `tester` | Sonnet | test code only (xUnit, Bruno, Playwright) |
| Reviewer | `reviewer` | Opus | nothing — findings only |

Models are set in the agent frontmatter, not in the spawn call. Change them there if needed.

## When to use

- Features and fixes that touch more than one layer or need design decisions (e.g. reply/forward, the folder model, rate limiting).
- **Not** for typos, single-line fixes, docs-only changes or dependency bumps — do those directly.

## Phase 0 — Intake

1. If an issue number is given: `gh issue view {n}`; check referenced issues for open dependencies (same rules as `/implement-feature` step 2). Stop and ask if a dependency is open.
2. If a description is given: search for existing issues first (`gh issue list --search "<keywords>" --state all`) to avoid duplicates.
3. Size check: if the change is small and unambiguous, tell the user and offer to do it without the flow.
4. Check the working tree of the main checkout (`git status`). Never switch the user's current branch; all work happens in worktrees under `.claude/worktrees/`.

## Phase 1 — Design (`/grill-me`)

1. Run `/grill-me` with the issue/description. Explore the codebase before asking — questions the code can answer are not questions for the user.
2. Result: a **spec** with scope (in/out), data model, API, UI, i18n, security/authorization, tests, acceptance criteria.
3. **Gate:** show the spec and get explicit approval. Then write it to the issue (update the body or add a comment `## Spec (approved <date>)`); create the issue if none exists (labels per `docs/ISSUES.md`).

## Phase 2 — Plan

1. Create the feature branch from `origin/main` in a worktree (naming per `CLAUDE.md`, e.g. `feature/<issue#>-<slug>`):
   ```bash
   git fetch origin
   git worktree add .claude/worktrees/<slug> -b feature/<issue#>-<slug> origin/main
   ```
2. Split the spec into **work packages** with explicit file ownership. Typical order: data model/migration → services → endpoints + DTOs in `Feirb.Shared` → UI. A package that depends on another's DTOs or schema runs after it.
3. Parallelize only packages with **disjoint files**. Each parallel coder gets its own worktree on a sub-branch (`<feature-branch>--<package>`); the orchestrator merges sub-branches into the feature branch afterwards. Sequential packages share the feature worktree.
4. Show the plan (packages, order, parallelism) to the user in a few lines, then start. No second approval needed unless the plan deviates from the spec.

## Phase 3 — Implement (`coder`)

Spawn one `coder` agent per package with a **self-contained brief** (agents start without this conversation):
- worktree path and branch, the approved spec (verbatim or issue link + relevant excerpt), the package's files and boundaries
- conventions that apply (CLAUDE.md sections; `/implement-ui` for any Razor work; `/implement-bruno-tests` for new endpoints)
- done criteria: builds, own unit tests green, `dotnet format` clean, one or more Conventional Commits referencing the issue, **no push**
- report format: files changed, decisions taken, anything unverified

## Phase 4 — Test (`tester`)

Spawn one `tester` agent on the feature branch after all packages are merged:
- fill test gaps against the acceptance criteria (xUnit; Bruno for new/changed endpoints; Playwright for new/changed pages)
- run `dotnet test`, then the container stack **under the lock** (see below)
- report exact numbers per layer and every failure with a classification: *product bug*, *outdated test*, or *environment*

Product bugs go back to a `coder` (Phase 3) with the failure as the brief.

## Phase 5 — Review (`reviewer`)

Spawn the `reviewer` agent on the diff `origin/main...<feature-branch>`, together with the spec. It returns findings with severities (🔴 blocker, 🟡 suggestion, 🔵 nitpick, 🟢 good), like `/pr-review`.

- 🔴 → new `coder` round with the findings as the brief, then tester and reviewer again.
- **At most two review loops.** If blockers remain, stop and present them to the user.
- 🟡/🔵 → list them in the PR description; fix only if trivial.

## Phase 6 — Deliver

1. Final check in the feature worktree: `dotnet build Feirb.sln`, `dotnet test Feirb.sln`, `dotnet format Feirb.sln --verify-no-changes`.
2. `git push -u origin <branch>` and `gh pr create` using `.github/pull_request_template.md`, `Closes #<issue>`, the test numbers per layer and the open 🟡/🔵 findings.
3. Remove the worktrees (`git worktree remove ...`) and leftover test containers.
4. Report to the user: PR link, what was built, test results, review outcome, anything unverified. **Never merge** — merging is the user's decision.

## Shared rules for all agents

- **Container test stack is exclusive.** `tests/run-tests.sh` uses fixed ports and a fixed compose project. Always run it as
  ```bash
  flock /tmp/feirb-test-stack.lock tests/run-tests.sh > <log> 2>&1
  ```
  and read the log afterwards. Never run two stacks at once.
- **Never weaken a test to make it pass.** A test may only change if it contradicts the current, intended behavior — state the reason (PR, issue, commit) in the commit message.
- **Stay inside the brief.** Findings outside the scope go into the report, not into the code.
- **No pushes, no PRs, no merges** from role agents; the orchestrator does that.
- Commit attribution and PR footer follow the current session's attribution instructions.
