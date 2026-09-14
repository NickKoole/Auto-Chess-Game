# Ticket pipeline

GitHub stores tickets and PRs. Local Node scripts start Codex on demand; nothing polls in the background. `AGENTS.md` contains the project conventions.

## Commands

Run from the main checkout in a normal terminal:

```powershell
node automation/dev.mjs 42
node automation/dev.mjs 42 --fresh
node automation/dev.mjs 42 --no-review
node automation/review.mjs 42
node automation/review.mjs 42 --reviewer claude
node automation/approve.mjs 42 --dry-run
node automation/approve.mjs 42
node automation/ship.mjs 42 --dry-run
node automation/ship.mjs 42
```

Omit the number from `dev.mjs` to choose from the ready pool interactively. `--fresh` starts new Codex memory while preserving the recorded branch, PR, review history, and feedback. `--no-review` publishes a draft PR and stops at `state:needs-you`.

The attached dev session claims a worktree, opens Codex, and waits for you to exit. A successful exit invokes `finish.mjs`, which commits remaining edits, pushes, creates or updates the draft PR, and starts a fresh reviewer. A failed session leaves work local and skips publishing. To publish later, run `finish.mjs` explicitly.

## Working in an existing Codex or Claude conversation

In Codex, ask for `ready`, `build 42`, `approve 42`, or `ship 42` in plain language; these are workflow requests, not installed slash commands. In Claude Code, the same workflows are the `/ready`, `/build`, `/approve` and `/ship` commands in `.claude/commands/`, and `CLAUDE.md` imports `AGENTS.md`. The commands run their work in Sonnet-pinned agents from `.claude/agents/`: `pipeline-runner` for ready/approve/ship, and `ticket-builder` for build, which the main session coordinates — it relays the agent's plan and questions to you (agents cannot ask directly), resumes it with your answers, and publishes with `finish.mjs --reviewer claude`.

- **Ready:** list open `state:ready` issues with `gh issue list`; exclude epics. If empty, summarize in-flight work and backlog. Change no labels.
- **Build:** use the named ticket; if missing, list the ready pool and ask which one. Run `node automation/claim.mjs 42`, read its printed worktree path, then read `automation/prompts/dev-system.md` and the worktree's `.agent/task.md`. Perform all edits and tests in that worktree. State the approach, meet the acceptance criteria, commit locally, and write `.agent/report.md`. Use `finish.mjs 42 --reviewer codex` to publish and review (always pass the flag: a ticket last built with Claude's `/build` has `claude` recorded as its reviewer) once publication is authorized by the user's request; do not request the same authorization again. If publication was not requested, prepare the completed report and commits before asking. Summarize the actual reviewer findings and link the draft PR. Do not start `dev.mjs` inside an already-running agent session.
- **Approve:** only on the human's explicit request naming a ticket. First run `approve.mjs 42 --dry-run` and show its findings; then run the real command unless the user requested dry-run only. Never infer approval from a clean automated review. This records the human's decision, not the agent's.
- **Ship:** require an explicit ticket number and an existing `state:approved` label. Run the dry-run first; on success, execute `ship.mjs` unless the request was dry-run only. Preserve requested `--keep` and `--timeout` options. Report CI failures or cleanup problems accurately; never force-remove unexpected work. Do not add an approval label to get past a refusal.

The split command path is also available:

```powershell
node automation/claim.mjs 42
# Run the absolute automation/session.mjs command printed by claim.
# Or build in an existing Codex conversation using the task and standing rules.
node automation/finish.mjs 42 --reviewer codex
```

`session.mjs` saves the Codex thread ID before opening the interactive UI, so this path supports resume too. An externally managed conversation has its own memory; the ticket's files, commits, feedback, and report remain the handover between sessions.

## States and human gates

Exactly one `state:*` label belongs on each issue:

| State | Meaning |
| --- | --- |
| `state:backlog` | Captured, not yet selected. |
| `state:ready` | Human-selected work with checkable acceptance criteria. |
| `state:building` | A build has been claimed. |
| `state:reviewing` | Validation and independent review are running. |
| `state:needs-you` | Draft PR/findings or a failed session need attention. |
| `state:approved` | The human approved this ticket. |
| `state:done` | GitHub's PR-closed workflow confirmed the merge. |
| `state:blocked` | A runner failed or an external decision is needed. |

Agents never choose `ready` or `approved` themselves. `approve.mjs` is a separate human-triggered action. `ship.mjs` requires that approval; no unattended step chains approval into shipping. Only the GitHub workflow sets `done`.

## Codex configuration and sessions

Install Codex CLI and authenticate with `codex login`; `gh auth login` supplies GitHub access to the Node runner. This migration was checked against Codex CLI 0.154.0. Both dev and reviewer use the explicit model and reasoning effort in `automation/settings/codex.json`. Model failures are reported; there is no silent fallback to a different model.

A new attached session first runs a minimal read-only initialization turn, captures Codex's `thread.started` ID, saves it as `codexSessionId`, then opens `codex resume` with workspace-write permissions. This extra turn exists because the interactive CLI does not accept a caller-assigned session ID. It consumes a small model request. Resumes use the exact stored ID, never `--last`.

Legacy `sessionId` values remain historical Claude IDs and are never passed to Codex. Existing issue state is retained. If Codex memory is unavailable, use `--fresh`.

On Windows the runner launches Codex's npm JavaScript entry point through Node without a shell, preserving arguments and paths with spaces. A native installation can be selected using `AUTO_CHESS_CODEX_BIN` (absolute executable path). The runner strips `AGENT_PUSH_OK` from Codex's environment.

## Review and validation

Before starting the reviewer, `review.mjs` requires a clean worktree whose HEAD equals the published branch. It refuses local changes or unpublished commits rather than resetting them.

The Node runner builds/tests the server, installs locked client dependencies and runs client typecheck/tests/build, and runs automation tests where present. Commands stop at the first failure; full output is stored under `.agent/validation/`. Validation has a 15-minute timeout per command. It does not run the client lint script because that script applies fixes. CI remains the required merge gate for lint and generated-contract drift.

Codex then audits in a fresh ephemeral read-only session with approval requests disabled. It reads the diff, ticket, PR report, and independent validation logs. It does not run builds or tests itself. `--timeout 40` controls the model review timeout in minutes, separately from validation. A failed validation forces a `changes` verdict; a failed model run or malformed verdict is reported as an incomplete review, never as evidence that code is correct.

`--reviewer claude` (accepted by `finish.mjs` and `review.mjs`) swaps the model for a headless `claude -p` run: same prompt, validation and schema (`--json-schema`), no session persistence, `--permission-mode dontAsk` under `automation/settings/claude-review.json`, which allows reading and read-only git and denies writes, `gh` and web access. The choice is recorded as `reviewer` in `.agent-state/issue-42.json`, so a later bare `review.mjs 42` keeps it; `dev.mjs` always records `codex`. The launcher strips the parent Claude Code session's environment so it works from inside `/build`; set `AUTO_CHESS_CLAUDE_BIN` to use a non-PATH executable. The PR comment names which reviewer wrote it.

The final response must satisfy `automation/settings/review.schema.json`; the runner also validates severity and verdict consistency before posting findings. Automated `approve` means no blocking findings, not a GitHub approval. Subsequent passes may focus on commits since `reviewedSha`, while validation still covers the whole branch.

## Permissions and publishing

Dev runs use `workspace-write` with no per-command approvals and network access enabled, so dependency installation, tests and read-only GitHub inspection do not interrupt generation. Review runs use `read-only` with no interactive escalation. Direct pushes, destructive Git operations, GitHub writes and human approval actions remain forbidden by `.codex/rules/pipeline.rules`; publishing and ticket-state transitions stay in the pipeline. Standing dev/reviewer instructions are supplied through Codex's developer instructions. These are Codex rules, not a translation of Claude's tool allow-list; user configuration, project trust, and available connectors can affect the broader tool surface.

The worktree pre-push hook requires `AGENT_PUSH_OK`, which the publishing runner supplies. It catches accidental direct pushes, including from sessions opened elsewhere; it is not a security boundary against code that can deliberately alter hooks or environment variables. Agents must not bypass it, invoke GitHub mutations during development/review, or change branches. The coordinator runs the authorized pipeline scripts.

## Files and recovery

- `.agent-state/issue-42.json`: branch, worktree, PR, feedback timestamps, Codex thread ID, and review history. Gitignored in the main checkout.
- `.agent/task.md`: ticket and new human feedback, refreshed on claim.
- `.agent/report.md`: dev report used as the PR body; revisions refresh it. Without a report, an existing PR retains its description and a new PR gets a placeholder.
- `.agent/validation/`: full independent validation output.

Ticket worktrees live in `../Auto-Chess-Game-work/issue-42/` on `agent/42-<slug>`. Use the pipeline scripts for their lifecycle. When using Orca-managed worktrees or terminals, follow the Orca CLI skill for that managed state rather than creating a second competing worktree.

A failed build or reviewer can be retried explicitly; nothing watches for it. A merge can succeed while cleanup fails: inspect the remaining worktree before any forced removal. `ship.mjs` can finish cleanup for an already-merged ticket without a now-consumed approval label.
